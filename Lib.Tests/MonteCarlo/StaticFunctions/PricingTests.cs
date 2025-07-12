using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class PricingTests
{
    [Fact]
    public void CreateHypotheticalPricingForRuns_GeneratesCorrectNumberOfRuns()
    {
        // Arrange

        // Act
        var result = TestDataManager.CreateOrFetchHypotheticalPricingForRuns();
        

        // Assert
        Assert.Equal(MonteCarloConfig.MaxLivesPerBatch, result.Length);
    }

    [Fact]
    public void CreateHypotheticalPricingForRuns_GeneratesCorrectDateRange()
    {
        // Arrange
        var expectedFirstDate = new LocalDateTime(2025, 2, 1, 0, 0);
        var expectedLastDate = new LocalDateTime(2125, 2, 1, 0, 0);

        // Act
        var result = TestDataManager.CreateOrFetchHypotheticalPricingForRuns();

        // Assert
        Assert.NotNull(result[0]); // Check first run
        Assert.Contains(expectedFirstDate, result[0].Keys);
        Assert.Contains(expectedLastDate, result[0].Keys);
    }

    [Fact]
    public void CreateHypotheticalPricingForRuns_GeneratesMonthlyData()
    {
        // Arrange
        var firstDate = new LocalDateTime(2025, 2, 1, 0, 0);
        var secondDate = new LocalDateTime(2025, 3, 1, 0, 0);

        // Act
        var result = TestDataManager.CreateOrFetchHypotheticalPricingForRuns();


        // Assert
        Assert.NotNull(result[0]);
        Assert.Contains(firstDate, result[0].Keys);
        Assert.Contains(secondDate, result[0].Keys);
    }
    
    [Theory]
    [InlineData(1, 2031, 6, -0.0291)]
    [InlineData(996, 2045, 11, -0.0886)]
    [InlineData(409, 2029, 1, 0.0070)]
    public void CreateHypotheticalPricingForRuns_GeneratesTheSameEveryTime(int runNumber, int year, int month, decimal expectedPrice)
    {
        // Arrange
        LocalDateTime date = new LocalDateTime(year, month, 1, 0, 0);

        // Act
        var result = TestDataManager.CreateOrFetchHypotheticalPricingForRuns();


        // Assert
        var actualPrice  = result[runNumber][date];
        Assert.Equal(expectedPrice, actualPrice);
    }

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
}