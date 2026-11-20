namespace Olvanta.Application;

public static class AccessRules
{
    public const string Read = "masters.read";
    public const string Write = "masters.write";
    public const string InventoryRead = "inventory.read";
    public const string InventoryWrite = "inventory.write";
    public const string InventoryAdjust = "inventory.adjust";
    public const string Reserve = "inventory.reserve";
    public static readonly string[] All = [Read, Write, InventoryRead, InventoryWrite, InventoryAdjust, Reserve];

    public static readonly IReadOnlyDictionary<string, string[]> Roles = new Dictionary<string, string[]>
    {
        ["supervisor"] = All,
        ["salesperson"] = [Read, Write, InventoryRead, Reserve],
        ["warehouse"] = [Read, InventoryRead, InventoryWrite, Reserve],
        ["cashier"] = [Read],
        ["rentals"] = [Read],
        ["parking"] = [Read],
        ["billing"] = [Read],
        ["auditor"] = [Read, InventoryRead]
    };
}
