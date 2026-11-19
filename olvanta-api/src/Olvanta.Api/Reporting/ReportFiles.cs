using System.Globalization;
using System.Text;
using Olvanta.Application;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Olvanta.Api.Reporting;

public static class ReportFiles
{
    public static byte[] Csv(BranchReport report)
    {
        static string Cell(string value)
        {
            var first = value.TrimStart().FirstOrDefault();
            if (first is '=' or '+' or '-' or '@' && !decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _))
                value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        var text = new StringBuilder("\uFEFF");
        text.AppendJoin(',', report.Columns.Select(Cell)).Append("\r\n");
        foreach (var row in report.Rows) text.AppendJoin(',', row.Select(Cell)).Append("\r\n");
        return Encoding.UTF8.GetBytes(text.ToString());
    }

    public static byte[] CashPdf(BranchReport report)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(34);
            page.DefaultTextStyle(style => style.FontSize(9));
            page.Header().Column(header =>
            {
                header.Spacing(4);
                header.Item().Text(report.Title).FontSize(24).Bold();
                header.Item().Text($"{report.Branch} · {report.Currency} · {report.Scope}").FontColor("#674052");
            });
            page.Content().PaddingVertical(18).Column(content =>
            {
                if (report.Rows.Count == 0) content.Item().Text("Sin arqueos en este periodo.");
                else content.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(0.55f);
                        columns.RelativeColumn(1.65f);
                        columns.RelativeColumn(1.65f);
                        for (int index = 3; index < report.Columns.Length; index++) columns.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        foreach (var label in report.Columns)
                            header.Cell().Background("#392735").Padding(6).Text(label).FontColor(Colors.White).SemiBold();
                    });
                    foreach (var row in report.Rows)
                        foreach (var cell in row)
                            table.Cell().BorderBottom(0.5f).BorderColor("#E1D8DB").Padding(6).Text(cell);
                });
            });
            page.Footer().AlignRight().Text(footer =>
            {
                footer.Span("Olvanta · ");
                footer.CurrentPageNumber();
                footer.Span(" / ");
                footer.TotalPages();
            });
        })).GeneratePdf();
    }
}
