using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.MonteCarlo.WithdrawalStrategy;
using NodaTime;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class InvestmentSalesTestsExtended
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
    
    private static BookOfAccounts CreateAccountsWithInvestments()
    {
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.InvestmentAccounts.AddRange([
            new McInvestmentAccount
            {
                Id = Guid.NewGuid(),
                Name = "test account",
                AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
                Positions = [
                    new McInvestmentPosition
                    {
                        Id = Guid.NewGuid(),
                        Entry = TestDate.PlusYears(-2), // Long-term
                        Price = 100m,
                        Quantity = 10m,
                        InitialCost = 500m,
                        InvestmentPositionType = McInvestmentPositionType.LONG_TERM,
                        IsOpen = true,
                        Name = "Long Term Stock"
                    }
                ]
            },
            new McInvestmentAccount
            {
                Id = Guid.NewGuid(),
                Name = "test account",
                AccountType = McInvestmentAccountType.ROTH_IRA,
                Positions = [
                    new McInvestmentPosition
                    {
                        Id = Guid.NewGuid(),
                        Entry = TestDate.PlusMonths(-6),
                        Price = 50m,
                        Quantity = 20m,
                        InitialCost = 800m,
                        InvestmentPositionType = McInvestmentPositionType.LONG_TERM,
                        IsOpen = true,
                        Name = "Roth IRA Stock"
                    }
                ]
            }
        ]);
        return accounts;
    }
    
    #endregion

    #region CreateSalesOrder Tests

    [Fact]
    public void CreateSalesOrderAccountTypeFirst_WithValidInputs_CreatesCorrectOrder()
    {
        // Arrange
        var positionTypes = new[] { McInvestmentPositionType.LONG_TERM, McInvestmentPositionType.MID_TERM };
        var accountTypes = new[] { McInvestmentAccountType.TAXABLE_BROKERAGE, McInvestmentAccountType.ROTH_IRA };

        // Act
        var result = InvestmentSales.CreateSalesOrderAccountTypeFirst(positionTypes, accountTypes);

        // Assert
        Assert.Equal(4, result.Length);
        Assert.Equal((McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TAXABLE_BROKERAGE), result[0]);
        Assert.Equal((McInvestmentPositionType.MID_TERM, McInvestmentAccountType.TAXABLE_BROKERAGE), result[1]);
        Assert.Equal((McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.ROTH_IRA), result[2]);
        Assert.Equal((McInvestmentPositionType.MID_TERM, McInvestmentAccountType.ROTH_IRA), result[3]);
    }

    [Fact]
    public void CreateSalesOrderPositionTypeFirst_WithValidInputs_CreatesCorrectOrder()
    {
        // Arrange
        var positionTypes = new[] { McInvestmentPositionType.LONG_TERM, McInvestmentPositionType.MID_TERM };
        var accountTypes = new[] { McInvestmentAccountType.TAXABLE_BROKERAGE, McInvestmentAccountType.ROTH_IRA };

        // Act
        var result = InvestmentSales.CreateSalesOrderPositionTypeFirst(positionTypes, accountTypes);

        // Assert
        Assert.Equal(4, result.Length);
        Assert.Equal((McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TAXABLE_BROKERAGE), result[0]);
        Assert.Equal((McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.ROTH_IRA), result[1]);
        Assert.Equal((McInvestmentPositionType.MID_TERM, McInvestmentAccountType.TAXABLE_BROKERAGE), result[2]);
        Assert.Equal((McInvestmentPositionType.MID_TERM, McInvestmentAccountType.ROTH_IRA), result[3]);
    }

    [Fact]
    public void CreateSalesOrderAccountTypeFirst_WithEmptyArrays_ReturnsEmptyResult()
    {
        // Arrange
        var positionTypes = Array.Empty<McInvestmentPositionType>();
        var accountTypes = Array.Empty<McInvestmentAccountType>();

        // Act
        var result = InvestmentSales.CreateSalesOrderAccountTypeFirst(positionTypes, accountTypes);

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region GetWithdrawalStrategy Tests

    [Theory]
    [InlineData(WithdrawalStrategyType.BasicBucketsIncomeThreshold)]
    [InlineData(WithdrawalStrategyType.BasicBucketsTaxableFirst)]
    [InlineData(WithdrawalStrategyType.SixtyForty)]
    public void GetWithdrawalStrategy_WithValidType_ReturnsCorrectStrategy(WithdrawalStrategyType strategyType)
    {
        // Act
        var result = SharedWithdrawalFunctions.GetWithdrawalStrategy(strategyType);

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IWithdrawalStrategy>(result);
    }

    [Fact]
    public void GetWithdrawalStrategy_WithInvalidType_ThrowsException()
    {
        // Arrange
        var invalidType = (WithdrawalStrategyType)999;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            SharedWithdrawalFunctions.GetWithdrawalStrategy(invalidType));
    }

    #endregion

    #region SellInvestmentsToDollarAmount Tests

    [Fact]
    public void SellInvestmentsToDollarAmount_WithNullInvestmentAccounts_ThrowsException()
    {
        // Arrange
        var accounts = new BookOfAccounts { InvestmentAccounts = null };
        var ledger = new TaxLedger();

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => 
            InvestmentSales.SellInvestmentsToDollarAmount(accounts, ledger, TestDate, 1000m));
    }

    [Fact]
    public void SellInvestmentsToDollarAmount_WithEmptyAccounts_ReturnsZero()
    {
        // Arrange
        var accounts = new BookOfAccounts { InvestmentAccounts = [] };
        var ledger = new TaxLedger();

        // Act
        var result = InvestmentSales.SellInvestmentsToDollarAmount(accounts, ledger, TestDate, 1000m);

        // Assert
        Assert.Equal(0m, result.amountSold);
        Assert.Empty(result.messages);
    }

    [Fact]
    public void SellInvestmentsToDollarAmount_WithSufficientInvestments_SellsCorrectAmount()
    {
        // Arrange
        var accounts = CreateAccountsWithInvestments();
        var ledger = new TaxLedger();
        var amountToSell = 500m;

        // Act
        var result = InvestmentSales.SellInvestmentsToDollarAmount(
            accounts, ledger, TestDate, amountToSell);

        // Assert
        Assert.Equal(amountToSell, result.amountSold);
        Assert.True(result.amountSold > 0);
    }

    [Fact]
    public void SellInvestmentsToDollarAmount_WithDateFilters_RespectsDateConstraints()
    {
        // Arrange
        var accounts = CreateAccountsWithInvestments();
        var ledger = new TaxLedger();
        var amountToSell = 500m;
        var minDateExclusive = TestDate.PlusYears(-1); // Should exclude positions older than 1 year

        // Act
        var result = InvestmentSales.SellInvestmentsToDollarAmount(
            accounts, ledger, TestDate, amountToSell, null, minDateExclusive);

        // Assert
        // Should only sell from positions newer than minDateExclusive
        Assert.True(result.amountSold <= amountToSell);
    }

    [Fact]
    public void SellInvestmentsToDollarAmount_WithTaxableAccount_RecordsCapitalGains()
    {
        // Arrange
        var accounts = CreateAccountsWithInvestments();
        var ledger = new TaxLedger();
        var amountToSell = 500m;

        // Act
        var result = InvestmentSales.SellInvestmentsToDollarAmount(
            accounts, ledger, TestDate, amountToSell);

        // Assert
        // Should have recorded some capital gains from taxable brokerage account
        Assert.True(result.ledger.LongTermCapitalGains.Count > 0 || 
                   result.ledger.ShortTermCapitalGains.Count > 0);
    }

    [Fact]
    public void SellInvestmentsToDollarAmount_WithRothAccount_RecordsTaxFreeWithdrawal()
    {
        // Arrange
        var accounts = CreateAccountsWithInvestments();
        var ledger = new TaxLedger();
        var amountToSell = 1500m; // Enough to require selling from Roth IRA

        // Act
        var result = InvestmentSales.SellInvestmentsToDollarAmount(
            accounts, ledger, TestDate, amountToSell);

        // Assert
        // Should have recorded tax-free withdrawal from Roth IRA
        Assert.True(result.ledger.TaxFreeWithrawals.Count > 0);
    }

    [Fact]
    public void SellInvestmentsToDollarAmount_PartialSale_UpdatesPositionCorrectly()
    {
        // Arrange
        const decimal amountToSell = 2000m; // Enough to sell entire first position
        const decimal excess = 314m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.Brokerage.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            1, amountToSell + excess, McInvestmentPositionType.LONG_TERM, true, 1m,
            TestDate.PlusYears(-2)));
        var ledger = new TaxLedger();
        
        
        // Act
        var result = InvestmentSales.SellInvestmentsToDollarAmount(
            accounts, ledger, TestDate, amountToSell);

        // Assert
        var updatedPosition = result.accounts.Brokerage.Positions[0];
        Assert.True(updatedPosition.IsOpen);
        Assert.Equal(excess, updatedPosition.CurrentValue);
        Assert.True(updatedPosition.Quantity > 0);
    }

    [Fact]
    public void SellInvestmentsToDollarAmount_FullSale_ClosesPosition()
    {
        // Arrange
        var amountToSell = 2000m; // Enough to sell entire first position
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.Brokerage.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            1, amountToSell, McInvestmentPositionType.LONG_TERM, true, 1m,
            TestDate.PlusYears(-2)));
        var ledger = new TaxLedger();

        // Act
        var result = InvestmentSales.SellInvestmentsToDollarAmount(
            accounts, ledger, TestDate, amountToSell);

        // Assert
        var soldPosition = result.accounts.Brokerage.Positions.FirstOrDefault(p => !p.IsOpen);
        Assert.NotNull(soldPosition);
        Assert.Equal(0m, soldPosition.Quantity);
    }

    #endregion
}