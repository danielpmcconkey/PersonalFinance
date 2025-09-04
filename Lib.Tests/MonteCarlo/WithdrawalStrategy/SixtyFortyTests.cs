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
}