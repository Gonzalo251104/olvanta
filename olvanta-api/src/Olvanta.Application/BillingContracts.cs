using System.ComponentModel.DataAnnotations;

namespace Olvanta.Application;

public sealed record FiscalInput(Guid RequestId, int SaleId, [Required] string Kind, int? SeriesId = null,
    int? ParentId = null, long? ReturnId = null, decimal? Charge = null, [MaxLength(64)] string? ChargeReference = null,
    [MaxLength(200)] string? Reason = null, string? ZeroTaxCode = null, bool NotDelivered = false, int? SaleLineId = null);
public sealed record SubmissionInput(string Action, string? Scenario = null);
public sealed record FiscalLine(int SaleLineId, string Name, string Unit, decimal Quantity, decimal UnitPrice,
    decimal Total, decimal Tax, decimal Rate, string Affectation);
public sealed record FiscalSnapshot(string Issuer, string Ruc, string Address, string SiteCode, string Customer,
    string CustomerType, string CustomerId, string Currency, DateOnly Date, string Time, string Terms, DateOnly? Due,
    decimal Outstanding, string? Reference, string? ReferenceType, DateOnly? ReferenceDate, string? Reason,
    string? ReasonCode, FiscalLine[] Lines, string? AffectedNumber = null, string? AffectedType = null)
{
    public decimal Total => Lines.Sum(l => l.Total);
    public decimal Tax => Lines.Sum(l => l.Tax);
}
public sealed record FiscalSeries(int Id, string Kind, string Code, string Site);
public sealed record BillableReturn(long Id, int LineId, decimal Amount, string Reason);
public sealed record BillableSale(int Id, string Customer, string Currency, decimal Total, List<BillableReturn> Returns);
public sealed record BillRow(int Id, int SaleId, int? ParentId, string Type, string Identifier, string Status, string Mode, DateTimeOffset Created, bool HasCdr);
public sealed record BillingDesk(string Mode, List<FiscalSeries> Series, List<BillableSale> Sales, List<BillRow> Documents);
public sealed record AttemptView(long Id, DateTimeOffset Time, string Actor, string Action, string Outcome, string Detail, bool HasResponse);
public sealed record BillDetail(BillRow Document, FiscalSnapshot Data, string Digest, string? Ticket, List<AttemptView> Attempts);
