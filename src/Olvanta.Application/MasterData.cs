using System.ComponentModel.DataAnnotations;
using Olvanta.Domain;

namespace Olvanta.Application;

public sealed record MasterInput(
    [Required, MaxLength(180)] string Name, [Required, MaxLength(40)] string Code,
    bool Active, [MaxLength(20)] string? PersonType = null, [MaxLength(20)] string? DocumentType = null,
    [MaxLength(300)] string? Address = null, [MaxLength(180)] string? Contact = null, [MaxLength(300)] string? Terms = null,
    [MaxLength(80)] string? Category = null, [MaxLength(40)] string? Unit = null, [MaxLength(80)] string? Presentation = null,
    decimal? Conversion = null, [MaxLength(64)] string? Barcode = null,
    decimal? Price = null, [MaxLength(3)] string? Currency = null, decimal? TaxRate = null, byte[]? Version = null)
{
    public static bool IsParty(string kind) => kind is "customers" or "suppliers";
    public static bool IsKind(string kind) => IsParty(kind) || kind is "products" or "services";

    public string? ValidateFor(string kind)
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Code)) return "Complete el nombre y el identificador.";
        if (IsParty(kind))
        {
            if (PersonType is not ("person" or "company") || string.IsNullOrWhiteSpace(DocumentType))
                return "Seleccione la clase de persona y el tipo de documento.";
            return null;
        }
        if (string.IsNullOrWhiteSpace(Category) || string.IsNullOrWhiteSpace(Unit)) return "Complete la categoría y la unidad.";
        if (Price is null or < 0 or > 99999999999999.99m || decimal.Round(Price.Value, 2) != Price)
            return "Indique un precio válido con hasta dos decimales.";
        if (Currency is not ("PEN" or "USD") || TaxRate is null or < 0 or > 100 || decimal.Round(TaxRate.Value, 2) != TaxRate)
            return "Revise la moneda y el porcentaje de impuesto.";
        if (kind == "products" && (Conversion is null or <= 0 or > 9999999999.999999m || decimal.Round(Conversion.Value, 6) != Conversion))
            return "Indique cuántas unidades contiene la presentación (hasta seis decimales).";
        return null;
    }

    public void Apply(MasterRecord target)
    {
        target.Name = Name.Trim();
        target.Code = Code.Trim().ToUpperInvariant();
        target.Active = Active;
        if (IsParty(target.Kind))
        {
            target.PersonType = PersonType;
            target.DocumentType = DocumentType!.Trim();
            target.Address = Address?.Trim();
            target.Contact = Contact?.Trim();
            target.Terms = Terms?.Trim();
        }
        else
        {
            target.Category = Category!.Trim();
            target.Unit = Unit!.Trim();
            target.Price = Price;
            target.Currency = Currency;
            target.TaxRate = TaxRate;
            if (target.Kind == "products")
            {
                target.Presentation = Presentation?.Trim();
                target.Conversion = Conversion;
                target.Barcode = Barcode?.Trim();
            }
        }
    }
}

public sealed record MasterView(int Id, string Kind, MasterInput Data)
{
    public static readonly System.Linq.Expressions.Expression<Func<MasterRecord, MasterView>> Project = x =>
        new MasterView(x.Id, x.Kind, new MasterInput(x.Name, x.Code, x.Active, x.PersonType, x.DocumentType, x.Address,
            x.Contact, x.Terms, x.Category, x.Unit, x.Presentation, x.Conversion, x.Barcode, x.Price, x.Currency, x.TaxRate, x.Version));
}

public sealed record MasterPage(int Total, int Page, int PageSize, IReadOnlyList<MasterView> Items);
