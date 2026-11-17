using Olvanta.Application;
using Olvanta.Domain;
using Xunit;

namespace Olvanta.FoundationTests;

public class MasterRulesTests
{
    private static readonly MasterInput Catalog = new("Entrega", "sv-01", true,
        Category: "Transporte", Unit: "servicio", Price: 20m, Currency: "PEN", TaxRate: 0m);

    [Fact]
    public void ServicesOmitInventoryFields()
    {
        var input = Catalog with { Barcode = "123", Conversion = 2m, Presentation = "caja" };
        Assert.Null(input.ValidateFor("services"));
        var row = new MasterRecord { Kind = "services", Code = "", Name = "" };
        input.Apply(row);
        Assert.Equal("SV-01", row.Code);
        Assert.Null(row.Conversion);
        Assert.Null(row.Barcode);
        Assert.Null(row.Presentation);
    }

    [Fact]
    public void ProductsRequirePositiveConversionAndExactPrice()
    {
        Assert.NotNull(Catalog.ValidateFor("products"));
        Assert.NotNull((Catalog with { Conversion = 0m }).ValidateFor("products"));
        Assert.NotNull((Catalog with { Conversion = 1m, Price = 0.001m }).ValidateFor("products"));
        Assert.Null((Catalog with { Conversion = 0.125m }).ValidateFor("products"));
    }

    [Theory]
    [InlineData("customers")]
    [InlineData("suppliers")]
    public void PartiesRequireDocumentTypeAndPersonClass(string kind)
    {
        Assert.NotNull(Catalog.ValidateFor(kind));
        Assert.Null((Catalog with { PersonType = "company", DocumentType = "RUC" }).ValidateFor(kind));
    }
}
