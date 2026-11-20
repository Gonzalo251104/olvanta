using Olvanta.Domain;
using Xunit;

namespace Olvanta.FoundationTests;

public class SalesRulesTests
{
    [Fact]
    public void DiscountAndIncludedTaxUseRoundedLineTotal()
    {
        var total = SalesMath.LineTotal(11.80m, 2, 10);
        Assert.Equal(21.24m, total);
        Assert.Equal(3.24m, SalesMath.IncludedTax(total, 18));
        Assert.Equal(0.01m, SalesMath.LineTotal(0.01m, 0.5m, 0));
    }

    [Fact]
    public void ThreePartialReturnsRecoverExactlyTheOriginalAmountAndValue()
    {
        foreach (var (value, scale) in new[] { (10m, 2), (1.100001m, 6) })
        {
            decimal refunded = 0;
            for (int previous = 0; previous < 3; previous++)
                refunded += SalesMath.ReturnedAmount(value, 3, previous, 1, scale);
            Assert.Equal(value, refunded);
        }
    }
}
