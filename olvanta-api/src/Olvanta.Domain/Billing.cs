namespace Olvanta.Domain;

public sealed class FiscalDocument
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int SaleId { get; set; }
    public int? ParentId { get; set; }
    public Guid RequestId { get; set; }
    public required string RequestHash { get; set; }
    public required string Mode { get; set; }
    public required string SourceKey { get; set; }
    public bool Active { get; set; } = true;
    public required string Type { get; set; }
    public required string Series { get; set; }
    public int Number { get; set; }
    public required string Identifier { get; set; }
    public required string FileStem { get; set; }
    public required string Status { get; set; }
    public string? Ticket { get; set; }
    public Guid? Lease { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset? NextQuery { get; set; }
    public int PlannedQueries { get; set; }
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    public required string Snapshot { get; set; }
    public required string Digest { get; set; }
    public required byte[] Xml { get; set; }
    public required byte[] Zip { get; set; }
    public required byte[] Pdf { get; set; }
    public byte[]? Cdr { get; set; }
}

public sealed class FiscalAttempt
{
    public long Id { get; set; }
    public int DocumentId { get; set; }
    public DateTimeOffset Time { get; set; } = DateTimeOffset.UtcNow;
    public required string Actor { get; set; }
    public required string Action { get; set; }
    public required string Outcome { get; set; }
    public required string Detail { get; set; }
    public byte[]? Response { get; set; }
}

// Independent acknowledgement at the simulated recipient survives a lost HTTP response.
public sealed class SimulationReceipt
{
    public int DocumentId { get; set; }
    public required byte[] Cdr { get; set; }
}
