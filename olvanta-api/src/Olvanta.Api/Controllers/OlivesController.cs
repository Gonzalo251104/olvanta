using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Olvanta.Api.Security;
using Olvanta.Application;
using Olvanta.Domain;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Controllers;

[Authorize, ApiController, Route("api/v1/branches/{branchId:int}/olives")]
public sealed class OlivesController(OlvantaDbContext db, SessionAccess access) : ControllerBase
{
    private static readonly Expression<Func<OliveLot, OliveLotView>> LotView = x => new OliveLotView(
        x.Id, x.ProductId, x.Product, x.Code, x.Unit, x.Presentation, x.KgPerUnit, x.WholeUnits, x.RootId, x.ProcessId, x.Variety,
        x.Grade, x.Origin, x.Supplier, x.SupplierLot, x.Gross, x.Tare, x.Gross - x.Tare, x.Receipt == null ? null : x.Receipt.Expiry);

    private IQueryable<OliveLot> Lots(int branchId) => db.OliveLots.Where(l => l.BranchId == branchId);
    private IQueryable<StockMovement> Ledger(int branchId) => db.Movements.Where(m => m.BranchId == branchId);
    private IQueryable<OliveDelivery> Deliveries(int branchId, int? customerId = null, int[]? lotIds = null)
    {
        var lots = Lots(branchId);
        if (lotIds != null) lots = lots.Where(l => lotIds.Contains(l.Id));
        var sales = db.Sales.Where(s => s.BranchId == branchId && (customerId == null || s.CustomerId == customerId));
        return from move in Ledger(branchId)
               join lot in lots on new { move.ProductId, Code = move.Lot } equals new { lot.ProductId, lot.Code }
               join line in db.Set<SaleLine>() on move.SaleLineId equals (int?)line.Id
               join sale in sales on line.SaleId equals sale.Id
               where move.Kind == "sale-dispatch" || move.Kind == "sale-return"
               orderby move.Id descending
               select new OliveDelivery(lot.Id, sale.Id, sale.Customer, sale.CustomerId, move.Id, move.Time, move.Kind, move.Quantity, move.Quantity * lot.KgPerUnit);
    }

    [HttpGet]
    public async Task<ActionResult<OliveDesk>> Desk(int branchId)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.InventoryRead)) return Forbid();
        bool sales = await access.AllowsAsync(branchId, AccessRules.SalesRead);
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.ReadInventoryBranchAsync(branchId);
        var lots = await Lots(branchId).OrderByDescending(l => l.Id).Select(LotView).ToListAsync();
        var stock = await (from l in Lots(branchId)
                           join m in Ledger(branchId) on new { l.ProductId, l.Code } equals new { m.ProductId, Code = m.Lot }
                           group m by new { l.Id, m.WarehouseId } into g
                           where g.Sum(m => m.Quantity) != 0
                           select new OliveStock(g.Key.Id, g.Key.WarehouseId, g.Sum(m => m.Quantity), g.Sum(m => m.Quantity - m.Reserved), g.Sum(m => m.Value))).ToListAsync();
        var masters = db.Masters.Where(m => m.BranchId == branchId);
        var products = await masters.Where(m => m.Active && m.Kind == "products" && m.Conversion > 0).OrderBy(m => m.Name)
            .Select(m => new OliveProduct(m.Id, m.Name, m.Unit!, m.Presentation ?? "", m.Conversion!.Value)).ToListAsync();
        var warehouses = await db.Warehouses.Where(w => w.BranchId == branchId).OrderBy(w => w.Name).Select(w => new InventoryOption(w.Id, w.Name)).ToListAsync();
        var customers = sales ? await masters.Where(m => m.Kind == "customers").OrderBy(m => m.Name).Select(m => new InventoryOption(m.Id, m.Name + " · " + m.Code)).ToListAsync() : [];
        await tx.CommitAsync();
        return new OliveDesk(lots, stock, products, warehouses, customers);
    }

    [HttpGet("deliveries")]
    public async Task<ActionResult<List<OliveDelivery>>> ByCustomer(int branchId, int customerId)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.InventoryRead) || !await access.AllowsAsync(branchId, AccessRules.SalesRead)) return Forbid();
        return await Deliveries(branchId, customerId).ToListAsync();
    }

    [HttpGet("lots/{id:int}/trace")]
    public async Task<ActionResult<OliveTrace>> Trace(int branchId, int id)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.InventoryRead)) return Forbid();
        bool sales = await access.AllowsAsync(branchId, AccessRules.SalesRead);
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.ReadInventoryBranchAsync(branchId);
        var requested = await Lots(branchId).SingleOrDefaultAsync(l => l.Id == id);
        if (requested == null) return NotFound();
        var root = requested.RootId ?? requested.Id;
        var receipt = await (from lot in Lots(branchId)
                             join line in db.PurchaseLines on lot.Receipt!.PurchaseLineId equals (int?)line.Id
                             join order in db.Purchases on line.PurchaseId equals order.Id
                             where lot.Id == root && order.BranchId == branchId
                             select new OlivePurchase(order.Id, line.Id, order.Reference, lot.Receipt!.Id, lot.Receipt.Time, lot.Receipt.Value)).SingleAsync();
        var lineage = Lots(branchId).Where(l => l.Id == root || l.RootId == root);
        var lots = await lineage.OrderBy(l => l.Id).Select(LotView).ToListAsync();
        var processes = await (from p in db.OliveProcesses.Where(p => p.BranchId == branchId)
                               join l in lineage on p.SourceId equals l.Id
                               orderby p.Id
                               select new OliveProcessView(p.Id, p.SourceId, p.Consume.Time, p.Consume.Actor, p.Consume.Reason, -p.Consume.Quantity,
                                   p.InputKg, p.LossKg, p.LossValue)).ToListAsync();
        var related = lots.Select(l => l.Id).ToArray();
        var outputs = await (from l in lineage
                             join m in Ledger(branchId) on new { l.ProductId, l.Code } equals new { m.ProductId, Code = m.Lot }
                             where l.ProcessId != null && m.Kind == "olive-pack"
                             orderby m.Id
                             select new OliveProcessOutput(l.ProcessId!.Value, l.Id, m.Quantity, m.Quantity * l.KgPerUnit, m.Value)).ToListAsync();
        var deliveries = sales ? await Deliveries(branchId, lotIds: related).ToListAsync() : [];
        await tx.CommitAsync();
        return new OliveTrace(root, receipt, lots, processes, outputs, deliveries);
    }

    [HttpPost("repacking")]
    public async Task<ActionResult<int>> Repack(int branchId, OliveRepackInput input)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.InventoryWrite)) return Forbid();
        if (input.OperationId == Guid.Empty || !StockMath.ValidQuantity(input.Quantity) || !StockMath.ValidCost(input.LossKg) ||
            input.Outputs.Any(x => x == null || !StockMath.ValidQuantity(x.Quantity)))
            throw new TradeException("Revise las cantidades, la merma y el identificador del reenvasado.", 400);

        await using var tx = await db.Database.BeginTransactionAsync();
        var branch = await db.LockBranchAsync(branchId);
        var ledger = Ledger(branchId);
        string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input)));
        var previous = await db.OliveProcesses.Where(p => p.BranchId == branchId && p.OperationId == input.OperationId)
            .Select(p => new { p.Id, p.Consume.RequestHash }).SingleOrDefaultAsync();
        if (previous != null) return previous.RequestHash == hash ? previous.Id : throw new TradeException("El identificador ya pertenece a otro reenvasado.");
        if (await ledger.AnyAsync(m => m.OperationId == input.OperationId)) throw new TradeException("El identificador ya pertenece a otro movimiento.");

        var source = await Lots(branchId).SingleOrDefaultAsync(l => l.Id == input.SourceId);
        if (source == null || !await db.Warehouses.AnyAsync(w => w.BranchId == branchId && w.Id == input.WarehouseId)) return NotFound();
        if (!OliveUnits.Valid(source, input.Quantity)) throw new TradeException("La cantidad debe corresponder a la unidad del lote.", 400);
        decimal inputKg = Weight(input.Quantity, source.KgPerUnit);
        var stock = ledger.Where(m => m.WarehouseId == input.WarehouseId && m.ProductId == source.ProductId);
        var total = await stock.GroupBy(m => m.ProductId).Select(g => new
        {
            Available = g.Sum(m => m.Lot == source.Code ? m.Quantity - m.Reserved : 0),
            Quantity = g.Sum(m => m.Quantity),
            Value = g.Sum(m => m.Value)
        }).SingleOrDefaultAsync();
        if (total == null || input.Quantity > total.Available) throw new TradeException("La cantidad supera el disponible del lote. Revise las reservas.");
        var expiry = await stock.Where(m => m.Lot == source.Code).Select(m => m.Expiry).FirstAsync();

        var outputs = new List<(OliveLot Lot, decimal Quantity, decimal Weight)>();
        var codes = new HashSet<(int, string)>();
        foreach (var row in input.Outputs)
        {
            var code = row.Code.Trim().ToUpperInvariant();
            if (!codes.Add((row.ProductId, code)) || await ledger.AnyAsync(m => m.ProductId == row.ProductId && m.Lot == code))
                throw new TradeException("Cada presentación necesita un código de lote nuevo para su producto.");
            var product = await db.Masters.SingleOrDefaultAsync(m => m.BranchId == branchId && m.Id == row.ProductId && m.Active && m.Kind == "products")
                ?? throw new TradeException("Producto de presentación no encontrado.", 404);
            if (product.Currency != branch!.Currency) throw new TradeException("La presentación debe usar la moneda de la sucursal.", 400);
            var bulk = OliveUnits.IsBulk(product.Unit!);
            if (product.Conversion is not decimal factor || !StockMath.ValidQuantity(factor) ||
                (bulk ? factor != 1 : decimal.Truncate(row.Quantity) != row.Quantity || string.IsNullOrWhiteSpace(product.Presentation)))
                throw new TradeException("Configure kilos por unidad y nombre de presentación. Los envases se registran enteros y el granel usa kg con factor 1.", 400);
            var lot = new OliveLot
            {
                BranchId = branchId,
                ProductId = product.Id,
                Product = product.Name,
                Code = code,
                Unit = product.Unit!,
                Presentation = product.Presentation ?? "Granel",
                KgPerUnit = factor,
                WholeUnits = !bulk,
                RootId = source.RootId ?? source.Id
            };
            outputs.Add((lot, row.Quantity, Weight(row.Quantity, factor)));
        }

        if (outputs.Sum(o => o.Weight) + input.LossKg != inputKg)
            throw new TradeException("El peso de salida más la merma debe igualar el peso consumido.", 400);

        var issuedValue = StockMath.IssueValue(input.Quantity, total.Quantity, total.Value);
        var lossValue = OliveUnits.ValueShare(issuedValue, input.LossKg, inputKg);
        var consumed = new StockMovement
        {
            BranchId = branchId,
            WarehouseId = input.WarehouseId,
            ProductId = source.ProductId,
            OperationId = input.OperationId,
            RequestHash = hash,
            Actor = access.Actor,
            Reason = input.Reason.Trim(),
            Kind = "olive-consume",
            Lot = source.Code,
            Expiry = expiry,
            Quantity = -input.Quantity,
            Value = -issuedValue
        };
        var process = new OliveProcess
        {
            BranchId = branchId,
            SourceId = source.Id,
            Consume = consumed,
            OperationId = input.OperationId,
            InputKg = inputKg,
            LossKg = input.LossKg,
            LossValue = lossValue
        };
        db.OliveProcesses.Add(process);
        await db.SaveChangesAsync();

        decimal doneWeight = 0;
        decimal doneValue = 0;
        var usableWeight = inputKg - input.LossKg;
        foreach (var output in outputs)
        {
            doneWeight += output.Weight;
            decimal value = OliveUnits.ValueShare(issuedValue - lossValue, doneWeight, usableWeight);
            output.Lot.ProcessId = process.Id;
            db.OliveLots.Add(output.Lot);
            db.Movements.Add(new StockMovement
            {
                BranchId = branchId,
                WarehouseId = input.WarehouseId,
                ProductId = output.Lot.ProductId,
                OperationId = Guid.NewGuid(),
                RequestHash = hash,
                Actor = consumed.Actor,
                Reason = consumed.Reason,
                Kind = "olive-pack",
                Lot = output.Lot.Code,
                Expiry = expiry,
                Quantity = output.Quantity,
                Value = value - doneValue,
                RelatedId = consumed.Id,
                Time = consumed.Time
            });
            doneValue = value;
        }
        access.Record("olives.repack", $"Reenvasado #{process.Id} · {inputKg} kg · merma {input.LossKg} kg", branchId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return process.Id;
    }

    private static decimal Weight(decimal units, decimal factor)
    {
        decimal weight = units * factor;
        return StockMath.ValidQuantity(weight) ? weight : throw new TradeException("El peso calculado debe representarse con hasta seis decimales.", 400);
    }
}
