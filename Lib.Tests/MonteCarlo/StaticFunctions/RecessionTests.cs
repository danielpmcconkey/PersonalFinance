using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class RecessionTests
{
    // private McModel CreateTestModel()
    // {
    //     return new McModel
    //     {
    //         RecessionRecoveryPointModifier = 1.05m,
    //         RecessionCheckLookBackMonths = 12
    //     };
    // }
    private McModel CreateTestModel()
    {
        return new McModel
        {
            RecessionRecoveryPointModifier = 1.05m,
            RecessionCheckLookBackMonths = 12,
            RetirementDate = new LocalDateTime(2037, 3, 1, 0, 0),
            NumMonthsCashOnHand = 12,
            NumMonthsMidBucketOnHand = 24,
            NumMonthsPriorToRetirementToBeginRebalance = 60,
            RebalanceFrequency = RebalanceFrequency.QUARTERLY,
            Id = Guid.Empty,
            PersonId = Guid.Empty,
            ParentAId = Guid.Empty,
            ParentBId = Guid.Empty, AusterityRatio = 0m,
            DesiredMonthlySpendPostRetirement = 0,
            DesiredMonthlySpendPreRetirement = 0,
            ExtremeAusterityNetWorthTrigger = 0,
            ExtremeAusterityRatio = 0,
            ModelCreatedDate = new LocalDateTime(2025, 1, 1, 0, 0),
            MonthlyInvest401kRoth = 0,
            MonthlyInvest401kTraditional = 0,
            MonthlyInvestBrokerage = 0,
            MonthlyInvestHSA = 0,
            RequiredMonthlySpend = 0,
            RequiredMonthlySpendHealthCare = 0,
            SimEndDate = new LocalDateTime(2066, 3, 1, 0, 0),
            SimStartDate = new LocalDateTime(2025, 1, 1, 0, 0),
            SocialSecurityStart = new LocalDateTime(2025, 1, 1, 0, 0),
        };
    }

    private CurrentPrices CreateTestPrices(decimal currentPrice, List<decimal>? history = null)
    {
        return new CurrentPrices
        {
            CurrentLongTermInvestmentPrice = currentPrice,
            LongRangeInvestmentCostHistory = history ?? new List<decimal>()
        };
    }

    [Fact]
    public void CopyRecessionStats_CreatesCopyWithSameValues()
    {
        // Arrange
        var original = new RecessionStats
        {
            AreWeInADownYear = true,
            DownYearCounter = 1.5m,
            AreWeInAusterityMeasures = true,
            AreWeInExtremeAusterityMeasures = false,
            LastExtremeAusterityMeasureEnd = new LocalDateTime(2025, 1, 1, 0, 0),
            RecessionRecoveryPoint = 100m
        };

        // Act
        var copy = Recession.CopyRecessionStats(original);

        // Assert
        Assert.Equal(original.AreWeInADownYear, copy.AreWeInADownYear);
        Assert.Equal(original.DownYearCounter, copy.DownYearCounter);
        Assert.Equal(original.AreWeInAusterityMeasures, copy.AreWeInAusterityMeasures);
        Assert.Equal(original.AreWeInExtremeAusterityMeasures, copy.AreWeInExtremeAusterityMeasures);
        Assert.Equal(original.LastExtremeAusterityMeasureEnd, copy.LastExtremeAusterityMeasureEnd);
        Assert.Equal(original.RecessionRecoveryPoint, copy.RecessionRecoveryPoint);
    }

    [Fact]
    public void CalculateRecessionStats_InsufficientHistory_ReturnsUnchangedStats()
    {
        // Arrange
        var currentStats = new RecessionStats { RecessionRecoveryPoint = 100m };
        var currentPrices = CreateTestPrices(110m);
        var simParams = CreateTestModel();

        // Act
        var result = Recession.CalculateRecessionStats(currentStats, currentPrices, simParams);

        // Assert
        Assert.Equal(currentStats.RecessionRecoveryPoint, result.RecessionRecoveryPoint);
        Assert.Equal(currentStats.AreWeInADownYear, result.AreWeInADownYear);
    }

    [Fact]
    public void CalculateRecessionStats_PreviousRecessionNowOver_ReturnsNoLongerInRecession()
    {
        // arrange
        const decimal oldHighWaterMark = 100m;
        const decimal newHighWaterMark = 106m;
        var simParams = CreateTestModel();
        simParams.RecessionCheckLookBackMonths = 12;
        simParams.RecessionRecoveryPointModifier = 1.05m;
        
        var currentStats = new RecessionStats
        {
            AreWeInADownYear = true,
            RecessionRecoveryPoint = oldHighWaterMark, 
        };
        var currentPrices = new CurrentPrices();
        currentPrices.CurrentLongTermGrowthRate = .05m;
        currentPrices.CurrentLongTermInvestmentPrice = newHighWaterMark; 
        // currentPrices.LongRangeInvestmentCostHistory.Add(oldHighWaterMark);
        // for (var i = 88m; i < (newHighWaterMark + 1); i++)
        // {
        //     currentPrices.LongRangeInvestmentCostHistory.Add(i);
        // };
        
        // act
        var result = Recession.CalculateRecessionStats(currentStats, currentPrices, simParams);
        
        // assert
        Assert.False(result.AreWeInADownYear);
        Assert.Equal(newHighWaterMark, result.RecessionRecoveryPoint);
        
    }

    [Fact]
    public void CalculateRecessionStats_PreviousRecessionNotOver_UpdatesDownYearCounter()
    {
        // arrange
        const decimal oldHighWaterMark = 100m;
        const decimal oldDownYearCounter = 1.5m;
        var expectedDownYearCounter = oldDownYearCounter + (1m / 12m);
        var simParams = CreateTestModel();
        simParams.RecessionCheckLookBackMonths = 12;
        simParams.RecessionRecoveryPointModifier = 1.05m;
        
        var currentStats = new RecessionStats
        {
            AreWeInADownYear = true,
            RecessionRecoveryPoint = oldHighWaterMark,
            DownYearCounter = oldDownYearCounter,
        };
        var currentPrices = new CurrentPrices();
        currentPrices.CurrentLongTermGrowthRate = .05m;
        currentPrices.CurrentLongTermInvestmentPrice = oldHighWaterMark; // haven't moved
        
        // act
        var result = Recession.CalculateRecessionStats(currentStats, currentPrices, simParams);
        
        // assert
        Assert.True(result.AreWeInADownYear);
        Assert.Equal(expectedDownYearCounter, result.DownYearCounter);

    }
    

    [Fact]
    public void CalculateRecessionStats_EnteringRecession_UpdatesRecessionStatsCorrectly()
    {
        // arrange
        const decimal lastYearsPrice = 100m;
        const decimal currentPrice = 97m;
        var simParams = CreateTestModel();
        simParams.RecessionCheckLookBackMonths = 12;
        simParams.RecessionRecoveryPointModifier = 1.05m;
        
        var currentStats = new RecessionStats
        {
            AreWeInADownYear = false,
            RecessionRecoveryPoint = lastYearsPrice, 
        };
        var currentPrices = new CurrentPrices();
        currentPrices.CurrentLongTermGrowthRate = .05m;
        currentPrices.CurrentLongTermInvestmentPrice = currentPrice; 
        currentPrices.LongRangeInvestmentCostHistory.Add(lastYearsPrice);
        for (var i = 0; i < simParams.RecessionCheckLookBackMonths; i++)
        {
            currentPrices.LongRangeInvestmentCostHistory.Add(lastYearsPrice - i);
        };
        
        // act
        var result = Recession.CalculateRecessionStats(currentStats, currentPrices, simParams);
        
        // assert
        Assert.True(result.AreWeInADownYear);
        Assert.Equal(lastYearsPrice, result.RecessionRecoveryPoint);
    }

    [Fact]
    public void CalculateRecessionStats_StillNotInRecession_UpdatesNewHighWaterMark()
    {
        // arrange
        const decimal lastYearsPrice = 95m;
        const decimal currentPrice = 110m;
        var simParams = CreateTestModel();
        simParams.RecessionCheckLookBackMonths = 12;
        simParams.RecessionRecoveryPointModifier = 1.05m;
        
        var currentStats = new RecessionStats
        {
            AreWeInADownYear = false,
            RecessionRecoveryPoint = lastYearsPrice, 
        };
        var currentPrices = new CurrentPrices();
        currentPrices.CurrentLongTermGrowthRate = .05m;
        currentPrices.CurrentLongTermInvestmentPrice = currentPrice; 
        currentPrices.LongRangeInvestmentCostHistory.Add(lastYearsPrice);
        for (var i = 0; i < simParams.RecessionCheckLookBackMonths; i++)
        {
            currentPrices.LongRangeInvestmentCostHistory.Add(lastYearsPrice + i);
        };
        
        // act
        var result = Recession.CalculateRecessionStats(currentStats, currentPrices, simParams);
        
        // assert
        Assert.False(result.AreWeInADownYear);
        Assert.Equal(currentPrice, result.RecessionRecoveryPoint);
    }

    

    [Fact]
    public void CalculateRecessionStats_ExitingRecession_UpdatesStats()
    {
        // Arrange
        var currentStats = new RecessionStats 
        { 
            AreWeInADownYear = true,
            RecessionRecoveryPoint = 100m,
            DownYearCounter = 0.5m
        };
        var currentPrices = CreateTestPrices(120m);
        var simParams = CreateTestModel();

        // Act
        var result = Recession.CalculateRecessionStats(currentStats, currentPrices, simParams);

        // Assert
        Assert.False(result.AreWeInADownYear);
        Assert.Equal(120m, result.RecessionRecoveryPoint);
        Assert.Equal(0m, result.DownYearCounter);
    }

    

    [Fact]
    public void CalculateRecessionStats_EnteringRecession_SetsRecoveryPoint()
    {
        // Arrange
        var currentStats = new RecessionStats 
        { 
            AreWeInADownYear = false,
            RecessionRecoveryPoint = 90m
        };
        var history = new List<decimal>();
        for (int i = 0; i < 12; i++)
        {
            history.Add(100m);
        }
        var currentPrices = CreateTestPrices(80m, history);
        var simParams = CreateTestModel();

        // Act
        var result = Recession.CalculateRecessionStats(currentStats, currentPrices, simParams);

        // Assert
        Assert.True(result.AreWeInADownYear);
        Assert.Equal(100m, result.RecessionRecoveryPoint);
    }

    [Fact]
    public void CalculateRecessionStats_NoRecession_UpdatesHighWaterMark()
    {
        // Arrange
        var currentStats = new RecessionStats 
        { 
            AreWeInADownYear = false,
            RecessionRecoveryPoint = 100m
        };
        var history = new List<decimal>();
        for (int i = 0; i < 12; i++)
        {
            history.Add(90m);
        }
        var currentPrices = CreateTestPrices(110m, history);
        var simParams = CreateTestModel();

        // Act
        var result = Recession.CalculateRecessionStats(currentStats, currentPrices, simParams);

        // Assert
        Assert.False(result.AreWeInADownYear);
        Assert.Equal(110m, result.RecessionRecoveryPoint);
    }

    [Fact]
    public void CalculateRecessionStats_RecoveryBelowModifier_StaysInRecession()
    {
        // Arrange
        var currentStats = new RecessionStats 
        { 
            AreWeInADownYear = true,
            RecessionRecoveryPoint = 100m
        };
        var currentPrices = CreateTestPrices(104m); // Below 105m (100 * 1.05)
        var simParams = CreateTestModel();

        // Act
        var result = Recession.CalculateRecessionStats(currentStats, currentPrices, simParams);

        // Assert
        Assert.True(result.AreWeInADownYear);
        Assert.Equal(100m, result.RecessionRecoveryPoint);
    }

    [Fact]
    public void CalculateRecessionStats_KeepsHigherRecoveryPoint()
    {
        // Arrange
        var currentStats = new RecessionStats 
        { 
            AreWeInADownYear = false,
            RecessionRecoveryPoint = 120m
        };
        var history = new List<decimal>();
        for (int i = 0; i < 12; i++)
        {
            history.Add(100m);
        }
        var currentPrices = CreateTestPrices(110m, history);
        var simParams = CreateTestModel();

        // Act
        var result = Recession.CalculateRecessionStats(currentStats, currentPrices, simParams);

        // Assert
        Assert.Equal(120m, result.RecessionRecoveryPoint); // Keeps higher point
    }
}