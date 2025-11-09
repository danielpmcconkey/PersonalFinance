using System.Diagnostics;
using Xunit;
using NodaTime;
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
            CurrentLongTermInvestmentPrice = 100m,
            CurrentMidTermInvestmentPrice = 100m,
            CurrentShortTermInvestmentPrice = 100m,
        };
        var growthRate = 0.10m;
        var expectedLongTermPrice = 100m + (100m * growthRate);
        var expectedMidTermPrice = 100m + (100m * (growthRate * InvestmentConfig.MidTermGrowthRateModifier));
        var expectedShortTermPrice = 100m + (100m * (growthRate * InvestmentConfig.ShortTermGrowthRateModifier));

        // Act
        var result = Pricing.SetLongTermGrowthRateAndPrices(prices, growthRate);

        // Assert
        // Long term growth
        Assert.Equal(growthRate, result.CurrentLongTermGrowthRate);
        
        // long term price
        Assert.Equal(expectedLongTermPrice, result.CurrentLongTermInvestmentPrice);

        // Mid term price
        Assert.Equal(expectedMidTermPrice, result.CurrentMidTermInvestmentPrice);

        // Short term price
        Assert.Equal(expectedShortTermPrice, result.CurrentShortTermInvestmentPrice);
    }

    [Fact]
    public void SetLongTermGrowthRateAndPrices_HandlesZeroGrowthRate()
    {
        // Arrange
        var prices = new CurrentPrices
        {
            CurrentLongTermInvestmentPrice = 100m,
            CurrentMidTermInvestmentPrice = 100m,
            CurrentShortTermInvestmentPrice = 100m,
        };
        var growthRate = 0m;

        // Act
        prices = Pricing.SetLongTermGrowthRateAndPrices(prices, growthRate);

        // Assert
        Assert.Equal(100m, prices.CurrentLongTermInvestmentPrice);
        Assert.Equal(100m, prices.CurrentMidTermInvestmentPrice);
        Assert.Equal(100m, prices.CurrentShortTermInvestmentPrice);
    }

    [Fact]
    public void SetLongTermGrowthRateAndPrices_HandlesNegativeGrowthRate()
    {
        // Arrange
        var prices = new CurrentPrices
        {
            CurrentLongTermInvestmentPrice = 100m,
            CurrentMidTermInvestmentPrice = 100m,
            CurrentShortTermInvestmentPrice = 100m,
        };
        var growthRate = -0.10m;

        // Act
        prices = Pricing.SetLongTermGrowthRateAndPrices(prices, growthRate);

        // Assert
        Assert.Equal(90m, prices.CurrentLongTermInvestmentPrice); // 100 + (100 * -0.10)
        
        // Mid term
        var expectedMidTermPrice = 100m + (100m * (growthRate * InvestmentConfig.MidTermGrowthRateModifier));
        Assert.Equal(expectedMidTermPrice, prices.CurrentMidTermInvestmentPrice);

        // Short term
        var expectedShortTermPrice = 100m + (100m * (growthRate * InvestmentConfig.ShortTermGrowthRateModifier));
        Assert.Equal(expectedShortTermPrice, prices.CurrentShortTermInvestmentPrice);
    }

    [Theory]
    // note that any time you add new values to the historicalgrowth table, you will throw off any of these that "wrap" by the number of months you added
    [InlineData(17, 27, 0.0277)]
    [InlineData(43, 119, -0.0053)]
    [InlineData(143, 199, -0.0152)]
    [InlineData(501, 288, -0.0260)]
    public void CreateHypotheticalPricingForARun_ForVariousScenarios_ReturnsCorrectValues(
        int blockStart, int ordinal, decimal expected)
    {
        // Arrange
        var start = MonteCarloConfig.MonteCarloSimStartDate;
        var lookupDate = start.PlusMonths(ordinal);
        // Act
        var dict = Pricing.CreateHypotheticalPricingForARun(blockStart);
        var actual = dict[lookupDate];
        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(17)]
    [InlineData(37)]
    [InlineData(0)]
    [InlineData(101)]
    
    // this is a flawed test and will often fail if run at the same time as other tests that might populate the cache
    public void CreateHypotheticalPricingForARun_WhenRunASecondTimeWithSameBlockStart_UsesCache(int blockStart)
    {
        // Arrange
        var lookupDate = new LocalDateTime(2041, 6, 1, 0, 0);
        
        // Act
        
        // first run should take time
        Stopwatch stopwatch = Stopwatch.StartNew();
        var prices = Pricing.CreateHypotheticalPricingForARun(blockStart);
        stopwatch.Stop();
        var firstRunMs = stopwatch.ElapsedMilliseconds;
        var firstRunValue = prices[lookupDate];
        
        // second run should take less time
        stopwatch = Stopwatch.StartNew();
        var newPrices = Pricing.CreateHypotheticalPricingForARun(blockStart);
        stopwatch.Stop();
        var secondRunMs = stopwatch.ElapsedMilliseconds;
        var secondRunValue = newPrices[lookupDate];
        
        // Assert
        Assert.True(secondRunMs < firstRunMs);
        Assert.Equal(secondRunValue, firstRunValue);

    }
}