using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.MonteCarlo.WithdrawalStrategy;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class AccountCashManagementTestsExtended
{
    /*
     * this entire class was written by Claude 4 using the following prompt:
     *
     * Please write comprehensive unit tests for the following C# classes in my financial Monte Carlo simulation project. I need xUnit tests that follow the existing patterns in my test suite. Focus on the missing coverage areas I identified:
       **High Priority Tests Needed:**
       1. **Tax.cs module** - Create tests for these specific methods that are marked with TODOs or lacking coverage:
           - `RecordTaxFreeWithdrawal()` - test with zero amounts, positive amounts, debug mode on/off
           - `RecordInvestmentSale()` - test all account types (ROTH_401K, HSA, TAXABLE_BROKERAGE, TRADITIONAL_IRA, etc.) with various position types
           - `MeetRmdRequirements()` - test edge cases like no RMD required, insufficient funds, successful RMD fulfillment
       
       2. - Create tests for complex withdrawal scenarios: **AccountCashManagement.cs**
           - `WithdrawCash()` edge cases when cascading through multiple investment sales fails
           - Boundary testing for insufficient funds scenarios
           - Integration testing of the complete withdrawal waterfall logic
       
       3. - Test the complex investment selling logic: **InvestmentSales.cs**
           - `SellInvestmentsToDollarAmount()` with various type orders and date filters
           - Edge cases with empty accounts, insufficient investments, partial sales
           - Tax implications for different account types and holding periods
       
       4. **TaxCalculation.cs methods** - Test all the calculation methods with boundary values and edge cases
       
       **Testing Patterns to Follow:**
       - Use the same test class structure as my existing `ModelFuncTests.cs`
       - Include comprehensive Arrange/Act/Assert patterns
       - Add parameterized tests with and `[InlineData]` where appropriate `[Theory]`
       - Test both happy path and error conditions
       - Include boundary value testing for all numeric inputs
       - Test null/empty data scenarios
       - Verify return tuples contain expected values and messages
       
       **Code Quality Requirements:**
       - Follow existing naming conventions (e.g., `MethodName_Scenario_ExpectedResult`)
       - Include appropriate statements `using`
       - Use existing test data patterns (like field) `_birthdate`
       - Add helpful comments for complex test scenarios
       - Ensure all assertions are meaningful and specific
       
       Please generate complete test classes with multiple test methods per class, covering normal operations, edge cases, error conditions, and boundary values. Make the tests robust and maintainable."
       
     */
    
    #region utility functions and private variables
    
    private static readonly LocalDateTime TestDate = new(2025, 1, 15, 0, 0);
    
    private static BookOfAccounts CreateEmptyAccounts()
    {
        return TestDataManager.CreateEmptyBookOfAccounts();
    }

    private static BookOfAccounts CreateAccountsWithCash(decimal cashAmount)
    {
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        return AccountCashManagement.DepositCash(
                accounts, cashAmount, new LocalDateTime(2025, 1, 1, 0, 0))
            .accounts;
    }

    private static Model CreateTestModel()
    {
        return TestDataManager.CreateTestModel(WithdrawalStrategyType.BasicBucketsTaxableFirst);
    }
    
    #endregion

    #region WithdrawCash Edge Cases

    [Fact]
    public void WithdrawCash_WithSufficientCash_ReturnsSuccessfully()
    {
        // Arrange
        var accounts = CreateAccountsWithCash(5000m);
        var taxLedger = new TaxLedger();
        var model = CreateTestModel();
        var withdrawAmount = 2000m;

        // Act
        var result = AccountCashManagement.WithdrawCash(
            accounts, withdrawAmount, TestDate, taxLedger, model);

        // Assert
        Assert.True(result.isSuccessful);
        Assert.Equal(3000m, AccountCalculation.CalculateCashBalance(result.accounts));
    }

    [Fact]
    public void WithdrawCash_WithInsufficientCashAndNoInvestments_ReturnsFalse()
    {
        // Arrange
        var accounts = CreateAccountsWithCash(1000m);
        var taxLedger = new TaxLedger();
        var model = CreateTestModel();
        var withdrawAmount = 2000m;

        // Act
        var result = AccountCashManagement.WithdrawCash(
            accounts, withdrawAmount, TestDate, taxLedger, model);

        // Assert
        Assert.False(result.isSuccessful);
    }

    [Fact]
    public void WithdrawCash_WithZeroAmount_ReturnsSuccessImmediately()
    {
        // Arrange
        var accounts = CreateAccountsWithCash(1000m);
        var taxLedger = new TaxLedger();
        var model = CreateTestModel();
        var withdrawAmount = 0m;

        // Act
        var result = AccountCashManagement.WithdrawCash(
            accounts, withdrawAmount, TestDate, taxLedger, model);

        // Assert
        Assert.True(result.isSuccessful);
        Assert.Equal(1000m, AccountCalculation.CalculateCashBalance(result.accounts));
    }

    [Fact]
    public void WithdrawCash_InDebugModeWhenFailing_IncludesFailureMessage()
    {
        // Arrange
        var originalDebugMode = MonteCarloConfig.DebugMode;
        MonteCarloConfig.DebugMode = true;
        var accounts = CreateAccountsWithCash(1000m);
        var taxLedger = new TaxLedger();
        var model = CreateTestModel();
        var withdrawAmount = 5000m;

        try
        {
            // Act
            var result = AccountCashManagement.WithdrawCash(
                accounts, withdrawAmount, TestDate, taxLedger, model);

            // Assert
            Assert.False(result.isSuccessful);
            Assert.NotNull(result.messages.FirstOrDefault(m => m.Description is not null && m.Description.Contains("Cash withdrawal failed")));
        }
        finally
        {
            MonteCarloConfig.DebugMode = originalDebugMode;
        }
    }

    #endregion

    #region TryWithdrawCash Boundary Tests

    [Theory]
    [InlineData(-100)]
    [InlineData(-0.01)]
    public void TryWithdrawCash_WithNegativeAmount_ThrowsException(decimal amount)
    {
        // Arrange
        var accounts = CreateAccountsWithCash(1000m);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            AccountCashManagement.TryWithdrawCash(accounts, amount, TestDate));
    }

    [Fact]
    public void TryWithdrawCash_WithExactCashAmount_ReturnsSuccessWithZeroBalance()
    {
        // Arrange
        var accounts = CreateAccountsWithCash(1000m);

        // Act
        var result = AccountCashManagement.TryWithdrawCash(accounts, 1000m, TestDate);

        // Assert
        Assert.True(result.isSuccessful);
        Assert.Equal(0m, AccountCalculation.CalculateCashBalance(result.newAccounts));
    }

    [Fact]
    public void TryWithdrawCash_WithAmountExceedingCash_ReturnsFalseWithOriginalAccounts()
    {
        // Arrange
        var accounts = CreateAccountsWithCash(1000m);

        // Act
        var result = AccountCashManagement.TryWithdrawCash(accounts, 1500m, TestDate);

        // Assert
        Assert.False(result.isSuccessful);
        Assert.Equal(1000m, AccountCalculation.CalculateCashBalance(result.newAccounts));
    }

    #endregion

    #region DepositCash Tests

    [Theory]
    [InlineData(-100)]
    [InlineData(-0.01)]
    public void DepositCash_WithNegativeAmount_ThrowsException(decimal amount)
    {
        // Arrange
        var accounts = CreateEmptyAccounts();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            AccountCashManagement.DepositCash(accounts, amount, TestDate));
    }

    [Fact]
    public void DepositCash_WithZeroAmount_ReturnsOriginalAccounts()
    {
        // Arrange
        var accounts = CreateAccountsWithCash(1000m);
        var originalBalance = AccountCalculation.CalculateCashBalance(accounts);

        // Act
        var result = AccountCashManagement.DepositCash(accounts, 0m, TestDate);

        // Assert
        Assert.Equal(originalBalance, AccountCalculation.CalculateCashBalance(result.accounts));
        Assert.Empty(result.messages);
    }

    [Fact]
    public void DepositCash_WithPositiveAmount_IncreasesCashBalance()
    {
        // Arrange
        const decimal expectedCash = 1000m;
        
        
        // Arrange
        var accounts = CreateAccountsWithCash(1000m);
        var depositAmount = 500m;

        // Act
        var result = AccountCashManagement.DepositCash(accounts, depositAmount, TestDate);

        // Assert
        Assert.Equal(1500m, AccountCalculation.CalculateCashBalance(result.accounts));
    }

    [Fact]
    public void DepositCash_InDebugMode_IncludesMessage()
    {
        // Arrange
        var originalDebugMode = MonteCarloConfig.DebugMode;
        MonteCarloConfig.DebugMode = true;
        var accounts = CreateAccountsWithCash(1000m);
        var depositAmount = 500m;

        try
        {
            // Act
            var result = AccountCashManagement.DepositCash(accounts, depositAmount, TestDate);

            // Assert
            Assert.Single(result.messages);
            Assert.Contains("Generic cash deposit", result.messages[0].Description);
            Assert.Equal(depositAmount, result.messages[0].Amount);
        }
        finally
        {
            MonteCarloConfig.DebugMode = originalDebugMode;
        }
    }

    #endregion

    #region UpdateCashAccountBalance Tests

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(999999.99)]
    public void UpdateCashAccountBalance_WithValidBalance_UpdatesCorrectly(decimal newBalance)
    {
        // Arrange
        var accounts = CreateAccountsWithCash(500m);

        // Act
        var result = AccountCashManagement.UpdateCashAccountBalance(accounts, newBalance, TestDate);

        // Assert
        Assert.Equal(newBalance, AccountCalculation.CalculateCashBalance(result));
        Assert.Single(result.Cash.Positions);
        Assert.Equal(newBalance, result.Cash.Positions[0].Quantity);
        Assert.Equal(1m, result.Cash.Positions[0].Price);
        Assert.Equal(0m, result.Cash.Positions[0].InitialCost);
    }

    #endregion
}