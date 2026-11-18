using Microsoft.EntityFrameworkCore;
using Olvanta.Application;
using Olvanta.Domain;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Commerce;

internal sealed class SalesDesk(OlvantaDbContext db)
{
    public async Task CreateAsync(Branch branch, QuoteInput input, SalesEvent action)
    {
        if (input.Terms is not ("cash" or "credit") || (input.Terms == "credit") != input.DueDate.HasValue)
            throw new TradeException("Seleccione contado o crédito con fecha de vencimiento.", 400);
        if (input.DueDate < DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5)))
            throw new TradeException("El vencimiento debe ser hoy o posterior.", 400);
        var masters = db.Masters.Where(x => x.BranchId == branch.Id && x.Active);
        var customer = await masters.SingleOrDefaultAsync(x => x.Id == input.CustomerId && x.Kind == "customers")
            ?? throw new TradeException("Cliente activo no encontrado.", 404);
        var sale = new Sale
        {
            BranchId = branch.Id,
            CustomerId = customer.Id,
            Customer = $"{customer.Name} · {customer.Code}",
            Currency = branch.Currency,
            Terms = input.Terms,
            DueDate = input.DueDate
        };
        foreach (var row in input.Lines)
        {
            if (row == null || !StockMath.ValidQuantity(row.Quantity) || row.Discount is < 0 or > 100 || SalesMath.Money(row.Discount) != row.Discount)
                throw new TradeException("Revise la cantidad y el porcentaje de descuento por partida.", 400);
            var item = await masters.SingleOrDefaultAsync(x => x.Id == row.ItemId && (x.Kind == "products" || x.Kind == "services"))
                ?? throw new TradeException("Artículo activo no encontrado.", 404);
            if (item.Currency != branch.Currency) throw new TradeException("El artículo tiene otra moneda.");
            if (item.Kind == "products")
            {
                if (!await db.Warehouses.AnyAsync(x => x.BranchId == branch.Id && x.Id == row.WarehouseId))
                    throw new TradeException("Seleccione un almacén de la sucursal.", 400);
            }
            else if (row.WarehouseId != null || !string.IsNullOrWhiteSpace(row.Lot))
                throw new TradeException("Un servicio se registra sin almacén ni lote.", 400);
            var total = SalesMath.LineTotal(item.Price!.Value, row.Quantity, row.Discount);
            if (!SalesMath.ValidMoney(total)) throw new TradeException("El importe excede el máximo por partida.", 400);
            sale.Lines.Add(new SaleLine
            {
                ItemId = item.Id,
                Description = $"{item.Name} · {item.Code}",
                Unit = item.Unit!,
                Price = item.Price.Value,
                Quantity = row.Quantity,
                TaxRate = item.TaxRate!.Value,
                Discount = row.Discount,
                Total = total,
                Tax = SalesMath.IncludedTax(total, item.TaxRate.Value),
                WarehouseId = row.WarehouseId,
                Lot = row.Lot?.Trim().ToUpperInvariant() ?? ""
            });
        }
        if (!SalesMath.ValidMoney(sale.Lines.Sum(x => x.Total))) throw new TradeException("El total excede el máximo por venta.", 400);
        db.Sales.Add(sale);
        await db.SaveChangesAsync();
        action.SaleId = sale.Id;
    }

    public async Task ApplyAsync(Sale sale, TradeInput input, SalesEvent action, List<SalesEvent> history)
    {
        if (await db.RentCharges.AnyAsync(x => x.SaleId == sale.Id))
            throw new TradeException("La cuenta corresponde a un cargo de alquiler. Gestione el contrato en Alquileres.");
        var fulfillment = new SaleFulfillment(db);
        var total = sale.Lines.Sum(x => x.Total);
        switch (input.Kind)
        {
            case "order" when sale.Stage == "quote":
                sale.Stage = "order";
                break;
            case "confirm" when sale.Stage == "order":
                var ids = sale.Lines.Select(x => x.ItemId).Append(sale.CustomerId).Distinct().ToArray();
                if (await db.Masters.CountAsync(x => ids.Contains(x.Id) && x.Active && x.BranchId == sale.BranchId) != ids.Length)
                    throw new TradeException("Hay fichas inactivas en el pedido.");
                foreach (var line in sale.Lines) await fulfillment.ApplyAsync(sale, line, action);
                sale.Stage = "confirmed";
                break;
            case "dispatch" when sale.Stage == "confirmed":
                if (sale.Terms == "cash" && SalesMath.Balance(total, history) > 0)
                    throw new TradeException("Complete el cobro al contado antes del despacho.");
                foreach (var line in sale.Lines) await fulfillment.ApplyAsync(sale, line, action);
                sale.Stage = "dispatched";
                break;
            case "cancel" when sale.Stage is "quote" or "order" or "confirmed":
                if (await db.FiscalDocuments.AnyAsync(x => x.SaleId == sale.Id && x.Active && (x.Type == "01" || x.Type == "03")))
                    throw new TradeException("La venta tiene un comprobante vigente. Revise su estado en Facturación.");
                if (history.Any(x => x.Kind == "pay")) throw new TradeException("La venta ya tiene cobros.");
                if (sale.Stage == "confirmed")
                    foreach (var line in sale.Lines) await fulfillment.ApplyAsync(sale, line, action);
                sale.Stage = "cancelled";
                break;
            case "return" when sale.Stage == "dispatched":
                var returnedLine = sale.Lines.Find(x => x.Id == input.LineId) ?? throw new TradeException("Partida no encontrada.", 404);
                var previous = history.Where(e => e.Kind == "return" && e.LineId == input.LineId).Sum(e => e.Quantity);
                if (input.Quantity is not decimal quantity || !StockMath.ValidQuantity(quantity) || previous + quantity > returnedLine.Quantity)
                    throw new TradeException("Revise la cantidad disponible para devolver.");
                action.LineId = returnedLine.Id;
                action.Quantity = quantity;
                action.Amount = SalesMath.ReturnedAmount(returnedLine.Total, returnedLine.Quantity, previous, quantity, 2);
                await fulfillment.ApplyAsync(sale, returnedLine, action);
                break;
            default:
                throw new TradeException("La operación no corresponde al estado de la venta.");
        }
    }
}
