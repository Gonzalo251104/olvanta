namespace Olvanta.Domain;

public sealed class OliveLot
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int ProductId { get; set; }
    public required string Code { get; set; }
    public required string Product { get; set; }
    public required string Unit { get; set; }
    public required string Presentation { get; set; }
    public decimal KgPerUnit { get; set; }
    public bool WholeUnits { get; set; }
    public int? RootId { get; set; }
    public int? ProcessId { get; set; }
    public long? ReceiptId { get; set; }
    public StockMovement? Receipt { get; set; }
    public string? Variety { get; set; }
    public string? Grade { get; set; }
    public string? Origin { get; set; }
    public string? SupplierLot { get; set; }
    public string? Supplier { get; set; }
    public decimal? Gross { get; set; }
    public decimal? Tare { get; set; }
}

public sealed class OliveProcess
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int SourceId { get; set; }
    public long ConsumeId { get; set; }
    public required StockMovement Consume { get; set; }
    public Guid OperationId { get; set; }
    public decimal InputKg { get; set; }
    public decimal LossKg { get; set; }
    public decimal LossValue { get; set; }
}

public static class OliveUnits
{
    public static bool IsBulk(string unit) => unit.Trim().ToUpperInvariant() is "KG" or "KGM" or "KILO";

    public static bool Valid(OliveLot lot, decimal quantity) =>
        StockMath.ValidQuantity(quantity) && (!lot.WholeUnits || decimal.Truncate(quantity) == quantity);

    public static decimal ValueShare(decimal totalValue, decimal weight, decimal sumWeight) =>
        weight == sumWeight ? totalValue : decimal.Round(totalValue * (weight / sumWeight), 6, MidpointRounding.AwayFromZero);
}
