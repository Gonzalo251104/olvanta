using Microsoft.EntityFrameworkCore;
using Olvanta.Application;
using Olvanta.Domain;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Commerce;

internal sealed class CashDesk(OlvantaDbContext db)
{
    public async Task ApplyAsync(Branch branch, Sale? sale, TradeInput input, SalesEvent action, List<SalesEvent> history)
    {
        if (input.Amount is not decimal amount || !SalesMath.ValidMoney(amount))
            throw new TradeException("Ingrese un importe válido con hasta dos decimales.", 400);
        var session = await db.CashSessions.SingleOrDefaultAsync(x => x.BranchId == branch.Id && x.Closed == null);
        if (input.Kind == "open")
        {
            if (session != null) throw new TradeException("Ya hay una caja abierta en la sucursal.");
            session = new CashSession { BranchId = branch.Id, Currency = branch.Currency, Opening = amount };
            db.CashSessions.Add(session);
            await db.SaveChangesAsync();
            action.CashSessionId = session.Id;
            action.Amount = amount;
            return;
        }
        if (session == null || session.Id != input.CashSessionId) throw new TradeException("La sesión de caja indicada ya no está abierta.");
        action.CashSessionId = session.Id;
        if (sale != null && sale.Currency != session.Currency) throw new TradeException("La cuenta y la caja deben tener la misma moneda.");
        action.Amount = amount;
        var expected = session.Opening + await db.SalesEvents.Where(x => x.CashSessionId == session.Id).SumAsync(x => x.CashDelta);
        if (input.Kind == "close")
        {
            session.Closed = action.Time;
            session.Counted = amount;
            return;
        }
        if (amount == 0) throw new TradeException("El importe debe ser positivo.", 400);
        switch (input.Kind)
        {
            case "pay":
            case "refund":
                if (sale == null || sale.Stage is not ("confirmed" or "dispatched"))
                    throw new TradeException("La venta debe estar confirmada.");
                if (input.Method is not ("cash" or "card" or "transfer")) throw new TradeException("Seleccione el medio de pago.", 400);
                var balance = SalesMath.Balance(sale.Lines.Sum(x => x.Total), history);
                if (input.Kind == "pay" && amount > balance) throw new TradeException("El importe excede el saldo de la venta.");
                if (input.Kind == "refund")
                {
                    var source = history.Find(e => e.Id == input.ReturnId && e.Kind == "return")
                        ?? throw new TradeException("Seleccione una devolución de esta venta.", 400);
                    var refundable = source.Amount - history.Where(e => e.ReturnId == source.Id).Sum(e => e.Amount);
                    if (amount > -balance || amount > refundable) throw new TradeException("El reembolso excede el saldo a favor o la devolución.");
                    action.ReturnId = source.Id;
                }
                action.Method = input.Method;
                if (input.Method == "cash") action.CashDelta = input.Kind == "pay" ? amount : -amount;
                break;
            case "parking-pay":
                if (input.Method is not ("cash" or "card" or "transfer")) throw new TradeException("Seleccione el medio de pago.", 400);
                action.Method = input.Method;
                if (input.Method == "cash") action.CashDelta = amount;
                break;
            case "deposit-in":
            case "deposit-out":
                if (action.DepositLeaseId == null || input.Method is not ("cash" or "card" or "transfer"))
                    throw new TradeException("Seleccione el contrato y el medio de pago.", 400);
                action.Method = input.Method;
                if (input.Method == "cash") action.CashDelta = input.Kind == "deposit-in" ? amount : -amount;
                break;
            case "cash-in":
            case "cash-out":
                action.CashDelta = input.Kind == "cash-in" ? amount : -amount;
                break;
            default:
                throw new TradeException("Movimiento de caja desconocido.", 400);
        }
        if (expected + action.CashDelta < 0) throw new TradeException("El efectivo disponible en caja es insuficiente.");
    }
}
