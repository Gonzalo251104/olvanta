using System.Globalization;
using Olvanta.Application;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Olvanta.Api.Billing;

public static class FiscalPdf
{
    public static byte[] Render(FiscalSnapshot s, string type, string series, int number, string mode, string digest)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var title = type switch
        {
            "01" => "Factura electrónica",
            "03" => "Boleta de venta electrónica",
            "07" => "Nota de crédito",
            "08" => "Nota de débito",
            "RC" => "Resumen diario",
            _ => "Comunicación de baja"
        };
        var payload = string.Join("|", s.Ruc, type, series, number.ToString(CultureInfo.InvariantCulture),
            FiscalXml.Amount(s.Tax), FiscalXml.Amount(s.Total), s.Date.ToString("yyyy-MM-dd"), s.CustomerType, s.CustomerId, digest, "");
        using var qr = QRCodeGenerator.GenerateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        using var renderer = new PngByteQRCode(qr);
        var png = renderer.GetGraphic(6);
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4); page.Margin(38); page.DefaultTextStyle(t => t.FontSize(10));
            page.Header().Column(c =>
            {
                c.Item().Text(mode == "simulation" ? "SIMULACIÓN LOCAL · SIN VALIDEZ TRIBUTARIA" : "PRUEBAS SUNAT BETA · SIN VALIDEZ TRIBUTARIA").Bold().FontColor("#793E55");
                c.Item().PaddingTop(8).Text(title).FontSize(23).Bold();
                c.Item().Text(series + "-" + number + "    ·    " + s.Date.ToString("dd/MM/yyyy")).FontSize(13);
            });
            page.Content().PaddingVertical(15).Column(c =>
            {
                c.Spacing(7); c.Item().Text($"{s.Issuer} · RUC {s.Ruc}").Bold();
                c.Item().Text(s.Address); c.Item().Text($"Adquirente: {s.Customer} · {s.CustomerId}");
                if (s.Reference != null) c.Item().Text($"Referencia: {s.Reference} · {s.Reason}");
                if (type == "01") c.Item().Text(s.Terms == "credit" && s.Outstanding > 0 ? $"Crédito · vence {s.Due:dd/MM/yyyy} · saldo {s.Currency} {FiscalXml.Amount(s.Outstanding)}" : "Contado");
                c.Item().Table(t =>
                {
                    t.ColumnsDefinition(cols => { cols.RelativeColumn(5); cols.RelativeColumn(2); cols.RelativeColumn(2); cols.RelativeColumn(2); });
                    t.Header(h => { foreach (var label in new[] { "Descripción", "Cantidad", "Precio unit.", "Importe" }) h.Cell().PaddingVertical(5).Text(label).Bold(); });
                    foreach (var l in s.Lines)
                    {
                        t.Cell().BorderTop(.5f).PaddingVertical(4).Text(l.Name);
                        t.Cell().Padding(4).Text($"{l.Quantity:0.######} {l.Unit}");
                        t.Cell().Padding(4).Text($"{l.UnitPrice:0.######}");
                        t.Cell().Padding(4).Text(FiscalXml.Amount(l.Total));
                    }
                });
                c.Item().AlignRight().Text($"Base {FiscalXml.Amount(s.Total - s.Tax)}    IGV {FiscalXml.Amount(s.Tax)}");
                c.Item().AlignRight().Text($"Total {s.Currency} {FiscalXml.Amount(s.Total)}").Bold().FontSize(15);
                c.Item().Text(FiscalLegend.Total(s.Total, s.Currency)).FontSize(9);
                c.Item().Width(95).Image(png);
                c.Item().Text("Resumen de firma: " + digest).FontSize(8);
            });
            page.Footer().AlignRight().Text(t => { t.Span("Olvanta · "); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
        })).GeneratePdf();
    }
}
