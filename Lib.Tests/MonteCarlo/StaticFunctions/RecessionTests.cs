using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Model = Lib.DataTypes.MonteCarlo.Model;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class RecessionTests
{
    private LocalDateTime _testDate = new LocalDateTime(2025, 1, 1, 0, 0);
    private BookOfAccounts _bookOfAccounts = new BookOfAccounts()
    {
        InvestmentAccounts = new List<McInvestmentAccount>() , DebtAccounts = new List<McDebtAccount>()
    };
    private Model CreateTestModel()
    {
        return new Model
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
            Percent401KTraditional = 0,
            SimEndDate = new LocalDateTime(2066, 3, 1, 0, 0),
            SimStartDate = new LocalDateTime(2025, 1, 1, 0, 0),
            SocialSecurityStart = new LocalDateTime(2025, 1, 1, 0, 0),
            LivinLargeRatio = 1.0m,
            LivinLargeNetWorthTrigger = 4000000m,
            Generation = -1,
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

    private BookOfAccounts CreateTestBookOfAccounts(decimal totalNetWorth)
    {
        var position = new McInvestmentPosition()
        {
            Id = Guid.NewGuid(), 
            Name = "test position", 
            InvestmentPositionType = McInvestmentPositionType.LONG_TERM, 
            Entry = _testDate.PlusYears(-2), 
            IsOpen = true, 
            InitialCost = totalNetWorth / 2m,
            Price = 1m, Quantity = totalNetWorth
        };
        var account = new McInvestmentAccount()
        {
            Id = Guid.NewGuid(), 
            AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
            Name = "Test account",
            Positions = [position]
        };
        var investmentAccounts = new List<McInvestmentAccount>() { account };
        var debtAccounts = new List<McDebtAccount>();
        var bookOfAccounts = Account.CreateBookOfAccounts(investmentAccounts, debtAccounts);
        return bookOfAccounts;
    }
    #region CalculateExtremeAusterityMeasures Tests

    [Fact]
    public void CalculateExtremeAusterityMeasures_BelowTrigger_EntersAusterity()
    {
        // Arrange
        var model = CreateTestModel();
        model.ExtremeAusterityNetWorthTrigger = 100000m;
        model.RecessionRecoveryPointModifier = 1.1m;
        model.RecessionCheckLookBackMonths = 13;
        
        var bookOfAccounts = CreateTestBookOfAccounts(50000m); // below trigger

        
        var recessionStats = new RecessionStats();
        var currentDate = new LocalDateTime(2024, 1, 1, 0, 0);

        // Act
        var result = 
            Recession.CalculateExtremeAusterityMeasures(model, bookOfAccounts, recessionStats, currentDate);

        // Assert
        Assert.True(result.areWeInExtremeAusterityMeasures);
        Assert.Equal(currentDate, result.lastExtremeAusterityMeasureEnd);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    [InlineData(7, true)]
    [InlineData(8, true)]
    [InlineData(9, true)]
    [InlineData(10, true)]
    [InlineData(11, true)]
    [InlineData(12, true)]
    [InlineData(13, false)]
    [InlineData(14, false)]
    public void CalculateExtremeAusterityMeasures_AboveTrigger_ExitsAusterityAtRightTime(
        int monthsLater, bool expectation)
    {
        // Arrange
        var model = CreateTestModel();
        model.ExtremeAusterityNetWorthTrigger = 100000m;
        model.RecessionRecoveryPointModifier = 1.1m;
        model.RecessionCheckLookBackMonths = 13;
        var bookOfAccounts = CreateTestBookOfAccounts(150000m); // Above trigger

        var lastEndDate = new LocalDateTime(2024, 1, 1, 0, 0);
        var currentDate = lastEndDate.PlusMonths(monthsLater);
        var recessionStats = new RecessionStats
        {
            AreWeInExtremeAusterityMeasures = true,
            LastExtremeAusterityMeasureEnd = lastEndDate
        };

        // Act
        var result =
            Recession.CalculateExtremeAusterityMeasures(model, bookOfAccounts, recessionStats, currentDate);

        // Assert
        Assert.Equal(expectation, result.areWeInExtremeAusterityMeasures);
    }


    #endregion

    #region CalculateAreWeInARecession Tests

    [Fact]
    public void CalculateAreWeInARecession_NotEnoughHistory_ReturnsCurrentState()
    {
        // Arrange
        var currentStats = new RecessionStats();
        var currentPrices = new CurrentPrices
        {
            LongRangeInvestmentCostHistory = new List<decimal> { 100m, 95m } // Less than required history
        };
        var model = CreateTestModel();
        model.ExtremeAusterityNetWorthTrigger = 100000m;
        model.RecessionRecoveryPointModifier = 1.1m;
        model.RecessionCheckLookBackMonths = 13;

        // Act
        var result = Recession.CalculateAreWeInARecession(currentStats, currentPrices, model);

        // Assert
        Assert.False(result.areWeInARecession);
        Assert.Equal(0m, result.recessionDurationCounter);
    }
    
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(5, false)]
    [InlineData(6, false)]
    [InlineData(7, false)]
    [InlineData(8, false)]
    [InlineData(9, false)]
    [InlineData(10, false)]
    [InlineData(11, false)]
    [InlineData(12, false)]
    [InlineData(13, true)]
    [InlineData(14, true)]
    [InlineData(15, true)]
    [InlineData(16, true)]
    [InlineData(17, true)]
    [InlineData(18, true)]
    [InlineData(19, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    [InlineData(22, false)]
    [InlineData(23, false)]
    [InlineData(24, false)]
    [InlineData(25, false)]

    public void CalculateAreWeInARecession_WithMultipleMonthsHistory_UsesCorrectPricePointInTheArray(int position, bool expected)
    {
        // Arrange
        var currentStats = new RecessionStats();
        var fullHistory = new decimal[]{
                93.0m,
                94.0m,
                95.0m,
                96.0m,
                97.0m,
                98.0m,
                99.0m,
                100.0m,
                99.0m,
                98.0m,
                97.0m,
                96.0m,
                95.0m,
                94.0m,
                93.0m,
                92.0m,
                91.0m,
                90.0m,
                89.0m,
                88.0m,
                87.0m,
                96.0m,
                95.0m,
                94.0m,
                93.0m,
                92.0m
            };
        var currentPrices = new CurrentPrices
        {
            LongRangeInvestmentCostHistory = fullHistory[0..(position + 1)].ToList(),
            CurrentLongTermInvestmentPrice = fullHistory[position ]
            
        };
        var model = CreateTestModel();
        model.RecessionRecoveryPointModifier = 1.0m;
        model.RecessionCheckLookBackMonths = 10;

        // Act
        var result = Recession.CalculateAreWeInARecession(currentStats, currentPrices, model);

        // Assert
        Assert.Equal(expected, result.areWeInARecession);
    }

    [Fact]
    public void CalculateAreWeInARecession_PriceDropYearOverYear_EntersRecession()
        {
            // Arrange
            var currentStats = new RecessionStats();
            var currentPrices = new CurrentPrices
            {
                CurrentLongTermInvestmentPrice = 90m,
                LongRangeInvestmentCostHistory = new List<decimal>()
            };
            // Add 13 months of history
            for (int i = 0; i < 14; i++)
            {
                currentPrices.LongRangeInvestmentCostHistory.Add(100m);
            }
            var model = CreateTestModel();
            model.ExtremeAusterityNetWorthTrigger = 100000m;
            model.RecessionRecoveryPointModifier = 1.1m;
            model.RecessionCheckLookBackMonths = 13;

            // Act
            var result = Recession.CalculateAreWeInARecession(currentStats, currentPrices, model);

            // Assert
            Assert.True(result.areWeInARecession);
            Assert.Equal(100m, result.recessionRecoveryPoint);
        }

    [Fact]
    public void CalculateAreWeInARecession_InRecession_RecoveryReached_ExitsRecession()
        {
            // Arrange
            var currentStats = new RecessionStats
            {
                AreWeInARecession = true,
                RecessionRecoveryPoint = 100m
            };
            var currentPrices = new CurrentPrices
            {
                CurrentLongTermInvestmentPrice = 120m // Above recovery point * modifier
            };
            // Add 13 months of history
            for (int i = 0; i < 13; i++)
            {
                currentPrices.LongRangeInvestmentCostHistory.Add(90m);
            }
            var model = CreateTestModel();
            model.ExtremeAusterityNetWorthTrigger = 100000m;
            model.RecessionRecoveryPointModifier = 1.1m;
            model.RecessionCheckLookBackMonths = 13;

            // Act
            var result = Recession.CalculateAreWeInARecession(currentStats, currentPrices, model);

            // Assert
            Assert.False(result.areWeInARecession);
            Assert.Equal(0m, result.recessionDurationCounter);
            Assert.Equal(120m, result.recessionRecoveryPoint);
        }

    [Fact]
    public void CalculateAreWeInARecession_InRecession_BelowRecovery_IncrementsDuration()
        {
            // Arrange
            var currentStats = new RecessionStats
            {
                AreWeInARecession = true,
                RecessionRecoveryPoint = 100m,
                RecessionDurationCounter = 1.0m
            };
            var currentPrices = new CurrentPrices
            {
                CurrentLongTermInvestmentPrice = 90m // Below recovery point
            };
            var model = CreateTestModel();
            model.ExtremeAusterityNetWorthTrigger = 100000m;
            model.RecessionRecoveryPointModifier = 1.1m;
            model.RecessionCheckLookBackMonths = 13;

            // Act
            var result = Recession.CalculateAreWeInARecession(currentStats, currentPrices, model);

            // Assert
            Assert.True(result.areWeInARecession);
            Assert.Equal(1.0833m, result.recessionDurationCounter, 4); // 1 + 1/12
        }

    [Fact]
    public void CalculateAreWeInARecession_NotInRecession_NewHighWaterMark_UpdatesRecoveryPoint()
    {
        // Arrange
        var currentStats = new RecessionStats
        {
            RecessionRecoveryPoint = 100m
        };
        var currentPrices = new CurrentPrices
        {
            CurrentLongTermInvestmentPrice = 110m,
            LongRangeInvestmentCostHistory = new List<decimal>()
        };
        // Add 13 months of history with increasing prices
        for (int i = 0; i < 14; i++)
        {
            currentPrices.LongRangeInvestmentCostHistory.Add(90m + i);
        }
        var model = CreateTestModel();
        model.ExtremeAusterityNetWorthTrigger = 100000m;
        model.RecessionRecoveryPointModifier = 1.1m;
        model.RecessionCheckLookBackMonths = 13;

        // Act
        var result = Recession.CalculateAreWeInARecession(currentStats, currentPrices, model);

        // Assert
        Assert.False(result.areWeInARecession);
        Assert.Equal(110m, result.recessionRecoveryPoint);
    }

    #endregion


    [Fact]
    public void CopyRecessionStats_CreatesCopyWithSameValues()
    {
        // Arrange
        var original = new RecessionStats
        {
            AreWeInARecession = true,
            RecessionDurationCounter = 1.5m,
            AreWeInExtremeAusterityMeasures = false,
            LastExtremeAusterityMeasureEnd = new LocalDateTime(2025, 1, 1, 0, 0),
            RecessionRecoveryPoint = 100m
        };

        // Act
        var copy = Recession.CopyRecessionStats(original);

        // Assert
        Assert.Equal(original.AreWeInARecession, copy.AreWeInARecession);
        Assert.Equal(original.RecessionDurationCounter, copy.RecessionDurationCounter);
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
        var model = CreateTestModel();
        
        

        // Act
        var result = Recession.CalculateRecessionStats(
            currentStats, currentPrices, model, _bookOfAccounts, _testDate);

        // Assert
        Assert.Equal(currentStats.RecessionRecoveryPoint, result.RecessionRecoveryPoint);
        Assert.Equal(currentStats.AreWeInARecession, result.AreWeInARecession);
    }

    [Fact]
    public void CalculateRecessionStats_PreviousRecessionNowOver_ReturnsNoLongerInRecession()
    {
        // arrange
        const decimal oldHighWaterMark = 100m;
        const decimal newHighWaterMark = 106m;
        var model = CreateTestModel();
        model.RecessionCheckLookBackMonths = 12;
        model.RecessionRecoveryPointModifier = 1.05m;
        
        var currentStats = new RecessionStats
        {
            AreWeInARecession = true,
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
        var result = Recession.CalculateRecessionStats(
            currentStats, currentPrices, model,_bookOfAccounts, _testDate);
        
        // assert
        Assert.False(result.AreWeInARecession);
        Assert.Equal(newHighWaterMark, result.RecessionRecoveryPoint);
        
    }

    [Fact]
    public void CalculateRecessionStats_PreviousRecessionNotOver_UpdatesDownYearCounter()
    {
        // arrange
        const decimal oldHighWaterMark = 100m;
        const decimal oldDownYearCounter = 1.5m;
        var expectedDownYearCounter = oldDownYearCounter + (1m / 12m);
        var model = CreateTestModel();
        model.RecessionCheckLookBackMonths = 12;
        model.RecessionRecoveryPointModifier = 1.05m;
        
        var currentStats = new RecessionStats
        {
            AreWeInARecession = true,
            RecessionRecoveryPoint = oldHighWaterMark,
            RecessionDurationCounter = oldDownYearCounter,
        };
        var currentPrices = new CurrentPrices();
        currentPrices.CurrentLongTermGrowthRate = .05m;
        currentPrices.CurrentLongTermInvestmentPrice = oldHighWaterMark; // haven't moved
        
        // act
        var result = Recession.CalculateRecessionStats(
            currentStats, currentPrices, model, _bookOfAccounts, _testDate);
        
        // assert
        Assert.True(result.AreWeInARecession);
        Assert.Equal(expectedDownYearCounter, result.RecessionDurationCounter);

    }
    

    [Fact]
    public void CalculateRecessionStats_EnteringRecession_UpdatesRecessionStatsCorrectly()
    {
        // arrange
        const decimal lastYearsPrice = 100m;
        const decimal currentPrice = 97m;
        var model = CreateTestModel();
        model.RecessionCheckLookBackMonths = 12;
        model.RecessionRecoveryPointModifier = 1.05m;
        
        var currentStats = new RecessionStats
        {
            AreWeInARecession = false,
            RecessionRecoveryPoint = lastYearsPrice, 
        };
        var currentPrices = new CurrentPrices();
        currentPrices.CurrentLongTermGrowthRate = .05m;
        currentPrices.CurrentLongTermInvestmentPrice = currentPrice; 
        currentPrices.LongRangeInvestmentCostHistory.Add(lastYearsPrice);
        for (var i = 0; i < model.RecessionCheckLookBackMonths; i++)
        {
            currentPrices.LongRangeInvestmentCostHistory.Add(lastYearsPrice - i);
        };
        
        // act
        var result = Recession.CalculateRecessionStats(
            currentStats, currentPrices, model, _bookOfAccounts, _testDate);
        
        // assert
        Assert.True(result.AreWeInARecession);
        Assert.Equal(lastYearsPrice, result.RecessionRecoveryPoint);
    }

    [Fact]
    public void CalculateRecessionStats_StillNotInRecession_UpdatesNewHighWaterMark()
    {
        // arrange
        const decimal lastYearsPrice = 95m;
        const decimal currentPrice = 110m;
        var model = CreateTestModel();
        model.RecessionCheckLookBackMonths = 12;
        model.RecessionRecoveryPointModifier = 1.05m;
        
        var currentStats = new RecessionStats
        {
            AreWeInARecession = false,
            RecessionRecoveryPoint = lastYearsPrice, 
        };
        var currentPrices = new CurrentPrices();
        currentPrices.CurrentLongTermGrowthRate = .05m;
        currentPrices.CurrentLongTermInvestmentPrice = currentPrice; 
        currentPrices.LongRangeInvestmentCostHistory.Add(lastYearsPrice);
        for (var i = 0; i < model.RecessionCheckLookBackMonths; i++)
        {
            currentPrices.LongRangeInvestmentCostHistory.Add(lastYearsPrice + i);
        };
        
        // act
        var result = Recession.CalculateRecessionStats(
            currentStats, currentPrices, model, _bookOfAccounts, _testDate);
        
        // assert
        Assert.False(result.AreWeInARecession);
        Assert.Equal(currentPrice, result.RecessionRecoveryPoint);
    }

    

    [Fact]
    public void CalculateRecessionStats_ExitingRecession_UpdatesStats()
    {
        // Arrange
        var currentStats = new RecessionStats 
        { 
            AreWeInARecession = true,
            RecessionRecoveryPoint = 100m,
            RecessionDurationCounter = 0.5m
        };
        var currentPrices = CreateTestPrices(120m);
        var model = CreateTestModel();

        // Act
        var result = Recession.CalculateRecessionStats(
            currentStats, currentPrices, model, _bookOfAccounts, _testDate);

        // Assert
        Assert.False(result.AreWeInARecession);
        Assert.Equal(120m, result.RecessionRecoveryPoint);
        Assert.Equal(0m, result.RecessionDurationCounter);
    }

    

    [Fact]
    public void CalculateRecessionStats_EnteringRecession_SetsRecoveryPoint()
    {
        // Arrange
        var currentStats = new RecessionStats 
        { 
            AreWeInARecession = false,
            RecessionRecoveryPoint = 90m
        };
        var history = new List<decimal>();
        for (int i = 0; i < 12; i++)
        {
            history.Add(100m);
        }
        var currentPrices = CreateTestPrices(80m, history);
        var model = CreateTestModel();
        model.RecessionCheckLookBackMonths = 10;

        // Act
        var result = Recession.CalculateRecessionStats(
            currentStats, currentPrices, model, _bookOfAccounts, _testDate);

        // Assert
        Assert.True(result.AreWeInARecession);
        Assert.Equal(100m, result.RecessionRecoveryPoint);
    }

    [Fact]
    public void CalculateRecessionStats_NoRecession_UpdatesHighWaterMark()
    {
        // Arrange
        var currentStats = new RecessionStats 
        { 
            AreWeInARecession = false,
            RecessionRecoveryPoint = 100m
        };
        var history = new List<decimal>();
        for (int i = 0; i < 12; i++)
        {
            history.Add(90m);
        }
        var currentPrices = CreateTestPrices(110m, history);
        var model = CreateTestModel();
        model.RecessionCheckLookBackMonths = 10;

        // Act
        var result = Recession.CalculateRecessionStats(
            currentStats, currentPrices, model, _bookOfAccounts, _testDate);

        // Assert
        Assert.False(result.AreWeInARecession);
        Assert.Equal(110m, result.RecessionRecoveryPoint);
    }

    [Fact]
    public void CalculateRecessionStats_RecoveryBelowModifier_StaysInRecession()
    {
        // Arrange
        var currentStats = new RecessionStats 
        { 
            AreWeInARecession = true,
            RecessionRecoveryPoint = 100m
        };
        var currentPrices = CreateTestPrices(104m); // Below 105m (100 * 1.05)
        var model = CreateTestModel();

        // Act
        var result = Recession.CalculateRecessionStats(
            currentStats, currentPrices, model, _bookOfAccounts, _testDate);

        // Assert
        Assert.True(result.AreWeInARecession);
        Assert.Equal(100m, result.RecessionRecoveryPoint);
    }

    [Fact]
    public void CalculateRecessionStats_KeepsHigherRecoveryPoint()
    {
        // Arrange
        var currentStats = new RecessionStats 
        { 
            AreWeInARecession = false,
            RecessionRecoveryPoint = 120m
        };
        var history = new List<decimal>();
        for (int i = 0; i < 12; i++)
        {
            history.Add(100m);
        }
        var currentPrices = CreateTestPrices(110m, history);
        var model = CreateTestModel();

        // Act
        var result = Recession.CalculateRecessionStats(
            currentStats, currentPrices, model, _bookOfAccounts, _testDate);

        // Assert
        Assert.Equal(120m, result.RecessionRecoveryPoint); // Keeps higher point
    }

    [Theory]
    [InlineData(2800000, false)]
    [InlineData(2900000, false)]
    [InlineData(3000000, false)]
    [InlineData(3100000, true)]
    [InlineData(3200000, true)]
    [InlineData(3300000, true)]
    internal void WeLivinLarge_ReturnsCorrectValue(decimal netWorth, bool expected)
    {
        // Arrange
        var model = CreateTestModel();
        model.LivinLargeNetWorthTrigger = 3100000m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts = AccountCashManagement.DepositCash(accounts, netWorth, _testDate).accounts;
        
        // Act
        var actual = Recession.WeLivinLarge(model, accounts);
        
        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Theory]
    [InlineData(2800000, false)]
    [InlineData(2900000, false)]
    [InlineData(3000000, false)]
    [InlineData(3100000, true)]
    [InlineData(3200000, true)]
    [InlineData(3300000, true)]
    internal void CalculateRecessionStats_WhenRich_SetsWeLivinLargeToTrue(decimal netWorth, bool expected)
    {
        // Arrange
        var model = CreateTestModel();
        model.LivinLargeNetWorthTrigger = 3100000m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts = AccountCashManagement.DepositCash(accounts, netWorth, _testDate).accounts;
        var prices = TestDataManager.CreateTestCurrentPrices(
            1m, 100m, 50m, 0m);
        var recessionStats = new RecessionStats();
        
        // Act
        var actual = Recession.CalculateRecessionStats(
            recessionStats, prices, model, accounts, _testDate).AreWeInLivinLargeMode;
        
        // Assert
        Assert.Equal(expected, actual);
    }
}