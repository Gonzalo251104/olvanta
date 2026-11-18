namespace Olvanta.Domain;

public sealed class Sale
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int CustomerId { get; set; }
    public required string Customer { get; set; }
    public required string Currency { get; set; }
    public required string Terms { get; set; }
    public DateOnly? DueDate { get; set; }
    public string Stage { get; set; } = "quote";
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    public List<SaleLine> Lines { get; set; } = [];
}

public sealed class SaleLine
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public int ItemId { get; set; }
    public required string Description { get; set; }
    public required string Unit { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal TaxRate { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public decimal Tax { get; set; }
    public int? WarehouseId { get; set; }
    public required string Lot { get; set; }
}

public sealed class CashSession
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public required string Currency { get; set; }
    public DateTimeOffset Opened { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? Closed { get; set; }
    public decimal Opening { get; set; }
    public decimal? Counted { get; set; }
}

// One immutable entry for each accepted business command, including each collection or refund.
public sealed class SalesEvent
{
    public int? DepositLeaseId { get; set; }
    public long Id { get; set; }
    public Guid RequestId { get; set; }
    public required string RequestHash { get; set; }
    public int BranchId { get; set; }
    public int? SaleId { get; set; }
    public int? LineId { get; set; }
    public int? CashSessionId { get; set; }
    public long? ReturnId { get; set; }
    public required string Kind { get; set; }
    public required string Actor { get; set; }
    public required string Note { get; set; }
    public string? Method { get; set; }
    public decimal Amount { get; set; }
    public decimal Quantity { get; set; }
    public decimal CashDelta { get; set; }
    public DateTimeOffset Time { get; set; } = DateTimeOffset.UtcNow;
}

public static class SalesMath
{
    public static decimal Money(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    public static bool ValidMoney(decimal amount) => amount is >= 0 and <= 99999999999999.99m && Money(amount) == amount;
    public static decimal LineTotal(decimal price, decimal quantity, decimal discount) =>
        Money(price * quantity * (1 - discount / 100m));
    public static decimal IncludedTax(decimal total, decimal rate) => total - Money(total / (1 + rate / 100m));
    public static decimal ReturnedAmount(decimal total, decimal ordered, decimal previouslyReturned, decimal quantity, int scale)
    {
        decimal Share(decimal q) => q == ordered ? total : decimal.Round(q / ordered * total, scale, MidpointRounding.AwayFromZero);
        return Share(previouslyReturned + quantity) - Share(previouslyReturned);
    }
    public static decimal Balance(decimal total, IEnumerable<SalesEvent> events) =>
        total - events.Where(e => e.Kind is "pay" or "return").Sum(e => e.Amount) + events.Where(e => e.Kind == "refund").Sum(e => e.Amount);
}
