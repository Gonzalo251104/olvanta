using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Olvanta.Api.Reporting;
using Olvanta.Api.Security;
using Olvanta.Application;
using Olvanta.Domain;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Controllers;

[Authorize, ApiController, Route("api/v1/branches/{branchId:int}/reports/{kind}")]
public sealed class ReportsController(OlvantaDbContext db, SessionAccess access) : ControllerBase
{
    private const int Limit = 5000;
    private static readonly TimeSpan Lima = TimeSpan.FromHours(-5);
    private static string Text(object? value) => value switch
    {
        null => "",
        decimal amount => amount.ToString("0.00####", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToOffset(Lima).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateOnly day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
    };

    private static string[] Cells(params object?[] cells) => cells.Select(Text).ToArray();

    [HttpGet]
    public async Task<ActionResult<BranchReport>> Read(int branchId, string kind, [FromQuery] ReportFilter filter) =>
        await Allowed(branchId, kind) ? await Build(branchId, kind, filter) : Forbid();

    [HttpGet("export/{format}")]
    public async Task<IActionResult> Export(int branchId, string kind, string format, [FromQuery] ReportFilter filter)
    {
        if (!await Allowed(branchId, kind)) return Forbid();
        if (format != "csv" && !(format == "pdf" && kind == "cash")) return NotFound();
        var report = await Build(branchId, kind, filter);
        var file = $"olvanta-{kind}-{branchId}.{format}";
        return format == "csv" ? File(ReportFiles.Csv(report), "text/csv; charset=utf-8", file)
            : File(ReportFiles.CashPdf(report), "application/pdf", file);
    }

    private Task<bool> Allowed(int branchId, string kind) => access.AllowsAsync(branchId, kind switch
    {
        "sales" or "debt" => AccessRules.SalesRead,
        "cash" or "collections" => AccessRules.CashRead,
        "stock" or "ledger" or "lots" => AccessRules.InventoryRead,
        "rentals" => AccessRules.RentalRead,
        "parking" or "parking-income" => AccessRules.ParkingRead,
        "billing" => AccessRules.BillingRead,
        _ => throw new TradeException("Reporte no disponible.", 404)
    });

    private async Task<BranchReport> Build(int branchId, string kind, ReportFilter filter)
    {
        var today = ParkingClock.LimaDate(DateTimeOffset.UtcNow);
        var from = filter.From ?? new DateOnly(today.Year, today.Month, 1);
        var through = filter.Through ?? today;
        if (through < from || through == DateOnly.MaxValue) throw new TradeException("Revise el rango de fechas.", 400);
        var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), Lima);
        var end = new DateTimeOffset(through.AddDays(1).ToDateTime(TimeOnly.MinValue), Lima);
        var period = $"{Text(from)} a {Text(through)} · Lima";
        var current = "Corte al consultar";
        await using var transaction = await db.Database.BeginTransactionAsync();
        var branch = await db.ReadInventoryBranchAsync(branchId) ?? throw new TradeException("Sucursal no encontrada.", 404);
        var journal = db.SalesEvents.Where(e => e.BranchId == branchId);
        var sales = db.Sales.Where(s => s.BranchId == branchId && (s.Stage == "confirmed" || s.Stage == "dispatched"));
        var ledger = db.Movements.Where(m => m.BranchId == branchId);
        var masters = db.Masters.Where(m => m.BranchId == branchId);
        var warehouses = db.Warehouses.Where(w => w.BranchId == branchId);
        BranchReport Report(string title, string scope, string[] columns, List<string[]> rows)
        {
            if (rows.Count > Limit)
                throw new TradeException("El reporte supera 5.000 filas. Acote el periodo, el vendedor o el lote donde corresponda.", 422);
            return new(title, branch.Name, branch.Currency, scope, columns, rows);
        }

        BranchReport report;
        switch (kind)
        {
            case "sales":
                var confirmed = journal.Where(e => e.Kind == "confirm" && e.Time >= start && e.Time < end);
                var seller = filter.Seller?.Trim();
                var invoices = from s in sales
                               join e in confirmed on s.Id equals e.SaleId
                               let author = journal.Where(x => x.SaleId == s.Id && x.Kind == "quote").Select(x => x.Actor).FirstOrDefault() ?? e.Actor
                               where string.IsNullOrEmpty(seller) || author == seller
                               orderby e.Time, e.Id
                               select new
                               {
                                   s.Id,
                                   e.Time,
                                   s.Customer,
                                   Seller = author,
                                   Total = s.Lines.Sum(l => l.Total),
                                   Returns = journal.Where(x => x.SaleId == s.Id && x.Kind == "return").Sum(x => x.Amount)
                               };
                report = Report("Ventas confirmadas", period, ["Venta", "Confirmación", "Vendedor", "Cliente", "Importe", "Devoluciones"],
                    (await invoices.Take(Limit + 1).ToListAsync()).Select(x => Cells(x.Id, x.Time, x.Seller, x.Customer, x.Total, x.Returns)).ToList());
                break;
            case "debt":
                var debts = from s in sales
                            let balance = s.Lines.Sum(l => l.Total) - journal.Where(e => e.SaleId == s.Id && (e.Kind == "pay" || e.Kind == "return")).Sum(e => e.Amount)
                                + journal.Where(e => e.SaleId == s.Id && e.Kind == "refund").Sum(e => e.Amount)
                            where balance > 0
                            orderby s.DueDate, s.Id
                            select new { s.Id, s.Customer, s.DueDate, Balance = balance };
                report = Report("Cuentas por cobrar", current, ["Cuenta", "Cliente", "Vencimiento", "Saldo"],
                    (await debts.Take(Limit + 1).ToListAsync()).Select(x => Cells(x.Id, x.Customer, x.DueDate, x.Balance)).ToList());
                break;
            case "cash":
                var sessions = await db.CashSessions.Where(s => s.BranchId == branchId &&
                        (s.Closed != null ? s.Closed >= start && s.Closed < end : s.Opened >= start && s.Opened < end))
                    .OrderBy(s => s.Id).Select(s => new
                    {
                        s.Id,
                        s.Opened,
                        s.Closed,
                        s.Opening,
                        s.Counted,
                        Delta = journal.Where(e => e.CashSessionId == s.Id).Sum(e => e.CashDelta)
                    }).Take(Limit + 1).ToListAsync();
                report = Report("Arqueos de caja", period, ["Caja", "Apertura", "Cierre", "Fondo", "Variación", "Esperado", "Contado", "Diferencia"],
                    sessions.Select(x => Cells(x.Id, x.Opened, x.Closed, x.Opening, x.Delta, x.Opening + x.Delta, x.Counted, x.Counted - x.Opening - x.Delta)).ToList());
                break;
            case "collections":
                var collections = await journal.Where(e => e.CashSessionId != null && e.Time >= start && e.Time < end && e.Method != null)
                    .OrderBy(e => e.Time).ThenBy(e => e.Id).Select(e => new { e.Id, e.Time, e.CashSessionId, e.Kind, e.Method, e.Amount, e.CashDelta, e.Note })
                    .Take(Limit + 1).ToListAsync();
                report = Report("Cobros y devoluciones", period, ["Movimiento", "Fecha", "Caja", "Tipo", "Medio", "Importe", "Efectivo", "Detalle"],
                    collections.Select(x => Cells(x.Id, x.Time, x.CashSessionId, x.Kind, x.Method, x.Amount, x.CashDelta, x.Note)).ToList());
                break;
            case "stock":
                var lot = filter.Lot?.Trim().ToUpperInvariant();
                var balances = from m in ledger.Where(x => string.IsNullOrEmpty(lot) || x.Lot == lot)
                               join p in masters on m.ProductId equals p.Id
                               join w in warehouses on m.WarehouseId equals w.Id
                               group m by new { m.WarehouseId, m.ProductId, p.Name, Store = w.Name, m.Lot, p.Unit } into g
                               where g.Sum(m => m.Quantity) != 0
                               orderby g.Key.WarehouseId, g.Key.ProductId, g.Key.Lot
                               select new
                               {
                                   g.Key.Name,
                                   g.Key.Store,
                                   g.Key.Lot,
                                   g.Key.Unit,
                                   Quantity = g.Sum(m => m.Quantity),
                                   Free = g.Sum(m => m.Quantity - m.Reserved),
                                   Value = g.Sum(m => m.Value)
                               };
                report = Report("Existencias y valor de kardex", current, ["Producto", "Almacén", "Lote", "Unidad", "Existencia", "Disponible", "Valor"],
                    (await balances.Take(Limit + 1).ToListAsync()).Select(x => Cells(x.Name, x.Store, x.Lot, x.Unit, x.Quantity, x.Free, x.Value)).ToList());
                break;
            case "ledger":
                var code = filter.Lot?.Trim().ToUpperInvariant();
                var entries = from m in ledger.Where(x => x.Time >= start && x.Time < end && (string.IsNullOrEmpty(code) || x.Lot == code))
                              join p in masters on m.ProductId equals p.Id
                              join w in warehouses on m.WarehouseId equals w.Id
                              orderby m.Time, m.Id
                              select new { m.Id, m.Time, m.Kind, Product = p.Name, Store = w.Name, m.Lot, m.Quantity, m.Reserved, m.Value, m.Reason };
                report = Report("Kardex por periodo", period, ["Movimiento", "Fecha", "Tipo", "Producto", "Almacén", "Lote", "Cantidad", "Reserva", "Valor", "Motivo"],
                    (await entries.Take(Limit + 1).ToListAsync()).Select(x => Cells(x.Id, x.Time, x.Kind, x.Product, x.Store, x.Lot, x.Quantity, x.Reserved, x.Value, x.Reason)).ToList());
                break;
            case "rentals":
                var units = await db.Units.Where(u => u.BranchId == branchId).OrderBy(u => u.Id)
                    .Select(u => new
                    {
                        u.Code,
                        u.Name,
                        Occupied = db.Leases.Any(c => c.UnitId == u.Id && c.Start <= today && (c.EndedOn ?? c.End) >= today),
                        Debt = (from c in db.Leases
                                join q in db.RentCharges on c.Id equals q.LeaseId
                                where c.UnitId == u.Id
                                select q.Amount - journal.Where(e => e.SaleId == q.SaleId && e.Kind == "pay").Sum(e => e.Amount)).Sum()
                    })
                    .Take(Limit + 1).ToListAsync();
                report = Report("Ocupación y deuda de locales", current, ["Código", "Local", "Ocupación", "Saldo de cuotas"],
                    units.Select(x => Cells(x.Code, x.Name, x.Occupied ? "Ocupado" : "Libre", x.Debt)).ToList());
                break;
            case "lots":
                var lotCode = filter.Lot?.Trim().ToUpperInvariant();
                var oliveLots = db.OliveLots.Where(l => l.BranchId == branchId);
                var lineage = from l in oliveLots
                              join root in oliveLots on (l.RootId ?? l.Id) equals root.Id
                              where string.IsNullOrEmpty(lotCode) || l.Code == lotCode || root.Code == lotCode
                              orderby root.Id, l.Id
                              select new
                              {
                                  l.Code,
                                  l.Product,
                                  l.Unit,
                                  l.KgPerUnit,
                                  OriginLot = root.Code,
                                  root.Variety,
                                  root.Grade,
                                  root.Origin,
                                  root.Supplier,
                                  root.SupplierLot,
                                  root.ReceiptId
                              };
                report = Report("Trazabilidad de lotes de aceitunas", current, ["Lote", "Producto", "Unidad", "Kg por unidad", "Lote original",
                        "Variedad", "Clasificación", "Procedencia", "Proveedor", "Lote del proveedor", "Recepción"],
                    (await lineage.Take(Limit + 1).ToListAsync()).Select(x => Cells(x.Code, x.Product, x.Unit, x.KgPerUnit,
                        x.OriginLot, x.Variety, x.Grade, x.Origin, x.Supplier, x.SupplierLot, x.ReceiptId)).ToList());
                break;
            case "parking":
                var zones = from s in db.ParkingSites.Where(s => s.BranchId == branchId)
                            join z in db.ParkingZones on s.Id equals z.SiteId
                            orderby s.Id, z.Id
                            select new { Site = s.Name, Zone = z.Name, z.Capacity, Occupied = db.ParkingStays.Count(o => o.ZoneId == z.Id && o.Exited == null) };
                report = Report("Ocupación de estacionamientos", current, ["Estacionamiento", "Zona", "Capacidad", "Ocupadas", "Libres"],
                    (await zones.Take(Limit + 1).ToListAsync()).Select(x => Cells(x.Site, x.Zone, x.Capacity, x.Occupied, x.Capacity - x.Occupied)).ToList());
                break;
            case "parking-income":
                var sites = db.ParkingSites.Where(s => s.BranchId == branchId);
                var stays = from s in sites
                            join o in db.ParkingStays on s.Id equals o.SiteId
                            where o.Payment != null
                            select new { Site = s.Name, Kind = "Salida", PaymentId = o.PaymentId!.Value };
                var passes = from s in sites
                             join p in db.ParkingPasses on s.Id equals p.SiteId
                             select new { Site = s.Name, Kind = "Abono", p.PaymentId };
                var parking = from o in stays.Concat(passes)
                              join e in journal on o.PaymentId equals e.Id
                              where e.Time >= start && e.Time < end
                              orderby e.Time, e.Id
                              select new { e.Id, e.Time, o.Site, o.Kind, e.Method, e.Amount };
                report = Report("Ingresos de estacionamientos", period, ["Movimiento", "Fecha", "Estacionamiento", "Tipo", "Medio", "Importe"],
                    (await parking.Take(Limit + 1).ToListAsync()).Select(x => Cells(x.Id, x.Time, x.Site, x.Kind, x.Method, x.Amount)).ToList());
                break;
            default:
                var pending = await db.FiscalDocuments.Where(d => d.BranchId == branchId && d.Status != "accepted" && d.Status != "voided")
                    .OrderBy(d => d.Created).ThenBy(d => d.Id).Select(d => new { d.Id, d.Identifier, d.Created, d.Mode, d.Status, d.Ticket, d.NextQuery })
                    .Take(Limit + 1).ToListAsync();
                report = Report("Comprobantes pendientes y rechazados", current, ["Registro", "Comprobante", "Fecha", "Entorno", "Estado", "Ticket", "Próxima consulta"],
                    pending.Select(x => Cells(x.Id, x.Identifier, x.Created, x.Mode, x.Status, x.Ticket, x.NextQuery)).ToList());
                break;
        }
        await transaction.CommitAsync();
        return report;
    }
}
