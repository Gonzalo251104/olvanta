using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Olvanta.Api.Commerce;
using Olvanta.Api.Security;
using Olvanta.Application;
using Olvanta.Domain;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Controllers;

[Authorize, ApiController, Route("api/v1/branches/{branchId:int}/parking")]
public sealed class ParkingController(OlvantaDbContext db, SessionAccess access) : ControllerBase
{
    private const string SitePath = "sites/{siteId:int}";
    private static TradeException Invalid(string message) => new(message, 400);
    private static string Hash<T>(T data) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(data)));

    private static string Plate(string input)
    {
        var plate = Regex.Replace(input.Trim().ToUpperInvariant(), @"[\s-]+", "");
        if (!Regex.IsMatch(plate, "^[A-Z0-9]{3,12}$")) throw Invalid("Ingrese una placa de 3 a 12 letras o dígitos.");
        return plate;
    }

    private Task<ParkingSite?> FindSite(int branchId, int siteId) => db.ParkingSites.SingleOrDefaultAsync(s => s.BranchId == branchId && s.Id == siteId);
    private async Task<ParkingSite> Site(int branchId, int siteId) => await FindSite(branchId, siteId) ?? throw new TradeException("Estacionamiento no encontrado.", 404);
    private async Task<ParkingStay> Stay(int siteId, int id) => await db.ParkingStays.Include(s => s.Payment).SingleOrDefaultAsync(s => s.SiteId == siteId && s.Id == id)
        ?? throw new TradeException("Ticket no encontrado.", 404);
    private Task<ParkingRate?> Rate(int siteId) => db.ParkingRates.Where(r => r.SiteId == siteId).OrderByDescending(r => r.Id).FirstOrDefaultAsync();

    [HttpGet]
    public async Task<ActionResult<ParkingDeskView>> Desk(int branchId, int? siteId, string? historyPlate)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.ParkingRead)) return Forbid();
        var plate = string.IsNullOrWhiteSpace(historyPlate) ? null : Plate(historyPlate);
        await using var tx = await db.Database.BeginTransactionAsync();
        var branch = await db.ReadInventoryBranchAsync(branchId);
        var sites = await db.ParkingSites.Where(s => s.BranchId == branchId).OrderBy(s => s.Name).Select(s => new ParkingSiteView(s.Id, s.Name, s.Currency)).ToListAsync();
        var site = siteId == null ? sites.FirstOrDefault() : sites.Find(s => s.Id == siteId);
        if (siteId != null && site == null) return NotFound();
        List<ParkingZoneView> zones = [];
        List<ParkingStayView> stays = [];
        List<ParkingPassView> passes = [];
        ParkingRateView? rate = null;
        if (site != null)
        {
            var id = site.Id;
            var latest = await Rate(id);
            if (latest != null) rate = new(latest.Id, latest.ServiceId, latest.BlockMinutes, latest.GraceMinutes, latest.BlockPrice);
            zones = await db.ParkingZones.Where(z => z.SiteId == id).OrderBy(z => z.Name)
                .Select(z => new ParkingZoneView(z.Id, z.Name, z.Capacity, db.ParkingStays.Count(s => s.ZoneId == z.Id && s.Exited == null))).ToListAsync();
            IQueryable<ParkingStayView> Tickets(IQueryable<ParkingStay> source) =>
                from s in source
                join r in db.ParkingRates on s.RateId equals r.Id
                orderby s.Id descending
                select new ParkingStayView(s.Id, s.ZoneId, s.Plate, s.Entered, s.Exited, s.PassId, r.BlockMinutes, r.GraceMinutes, r.BlockPrice,
                    s.Blocks, s.Exited == null ? null : s.Payment == null ? 0 : s.Payment.Amount, s.Payment == null ? null : s.Payment.Method,
                    s.Payment == null ? null : s.Payment.SaleId, s.PaymentId);
            var siteStays = db.ParkingStays.Where(s => s.SiteId == id);
            stays = await Tickets(siteStays.Where(s => s.Exited == null)).ToListAsync();
            stays.AddRange(await Tickets(siteStays.Where(s => s.Exited != null && (plate == null || s.Plate == plate))).Take(100).ToListAsync());
            passes = await db.ParkingPasses.Where(p => p.SiteId == id).OrderByDescending(p => p.Id)
                .Select(p => new ParkingPassView(p.Id, p.Plate, p.From, p.Through, p.Payment.Amount, p.Payment.Method, p.Payment.SaleId, p.PaymentId)).Take(100).ToListAsync();
        }
        var masters = db.Masters.Where(m => m.BranchId == branchId && m.Active);
        var currency = branch!.Currency;
        var services = await masters.Where(m => m.Kind == "services" && m.Currency == currency).OrderBy(m => m.Name).Select(m => new InventoryOption(m.Id, m.Name)).ToListAsync();
        var customers = await masters.Where(m => m.Kind == "customers").OrderBy(m => m.Name).Select(m => new InventoryOption(m.Id, m.Name + " · " + m.Code)).ToListAsync();
        var cash = await db.CashSessions.Where(s => s.BranchId == branchId && s.Closed == null).Select(s => (int?)s.Id).SingleOrDefaultAsync();
        await tx.CommitAsync();
        return new ParkingDeskView(ParkingClock.LimaDate(DateTimeOffset.UtcNow), sites, site?.Id, zones, rate, stays, passes, cash, services, customers);
    }

    private async Task<ActionResult<int>> Write(int branchId, string rule, string action, Func<Branch, Task<int>> operation)
    {
        if (!await access.AllowsAsync(branchId, rule)) return Forbid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var branch = await db.LockBranchAsync(branchId);
        int result = await operation(branch!);
        access.Record("parking." + action, $"Registro #{result}", branchId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return result;
    }

    [HttpPost("sites")]
    public Task<ActionResult<int>> CreateSite(int branchId, ParkingSiteInput input) => Write(branchId, AccessRules.ParkingSetup, "site", async branch =>
    {
        var site = new ParkingSite { BranchId = branchId, Name = input.Name.Trim(), Currency = branch.Currency };
        db.ParkingSites.Add(site);
        await db.SaveChangesAsync();
        return site.Id;
    });

    [HttpPost(SitePath + "/zones")]
    public Task<ActionResult<int>> CreateZone(int branchId, int siteId, ParkingZoneInput input) => Write(branchId, AccessRules.ParkingSetup, "zone", async _ =>
    {
        await Site(branchId, siteId);
        var zone = new ParkingZone { SiteId = siteId, Name = input.Name.Trim(), Capacity = input.Capacity };
        db.ParkingZones.Add(zone);
        await db.SaveChangesAsync();
        return zone.Id;
    });

    [HttpPost(SitePath + "/rates")]
    public Task<ActionResult<int>> ChangeRate(int branchId, int siteId, ParkingRateInput input) => Write(branchId, AccessRules.ParkingSetup, "rate", async _ =>
    {
        var site = await Site(branchId, siteId);
        if (input.BlockPrice <= 0 || !SalesMath.ValidMoney(input.BlockPrice)) throw Invalid("Indique un precio positivo por bloque.");
        var service = await db.Masters.SingleOrDefaultAsync(m => m.Id == input.ServiceId && m.BranchId == branchId && m.Active && m.Kind == "services" && m.Currency == site.Currency)
            ?? throw Invalid("Seleccione un servicio activo en la moneda del estacionamiento.");
        var rate = new ParkingRate
        {
            SiteId = siteId,
            ServiceId = service.Id,
            BlockMinutes = input.BlockMinutes,
            GraceMinutes = input.GraceMinutes,
            BlockPrice = input.BlockPrice,
            TaxRate = service.TaxRate!.Value
        };
        db.ParkingRates.Add(rate);
        await db.SaveChangesAsync();
        return rate.Id;
    });

    [HttpPost(SitePath + "/entries")]
    public Task<ActionResult<int>> Enter(int branchId, int siteId, ParkingEntryInput input) => Write(branchId, AccessRules.ParkingEnter, "entry", async _ =>
    {
        await Site(branchId, siteId);
        if (input.RequestId == Guid.Empty) throw Invalid("Falta el identificador de ingreso.");
        var plate = Plate(input.Plate);
        var prior = await db.ParkingStays.SingleOrDefaultAsync(s => s.SiteId == siteId && s.EntryKey == input.RequestId);
        if (prior != null) return prior.Plate == plate && prior.ZoneId == input.ZoneId ? prior.Id : throw new TradeException("La clave ya corresponde a otro ingreso.");
        var zone = await db.ParkingZones.SingleOrDefaultAsync(z => z.SiteId == siteId && z.Id == input.ZoneId)
            ?? throw new TradeException("Zona no encontrada.", 404);
        if (await db.ParkingStays.AnyAsync(s => s.SiteId == siteId && s.Plate == plate && s.Exited == null))
            throw new TradeException("La placa ya tiene una estancia activa en el estacionamiento.");
        if (await db.ParkingStays.CountAsync(s => s.ZoneId == zone.Id && s.Exited == null) >= zone.Capacity)
            throw new TradeException("La zona está llena.");
        var rate = await Rate(siteId) ?? throw new TradeException("Configure la tarifa antes de registrar ingresos.");
        var now = DateTimeOffset.UtcNow;
        var day = ParkingClock.LimaDate(now);
        var pass = await db.ParkingPasses.Where(p => p.SiteId == siteId && p.Plate == plate && p.From <= day && day <= p.Through).Select(p => (int?)p.Id).SingleOrDefaultAsync();
        var stay = new ParkingStay { SiteId = siteId, ZoneId = zone.Id, Plate = plate, EntryKey = input.RequestId, RateId = rate.Id, PassId = pass, Entered = now };
        db.ParkingStays.Add(stay);
        await db.SaveChangesAsync();
        return stay.Id;
    });

    private async Task<(ParkingRate Rate, ParkingQuote Quote)> Calculate(ParkingSite site, ParkingStay stay, DateTimeOffset now)
    {
        var rate = (await db.ParkingRates.FindAsync(stay.RateId))!;
        var pass = stay.PassId == null ? null : await db.ParkingPasses.FindAsync(stay.PassId);
        int blocks = ParkingClock.Blocks(stay.Entered, now, pass?.Through, rate.GraceMinutes, rate.BlockMinutes);
        decimal amount = blocks * rate.BlockPrice;
        if (!SalesMath.ValidMoney(amount)) throw new TradeException("El importe de la estancia supera el máximo admitido.");
        return (rate, new ParkingQuote(stay.Id, blocks, amount, site.Currency));
    }

    [HttpGet(SitePath + "/stays/{id:int}/quote")]
    public async Task<ActionResult<ParkingQuote>> Quote(int branchId, int siteId, int id)
    {
        if (!await access.AllowsAsync(branchId, AccessRules.ParkingRead)) return Forbid();
        var site = await Site(branchId, siteId);
        var stay = await Stay(siteId, id);
        return stay.Exited == null ? (await Calculate(site, stay, DateTimeOffset.UtcNow)).Quote
            : new ParkingQuote(id, stay.Blocks!.Value, stay.Payment?.Amount ?? 0, site.Currency);
    }

    [HttpPost(SitePath + "/stays/{id:int}/exit")]
    public Task<ActionResult<int>> Exit(int branchId, int siteId, int id, ParkingExitInput input) => Write(branchId, AccessRules.ParkingCollect, "exit", async branch =>
    {
        var site = await Site(branchId, siteId);
        var stay = await Stay(siteId, id);
        if (input.RequestId == Guid.Empty) throw Invalid("Falta el identificador de salida.");
        var hash = Hash(input);
        if (stay.Exited != null)
            return stay.ExitKey == input.RequestId && stay.ExitHash == hash ? id : throw new TradeException("Este ticket ya tiene una salida registrada.");
        var now = DateTimeOffset.UtcNow;
        var (rate, quote) = await Calculate(site, stay, now);
        if (input.ExpectedAmount != quote.Amount) throw new TradeException("El importe cambió. Consulte el cálculo actualizado antes de cobrar.");
        if (quote.Amount > 0)
            stay.Payment = await Collect(branch, rate, input.RequestId, input.SessionId, input.Method, input.CustomerId, quote.Amount, $"Estacionamiento · ticket #{id} · {stay.Plate}");
        stay.ExitKey = input.RequestId;
        stay.ExitHash = hash;
        stay.Blocks = quote.Blocks;
        stay.Exited = now;
        return id;
    });

    [HttpPost(SitePath + "/passes")]
    public Task<ActionResult<int>> SellPass(int branchId, int siteId, ParkingPassInput input) => Write(branchId, AccessRules.ParkingCollect, "pass", async branch =>
    {
        var site = await Site(branchId, siteId);
        if (input.RequestId == Guid.Empty) throw Invalid("Falta el identificador del abono.");
        var hash = Hash(input);
        var prior = await db.ParkingPasses.SingleOrDefaultAsync(p => p.SiteId == siteId && p.RequestId == input.RequestId);
        if (prior != null) return prior.RequestHash == hash ? prior.Id : throw new TradeException("La clave ya corresponde a otro abono.");
        var plate = Plate(input.Plate);
        if (input.From < ParkingClock.LimaDate(DateTimeOffset.UtcNow) || input.Through < input.From || input.Price <= 0 || !SalesMath.ValidMoney(input.Price))
            throw Invalid("Revise las fechas y el importe del abono.");
        if (await db.ParkingPasses.AnyAsync(p => p.SiteId == siteId && p.Plate == plate && p.From <= input.Through && input.From <= p.Through))
            throw new TradeException("Ya existe un abono para esta placa que coincide con esas fechas.");
        var rate = await Rate(site.Id) ?? throw new TradeException("Configure la tarifa antes de vender abonos.");
        var payment = await Collect(branch, rate, input.RequestId, input.SessionId, input.Method, input.CustomerId, input.Price, $"Abono de estacionamiento · {plate} · {input.From:yyyy-MM-dd} a {input.Through:yyyy-MM-dd}");
        var pass = new ParkingPass { SiteId = siteId, Plate = plate, RequestId = input.RequestId, RequestHash = hash, From = input.From, Through = input.Through, Payment = payment };
        db.ParkingPasses.Add(pass);
        await db.SaveChangesAsync();
        return pass.Id;
    });

    private async Task<SalesEvent> Collect(Branch branch, ParkingRate rate, Guid key, int? sessionId, string? method, int? customerId, decimal amount, string note)
    {
        Sale? sale = null;
        if (customerId is int id)
        {
            var customer = await db.Masters.SingleOrDefaultAsync(m => m.Id == id && m.BranchId == branch.Id && m.Active && m.Kind == "customers")
                ?? throw new TradeException("Cliente no encontrado.", 404);
            sale = new Sale { BranchId = branch.Id, CustomerId = id, Customer = customer.Name + " · " + customer.Code, Currency = branch.Currency, Stage = "dispatched", Terms = "cash" };
            sale.Lines.Add(new SaleLine
            {
                ItemId = rate.ServiceId,
                Description = note,
                Unit = "servicio",
                Quantity = 1,
                Price = amount,
                Total = amount,
                TaxRate = rate.TaxRate,
                Tax = SalesMath.IncludedTax(amount, rate.TaxRate),
                Lot = ""
            });
            db.Sales.Add(sale);
            await db.SaveChangesAsync();
        }
        var kind = sale == null ? "parking-pay" : "pay";
        var payment = new SalesEvent { BranchId = branch.Id, SaleId = sale?.Id, RequestId = key, RequestHash = Hash(note), Kind = kind, Note = note, Actor = access.Actor };
        await new CashDesk(db).ApplyAsync(branch, sale, new TradeInput(key, kind, CashSessionId: sessionId, Amount: amount, Method: method), payment, []);
        db.SalesEvents.Add(payment);
        return payment;
    }
}
