namespace Olvanta.Domain;

public sealed class CommercialUnit
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Address { get; set; }
    public required string Features { get; set; }
}

public sealed class LeaseContract
{
    public int Id { get; set; }
    public int UnitId { get; set; }
    public int? PreviousId { get; set; }
    public int CustomerId { get; set; }
    public int ServiceId { get; set; }
    public required string Tenant { get; set; }
    public required string Currency { get; set; }
    public required string Conditions { get; set; }
    public decimal TaxRate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal Deposit { get; set; }
    public int DueDay { get; set; }
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; }
    public DateOnly? EndedOn { get; set; }
    public string? EndReason { get; set; }
}

public sealed class RentCharge
{
    public int Id { get; set; }
    public int LeaseId { get; set; }
    public int SaleId { get; set; }
    public required Sale Sale { get; set; }
    public required string Kind { get; set; }
    public required string Reference { get; set; }
    public required string Description { get; set; }
    public DateOnly From { get; set; }
    public DateOnly Through { get; set; }
    public DateOnly Due { get; set; }
    public decimal Amount { get; set; }
}

public static class RentCalendar
{
    public static (DateOnly From, DateOnly Through, DateOnly Due, decimal Amount)? ForMonth(LeaseContract lease, DateOnly date)
    {
        int days = DateTime.DaysInMonth(date.Year, date.Month);
        var start = new DateOnly(date.Year, date.Month, 1);
        var end = start.AddDays(days - 1);
        var from = lease.Start > start ? lease.Start : start;
        var until = lease.EndedOn ?? lease.End;
        var through = until < end ? until : end;
        if (from > through) return null;
        var regularDue = new DateOnly(date.Year, date.Month, Math.Min(lease.DueDay, days));
        var due = regularDue < from ? from : regularDue > through ? through : regularDue;
        return (from, through, due, SalesMath.Money(lease.MonthlyRent * (through.DayNumber - from.DayNumber + 1) / days));
    }
}
