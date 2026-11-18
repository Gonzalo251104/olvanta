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

[Authorize, ApiController, Route("api/v1/branches/{branchId:int}/rentals")]
public sealed class RentalsController(OlvantaDbContext db, SessionAccess access) : ControllerBase
{
    private IQueryable<LeaseContract> Contracts(int branchId) => from c in db.Leases
                                                                 join u in db.Units on c.UnitId equals u.Id
                                                                 where u.BranchId == branchId
                                                                 select c;
    private async Task<LeaseContract> Contract(int branchId, int id) => await Contracts(branchId).SingleOrDefaultAsync(c => c.Id == id)
        ?? throw new TradeException("Contrato no encontrado.", 404);

    [HttpGet]
    public async Task<ActionResult<RentalDeskView>> Desk(int branchId, DateOnly? date)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.RentalRead)) return Forbid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
        var selected = date ?? today;
        await using var tx = await db.Database.BeginTransactionAsync();
        var branch = await db.ReadInventoryBranchAsync(branchId);
        var units = await db.Units.Where(u => u.BranchId == branchId).OrderBy(u => u.Code).Select(u => new RentalUnitView(u.Id, u.Code, u.Name, u.Address, u.Features,
            db.Leases.Any(c => c.UnitId == u.Id && c.Start <= selected && selected <= (c.EndedOn ?? c.End)))).ToListAsync();
        var contracts = Contracts(branchId);
        var deposits = await db.SalesEvents.Where(e => e.BranchId == branchId && e.DepositLeaseId != null).OrderByDescending(e => e.Id)
            .Select(e => new DepositEntry(e.Id, e.DepositLeaseId!.Value, e.Kind, e.Amount, e.Method!, e.Actor, e.Time, e.Note)).ToListAsync();
        var held = deposits.ToLookup(d => d.LeaseId);
        var leases = (await contracts.AsNoTracking().OrderByDescending(c => c.Start).ThenByDescending(c => c.Id).ToListAsync()).Select(c => new LeaseView(c.Id, c.UnitId, c.CustomerId, c.ServiceId,
            c.PreviousId, c.Tenant, c.Currency, c.TaxRate, c.Start, c.End, c.EndedOn, c.EndReason, c.DueDay, c.MonthlyRent, c.Deposit,
            held[c.Id].Sum(e => e.Kind == "deposit-in" ? e.Amount : -e.Amount), c.Conditions)).ToList();
        var charges = await (from q in db.RentCharges
                             join c in contracts on q.LeaseId equals c.Id
                             orderby q.Due, q.Id
                             select new RentChargeView(q.Id, q.LeaseId, q.SaleId, q.Kind, q.Reference, q.Description, q.From, q.Through, q.Due, q.Amount,
                                 q.Amount - db.SalesEvents.Where(e => e.SaleId == q.SaleId && e.Kind == "pay").Sum(e => e.Amount))).ToListAsync();
        var masters = db.Masters.Where(m => m.BranchId == branchId && m.Active);
        var customers = await masters.Where(m => m.Kind == "customers").OrderBy(m => m.Name).Select(m => new InventoryOption(m.Id, m.Name + " · " + m.Code)).ToListAsync();
        var currency = branch!.Currency;
        var services = await masters.Where(m => m.Kind == "services" && m.Currency == currency).OrderBy(m => m.Name).Select(m => new InventoryOption(m.Id, m.Name + " · " + m.Currency)).ToListAsync();
        var session = await db.CashSessions.Where(s => s.BranchId == branchId && s.Closed == null).Select(s => new { s.Id, s.Currency }).SingleOrDefaultAsync();
        await tx.CommitAsync();
        return new RentalDeskView(today, units, leases, charges, customers, services, session?.Id, session?.Currency, deposits);
    }

    // The same branch lock protects the rent calendar, collection and cash closing.
    private async Task<ActionResult<int>> Write(int branchId, string permission, string action, Func<Task<int>> operation)
    {
        if (!await access.AllowsAsync(branchId, permission)) return Forbid();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.LockBranchAsync(branchId);
        int id = await operation();
        access.Record("rentals." + action, $"Registro #{id}", branchId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return id;
    }

    [HttpPost("units")]
    public Task<ActionResult<int>> Unit(int branchId, UnitInput input) => Write(branchId, AccessRules.RentalWrite, "unit", async () =>
    {
        var unit = new CommercialUnit { BranchId = branchId, Code = input.Code.Trim().ToUpperInvariant(), Name = input.Name.Trim(), Address = input.Address.Trim(), Features = input.Features.Trim() };
        db.Units.Add(unit);
        await db.SaveChangesAsync();
        return unit.Id;
    });

    [HttpPost("contracts")]
    public Task<ActionResult<int>> Lease(int branchId, LeaseInput input) => Write(branchId, AccessRules.RentalWrite, "contract", async () =>
    {
        if (input.Start == default || input.End < input.Start || input.DueDay is < 1 or > 31 || input.MonthlyRent <= 0 || !SalesMath.ValidMoney(input.MonthlyRent) || !SalesMath.ValidMoney(input.Deposit))
            throw new TradeException("Revise la vigencia, el día de vencimiento y los importes acordados.", 400);
        if (!await db.Units.AnyAsync(u => u.Id == input.UnitId && u.BranchId == branchId)) throw new TradeException("Local no encontrado.", 404);
        if (input.PreviousId is int previousId)
        {
            var previous = await Contract(branchId, previousId);
            if (previous.UnitId != input.UnitId || previous.CustomerId != input.CustomerId || input.Start.DayNumber != (previous.EndedOn ?? previous.End).DayNumber + 1)
                throw new TradeException("La renovación conserva el local y el arrendatario y empieza al día siguiente de la vigencia anterior.");
        }
        if (await db.Leases.AnyAsync(c => c.UnitId == input.UnitId && c.Start <= input.End && input.Start <= (c.EndedOn ?? c.End)))
            throw new TradeException("El local ya tiene un contrato que coincide con esas fechas.");
        var masters = db.Masters.Where(x => x.BranchId == branchId && x.Active);
        var customer = await masters.SingleOrDefaultAsync(x => x.Id == input.CustomerId && x.Kind == "customers") ?? throw new TradeException("Arrendatario activo no encontrado.", 404);
        var service = await masters.SingleOrDefaultAsync(x => x.Id == input.ServiceId && x.Kind == "services") ?? throw new TradeException("Servicio de alquiler activo no encontrado.", 404);
        if (service.Currency != (await db.Branches.FindAsync(branchId))!.Currency)
            throw new TradeException("El servicio debe usar la moneda de caja de la sucursal.");
        var lease = new LeaseContract
        {
            UnitId = input.UnitId,
            PreviousId = input.PreviousId,
            CustomerId = customer.Id,
            ServiceId = service.Id,
            Tenant = customer.Name + " · " + customer.Code,
            Start = input.Start,
            End = input.End,
            MonthlyRent = input.MonthlyRent,
            Deposit = input.Deposit,
            DueDay = input.DueDay,
            Conditions = input.Conditions.Trim(),
            TaxRate = service.TaxRate!.Value,
            Currency = service.Currency!
        };
        db.Leases.Add(lease);
        await db.SaveChangesAsync();
        return lease.Id;
    });

    [HttpPost("contracts/{id:int}/end")]
    public Task<ActionResult<int>> End(int branchId, int id, EndLeaseInput input) => Write(branchId, AccessRules.RentalWrite, "end", async () =>
    {
        var lease = await Contract(branchId, id);
        if (lease.EndedOn == input.Date && lease.EndReason == input.Reason.Trim()) return id;
        if (lease.EndedOn != null || input.Date < lease.Start || input.Date > lease.End)
            throw new TradeException("La fecha de finalización debe estar dentro de la vigencia de un contrato sin finalizar.");
        if (await db.RentCharges.AnyAsync(q => q.LeaseId == id && q.Through > input.Date) || await db.Leases.AnyAsync(c => c.PreviousId == id && input.Date != lease.End))
            throw new TradeException("La fecha afectaría cargos ya generados o una renovación registrada.");
        lease.EndedOn = input.Date;
        lease.EndReason = input.Reason.Trim();
        return id;
    });

    [HttpPost("contracts/{id:int}/months")]
    public Task<ActionResult<int>> Month(int branchId, int id, RentMonthInput input) => Write(branchId, AccessRules.RentalWrite, "month", async () =>
    {
        var lease = await Contract(branchId, id);
        var month = RentCalendar.ForMonth(lease, input.Month) ?? throw new TradeException("El periodo queda fuera de la vigencia del contrato.");
        var reference = input.Month.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        var existing = await db.RentCharges.SingleOrDefaultAsync(c => c.LeaseId == id && c.Kind == "rent" && c.Reference == reference);
        return existing?.Id ?? await AddCharge(branchId, lease, "rent", reference, "Renta " + reference, month.From, month.Through, month.Due, month.Amount);
    });

    [HttpPost("contracts/{id:int}/extras")]
    public Task<ActionResult<int>> Extra(int branchId, int id, ExtraChargeInput input) => Write(branchId, AccessRules.RentalExtra, "extra", async () =>
    {
        var lease = await Contract(branchId, id);
        if (input.Due < lease.Start || input.Due > (lease.EndedOn ?? lease.End) || input.Amount <= 0 || !SalesMath.ValidMoney(input.Amount))
            throw new TradeException("Revise el importe positivo y el vencimiento dentro de la vigencia.", 400);
        var reference = input.Reference.Trim().ToUpperInvariant();
        var reason = input.Reason.Trim();
        var existing = await db.RentCharges.SingleOrDefaultAsync(c => c.LeaseId == id && c.Kind == "extra" && c.Reference == reference);
        if (existing != null) return existing.Amount == input.Amount && existing.Description == reason && existing.Due == input.Due ? existing.Id
            : throw new TradeException("La referencia ya corresponde a otro cargo adicional.");
        return await AddCharge(branchId, lease, "extra", reference, reason, input.Due, input.Due, input.Due, input.Amount);
    });

    private async Task<int> AddCharge(int branchId, LeaseContract lease, string kind, string reference, string description, DateOnly from, DateOnly through, DateOnly due, decimal amount)
    {
        if (amount <= 0) throw new TradeException("El prorrateo del periodo resulta en un importe inferior a un céntimo.");
        var sale = new Sale { BranchId = branchId, CustomerId = lease.CustomerId, Customer = lease.Tenant, Currency = lease.Currency, Terms = "credit", Stage = "confirmed", DueDate = due };
        sale.Lines.Add(new SaleLine
        {
            ItemId = lease.ServiceId,
            Description = $"{description} · contrato #{lease.Id}",
            Unit = "servicio",
            Quantity = 1,
            Price = amount,
            Total = amount,
            TaxRate = lease.TaxRate,
            Tax = SalesMath.IncludedTax(amount, lease.TaxRate),
            Lot = ""
        });
        var charge = new RentCharge { LeaseId = lease.Id, Sale = sale, Kind = kind, Reference = reference, Description = description, From = from, Through = through, Due = due, Amount = amount };
        db.RentCharges.Add(charge);
        await db.SaveChangesAsync();
        return charge.Id;
    }

    [HttpPost("contracts/{id:int}/deposits")]
    public Task<ActionResult<int>> Deposit(int branchId, int id, DepositInput input) => Write(branchId, AccessRules.Cash, "deposit", async () =>
    {
        var lease = await Contract(branchId, id);
        if (input.RequestId == Guid.Empty || input.Kind is not ("deposit-in" or "deposit-out") || input.Amount <= 0 || !SalesMath.ValidMoney(input.Amount))
            throw new TradeException("Revise la operación y el importe de la garantía.", 400);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input)));
        var prior = await db.SalesEvents.SingleOrDefaultAsync(x => x.BranchId == branchId && x.RequestId == input.RequestId);
        if (prior != null) return prior.DepositLeaseId == id && prior.RequestHash == hash ? id : throw new TradeException("La clave ya corresponde a otra operación.");
        var held = await db.SalesEvents.Where(e => e.DepositLeaseId == id).SumAsync(e => e.Kind == "deposit-in" ? e.Amount : -e.Amount);
        if (input.Kind == "deposit-in" ? input.Amount > lease.Deposit - held : input.Amount > held)
            throw new TradeException("La garantía excede el importe pactado o el saldo disponible para devolver.");
        if (!await db.CashSessions.AnyAsync(s => s.BranchId == branchId && s.Id == input.SessionId && s.Closed == null && s.Currency == lease.Currency))
            throw new TradeException("Se necesita una caja abierta en la moneda del contrato.");
        var e = new SalesEvent { BranchId = branchId, DepositLeaseId = id, Kind = input.Kind, Actor = access.Actor, RequestId = input.RequestId, RequestHash = hash, Note = $"Garantía · contrato #{id} · {input.Note.Trim()}" };
        var branch = await db.Branches.FindAsync(branchId);
        await new CashDesk(db).ApplyAsync(branch!, null, new TradeInput(input.RequestId, input.Kind, CashSessionId: input.SessionId, Amount: input.Amount, Method: input.Method), e, []);
        db.SalesEvents.Add(e);
        return id;
    });
}
