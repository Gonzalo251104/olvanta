namespace Olvanta.Application;

public static class AccessRules
{
    public const string Read = "masters.read";
    public const string Write = "masters.write";
    public const string InventoryRead = "inventory.read";
    public const string InventoryWrite = "inventory.write";
    public const string InventoryAdjust = "inventory.adjust";
    public const string Reserve = "inventory.reserve";
    public const string SalesRead = "sales.read";
    public const string Sell = "sales.write";
    public const string Discount = "sales.discount";
    public const string Dispatch = "sales.dispatch";
    public const string Returns = "sales.return";
    public const string Cash = "cash.write";
    public const string CashRead = "cash.read";
    public const string BillingRead = "billing.read";
    public const string Bill = "billing.write";
    public static readonly string[] All = [Read, Write, InventoryRead, InventoryWrite, InventoryAdjust, Reserve,
        SalesRead, Sell, Discount, Dispatch, Returns, Cash, CashRead, BillingRead, Bill];

    public static readonly IReadOnlyDictionary<string, string[]> Roles = new Dictionary<string, string[]>
    {
        ["supervisor"] = All,
        ["salesperson"] = [Read, Write, InventoryRead, Reserve, SalesRead, Sell],
        ["warehouse"] = [Read, InventoryRead, InventoryWrite, Reserve, SalesRead, Dispatch],
        ["cashier"] = [Read, SalesRead, Cash, CashRead],
        ["rentals"] = [Read],
        ["parking"] = [Read],
        ["billing"] = [Read, BillingRead, Bill],
        ["auditor"] = [Read, InventoryRead, SalesRead, CashRead, BillingRead]
    };
}
