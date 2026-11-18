using System.ComponentModel.DataAnnotations;
using Olvanta.Domain;

namespace Olvanta.Application;

public sealed record QuoteLineInput(int ItemId, decimal Quantity, decimal Discount, int? WarehouseId, [MaxLength(80)] string? Lot);
public sealed record QuoteInput(Guid RequestId, int CustomerId, string Terms, DateOnly? DueDate, [MinLength(1), MaxLength(100)] QuoteLineInput[] Lines);
public sealed record TradeInput(Guid RequestId, string Kind, int? SaleId = null, int? LineId = null,
    int? CashSessionId = null, decimal? Amount = null, decimal? Quantity = null, string? Method = null,
    long? ReturnId = null, [MaxLength(240)] string? Note = null);

public sealed record TradeReceipt(long EventId, int? SaleId, int? CashSessionId);
public sealed record SaleItem(int Id, string Name, string Code, string Unit, string Kind, decimal Price, decimal TaxRate);
public sealed record SaleStock(int ItemId, int WarehouseId, string Lot, decimal Available);
public sealed record SalesCatalog(List<InventoryOption> Customers, List<SaleItem> Items, List<InventoryOption> Warehouses, List<SaleStock> Stock);
public sealed record TradeEntry(long Id, string Kind, DateTimeOffset Time, string Actor, string Note, decimal Amount,
    decimal Quantity, string? Method, decimal CashDelta, int? SaleId, int? LineId, int? CashSessionId, long? ReturnId)
{
    public static TradeEntry From(SalesEvent e) => new(e.Id, e.Kind, e.Time, e.Actor, e.Note, e.Amount, e.Quantity,
        e.Method, e.CashDelta, e.SaleId, e.LineId, e.CashSessionId, e.ReturnId);
}
public sealed record SaleLineDetail(int Id, int ItemId, string Description, string Unit, decimal Quantity,
    decimal Returnable, decimal Price, decimal Discount, decimal TaxRate, decimal Tax, decimal Total, string? Warehouse, string Lot);
public sealed record SaleDetail(int Id, string Customer, string Currency, string Terms, DateOnly? DueDate, string Stage,
    DateTimeOffset Created, decimal Total, decimal Balance, List<SaleLineDetail> Lines, List<TradeEntry> History, int? LeaseId = null);
public sealed record CashDetail(int Id, string Currency, DateTimeOffset Opened, DateTimeOffset? Closed, decimal Opening,
    decimal Expected, decimal? Counted, decimal? Difference, List<TradeEntry> History);

public sealed class TradeException(string message, int status = 409) : Exception(message)
{
    public int Status { get; } = status;
}
