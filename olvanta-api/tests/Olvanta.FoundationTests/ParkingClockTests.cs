using Olvanta.Domain;
using Xunit;

namespace Olvanta.FoundationTests;

public class ParkingClockTests
{
    [Theory]
    [InlineData(300, 0)]
    [InlineData(301, 1)]
    [InlineData(1800, 1)]
    [InlineData(1801, 2)]
    public void GraceAndBlockBoundariesAreInclusive(int seconds, int expected)
    {
        var entered = new DateTimeOffset(2026, 11, 18, 18, 0, 0, TimeSpan.Zero);
        Assert.Equal(expected, ParkingClock.Blocks(entered, entered.AddSeconds(seconds), null, 5, 30));
    }

    [Fact]
    public void OnlyTimeAfterTheLastCoveredDayIsChargeable()
    {
        var entered = new DateTimeOffset(2026, 11, 19, 3, 0, 0, TimeSpan.Zero);
        var through = new DateOnly(2026, 11, 18);
        Assert.Equal(0, ParkingClock.Blocks(entered, entered.AddHours(2).AddMinutes(5), through, 5, 30));
        Assert.Equal(1, ParkingClock.Blocks(entered, entered.AddHours(2).AddMinutes(6), through, 5, 30));
    }
}
