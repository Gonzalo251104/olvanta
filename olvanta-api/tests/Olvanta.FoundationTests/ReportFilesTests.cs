using System.Text;
using Olvanta.Api.Reporting;
using Olvanta.Application;
using Xunit;

namespace Olvanta.FoundationTests;

public class ReportFilesTests
{
    [Fact]
    public void CsvPreservesMoneyAndEscapesTextAsData()
    {
        var report = new BranchReport("Caja", "Pruebas", "PEN", "Periodo", ["Importe", "Nota"],
            [["-6.50", " =1+1"], ["7.00", "Cliente, \"Centro\""]]);
        var csv = Encoding.UTF8.GetString(ReportFiles.Csv(report));
        Assert.Equal("\uFEFF\"Importe\",\"Nota\"\r\n\"-6.50\",\"' =1+1\"\r\n\"7.00\",\"Cliente, \"\"Centro\"\"\"\r\n", csv);
    }
}
