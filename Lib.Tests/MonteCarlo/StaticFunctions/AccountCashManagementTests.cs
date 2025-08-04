using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using Lib.Tests;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class AccountCashManagementTests
{
    private readonly LocalDateTime _testDate = new LocalDateTime(2025, 1, 1, 12, 0);
    
    private BookOfAccounts CreateTestAccounts(decimal initialCash = 0)
    {
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        if (initialCash > 0)
        {
            accounts = AccountCashManagement.DepositCash(accounts, initialCash, _testDate).accounts;
        }
        return accounts;
    }

    [Fact]
    public void DepositCash_WithPositiveAmount_UpdatesBalance()
    {
        // Arrange
        var accounts = CreateTestAccounts();
        var depositAmount = 1000m;

        // Act
        var result = AccountCashManagement.DepositCash(accounts, depositAmount, _testDate);

        // Assert
        var newBalance = AccountCalculation.CalculateCashBalance(result.accounts);
        Assert.Equal(depositAmount, newBalance);
    }

    [Fact]
    public void DepositCash_WithZeroAmount_ReturnsUnchangedAccounts()
    {
        // Arrange
        var accounts = CreateTestAccounts(1000m);

        // Act
        var result = AccountCashManagement.DepositCash(accounts, 0m, _testDate);

        // Assert
        Assert.Equal(
            AccountCalculation.CalculateCashBalance(accounts),
            AccountCalculation.CalculateCashBalance(result.accounts));
    }

    [Fact]
    public void DepositCash_WithNegativeAmount_ThrowsArgumentException()
    {
        // Arrange
        var accounts = CreateTestAccounts();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            AccountCashManagement.DepositCash(accounts, -100m, _testDate));
    }

    [Fact]
    public void UpdateCashAccountBalance_SetsExactBalance()
    {
        // Arrange
        var accounts = CreateTestAccounts();
        var newBalance = 500m;

        // Act
        var result = AccountCashManagement.UpdateCashAccountBalance(accounts, newBalance, _testDate);

        // Assert
        Assert.Equal(newBalance, AccountCalculation.CalculateCashBalance(result));
        Assert.Single(result.Cash.Positions);
        Assert.Equal(newBalance, result.Cash.Positions[0].Quantity);
    }

    [Fact]
    public void TryWithdrawCash_WithSufficientFunds_Succeeds()
    {
        // Arrange
        var initialBalance = 1000m;
        var withdrawAmount = 500m;
        var accounts = CreateTestAccounts(initialBalance);

        // Act
        var (isSuccessful, newAccounts, messages) = AccountCashManagement.TryWithdrawCash(
            accounts, withdrawAmount, _testDate);

        // Assert
        Assert.True(isSuccessful);
        Assert.Equal(initialBalance - withdrawAmount, 
            AccountCalculation.CalculateCashBalance(newAccounts));
    }

    [Fact]
    public void TryWithdrawCash_WithInsufficientFunds_ReturnsFalse()
    {
        // Arrange
        var accounts = CreateTestAccounts(100m);
        var withdrawAmount = 500m;

        // Act
        var (isSuccessful, newAccounts, messages) = AccountCashManagement.TryWithdrawCash(
            accounts, withdrawAmount, _testDate);

        // Assert
        Assert.False(isSuccessful);
        Assert.Equal(AccountCalculation.CalculateCashBalance(accounts), 
            AccountCalculation.CalculateCashBalance(newAccounts));
    }

    [Fact]
    public void WithdrawCash_WithSufficientCash_SucceedsAndUpdatesBalance()
    {
        // Arrange
        var initialBalance = 1000m;
        var withdrawAmount = 500m;
        var accounts = CreateTestAccounts(initialBalance);
        var taxLedger = new TaxLedger();

        // Act
        var (isSuccessful, newAccounts, newLedger, messages) = 
            AccountCashManagement.WithdrawCash(accounts, withdrawAmount, _testDate, taxLedger);

        // Assert
        Assert.True(isSuccessful);
        Assert.Equal(initialBalance - withdrawAmount, 
            AccountCalculation.CalculateCashBalance(newAccounts));
    }
    [Fact]
    public void WithdrawCash_WithInSufficientCash_PullsFromInvestmentAccounts()
    {
        // Arrange
        var initialBalance = 1000m;
        var withdrawAmount = 5000m;
        var investmentsNeeded = 4000m;
        var accounts = CreateTestAccounts(initialBalance);
        // it should sell mid first, so put only enough to close it out, then put too much in long
        accounts.Brokerage.Positions = [
            new McInvestmentPosition() {
                Id = Guid.NewGuid(), Name = "test position", Entry = _testDate.PlusYears(-2),
                IsOpen = true, InitialCost = 1000m, Price = 1000m, Quantity = 1m, 
                InvestmentPositionType = McInvestmentPositionType.MID_TERM
            },
            new McInvestmentPosition() {
                Id = Guid.NewGuid(), Name = "test position", Entry = _testDate.PlusYears(-2),
                IsOpen = true, InitialCost = 1000m, Price = 2000m, Quantity = 1m, 
                InvestmentPositionType = McInvestmentPositionType.LONG_TERM
            },
            new McInvestmentPosition() {
                Id = Guid.NewGuid(), Name = "test position", Entry = _testDate.PlusYears(-2),
                IsOpen = true, InitialCost = 1000m, Price = 2000m, Quantity = 1m, 
                InvestmentPositionType = McInvestmentPositionType.LONG_TERM
            },
            new McInvestmentPosition() {
                Id = Guid.NewGuid(), Name = "test position", Entry = _testDate.PlusYears(-2),
                IsOpen = true, InitialCost = 1000m, Price = 2000m, Quantity = 1m, 
                InvestmentPositionType = McInvestmentPositionType.LONG_TERM
            },
        ];
        var expectedMidTermBalance = 0m;
        var expectedLongTermBalance = 2000m;
        var expectedCashBalance =
            initialBalance // what was in to begin with
            + 1000m // all the mid
            + 4000m // 2 of the long
            - withdrawAmount; // the final withdrawal
            ;
        var taxLedger = new TaxLedger();
        var expectedCapitalGains = 2000m;

        // Act
        var (isSuccessful, newAccounts, newLedger, messages) = 
            AccountCashManagement.WithdrawCash(accounts, withdrawAmount, _testDate, taxLedger);
        var newCashBalance = AccountCalculation.CalculateCashBalance(newAccounts);
        var newMidBalance = AccountCalculation.CalculateTotalBalanceByBucketType(
            newAccounts, McInvestmentPositionType.MID_TERM);
        var newLongBalance = AccountCalculation.CalculateTotalBalanceByBucketType(
            newAccounts, McInvestmentPositionType.LONG_TERM);
        var capitalGainsRecorded = newLedger.LongTermCapitalGains.Sum(x => x.amount);
        

        // Assert
        Assert.True(isSuccessful);
        Assert.Equal(expectedCashBalance, newCashBalance);
        Assert.Equal(expectedMidTermBalance, newMidBalance);
        Assert.Equal(expectedLongTermBalance, newLongBalance);
        Assert.Equal(expectedCapitalGains, capitalGainsRecorded);
    }
}