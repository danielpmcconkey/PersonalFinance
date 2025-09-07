using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.MonteCarlo.WithdrawalStrategy;
using NodaTime;

namespace Lib.Tests.MonteCarlo.WithdrawalStrategy;

public class SixtyFortyTests
{
    [Theory]
    // these values are in the testing spreadsheet in the 60-40 invest excess cash tab
    [InlineData(1, 2039, 100000, 525000, 180800, 525000)]
    [InlineData(2, 2039, 96000, 273230.77, 175100, 273230.77)]
    [InlineData(3, 2039, 92000, 163555.56, 169400, 163555.56)]
    [InlineData(4, 2039, 88000, 103304.35, 163700, 103304.35)]
    [InlineData(5, 2039, 84000, 66000, 158000, 66000)]
    [InlineData(6, 2039, 80000, 41212.12, 152300, 41212.12)]
    [InlineData(7, 2039, 76000, 24000, 136480, 34120)]
    [InlineData(8, 2039, 72000, 11720.93, 117009.38, 35611.55)]
    [InlineData(9, 2039, 68000, 2833.33, 101224.44, 36808.89)]
    [InlineData(10, 2039, 64000, 336000, 129500, 336000)]
    [InlineData(11, 2039, 60000, 170769.23, 123800, 170769.23)]
    [InlineData(12, 2039, 56000, 99555.56, 118100, 99555.56)]
    [InlineData(1, 2040, 52000, 61043.48, 104066.09, 69377.39)]
    [InlineData(1, 2041, 48000, 37714.29, 92238.17, 61492.12)]
    [InlineData(1, 2042, 44000, 22666.67, 80959.48, 53972.98)]
    [InlineData(1, 2043, 40000, 12631.58, 72688.3, 48458.86)]
    [InlineData(1, 2044, 36000, 5860.47, 66375.5, 44250.34)]
    [InlineData(1, 2043, 32000, 1333.33, 61109.35, 40739.56)]
    [InlineData(1, 2042, 28000, 147000, 96265.79, 147000)]
    [InlineData(1, 2041, 24000, 68307.69, 92016, 68307.69)]
    [InlineData(1, 2040, 20000, 35555.56, 69573.34, 46382.22)]
    [InlineData(1, 2039, 16000, 18782.61, 96800, 18782.61)]
    [InlineData(1, 2038, 12000, 9428.57, 109500, 9428.57)]
    [InlineData(1, 2037, 8000, 4121.21, 105500, 4121.21)]
    [InlineData(1, 2036, 4000, 1263.16, 101500, 1263.16)]

    public void InvestExcessCash_UnderDifferentScenarios_CalculatesCorrectly(
        int currentMonth, int currentYear, decimal currentLong, decimal currentMid, decimal expectedLong,
        decimal expectedMid)
    {
        // Arrange
        var currentDate = new LocalDateTime(currentYear, currentMonth, 1, 0, 0);
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts = AccountCashManagement.DepositCash(accounts, 100000m, currentDate).accounts;
        accounts.Brokerage.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            currentLong, 1, McInvestmentPositionType.LONG_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        accounts.Brokerage.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            currentMid, 1, McInvestmentPositionType.MID_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.SixtyForty);
        model.NumMonthsCashOnHand = 12;
        model.NumMonthsMidBucketOnHand = 6;
        model.DesiredMonthlySpendPreRetirement = 600m;
        model.DesiredMonthlySpendPostRetirement = 800m;
        model.RetirementDate = new LocalDateTime(2040, 1, 1, 0, 0);
        model.NumMonthsPriorToRetirementToBeginRebalance = 12;
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = new LocalDateTime(1976, 3, 7, 0, 0);
        person.RequiredMonthlySpend = 1000m;
        person.RequiredMonthlySpendHealthCare = 1500m;
        var prices = TestDataManager.CreateTestCurrentPrices(
            1.0m, 100m, 50m, 0m);
        
        // Act
        var results = model.WithdrawalStrategy.InvestExcessCash(
            currentDate, accounts, prices, model, person).accounts;
        var actualLong = AccountCalculation.CalculateLongBucketTotalBalance(results);
        var actualMid = AccountCalculation.CalculateMidBucketTotalBalance(results);
        // Assert
        Assert.Equal(Math.Round(expectedLong,1, MidpointRounding.AwayFromZero), 
            Math.Round(actualLong,1, MidpointRounding.AwayFromZero));
        Assert.Equal(Math.Round(expectedMid,1, MidpointRounding.AwayFromZero),
            Math.Round(actualMid,1, MidpointRounding.AwayFromZero));
        
    }
    
    [Theory]
    // these values are in the testing spreadsheet in the 60-40 invest excess cash tab
    // testing common scenarios at 60% target ratio
    [InlineData(1, 2040, 5000, 5000, -2500, 4500, 3000)]  // straightforward sale
    [InlineData(1, 2040, 5000, 15000, -2500, 5000, 12500)]  // sale where you already have a bad imbalance favoring mid and can afford both
    [InlineData(1, 2040, 15000, 5000, -2500, 12500, 5000)]  // sale where you already have a bad imbalance favoring long and can afford both
    [InlineData(1, 2040, 5000, 5000, -12000, 0, 0)]  // sale where you can’t afford it altogether
    [InlineData(1, 2040, 2000, 2000, -4000, 0, 0)]  // sale where the movement amount is the same as total balance
    // testing various balance and target scenarios
    [InlineData(1, 2041, 2000, 2000, -2000, 1200, 800)]  // low balances, low sales amount, after retirement
    [InlineData(1, 2038, 2000, 2000, -2000, 2000, 0)]  // low balances, low sales amount, before rebalance time
    [InlineData(6, 2039, 2000, 2000, -2000, 1666.66666666667, 333.333333333333)]  // low balances, low sales amount, between rebalance start and retirement
    [InlineData(1, 2041, 2000, 2000, -3500, 300, 200)]  // low balances, mid sales amount, after retirement
    [InlineData(1, 2038, 2000, 2000, -3500, 500, 0)]  // low balances, mid sales amount, before rebalance time
    [InlineData(6, 2039, 2000, 2000, -3500, 416.666666666667, 83.3333333333333)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(1, 2041, 2000, 2000, -10000, 0, 0)]  // low balances, high sales amount, after retirement
    [InlineData(1, 2038, 2000, 2000, -10000, 0, 0)]  // low balances, high sales amount, before rebalance time
    [InlineData(6, 2039, 2000, 2000, -10000, 0, 0)]  // low balances, high sales amount, between rebalance start and retirement
    [InlineData(1, 2039, 2000, 2000, -3500, 500, 0)]  // low balances, mid sales amount, at rebalance start
    [InlineData(2, 2039, 2000, 2000, -3500, 483.333333333333, 16.6666666666667)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(3, 2039, 2000, 2000, -3500, 466.666666666667, 33.3333333333333)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(4, 2039, 2000, 2000, -3500, 450, 50)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(5, 2039, 2000, 2000, -3500, 433.333333333334, 66.6666666666667)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(7, 2039, 2000, 2000, -3500, 400, 100)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(8, 2039, 2000, 2000, -3500, 383.333333333333, 116.666666666667)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(9, 2039, 2000, 2000, -3500, 366.666666666667, 133.333333333333)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(10, 2039, 2000, 2000, -3500, 350, 150)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(11, 2039, 2000, 2000, -3500, 333.333333333334, 166.666666666667)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(12, 2039, 2000, 2000, -3500, 316.666666666667, 183.333333333333)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(1, 2040, 2000, 2000, -3500, 300, 200)]  // low balances, mid sales amount, at retirement start
    public void SellInvestmentsToDollarAmount_UnderDifferentScenarios_SellsCorrectly(
        int currentMonth, int currentYear, decimal currentLong, decimal currentMid, decimal movementAmount,
        decimal expectedLong, decimal expectedMid)
    {
        // Arrange
        var currentDate = new LocalDateTime(currentYear, currentMonth, 1, 0, 0);
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts = AccountCashManagement.DepositCash(accounts, 100000m, currentDate).accounts;
        accounts.Brokerage.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            currentLong, 1, McInvestmentPositionType.LONG_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        accounts.Brokerage.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            currentMid, 1, McInvestmentPositionType.MID_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.SixtyForty);
        model.NumMonthsCashOnHand = 12;
        model.NumMonthsMidBucketOnHand = 6;
        model.DesiredMonthlySpendPreRetirement = 600m;
        model.DesiredMonthlySpendPostRetirement = 800m;
        model.RetirementDate = new LocalDateTime(2040, 1, 1, 0, 0);
        model.NumMonthsPriorToRetirementToBeginRebalance = 12;
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = new LocalDateTime(1976, 3, 7, 0, 0);
        person.RequiredMonthlySpend = 1000m;
        person.RequiredMonthlySpendHealthCare = 1500m;
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        
        // Act
        var results = model.WithdrawalStrategy.SellInvestmentsToDollarAmount(
            accounts, ledger, currentDate, -movementAmount, model).accounts;
        var actualLong = AccountCalculation.CalculateLongBucketTotalBalance(results);
        var actualMid = AccountCalculation.CalculateMidBucketTotalBalance(results);
        // Assert
        Assert.Equal(Math.Round(expectedLong,1, MidpointRounding.AwayFromZero), 
            Math.Round(actualLong,1, MidpointRounding.AwayFromZero));
        Assert.Equal(Math.Round(expectedMid,1, MidpointRounding.AwayFromZero),
            Math.Round(actualMid,1, MidpointRounding.AwayFromZero));
        
    }

    [Theory]
    // these values are in the testing spreadsheet in the 60-40 movement calc tab
    
    // testing common scenarios at 60% target ratio
    [InlineData(1, 2040, 5000, 5000, -2500, -500, -2000)]  // straightforward sale
    [InlineData(1, 2040, 5000, 15000, -2500, 0, -2500)]  // sale where you already have a bad imbalance favoring mid and can afford both
    [InlineData(1, 2040, 15000, 5000, -2500, -2500, 0)]  // sale where you already have a bad imbalance favoring long and can afford both
    [InlineData(1, 2040, 5000, 5000, 2500, 2500, 0)]  // buy where you wouln’t need to sell anything to reach ideal
    [InlineData(1, 2040, 5000, 50000, 2500, 2500, 0)]  // buy where you obviously have too much mid
    [InlineData(1, 2040, 50000, 5000, 2500, 0, 2500)]  // buy where you obviously have too much long
    [InlineData(1, 2040, 5000, 7000, 2500, 2500, 0)]  // buy where you have a little too much mid
    [InlineData(1, 2040, 12000, 5000, 2500, 0, 2500)]  // buy where you have a little too much long
    [InlineData(1, 2040, 5000, 5000, -12000, -5000, -5000)]  // sale where you can’t afford it altogether
    [InlineData(1, 2040, 2000, 2000, -4000, -2000, -2000)]  // sale where the movement amount is the same as total balance
    // testing common scenarios at 100% target ratio
    [InlineData(1, 2039, 5000, 5000, -2500, 0, -2500)]  // straightforward sale
    [InlineData(1, 2039, 5000, 15000, -2500, 0, -2500)]  // sale where you already have a bad imbalance favoring mid and can afford both
    [InlineData(1, 2039, 15000, 5000, -2500, 0, -2500)]  // sale where you already have a bad imbalance favoring long and can afford both
    [InlineData(1, 2039, 5000, 5000, 2500, 2500, 0)]  // buy where you wouln’t need to sell anything to reach ideal
    [InlineData(1, 2039, 5000, 50000, 2500, 2500, 0)]  // buy where you obviously have too much mid
    [InlineData(1, 2039, 50000, 5000, 2500, 2500, 0)]  // buy where you obviously have too much long
    [InlineData(1, 2039, 5000, 7000, 2500, 2500, 0)]  // buy where you have a little too much mid
    [InlineData(1, 2039, 12000, 5000, 2500, 2500, 0)]  // buy where you have a little too much long
    [InlineData(1, 2039, 5000, 5000, -12000, -5000, -5000)]  // sale where you can’t afford it altogether
    [InlineData(1, 2039, 2000, 2000, -4000, -2000, -2000)]  // sale where the movement amount is the same as total balance
    // testing all scenarios at 60% target ratio
    [InlineData(1, 2040, 2000, 2000, -2000, -800, -1200)] 
    [InlineData(1, 2040, 4000, 2000, -2000, -1600, -400)] 
    [InlineData(1, 2040, 6000, 2000, -2000, -2000, 0)] 
    [InlineData(1, 2040, 8000, 2000, -2000, -2000, 0)] 
    [InlineData(1, 2040, 10000, 2000, -2000, -2000, 0)] 
    [InlineData(1, 2040, 2000, 4000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 4000, 4000, -2000, -400, -1600)] 
    [InlineData(1, 2040, 6000, 4000, -2000, -1200, -800)] 
    [InlineData(1, 2040, 8000, 4000, -2000, -2000, 0)] 
    [InlineData(1, 2040, 10000, 4000, -2000, -2000, 0)] 
    [InlineData(1, 2040, 2000, 6000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 4000, 6000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 6000, 6000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 8000, 6000, -2000, -800, -1200)] 
    [InlineData(1, 2040, 10000, 6000, -2000, -1600, -400)] 
    [InlineData(1, 2040, 2000, 8000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 4000, 8000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 6000, 8000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 8000, 8000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 10000, 8000, -2000, -400, -1600)] 
    [InlineData(1, 2040, 2000, 10000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 4000, 10000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 6000, 10000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 8000, 10000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 10000, 10000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 4000, 2000, -4000, -2800, -1200)] 
    [InlineData(1, 2040, 6000, 2000, -4000, -3600, -400)] 
    [InlineData(1, 2040, 8000, 2000, -4000, -4000, 0)] 
    [InlineData(1, 2040, 10000, 2000, -4000, -4000, 0)] 
    [InlineData(1, 2040, 2000, 4000, -4000, -800, -3200)] 
    [InlineData(1, 2040, 4000, 4000, -4000, -1600, -2400)] 
    [InlineData(1, 2040, 6000, 4000, -4000, -2400, -1600)] 
    [InlineData(1, 2040, 8000, 4000, -4000, -3200, -800)] 
    [InlineData(1, 2040, 10000, 4000, -4000, -4000, 0)] 
    [InlineData(1, 2040, 2000, 6000, -4000, 0, -4000)] 
    [InlineData(1, 2040, 4000, 6000, -4000, -400, -3600)] 
    [InlineData(1, 2040, 6000, 6000, -4000, -1200, -2800)] 
    [InlineData(1, 2040, 8000, 6000, -4000, -2000, -2000)] 
    [InlineData(1, 2040, 10000, 6000, -4000, -2800, -1200)] 
    [InlineData(1, 2040, 2000, 8000, -4000, 0, -4000)] 
    [InlineData(1, 2040, 4000, 8000, -4000, 0, -4000)] 
    [InlineData(1, 2040, 6000, 8000, -4000, 0, -4000)] 
    [InlineData(1, 2040, 8000, 8000, -4000, -800, -3200)] 
    [InlineData(1, 2040, 10000, 8000, -4000, -1600, -2400)] 
    [InlineData(1, 2040, 2000, 10000, -4000, 0, -4000)] 
    [InlineData(1, 2040, 4000, 10000, -4000, 0, -4000)] 
    [InlineData(1, 2040, 6000, 10000, -4000, 0, -4000)] 
    [InlineData(1, 2040, 8000, 10000, -4000, 0, -4000)] 
    [InlineData(1, 2040, 10000, 10000, -4000, -400, -3600)] 
    [InlineData(1, 2040, 2000, 2000, -6000, -2000, -2000)] 
    [InlineData(1, 2040, 4000, 2000, -6000, -4000, -2000)] 
    [InlineData(1, 2040, 6000, 2000, -6000, -4800, -1200)] 
    [InlineData(1, 2040, 8000, 2000, -6000, -5600, -400)] 
    [InlineData(1, 2040, 10000, 2000, -6000, -6000, 0)] 
    [InlineData(1, 2040, 2000, 4000, -6000, -2000, -4000)] 
    [InlineData(1, 2040, 4000, 4000, -6000, -2800, -3200)] 
    [InlineData(1, 2040, 6000, 4000, -6000, -3600, -2400)] 
    [InlineData(1, 2040, 8000, 4000, -6000, -4400, -1600)] 
    [InlineData(1, 2040, 10000, 4000, -6000, -5200, -800)] 
    [InlineData(1, 2040, 2000, 6000, -6000, -800, -5200)] 
    [InlineData(1, 2040, 4000, 6000, -6000, -1600, -4400)] 
    [InlineData(1, 2040, 6000, 6000, -6000, -2400, -3600)] 
    [InlineData(1, 2040, 8000, 6000, -6000, -3200, -2800)] 
    [InlineData(1, 2040, 10000, 6000, -6000, -4000, -2000)] 
    [InlineData(1, 2040, 2000, 8000, -6000, 0, -6000)] 
    [InlineData(1, 2040, 4000, 8000, -6000, -400, -5600)] 
    [InlineData(1, 2040, 6000, 8000, -6000, -1200, -4800)] 
    [InlineData(1, 2040, 8000, 8000, -6000, -2000, -4000)] 
    [InlineData(1, 2040, 10000, 8000, -6000, -2800, -3200)] 
    [InlineData(1, 2040, 2000, 10000, -6000, 0, -6000)] 
    [InlineData(1, 2040, 4000, 10000, -6000, 0, -6000)] 
    [InlineData(1, 2040, 6000, 10000, -6000, 0, -6000)] 
    [InlineData(1, 2040, 8000, 10000, -6000, -800, -5200)] 
    [InlineData(1, 2040, 10000, 10000, -6000, -1600, -4400)] 
    [InlineData(1, 2040, 2000, 2000, -8000, -2000, -2000)] 
    [InlineData(1, 2040, 4000, 2000, -8000, -4000, -2000)] 
    [InlineData(1, 2040, 6000, 2000, -8000, -6000, -2000)] 
    [InlineData(1, 2040, 8000, 2000, -8000, -6800, -1200)] 
    [InlineData(1, 2040, 10000, 2000, -8000, -7600, -400)] 
    [InlineData(1, 2040, 2000, 4000, -8000, -2000, -4000)] 
    [InlineData(1, 2040, 4000, 4000, -8000, -4000, -4000)] 
    [InlineData(1, 2040, 6000, 4000, -8000, -4800, -3200)] 
    [InlineData(1, 2040, 8000, 4000, -8000, -5600, -2400)] 
    [InlineData(1, 2040, 10000, 4000, -8000, -6400, -1600)] 
    [InlineData(1, 2040, 2000, 6000, -8000, -2000, -6000)] 
    [InlineData(1, 2040, 4000, 6000, -8000, -2800, -5200)] 
    [InlineData(1, 2040, 6000, 6000, -8000, -3600, -4400)] 
    [InlineData(1, 2040, 8000, 6000, -8000, -4400, -3600)] 
    [InlineData(1, 2040, 10000, 6000, -8000, -5200, -2800)] 
    [InlineData(1, 2040, 2000, 8000, -8000, -800, -7200)] 
    [InlineData(1, 2040, 4000, 8000, -8000, -1600, -6400)] 
    [InlineData(1, 2040, 6000, 8000, -8000, -2400, -5600)] 
    [InlineData(1, 2040, 8000, 8000, -8000, -3200, -4800)] 
    [InlineData(1, 2040, 10000, 8000, -8000, -4000, -4000)] 
    [InlineData(1, 2040, 2000, 10000, -8000, 0, -8000)] 
    [InlineData(1, 2040, 4000, 10000, -8000, -400, -7600)] 
    [InlineData(1, 2040, 6000, 10000, -8000, -1200, -6800)] 
    [InlineData(1, 2040, 8000, 10000, -8000, -2000, -6000)] 
    [InlineData(1, 2040, 10000, 10000, -8000, -2800, -5200)] 
    [InlineData(1, 2040, 2000, 2000, -10000, -2000, -2000)] 
    [InlineData(1, 2040, 4000, 2000, -10000, -4000, -2000)] 
    [InlineData(1, 2040, 6000, 2000, -10000, -6000, -2000)] 
    [InlineData(1, 2040, 8000, 2000, -10000, -8000, -2000)] 
    [InlineData(1, 2040, 10000, 2000, -10000, -8800, -1200)] 
    [InlineData(1, 2040, 2000, 4000, -10000, -2000, -4000)] 
    [InlineData(1, 2040, 4000, 4000, -10000, -4000, -4000)] 
    [InlineData(1, 2040, 6000, 4000, -10000, -6000, -4000)] 
    [InlineData(1, 2040, 8000, 4000, -10000, -6800, -3200)] 
    [InlineData(1, 2040, 10000, 4000, -10000, -7600, -2400)] 
    [InlineData(1, 2040, 2000, 6000, -10000, -2000, -6000)] 
    [InlineData(1, 2040, 4000, 6000, -10000, -4000, -6000)] 
    [InlineData(1, 2040, 6000, 6000, -10000, -4800, -5200)] 
    [InlineData(1, 2040, 8000, 6000, -10000, -5600, -4400)] 
    [InlineData(1, 2040, 10000, 6000, -10000, -6400, -3600)] 
    [InlineData(1, 2040, 2000, 8000, -10000, -2000, -8000)] 
    [InlineData(1, 2040, 4000, 8000, -10000, -2800, -7200)] 
    [InlineData(1, 2040, 6000, 8000, -10000, -3600, -6400)] 
    [InlineData(1, 2040, 8000, 8000, -10000, -4400, -5600)] 
    [InlineData(1, 2040, 10000, 8000, -10000, -5200, -4800)] 
    [InlineData(1, 2040, 2000, 10000, -10000, -800, -9200)] 
    [InlineData(1, 2040, 4000, 10000, -10000, -1600, -8400)] 
    [InlineData(1, 2040, 6000, 10000, -10000, -2400, -7600)] 
    [InlineData(1, 2040, 8000, 10000, -10000, -3200, -6800)] 
    [InlineData(1, 2040, 10000, 10000, -10000, -4000, -6000)] 
    // testing all scenarios at 100% target ratio
    [InlineData(1, 2039, 2000, 2000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 4000, 2000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 6000, 2000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 8000, 2000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 10000, 2000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 2000, 4000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 4000, 4000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 6000, 4000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 8000, 4000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 10000, 4000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 2000, 6000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 4000, 6000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 6000, 6000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 8000, 6000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 10000, 6000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 2000, 8000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 4000, 8000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 6000, 8000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 8000, 8000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 10000, 8000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 2000, 10000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 4000, 10000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 6000, 10000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 8000, 10000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 10000, 10000, -2000, 0, -2000)]
    [InlineData(1, 2039, 4000, 2000, -4000, -2000, -2000)] 
    [InlineData(1, 2039, 6000, 2000, -4000, -2000, -2000)] 
    [InlineData(1, 2039, 8000, 2000, -4000, -2000, -2000)] 
    [InlineData(1, 2039, 10000, 2000, -4000, -2000, -2000)] 
    [InlineData(1, 2039, 2000, 4000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 4000, 4000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 6000, 4000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 8000, 4000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 10000, 4000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 2000, 6000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 4000, 6000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 6000, 6000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 8000, 6000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 10000, 6000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 2000, 8000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 4000, 8000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 6000, 8000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 8000, 8000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 10000, 8000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 2000, 10000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 4000, 10000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 6000, 10000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 8000, 10000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 10000, 10000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 2000, 2000, -6000, -2000, -2000)] 
    [InlineData(1, 2039, 4000, 2000, -6000, -4000, -2000)] 
    [InlineData(1, 2039, 6000, 2000, -6000, -4000, -2000)] 
    [InlineData(1, 2039, 8000, 2000, -6000, -4000, -2000)] 
    [InlineData(1, 2039, 10000, 2000, -6000, -4000, -2000)] 
    [InlineData(1, 2039, 2000, 4000, -6000, -2000, -4000)] 
    [InlineData(1, 2039, 4000, 4000, -6000, -2000, -4000)] 
    [InlineData(1, 2039, 6000, 4000, -6000, -2000, -4000)] 
    [InlineData(1, 2039, 8000, 4000, -6000, -2000, -4000)] 
    [InlineData(1, 2039, 10000, 4000, -6000, -2000, -4000)] 
    [InlineData(1, 2039, 2000, 6000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 4000, 6000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 6000, 6000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 8000, 6000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 10000, 6000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 2000, 8000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 4000, 8000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 6000, 8000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 8000, 8000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 10000, 8000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 2000, 10000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 4000, 10000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 6000, 10000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 8000, 10000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 10000, 10000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 2000, 2000, -8000, -2000, -2000)] 
    [InlineData(1, 2039, 4000, 2000, -8000, -4000, -2000)] 
    [InlineData(1, 2039, 6000, 2000, -8000, -6000, -2000)] 
    [InlineData(1, 2039, 8000, 2000, -8000, -6000, -2000)] 
    [InlineData(1, 2039, 10000, 2000, -8000, -6000, -2000)] 
    [InlineData(1, 2039, 2000, 4000, -8000, -2000, -4000)] 
    [InlineData(1, 2039, 4000, 4000, -8000, -4000, -4000)] 
    [InlineData(1, 2039, 6000, 4000, -8000, -4000, -4000)] 
    [InlineData(1, 2039, 8000, 4000, -8000, -4000, -4000)] 
    [InlineData(1, 2039, 10000, 4000, -8000, -4000, -4000)] 
    [InlineData(1, 2039, 2000, 6000, -8000, -2000, -6000)] 
    [InlineData(1, 2039, 4000, 6000, -8000, -2000, -6000)] 
    [InlineData(1, 2039, 6000, 6000, -8000, -2000, -6000)] 
    [InlineData(1, 2039, 8000, 6000, -8000, -2000, -6000)] 
    [InlineData(1, 2039, 10000, 6000, -8000, -2000, -6000)] 
    [InlineData(1, 2039, 2000, 8000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 4000, 8000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 6000, 8000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 8000, 8000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 10000, 8000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 2000, 10000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 4000, 10000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 6000, 10000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 8000, 10000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 10000, 10000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 2000, 2000, -10000, -2000, -2000)] 
    [InlineData(1, 2039, 4000, 2000, -10000, -4000, -2000)] 
    [InlineData(1, 2039, 6000, 2000, -10000, -6000, -2000)] 
    [InlineData(1, 2039, 8000, 2000, -10000, -8000, -2000)] 
    [InlineData(1, 2039, 10000, 2000, -10000, -8000, -2000)] 
    [InlineData(1, 2039, 2000, 4000, -10000, -2000, -4000)] 
    [InlineData(1, 2039, 4000, 4000, -10000, -4000, -4000)] 
    [InlineData(1, 2039, 6000, 4000, -10000, -6000, -4000)] 
    [InlineData(1, 2039, 8000, 4000, -10000, -6000, -4000)] 
    [InlineData(1, 2039, 10000, 4000, -10000, -6000, -4000)] 
    [InlineData(1, 2039, 2000, 6000, -10000, -2000, -6000)] 
    [InlineData(1, 2039, 4000, 6000, -10000, -4000, -6000)] 
    [InlineData(1, 2039, 6000, 6000, -10000, -4000, -6000)] 
    [InlineData(1, 2039, 8000, 6000, -10000, -4000, -6000)] 
    [InlineData(1, 2039, 10000, 6000, -10000, -4000, -6000)] 
    [InlineData(1, 2039, 2000, 8000, -10000, -2000, -8000)] 
    [InlineData(1, 2039, 4000, 8000, -10000, -2000, -8000)] 
    [InlineData(1, 2039, 6000, 8000, -10000, -2000, -8000)] 
    [InlineData(1, 2039, 8000, 8000, -10000, -2000, -8000)] 
    [InlineData(1, 2039, 10000, 8000, -10000, -2000, -8000)] 
    [InlineData(1, 2039, 2000, 10000, -10000, 0, -10000)] 
    [InlineData(1, 2039, 4000, 10000, -10000, 0, -10000)] 
    [InlineData(1, 2039, 6000, 10000, -10000, 0, -10000)] 
    [InlineData(1, 2039, 8000, 10000, -10000, 0, -10000)] 
    [InlineData(1, 2039, 10000, 10000, -10000, 0, -10000)] 

    public void CalculateMovementAmountNeededByPositionType_UnderDifferentScenarios_CalculatesCorrectly(
        int currentMonth, int currentYear, decimal currentLong, decimal currentMid, decimal movementAmount,
        decimal expectedLong, decimal expectedMid)
    {
        // Arrange
        var currentDate = new LocalDateTime(currentYear, currentMonth, 1, 0, 0);
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts = AccountCashManagement.DepositCash(accounts, 100000m, currentDate).accounts;
        accounts.Brokerage.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            currentLong, 1, McInvestmentPositionType.LONG_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        accounts.Brokerage.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            currentMid, 1, McInvestmentPositionType.MID_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.SixtyForty);
        model.NumMonthsCashOnHand = 12;
        model.NumMonthsMidBucketOnHand = 6;
        model.DesiredMonthlySpendPreRetirement = 600m;
        model.DesiredMonthlySpendPostRetirement = 800m;
        model.RetirementDate = new LocalDateTime(2040, 1, 1, 0, 0);
        model.NumMonthsPriorToRetirementToBeginRebalance = 12;
        
        var sixtyForty = new SixtyForty();
        
        // Act
        var (actualLong, actualMid) = sixtyForty.CalculateMovementAmountNeededByPositionType(
            accounts, currentDate, movementAmount, model);
        // Assert
        Assert.Equal(Math.Round(expectedLong,1, MidpointRounding.AwayFromZero), 
            Math.Round(actualLong,1, MidpointRounding.AwayFromZero));
        Assert.Equal(Math.Round(expectedMid,1, MidpointRounding.AwayFromZero),
            Math.Round(actualMid,1, MidpointRounding.AwayFromZero));
    }
}