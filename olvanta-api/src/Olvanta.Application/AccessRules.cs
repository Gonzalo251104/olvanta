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
    public const string RentalRead = "rentals.read";
    public const string RentalWrite = "rentals.write";
    public const string RentalExtra = "rentals.extra";
    public const string ParkingRead = "parking.read";
    public const string ParkingSetup = "parking.setup";
    public const string ParkingEnter = "parking.enter";
    public const string ParkingCollect = "parking.collect";
    public static readonly string[] All = [Read, Write, InventoryRead, InventoryWrite, InventoryAdjust, Reserve,
        SalesRead, Sell, Discount, Dispatch, Returns, Cash, CashRead, BillingRead, Bill, RentalRead, RentalWrite, RentalExtra,
        ParkingRead, ParkingSetup, ParkingEnter, ParkingCollect];

    public static readonly IReadOnlyDictionary<string, string[]> Roles = new Dictionary<string, string[]>
    {
        ["supervisor"] = All,
        ["salesperson"] = [Read, Write, InventoryRead, Reserve, SalesRead, Sell],
        ["warehouse"] = [Read, InventoryRead, InventoryWrite, Reserve, SalesRead, Dispatch],
        ["cashier"] = [Read, SalesRead, Cash, CashRead, RentalRead, ParkingRead, ParkingCollect],
        ["rentals"] = [Read, RentalRead, RentalWrite],
        ["parking"] = [Read, ParkingRead, ParkingEnter, ParkingCollect],
        ["billing"] = [Read, BillingRead, Bill],
        ["auditor"] = [Read, InventoryRead, SalesRead, CashRead, BillingRead, RentalRead, ParkingRead]
    };
}
