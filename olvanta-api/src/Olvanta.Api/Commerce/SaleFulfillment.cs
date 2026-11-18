using Microsoft.EntityFrameworkCore;
using Olvanta.Application;
using Olvanta.Domain;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Commerce;

internal sealed class SaleFulfillment(OlvantaDbContext db)
{
    // The caller owns the branch lock and transaction. Save each line so a later line
    // using the same lot sees the updated available quantity and average value.
    public async Task ApplyAsync(Sale sale, SaleLine line, SalesEvent action)
    {
        if (line.WarehouseId is not int warehouse) return;
        var ledger = db.Movements.Where(x => x.BranchId == sale.BranchId && x.WarehouseId == warehouse && x.ProductId == line.ItemId);
        var movement = new StockMovement
        {
            Kind = "sale-" + action.Kind,
            WarehouseId = warehouse,
            ProductId = line.ItemId,
            BranchId = sale.BranchId,
            SaleLineId = line.Id,
            OperationId = Guid.NewGuid(),
            RequestHash = action.RequestHash,
            Actor = action.Actor,
            Reason = $"Venta #{sale.Id} · partida {line.Id} · {action.Note}",
            Lot = line.Lot,
            Time = action.Time
        };
        if (movement.Reason.Length > 240) movement.Reason = movement.Reason[..240];
        if (action.Kind == "confirm")
        {
            var lot = ledger.Where(x => x.Lot == line.Lot);
            if (line.Quantity > await lot.SumAsync(x => x.Quantity - x.Reserved))
                throw new TradeException($"Stock disponible insuficiente para {line.Description}.");
            movement.Expiry = await lot.Select(x => x.Expiry).FirstOrDefaultAsync();
            movement.Reserved = line.Quantity;
        }
        else if (action.Kind == "return")
        {
            var dispatch = await ledger.SingleAsync(x => x.SaleLineId == line.Id && x.Kind == "sale-dispatch");
            var returned = await ledger.Where(x => x.SaleLineId == line.Id && x.Kind == "sale-return").SumAsync(x => x.Quantity);
            movement.RelatedId = dispatch.Id;
            movement.Expiry = dispatch.Expiry;
            movement.Quantity = action.Quantity;
            movement.Value = SalesMath.ReturnedAmount(-dispatch.Value, line.Quantity, returned, action.Quantity, 6);
        }
        else
        {
            var reservation = await ledger.SingleAsync(x => x.SaleLineId == line.Id && x.Kind == "sale-confirm");
            movement.RelatedId = reservation.Id;
            movement.Expiry = reservation.Expiry;
            movement.Reserved = -line.Quantity;
            if (action.Kind == "dispatch")
            {
                var totals = await ledger.GroupBy(x => x.ProductId).Select(g => new { Quantity = g.Sum(x => x.Quantity), Value = g.Sum(x => x.Value) }).SingleAsync();
                movement.Quantity = -line.Quantity;
                movement.Value = -StockMath.IssueValue(line.Quantity, totals.Quantity, totals.Value);
            }
        }
        db.Movements.Add(movement);
        await db.SaveChangesAsync();
    }
}
