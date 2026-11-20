using Olvanta.Application;
using Olvanta.Domain;
using Xunit;

namespace Olvanta.FoundationTests;

public class StockRulesTests
{
    [Fact]
    public void FinalIssueConsumesResidualAfterWeightedAverageRounding()
    {
        var value = StockMath.ReceiptValue(1, 1.5m) + StockMath.ReceiptValue(2, 1);
        var first = StockMath.IssueValue(1, 3, value);
        var last = StockMath.IssueValue(2, 2, value - first);
        Assert.Equal(1.166667m, first);
        Assert.Equal(value, first + last);
    }

    [Fact]
    public void MinimumUsesUnreservedQuantityInBaseUnits()
    {
        var balance = new BalanceView(1, 2, 0.3m, 0.2m, 3, 0.1m, []);
        Assert.Equal(0.1m, balance.Available);
        Assert.False(balance.BelowMinimum);
        Assert.True((balance with { Reserved = 0.200001m }).BelowMinimum);
    }

    [Fact]
    public void FractionalQuantitiesAreNeverSilentlyRounded()
    {
        Assert.True(StockMath.ValidQuantity(0.000001m));
        Assert.False(StockMath.ValidQuantity(0.0000001m));
        Assert.False(StockMath.ValidQuantity(0));
        Assert.False(StockMath.ValidCost(-1));
    }
}
