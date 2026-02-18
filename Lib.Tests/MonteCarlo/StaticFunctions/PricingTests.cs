using Xunit;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class PricingTests
{
    [Fact]
    public void SetLongTermGrowthRateAndPrices_UpdatesAllPricesCorrectly()
    {
        // Arrange
        var prices = new CurrentPrices
        {
            CurrentEquityInvestmentPrice   = 100m,
            CurrentMidTermInvestmentPrice  = 100m,
            CurrentShortTermInvestmentPrice = 100m,
        };
        var rates = new HypotheticalLifeTimeGrowthRate
        {
            SpGrowth       = 0.10m,
            CpiGrowth      = 0.02m,
            TreasuryGrowth = 0.01m,
        };
        var expectedLongTermPrice  = 100m + (100m * rates.SpGrowth);
        var expectedMidTermPrice   = 100m + (100m * (rates.SpGrowth * InvestmentConfig.MidTermGrowthRateModifier));
        var expectedShortTermPrice = 100m + (100m * (rates.SpGrowth * InvestmentConfig.ShortTermGrowthRateModifier));

        // Act
        var result = Pricing.SetLongTermGrowthRateAndPrices(prices, rates);

        // Assert
        Assert.Equal(rates.SpGrowth, result.CurrentEquityGrowthRate);
        Assert.Equal(expectedLongTermPrice,  result.CurrentEquityInvestmentPrice);
        Assert.Equal(expectedMidTermPrice,   result.CurrentMidTermInvestmentPrice);
        Assert.Equal(expectedShortTermPrice, result.CurrentShortTermInvestmentPrice);
    }

    [Fact]
    public void SetLongTermGrowthRateAndPrices_HandlesZeroGrowthRate()
    {
        // Arrange
        var prices = new CurrentPrices
        {
            CurrentEquityInvestmentPrice   = 100m,
            CurrentMidTermInvestmentPrice  = 100m,
            CurrentShortTermInvestmentPrice = 100m,
        };
        var rates = new HypotheticalLifeTimeGrowthRate
        {
            SpGrowth       = 0m,
            CpiGrowth      = 0m,
            TreasuryGrowth = 0m,
        };

        // Act
        prices = Pricing.SetLongTermGrowthRateAndPrices(prices, rates);

        // Assert
        Assert.Equal(100m, prices.CurrentEquityInvestmentPrice);
        Assert.Equal(100m, prices.CurrentMidTermInvestmentPrice);
        Assert.Equal(100m, prices.CurrentShortTermInvestmentPrice);
    }

    [Fact]
    public void SetLongTermGrowthRateAndPrices_HandlesNegativeGrowthRate()
    {
        // Arrange
        var prices = new CurrentPrices
        {
            CurrentEquityInvestmentPrice   = 100m,
            CurrentMidTermInvestmentPrice  = 100m,
            CurrentShortTermInvestmentPrice = 100m,
        };
        var rates = new HypotheticalLifeTimeGrowthRate
        {
            SpGrowth       = -0.10m,
            CpiGrowth      = 0m,
            TreasuryGrowth = 0m,
        };

        // Act
        prices = Pricing.SetLongTermGrowthRateAndPrices(prices, rates);

        // Assert
        Assert.Equal(90m, prices.CurrentEquityInvestmentPrice);

        var expectedMidTermPrice   = 100m + (100m * (rates.SpGrowth * InvestmentConfig.MidTermGrowthRateModifier));
        var expectedShortTermPrice = 100m + (100m * (rates.SpGrowth * InvestmentConfig.ShortTermGrowthRateModifier));
        Assert.Equal(expectedMidTermPrice,   prices.CurrentMidTermInvestmentPrice);
        Assert.Equal(expectedShortTermPrice, prices.CurrentShortTermInvestmentPrice);
    }
}
