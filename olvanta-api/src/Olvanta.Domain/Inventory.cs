namespace Olvanta.Domain;

public sealed class Warehouse
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public required string Name { get; set; }
}

public sealed class Purchase
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int SupplierId { get; set; }
    public required string Reference { get; set; }
    public required string Currency { get; set; }
    public DateTimeOffset Time { get; set; } = DateTimeOffset.UtcNow;
    public List<PurchaseLine> Lines { get; set; } = [];
}

public sealed class PurchaseLine
{
    public int Id { get; set; }
    public int PurchaseId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Received { get; set; }
}

public sealed class StockMinimum
{
    public int WarehouseId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class StockMovement
{
    public long Id { get; set; }
    public int BranchId { get; set; }
    public int WarehouseId { get; set; }
    public int ProductId { get; set; }
    public Guid OperationId { get; set; }
    public required string RequestHash { get; set; }
    public required string Kind { get; set; }
    public required string Actor { get; set; }
    public required string Reason { get; set; }
    public required string Lot { get; set; }
    public DateOnly? Expiry { get; set; }
    public int? PurchaseLineId { get; set; }
    public int? SaleLineId { get; set; }
    public long? RelatedId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Reserved { get; set; }
    public decimal Value { get; set; }
    public DateTimeOffset Time { get; set; } = DateTimeOffset.UtcNow;
}

public static class StockMath
{
    public static bool ValidQuantity(decimal value) =>
        value is > 0 and <= 99999999 && decimal.Round(value, 6) == value;

    public static bool ValidCost(decimal value) =>
        value is >= 0 and <= 99999999 && decimal.Round(value, 6) == value;

    public static decimal ReceiptValue(decimal quantity, decimal unitCost) =>
        decimal.Round(quantity * unitCost, 6, MidpointRounding.AwayFromZero);

    public static decimal IssueValue(decimal quantity, decimal onHand, decimal value) =>
        quantity == onHand ? value : decimal.Round(value / onHand * quantity, 6, MidpointRounding.AwayFromZero);
}
