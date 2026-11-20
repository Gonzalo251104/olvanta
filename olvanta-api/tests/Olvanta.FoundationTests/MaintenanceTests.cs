using Olvanta.Application;
using Olvanta.Domain;
using Xunit;

namespace Olvanta.FoundationTests;

public class MaintenanceTests
{
    [Fact]
    public void PartialReceiptsSumToTheValueOfASingleReceipt()
    {
        const decimal cost = 0.333333m;
        Assert.Equal(0.166667m, StockMath.PartialReceiptValue(0, 0.5m, cost));
        Assert.Equal(0.166666m, StockMath.PartialReceiptValue(0.5m, 0.5m, cost));
        Assert.Equal(StockMath.ReceiptValue(1, cost),
            StockMath.PartialReceiptValue(0, 0.5m, cost) + StockMath.PartialReceiptValue(0.5m, 0.5m, cost));
    }

    [Fact]
    public void CsvPreservesLeadingZeroesAndQuotedRecords()
    {
        var (rows, issues) = PartyImport.Parse(
            "\uFEFFcode,name,personType,documentType,active\r\n0007,\"Puesto, \"\"A\"\"\",company,Otro,0\r\n0008,Falta,company,,true",
            "customers");
        var party = Assert.Single(rows);
        Assert.Equal(2, party.Row);
        Assert.Equal("0007", party.Data.Code);
        Assert.Equal("Puesto, \"A\"", party.Data.Name);
        Assert.False(party.Data.Active);
        Assert.All(issues, issue => Assert.Equal(3, issue.Row));
        Assert.NotEmpty(issues);
    }
}
