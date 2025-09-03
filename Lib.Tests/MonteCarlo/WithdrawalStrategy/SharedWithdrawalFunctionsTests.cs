using Lib.DataTypes.MonteCarlo;
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
        var results = SharedWithdrawalFunctions.SellInvestmentsToRmdAmountStandardBucketsStrat(
            amountNeeded, accounts, ledger, currentDate);
        var actualAmountSold = results.amountSold;
        // Assert
        Assert.Equal(expectedAmountSold, actualAmountSold);
    }
}