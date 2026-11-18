namespace Olvanta.Application;

public static class AccessRules
{
    public const string Read = "masters.read";
    public const string Write = "masters.write";

    public static readonly IReadOnlyDictionary<string, string[]> Roles = new Dictionary<string, string[]>
    {
        ["supervisor"] = [Read, Write],
        ["salesperson"] = [Read, Write],
        ["warehouse"] = [Read],
        ["cashier"] = [Read],
        ["rentals"] = [Read],
        ["parking"] = [Read],
        ["billing"] = [Read],
        ["auditor"] = [Read]
    };
}
