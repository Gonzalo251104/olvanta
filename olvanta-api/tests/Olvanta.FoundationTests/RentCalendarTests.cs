using Olvanta.Domain;
using Xunit;

namespace Olvanta.FoundationTests;

public class RentCalendarTests
{
    private static LeaseContract Lease(DateOnly start, DateOnly end, decimal rent) => new()
    {
        Start = start,
        End = end,
        MonthlyRent = rent,
        DueDay = 31,
        Tenant = "Cliente",
        Currency = "PEN",
        Conditions = "Renta mensual"
    };

    [Fact]
    public void ProratesInclusiveLeapFebruaryAndClampsTheDueDate()
    {
        var contract = Lease(new(2028, 2, 15), new(2028, 4, 30), 1000);
        var charge = RentCalendar.ForMonth(contract, new(2028, 2, 1))!.Value;
        Assert.Equal(517.24m, charge.Amount);
        Assert.Equal(new DateOnly(2028, 2, 29), charge.Due);
        Assert.Equal(1000m, RentCalendar.ForMonth(contract, new(2028, 3, 31))!.Value.Amount);
        contract.EndedOn = new(2028, 3, 7);
        Assert.Equal(225.81m, RentCalendar.ForMonth(contract, new(2028, 3, 1))!.Value.Amount);
        Assert.Null(RentCalendar.ForMonth(contract, new(2028, 4, 1)));
    }
}
