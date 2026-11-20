using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Olvanta.Api.Commerce;
using Olvanta.Api.Security;
using Olvanta.Application;
using Olvanta.Domain;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Controllers;

[Authorize, ApiController, Route("api/v1/branches/{branchId:int}/sales")]
public sealed class SalesController(OlvantaDbContext db, SessionAccess access) : ControllerBase
{
    [HttpGet("catalog")]
    public async Task<ActionResult<SalesCatalog>> Catalog(int branchId)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.Sell)) return Forbid();
        var masters = db.Masters.Where(x => x.BranchId == branchId && x.Active);
        var currency = await db.Branches.Where(x => x.Id == branchId).Select(x => x.Currency).SingleAsync();
        return new SalesCatalog(await masters.Where(x => x.Kind == "customers").OrderBy(x => x.Name).Select(x => new InventoryOption(x.Id, x.Name + " · " + x.Code)).ToListAsync(),
            await masters.Where(x => (x.Kind == "products" || x.Kind == "services") && x.Currency == currency).OrderBy(x => x.Name)
                .Select(x => new SaleItem(x.Id, x.Name, x.Code, x.Unit!, x.Kind, x.Price!.Value, x.TaxRate!.Value)).ToListAsync(),
            await db.Warehouses.Where(x => x.BranchId == branchId).OrderBy(x => x.Name).Select(x => new InventoryOption(x.Id, x.Name)).ToListAsync(),
            await db.Movements.Where(x => x.BranchId == branchId).GroupBy(x => new { x.ProductId, x.WarehouseId, x.Lot })
                .Where(g => g.Sum(x => x.Quantity - x.Reserved) > 0)
                .Select(g => new SaleStock(g.Key.ProductId, g.Key.WarehouseId, g.Key.Lot, g.Sum(x => x.Quantity - x.Reserved))).ToListAsync());
    }

    [HttpGet]
    public async Task<ActionResult<List<SaleDetail>>> Sales(int branchId)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.SalesRead)) return Forbid();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.ReadInventoryBranchAsync(branchId);
        var sales = await db.Sales.AsNoTracking().Where(x => x.BranchId == branchId).OrderByDescending(x => x.Id).Include(x => x.Lines).ToListAsync();
        var warehouses = await db.Warehouses.Where(x => x.BranchId == branchId).ToDictionaryAsync(x => x.Id, x => x.Name);
        var events = (await db.SalesEvents.AsNoTracking().Where(x => x.BranchId == branchId && x.SaleId != null).OrderBy(x => x.Id).ToListAsync()).ToLookup(e => e.SaleId);
        var rentals = await db.RentCharges.Where(c => c.Sale.BranchId == branchId).ToDictionaryAsync(c => c.SaleId, c => (int?)c.LeaseId);
        var result = sales.Select(s =>
        {
            var history = events[s.Id];
            var total = s.Lines.Sum(l => l.Total);
            var returns = history.Where(e => e.Kind == "return").ToLookup(e => e.LineId);
            var lines = s.Lines.OrderBy(l => l.Id).Select(l => new SaleLineDetail(l.Id, l.ItemId, l.Description,
                l.Unit, l.Quantity, s.Stage == "dispatched" ? l.Quantity - returns[l.Id].Sum(e => e.Quantity) : 0,
                l.Price, l.Discount, l.TaxRate, l.Tax, l.Total, l.WarehouseId is int w ? warehouses[w] : null, l.Lot)).ToList();
            return new SaleDetail(s.Id, s.Customer, s.Currency, s.Terms, s.DueDate, s.Stage, s.Created, total,
                s.Stage is "confirmed" or "dispatched" ? SalesMath.Balance(total, history) : 0,
                lines, history.Select(TradeEntry.From).ToList(), rentals.GetValueOrDefault(s.Id));
        }).ToList();
        await tx.CommitAsync();
        return result;
    }

    [HttpGet("cash")]
    public async Task<ActionResult<List<CashDetail>>> Cash(int branchId)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.CashRead)) return Forbid();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.ReadInventoryBranchAsync(branchId);
        var sessions = await db.CashSessions.AsNoTracking().Where(x => x.BranchId == branchId).OrderByDescending(x => x.Id).ToListAsync();
        var events = (await db.SalesEvents.AsNoTracking().Where(x => x.BranchId == branchId && x.CashSessionId != null).OrderBy(x => x.Id).ToListAsync()).ToLookup(e => e.CashSessionId);
        var result = sessions.Select(s =>
        {
            var history = events[s.Id];
            var expected = s.Opening + history.Sum(e => e.CashDelta);
            return new CashDetail(s.Id, s.Currency, s.Opened, s.Closed, s.Opening, expected, s.Counted, s.Counted - expected, history.Select(TradeEntry.From).ToList());
        }).ToList();
        await tx.CommitAsync();
        return result;
    }

    [HttpPost("quotes")]
    public async Task<ActionResult<TradeReceipt>> Quote(int branchId, QuoteInput input)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.Sell)) return Forbid();
        if (input.Lines.Any(l => l?.Discount > 0) && !await access.AllowsAsync(branchId, AccessRules.Discount)) return Forbid();
        return await Record(branchId, input.RequestId, "quote", input, null, (b, e) => new SalesDesk(db).CreateAsync(b, input, e));
    }

    [HttpPost("actions")]
    public async Task<ActionResult<TradeReceipt>> Act(int branchId, TradeInput input)
    {
        var permission = input.Kind switch
        {
            "order" or "confirm" or "cancel" => AccessRules.Sell,
            "dispatch" => AccessRules.Dispatch,
            "return" => AccessRules.Returns,
            "open" or "close" or "cash-in" or "cash-out" or "pay" or "refund" => AccessRules.Cash,
            _ => null
        };
        if (permission == null) return Problem(statusCode: 400, detail: "Acción desconocida.");
        if (!await access.AllowsAsync(branchId, permission)) return Forbid();
        var cash = permission == AccessRules.Cash;
        var settlement = input.Kind is "pay" or "refund";
        if ((settlement || !cash) != input.SaleId.HasValue || (cash && input.Kind != "open") != input.CashSessionId.HasValue ||
            (input.Kind == "return") != input.LineId.HasValue || (input.Kind == "return") != input.Quantity.HasValue ||
            (input.Kind == "refund") != input.ReturnId.HasValue || cash != input.Amount.HasValue || settlement != (input.Method != null))
            return Problem(statusCode: 400, detail: "Los campos no corresponden a la operación seleccionada.");
        if (input.Kind is ("return" or "refund" or "cash-in" or "cash-out" or "cancel") && string.IsNullOrWhiteSpace(input.Note))
            return Problem(statusCode: 400, detail: "Registre el motivo de la operación.");
        return await Record(branchId, input.RequestId, input.Kind, input, input.Note, async (branch, action) =>
        {
            Sale? sale = null;
            List<SalesEvent> history = [];
            if (input.SaleId is int id)
            {
                sale = await db.Sales.Include(x => x.Lines).SingleOrDefaultAsync(x => x.BranchId == branchId && x.Id == id)
                    ?? throw new TradeException("Venta no encontrada.", 404);
                action.SaleId = sale.Id;
                history = await db.SalesEvents.Where(x => x.SaleId == id).ToListAsync();
            }
            if (cash) await new CashDesk(db).ApplyAsync(branch, sale, input, action, history);
            else await new SalesDesk(db).ApplyAsync(sale!, input, action, history);
        });
    }

    private async Task<ActionResult<TradeReceipt>> Record<T>(int branchId, Guid key, string kind, T input, string? note, Func<Branch, SalesEvent, Task> apply)
    {
        if (key == Guid.Empty) return Problem(statusCode: 400, detail: "Falta el identificador de operación.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var branch = await db.LockBranchAsync(branchId);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input)));
        var prior = await db.SalesEvents.SingleOrDefaultAsync(x => x.BranchId == branchId && x.RequestId == key);
        if (prior != null)
        {
            if (prior.Kind != kind || prior.RequestHash != hash) return Problem(statusCode: 409, detail: "El identificador ya pertenece a otra operación.");
            return new TradeReceipt(prior.Id, prior.SaleId, prior.CashSessionId);
        }
        var entry = new SalesEvent { BranchId = branchId, Actor = access.Actor, Kind = kind, Note = note?.Trim() ?? "", RequestId = key, RequestHash = hash };
        try { await apply(branch!, entry); }
        catch (TradeException error) { return Problem(statusCode: error.Status, detail: error.Message); }
        db.SalesEvents.Add(entry);
        access.Record("sales." + kind, $"Operación {key} · venta {entry.SaleId} · caja {entry.CashSessionId} · {entry.Note}", branchId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new TradeReceipt(entry.Id, entry.SaleId, entry.CashSessionId);
    }
}
