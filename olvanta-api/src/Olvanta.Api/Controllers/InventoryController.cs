using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Olvanta.Api.Security;
using Olvanta.Application;
using Olvanta.Domain;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Controllers;

[Authorize, ApiController, Route("api/v1/branches/{branchId:int}/inventory")]
public sealed class InventoryController(OlvantaDbContext db, SessionAccess access) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<InventoryView>> Get(int branchId)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.InventoryRead)) return Forbid();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.ReadInventoryBranchAsync(branchId);
        var masters = db.Masters.Where(x => x.BranchId == branchId);
        var warehouses = db.Warehouses.Where(x => x.BranchId == branchId);
        var movements = db.Movements.Where(x => x.BranchId == branchId);
        var orders = await (from order in db.Purchases
                            where order.BranchId == branchId
                            join supplier in db.Masters on order.SupplierId equals supplier.Id
                            orderby order.Id descending
                            select new PurchaseView(order.Id, order.Reference, supplier.Name, order.Currency,
                                order.Lines.OrderBy(l => l.Id).Select(l => new OrderLineView(l.Id, l.ProductId, l.Quantity, l.Received, l.UnitCost)).ToList())).ToListAsync();
        var lots = await movements.GroupBy(x => new { x.WarehouseId, x.ProductId, x.Lot, x.Expiry })
            .Select(g => new { g.Key.WarehouseId, g.Key.ProductId, g.Key.Lot, g.Key.Expiry, Quantity = g.Sum(m => m.Quantity), Reserved = g.Sum(m => m.Reserved), Value = g.Sum(m => m.Value) })
            .ToListAsync();
        var minima = await (from m in db.Minimums join w in warehouses on m.WarehouseId equals w.Id select m)
            .ToDictionaryAsync(m => (m.WarehouseId, m.ProductId), m => m.Quantity);
        var groups = lots.ToLookup(x => (x.WarehouseId, x.ProductId));
        var balances = groups.Select(g => g.Key).Union(minima.Keys).OrderBy(k => k.WarehouseId).ThenBy(k => k.ProductId).Select(k =>
        {
            var rows = groups[k];
            return new BalanceView(k.WarehouseId, k.ProductId, rows.Sum(x => x.Quantity), rows.Sum(x => x.Reserved),
                rows.Sum(x => x.Value), minima.GetValueOrDefault(k), rows.Where(x => x.Quantity != 0).OrderBy(x => x.Lot)
                    .Select(x => new LotBalance(x.Lot, x.Expiry, x.Quantity, x.Reserved)).ToList());
        }).ToList();
        var reservations = await movements.Where(x => x.Kind == "reserve" &&
                !movements.Any(m => m.RelatedId == x.Id && m.Kind == "release"))
            .OrderBy(x => x.Id).Select(x => new OpenReservation(x.Id, x.WarehouseId, x.ProductId, x.Lot, x.Reserved, x.Reason)).ToListAsync();
        var overview = new InventoryView(await warehouses.OrderBy(x => x.Name).Select(x => new InventoryOption(x.Id, x.Name)).ToListAsync(),
            await masters.Where(x => x.Kind == "products").OrderBy(x => x.Name).Select(x => new ProductOption(x.Id, x.Name, x.Unit!, x.Active)).ToListAsync(),
            await masters.Where(x => x.Kind == "suppliers" && x.Active).OrderBy(x => x.Name).Select(x => new InventoryOption(x.Id, x.Name)).ToListAsync(),
            orders, balances, reservations);
        await tx.CommitAsync();
        return overview;
    }

    [HttpGet("movements")]
    public async Task<ActionResult<List<MovementView>>> History(int branchId, int? warehouseId, int? productId, long? before)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.InventoryRead)) return Forbid();
        var query = db.Movements.Where(x => x.BranchId == branchId);
        if (warehouseId.HasValue) query = query.Where(x => x.WarehouseId == warehouseId);
        if (productId.HasValue) query = query.Where(x => x.ProductId == productId);
        if (before.HasValue) query = query.Where(x => x.Id < before);
        return await query.OrderByDescending(x => x.Id).Take(50).Select(x =>
            new MovementView(x.Id, x.OperationId, x.Kind, x.Time, x.Actor, x.Reason, x.WarehouseId, x.ProductId,
                x.Lot, x.Expiry, x.Quantity, x.Reserved, x.Value, x.PurchaseLineId, x.RelatedId)).ToListAsync();
    }

    [HttpPost("warehouses"), ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Warehouse(int branchId, WarehouseInput input)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.InventoryWrite)) return Forbid();
        db.Warehouses.Add(new Warehouse { BranchId = branchId, Name = input.Name.Trim() });
        access.Record("inventory.warehouse", input.Name, branchId);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("minimum"), ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Minimum(int branchId, MinimumInput input)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.InventoryWrite)) return Forbid();
        if (input.Quantity != 0 && !StockMath.ValidQuantity(input.Quantity)) return BadRequest();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.LockBranchAsync(branchId);
        if (!await db.Warehouses.AnyAsync(x => x.BranchId == branchId && x.Id == input.WarehouseId) ||
            !await db.Masters.AnyAsync(x => x.BranchId == branchId && x.Kind == "products" && x.Id == input.ProductId)) return NotFound();
        var minimum = await db.Minimums.FindAsync(input.WarehouseId, input.ProductId);
        if (minimum == null)
        {
            minimum = new StockMinimum { WarehouseId = input.WarehouseId, ProductId = input.ProductId };
            db.Minimums.Add(minimum);
        }
        minimum.Quantity = input.Quantity;
        access.Record("inventory.minimum", $"Almacén {input.WarehouseId} · producto {input.ProductId} · {input.Quantity}", branchId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }

    [HttpPost("purchases"), ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Purchase(int branchId, PurchaseInput input)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.InventoryWrite)) return Forbid();
        if (input.Lines.Any(l => l == null || !StockMath.ValidQuantity(l.Quantity) || !StockMath.ValidCost(l.UnitCost)))
            return Problem(statusCode: 400, detail: "Revise cantidades y costos por unidad base (hasta seis decimales).");
        await using var tx = await db.Database.BeginTransactionAsync();
        var branch = await db.LockBranchAsync(branchId);
        var active = db.Masters.Where(x => x.Active && x.BranchId == branchId);
        var ids = input.Lines.Select(x => x.ProductId).Distinct().ToArray();
        if (!await active.AnyAsync(x => x.Kind == "suppliers" && x.Id == input.SupplierId) ||
            await active.CountAsync(x => x.Kind == "products" && ids.Contains(x.Id)) != ids.Length) return NotFound();
        db.Purchases.Add(new Purchase
        {
            BranchId = branchId,
            SupplierId = input.SupplierId,
            Reference = input.Reference.Trim(),
            Currency = branch!.Currency,
            Lines = input.Lines.Select(l => new PurchaseLine { ProductId = l.ProductId, Quantity = l.Quantity, UnitCost = l.UnitCost }).ToList()
        });
        access.Record("purchase.created", input.Reference, branchId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }
}
