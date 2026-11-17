namespace Olvanta.Domain;

public sealed class Branch
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string Currency { get; set; } = "PEN";
    public decimal TaxRate { get; set; }
}

public sealed class Company
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? TaxId { get; set; }
}

public sealed class Establishment
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public required string Name { get; set; }
    public required string Code { get; set; }
    public required string Address { get; set; }
}

public sealed class DocumentSeries
{
    public int Id { get; set; }
    public int EstablishmentId { get; set; }
    public required string DocumentType { get; set; }
    public required string Code { get; set; }
}

public sealed class MasterRecord
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public required string Kind { get; set; }
    public required string Name { get; set; }
    public required string Code { get; set; }
    public string? PersonType { get; set; }
    public string? DocumentType { get; set; }
    public string? Address { get; set; }
    public string? Contact { get; set; }
    public string? Terms { get; set; }
    public string? Category { get; set; }
    public string? Unit { get; set; }
    public string? Presentation { get; set; }
    public decimal? Conversion { get; set; }
    public string? Barcode { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public decimal? TaxRate { get; set; }
    public bool Active { get; set; } = true;
    public byte[] Version { get; set; } = [];
}

public sealed class AuditEntry
{
    public long Id { get; set; }
    public DateTimeOffset Time { get; set; } = DateTimeOffset.UtcNow;
    public required string Actor { get; set; }
    public required string Action { get; set; }
    public required string Detail { get; set; }
    public int? BranchId { get; set; }
    public int? MasterId { get; set; }
}
