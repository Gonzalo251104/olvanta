using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Olvanta.Api.Security;
using Olvanta.Application;
using Olvanta.Domain;
using Olvanta.Infrastructure.Persistence;

namespace Olvanta.Api.Billing;

public sealed class FiscalIssuer(OlvantaDbContext db, FiscalSigner signer, SessionAccess access, IConfiguration config)
{
    internal static FiscalSnapshot Read(FiscalDocument d) => JsonSerializer.Deserialize<FiscalSnapshot>(d.Snapshot)!;
    private static TradeException Invalid(string text) => new(text, 400);
    public async Task<int> Prepare(int branchId, FiscalInput input)
    {
        if (input.RequestId == Guid.Empty) throw Invalid("Indique un identificador para la operación.");
        var mode = signer.Mode;
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.LockBranchAsync(branchId);
        // A company owns the daily RC/RA sequence even when multiple branches prepare reports on the same date.
        var company = await db.Companies.FromSqlRaw("SELECT * FROM [organization].[Company] WITH (XLOCK, ROWLOCK)").SingleAsync();
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input)));
        var prior = await db.FiscalDocuments.Where(x => x.BranchId == branchId && x.RequestId == input.RequestId).Select(x => new { x.Id, x.RequestHash, x.Mode }).SingleOrDefaultAsync();
        if (prior != null) return prior.RequestHash == hash && prior.Mode == mode ? prior.Id : throw new TradeException("El identificador ya pertenece a otra solicitud.");
        var sale = await db.Sales.Include(x => x.Lines).SingleOrDefaultAsync(x => x.BranchId == branchId && x.Id == input.SaleId)
            ?? throw new TradeException("Venta no encontrada.", 404);
        if (sale.Stage is not ("confirmed" or "dispatched")) throw new TradeException("Confirme la venta antes de facturar.");
        var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5));
        var date = DateOnly.FromDateTime(now.DateTime);
        var parent = input.ParentId == null ? null : await db.FiscalDocuments.SingleOrDefaultAsync(x =>
            x.Id == input.ParentId && x.BranchId == branchId && x.SaleId == sale.Id && x.Mode == mode);
        var series = "";
        var type = input.Kind switch
        {
            "invoice" => "01",
            "receipt" => "03",
            "credit" => "07",
            "debit" => "08",
            "summary" => "RC",
            "void" when parent != null => parent.Series.StartsWith('B') ? "RC" : "RA",
            _ => throw Invalid("Tipo o referencia no válida.")
        };
        var batch = type is "RC" or "RA";
        var original = type is "01" or "03";
        if (!original && parent == null) throw new TradeException("Documento de origen no encontrado.", 404);
        if (original && input.ParentId != null) throw Invalid("La factura o boleta corresponde directamente a la venta.");
        FiscalSnapshot snapshot;
        string sourceKey;
        if (batch)
        {
            if (parent!.Type is "RC" or "RA") throw Invalid("Seleccione un comprobante o nota.");
            if (input.Kind == "summary" && (!parent.Series.StartsWith('B') || parent.Status != "awaiting-summary"))
                throw new TradeException("Solo procede el resumen de boletas o notas pendientes.");
            if (input.Kind == "void" && (parent.Status != "accepted" || !input.NotDelivered || string.IsNullOrWhiteSpace(input.Reason)))
                throw new TradeException("La baja requiere un comprobante aceptado, motivo y confirmación de que no fue otorgado.");
            if (input.Kind == "void" && await db.FiscalDocuments.AnyAsync(x => x.ParentId == parent.Id && x.Active && (x.Type == "07" || x.Type == "08")))
                throw new TradeException("El comprobante tiene una nota vinculada.");
            var frozen = Read(parent);
            if (input.Kind == "void" && date.DayNumber - frozen.Date.DayNumber > 7) throw new TradeException("La baja supera el plazo admitido por este perfil de pruebas.");
            snapshot = frozen with
            {
                Date = date,
                Time = now.ToString("HH:mm:ss"),
                Reference = parent.Identifier,
                ReferenceType = parent.Type,
                ReferenceDate = frozen.Date,
                Reason = input.Reason?.Trim(),
                ReasonCode = input.Kind,
                AffectedNumber = frozen.Reference,
                AffectedType = frozen.ReferenceType
            };
            series = type + "-" + date.ToString("yyyyMMdd");
            sourceKey = input.Kind + "/" + parent.Id;
        }
        else
        {
            var setup = await (from doc in db.Series
                               join site in db.Establishments on doc.EstablishmentId equals site.Id
                               where doc.Id == input.SeriesId && site.BranchId == branchId && doc.DocumentType == input.Kind
                               select new { doc.Code, SiteCode = site.Code, site.Address }).SingleOrDefaultAsync()
                               ?? throw Invalid("Seleccione una serie del tipo y sucursal correspondientes.");
            series = setup.Code;
            var prefix = original ? (type == "01" ? "F" : "B") : parent!.Series[..1];
            if (!Regex.IsMatch(series, "^" + prefix + "[A-Z0-9]{3}$")) throw Invalid($"Use una serie de cuatro caracteres que comience con {prefix}.");
            if (!Regex.IsMatch(setup.SiteCode, "^[0-9]{4}$")) throw Invalid("El código SUNAT del establecimiento debe tener cuatro dígitos.");
            if (original)
            {
                if (!IsRuc(company.TaxId)) throw Invalid("Configure un RUC válido en Administración → Organización.");
                var buyer = await db.Masters.SingleAsync(x => x.Id == sale.CustomerId);
                var buyerType = buyer.DocumentType switch { "RUC" => "6", "DNI" => "1", "CE" => "4", "Pasaporte" => "7", _ => "" };
                if (!sale.Customer.EndsWith(" · " + buyer.Code, StringComparison.Ordinal) || buyerType == "" || type == "01" && buyerType != "6" ||
                    buyerType == "6" && !IsRuc(buyer.Code) || buyerType == "1" && !Regex.IsMatch(buyer.Code, "^[0-9]{8}$") ||
                    !Regex.IsMatch(buyer.Code, "^[A-Za-z0-9]{1,15}$")) throw Invalid("Revise el documento del cliente de la venta. La factura requiere RUC válido.");
                var lines = sale.Lines.OrderBy(l => l.Id).Select(l => Freeze(l, input.ZeroTaxCode)).ToArray();
                var events = await db.SalesEvents.Where(x => x.SaleId == sale.Id).ToListAsync();
                snapshot = new FiscalSnapshot(company.Name, company.TaxId!, setup.Address, setup.SiteCode, sale.Customer[..^(buyer.Code.Length + 3)],
                    buyerType, buyer.Code, sale.Currency, date, now.ToString("HH:mm:ss"), sale.Terms, sale.DueDate,
                    Math.Max(0, SalesMath.Balance(lines.Sum(l => l.Total), events)), null, null, null, null, null, lines);
                sourceKey = "sale/" + sale.Id;
            }
            else
            {
                if (parent!.Type is not ("01" or "03") || parent.Status != "accepted") throw new TradeException("La nota requiere una factura o boleta aceptada.");
                if (await db.FiscalDocuments.AnyAsync(x => x.Active && x.ParentId == parent.Id && x.SourceKey == "void/" + parent.Id))
                    throw new TradeException("La baja del documento está en curso.");
                var frozen = Read(parent);
                FiscalLine line;
                if (type == "07")
                {
                    var ret = await db.SalesEvents.SingleOrDefaultAsync(e => e.Id == input.ReturnId && e.SaleId == sale.Id && e.Kind == "return")
                        ?? throw Invalid("Seleccione una devolución de esta venta.");
                    var source = frozen.Lines.Single(l => l.SaleLineId == ret.LineId);
                    var before = await db.SalesEvents.Where(e => e.SaleId == sale.Id && e.LineId == ret.LineId && e.Kind == "return" && e.Id < ret.Id).SumAsync(e => e.Quantity);
                    line = source with { Quantity = ret.Quantity, Total = ret.Amount, Tax = SalesMath.ReturnedAmount(source.Tax, source.Quantity, before, ret.Quantity, 2) };
                    sourceKey = "return/" + ret.Id;
                }
                else
                {
                    if (input.Charge is not decimal charge || charge <= 0 || !SalesMath.ValidMoney(charge) || string.IsNullOrWhiteSpace(input.ChargeReference))
                        throw Invalid("Indique el importe y una referencia única para el aumento de valor.");
                    var source = frozen.Lines.SingleOrDefault(l => l.SaleLineId == input.SaleLineId) ?? throw Invalid("Seleccione la partida afectada.");
                    line = source with { Quantity = 1, Unit = "NIU", UnitPrice = charge, Total = charge, Tax = SalesMath.IncludedTax(charge, source.Rate), Name = input.Reason ?? "" };
                    sourceKey = $"debit/{parent.Id}/{input.ChargeReference.Trim().ToUpperInvariant()}";
                }
                if (string.IsNullOrWhiteSpace(input.Reason) || line.Total <= 0) throw Invalid("La nota requiere un motivo y un importe positivo.");
                snapshot = frozen with
                {
                    Date = date,
                    Time = now.ToString("HH:mm:ss"),
                    Reason = input.Reason.Trim(),
                    ReasonCode = type == "07" ? "07" : "02",
                    SiteCode = setup.SiteCode,
                    Address = setup.Address,
                    Reference = parent.Identifier,
                    ReferenceType = parent.Type,
                    ReferenceDate = frozen.Date,
                    Lines = [line]
                };
            }
        }
        if (mode == "beta" && snapshot.Ruc != config["Billing:BetaRuc"]) throw Invalid("El RUC no coincide con el emisor configurado para BETA.");
        if (await db.FiscalDocuments.AnyAsync(x => x.Mode == mode && x.Active && x.SourceKey == sourceKey)) throw new TradeException("Esta operación ya tiene un documento vigente.");
        int number = 1 + (await db.FiscalDocuments.Where(x => x.Mode == mode && x.Type == type && x.Series == series).MaxAsync(x => (int?)x.Number) ?? 0);
        if (number > (batch ? 99999 : 99999999)) throw new TradeException("Serie agotada.");
        var id = $"{series}-{number}";
        var stem = snapshot.Ruc + "-" + (batch ? "" : type + "-") + id;
        var (xml, digest) = signer.Sign(FiscalXml.Build(snapshot, type, id), type);
        var docRow = new FiscalDocument
        {
            BranchId = branchId,
            SaleId = sale.Id,
            ParentId = parent?.Id,
            RequestId = input.RequestId,
            RequestHash = hash,
            SourceKey = sourceKey,
            Mode = mode,
            Number = number,
            Type = type,
            Series = series,
            Identifier = id,
            FileStem = stem,
            Status = mode == "simulation" && !batch && series.StartsWith('B') ? "awaiting-summary" : "prepared",
            Snapshot = JsonSerializer.Serialize(snapshot),
            Digest = digest,
            Xml = xml,
            Zip = FiscalFiles.Pack(stem + ".xml", xml),
            Pdf = FiscalPdf.Render(snapshot, type, series, number, mode, digest)
        };
        db.FiscalDocuments.Add(docRow);
        access.Record("billing.prepared", $"{mode} · {stem}", branchId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return docRow.Id;
    }

    internal static bool IsRuc(string? ruc)
    {
        if (ruc == null || !Regex.IsMatch(ruc, "^[0-9]{11}$")) return false;
        int[] weights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
        return (11 - weights.Select((w, i) => w * (ruc[i] - '0')).Sum() % 11) % 10 == ruc[10] - '0';
    }
    private static FiscalLine Freeze(SaleLine line, string? zero)
    {
        if (line.Total <= 0) throw Invalid("Este perfil requiere partidas con importe positivo. Las operaciones gratuitas tienen otro tratamiento fiscal.");
        var code = line.TaxRate switch
        {
            18 => "10",
            0 when zero is "20" or "30" => zero,
            _ => throw Invalid("El perfil admite IGV 18% y partidas a tasa cero con afectación explícita.")
        };
        var unit = line.Unit.Trim().ToUpperInvariant() switch
        {
            "KG" or "KGM" or "KILO" => "KGM",
            "L" or "LTR" => "LTR",
            "UNIDAD" or "UND" or "UN" or "NIU" or "BALDE" or "BOLSA" => "NIU",
            "SERVICIO" or "ZZ" => "ZZ",
            "M" or "MTR" => "MTR",
            "CAJA" or "BX" => "BX",
            _ => throw Invalid($"Falta la equivalencia de la unidad {line.Unit} con el catálogo de unidades.")
        };
        return new FiscalLine(line.Id, line.Description, unit, line.Quantity, line.Total / line.Quantity, line.Total, line.Tax, line.TaxRate, code);
    }
}
