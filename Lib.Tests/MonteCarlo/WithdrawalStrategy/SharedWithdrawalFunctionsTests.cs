using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.MonteCarlo.WithdrawalStrategy;
using NodaTime;

namespace Lib.Tests.MonteCarlo.WithdrawalStrategy;

public class SharedWithdrawalFunctionsTests
{
    [Fact]
    public void SellInvestmentsToRmdAmountStandardBucketsStrat_WithShortTermPositions_StillWorks()
    {
        // Arrange
        var currentDate = new LocalDateTime(2050, 1, 1, 0, 0);
        var amountNeeded = 100000m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.TraditionalIra.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            1, amountNeeded * 1.5m, McInvestmentPositionType.LONG_TERM, true, 1m,
            currentDate));
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var expectedAmountSold = amountNeeded;
        // Act
        var results = SharedWithdrawalFunctions.BasicBucketsSellInvestmentsToRmdAmount(
            amountNeeded, accounts, ledger, currentDate);
        var actualAmountSold = results.amountSold;
        // Assert
        Assert.Equal(expectedAmountSold, actualAmountSold);
    }

    [Theory]
    // these values are in the testing spreadsheet in the 60-40 invest excess cash tab
    [InlineData(1, 2039, 100000, 525000, 80800)]
    [InlineData(2, 2039, 96000, 273230.769230769, 79100)]
    [InlineData(3, 2039, 92000, 163555.555555556, 77400)]
    [InlineData(4, 2039, 88000, 103304.347826087, 75700)]
    [InlineData(5, 2039, 84000, 66000, 74000)]
    [InlineData(6, 2039, 80000, 41212.1212121212, 72300)]
    [InlineData(7, 2039, 76000, 24000, 70600)]
    [InlineData(8, 2039, 72000, 11720.9302325582, 68900)]
    [InlineData(9, 2039, 68000, 2833.33333333334, 67200)]
    [InlineData(10, 2039, 64000, 336000, 65500)]
    [InlineData(11, 2039, 60000, 170769.230769231, 63800)]
    [InlineData(12, 2039, 56000, 99555.5555555556, 62100)]
    [InlineData(1, 2040, 52000, 61043.4782608696, 60400)]
    [InlineData(1, 2041, 48000, 37714.2857142857, 68016)]
    [InlineData(1, 2042, 44000, 22666.6666666667, 68265.79)]
    [InlineData(1, 2043, 40000, 12631.5789473684, 68515.58)]
    [InlineData(1, 2044, 36000, 5860.46511627908, 68765.37)]
    [InlineData(1, 2043, 32000, 1333.33333333334, 68515.58)]
    [InlineData(1, 2042, 28000, 147000, 68265.79)]
    [InlineData(1, 2041, 24000, 68307.6923076923, 68016)]
    [InlineData(1, 2040, 20000, 35555.5555555556, 60400)]
    [InlineData(1, 2039, 16000, 18782.6086956522, 80800)]
    [InlineData(1, 2038, 12000, 9428.57142857143, 97500)]
    [InlineData(1, 2037, 8000, 4121.21212121212, 97500)]
    [InlineData(1, 2036, 4000, 1263.15789473684, 97500)]

    public void CalculateExcessCash_UnderDifferentScenarios_CalculatesCorrectly(
        int currentMonth, int currentYear, decimal currentLong, decimal currentMid, decimal expectedExcessCash)
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
        var model = TestDataManager.CreateTestModel();
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
        
        
        
        // Act
        var actual = SharedWithdrawalFunctions.CalculateExcessCash(currentDate, accounts, model, person);
        // Assert
        Assert.Equal(expectedExcessCash, Math.Round(actual,2));
        
    }
}