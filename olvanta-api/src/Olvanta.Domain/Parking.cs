namespace Olvanta.Domain;

public sealed class ParkingSite
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public required string Name { get; set; }
    public required string Currency { get; set; }
}

public sealed class ParkingZone
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public required string Name { get; set; }
    public int Capacity { get; set; }
}

public sealed class ParkingRate
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public int ServiceId { get; set; }
    public int BlockMinutes { get; set; }
    public int GraceMinutes { get; set; }
    public decimal BlockPrice { get; set; }
    public decimal TaxRate { get; set; }
}

public sealed class ParkingPass
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public required string Plate { get; set; }
    public DateOnly From { get; set; }
    public DateOnly Through { get; set; }
    public Guid RequestId { get; set; }
    public required string RequestHash { get; set; }
    public long PaymentId { get; set; }
    public required SalesEvent Payment { get; set; }
}

public sealed class ParkingStay
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public int ZoneId { get; set; }
    public int RateId { get; set; }
    public int? PassId { get; set; }
    public required string Plate { get; set; }
    public Guid EntryKey { get; set; }
    public DateTimeOffset Entered { get; set; }
    public DateTimeOffset? Exited { get; set; }
    public Guid? ExitKey { get; set; }
    public string? ExitHash { get; set; }
    public int? Blocks { get; set; }
    public long? PaymentId { get; set; }
    public SalesEvent? Payment { get; set; }
}

public static class ParkingClock
{
    public static DateOnly LimaDate(DateTimeOffset date) => DateOnly.FromDateTime(date.ToOffset(TimeSpan.FromHours(-5)).DateTime);

    public static int Blocks(DateTimeOffset entered, DateTimeOffset exit, DateOnly? passThrough, int graceMinutes, int blockMinutes)
    {
        var start = entered;
        if (passThrough is DateOnly through)
        {
            if (LimaDate(exit) <= through) return 0;
            start = new DateTimeOffset(through.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-5));
        }
        var elapsed = exit - start;
        return elapsed <= TimeSpan.FromMinutes(graceMinutes) ? 0 : (int)Math.Ceiling(elapsed.TotalMinutes / blockMinutes);
    }
}
