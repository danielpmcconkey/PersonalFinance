using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using NodaTime;
using Xunit;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class TaxTestsExtended
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
    
    private static TaxLedger CreateEmptyTaxLedger()
    {
        return new TaxLedger
        {
            SocialSecurityIncome = [],
            W2Income = [],
            TaxableIraDistribution = [],
            TaxableInterestReceived = [],
            TaxFreeInterestPaid = [],
            FederalWithholdings = [],
            StateWithholdings = [],
            LongTermCapitalGains = [],
            ShortTermCapitalGains = [],
            TotalTaxPaidLifetime = 0m,
            TaxFreeWithrawals = []
        };
    }

    private static McInvestmentPosition CreateTestPosition(decimal currentValue, decimal initialCost)
    {
        return new McInvestmentPosition
        {
            Id = Guid.NewGuid(),
            Entry = TestDate.PlusMonths(-6),
            Price = 100m,
            Quantity = currentValue / 100m,
            InitialCost = initialCost,
            InvestmentPositionType = McInvestmentPositionType.LONG_TERM,
            IsOpen = true,
            Name = "Test Position"
        };
    }
    
    #endregion

    #region RecordTaxFreeWithdrawal Tests

    [Fact]
    public void RecordTaxFreeWithdrawal_WithZeroAmount_ReturnsOriginalLedger()
    {
        // Arrange
        var ledger = CreateEmptyTaxLedger();
        var originalCount = ledger.TaxFreeWithrawals.Count;

        // Act
        var result = Tax.RecordTaxFreeWithdrawal(ledger, TestDate, 0m);

        // Assert
        Assert.Equal(originalCount, result.ledger.TaxFreeWithrawals.Count);
        Assert.Empty(result.messages);
        Assert.Equal(ledger, result.ledger); // Should return original, not a copy
    }

    [Fact]
    public void RecordTaxFreeWithdrawal_WithNegativeAmount_ReturnsOriginalLedger()
    {
        // Arrange
        var ledger = CreateEmptyTaxLedger();
        var originalCount = ledger.TaxFreeWithrawals.Count;

        // Act
        var result = Tax.RecordTaxFreeWithdrawal(ledger, TestDate, -500m);

        // Assert
        Assert.Equal(originalCount, result.ledger.TaxFreeWithrawals.Count);
        Assert.Empty(result.messages);
        Assert.Equal(ledger, result.ledger);
    }

    [Fact]
    public void RecordTaxFreeWithdrawal_WithPositiveAmount_RecordsWithdrawal()
    {
        // Arrange
        var ledger = CreateEmptyTaxLedger();
        var amount = 1000m;

        // Act
        var result = Tax.RecordTaxFreeWithdrawal(ledger, TestDate, amount);

        // Assert
        Assert.Single(result.ledger.TaxFreeWithrawals);
        Assert.Equal((TestDate, amount), result.ledger.TaxFreeWithrawals[0]);
    }

    [Fact]
    public void RecordTaxFreeWithdrawal_InDebugMode_IncludesMessage()
    {
        // Arrange
        var originalDebugMode = MonteCarloConfig.DebugMode;
        MonteCarloConfig.DebugMode = true;
        var ledger = CreateEmptyTaxLedger();
        var amount = 1000m;

        try
        {
            // Act
            var result = Tax.RecordTaxFreeWithdrawal(ledger, TestDate, amount);

            // Assert
            Assert.Single(result.messages);
            Assert.Contains("Tax free withdrawal logged", result.messages[0].Description);
            Assert.Equal(amount, result.messages[0].Amount);
        }
        finally
        {
            MonteCarloConfig.DebugMode = originalDebugMode;
        }
    }

    [Fact]
    public void RecordTaxFreeWithdrawal_NotInDebugMode_NoMessages()
    {
        // Arrange
        var originalDebugMode = MonteCarloConfig.DebugMode;
        MonteCarloConfig.DebugMode = false;
        var ledger = CreateEmptyTaxLedger();
        var amount = 1000m;

        try
        {
            // Act
            var result = Tax.RecordTaxFreeWithdrawal(ledger, TestDate, amount);

            // Assert
            Assert.Empty(result.messages);
        }
        finally
        {
            MonteCarloConfig.DebugMode = originalDebugMode;
        }
    }

    #endregion

    #region RecordInvestmentSale Tests

    [Theory]
    [InlineData(McInvestmentAccountType.ROTH_401_K)]
    [InlineData(McInvestmentAccountType.ROTH_IRA)]
    [InlineData(McInvestmentAccountType.HSA)]
    public void RecordInvestmentSale_TaxFreeAccounts_RecordsTaxFreeWithdrawal(McInvestmentAccountType accountType)
    {
        // Arrange
        var ledger = CreateEmptyTaxLedger();
        var position = CreateTestPosition(5000m, 3000m);

        // Act
        var result = Tax.RecordInvestmentSale(ledger, TestDate, position, accountType);

        // Assert
        Assert.Single(result.ledger.TaxFreeWithrawals);
        Assert.Equal((TestDate, 5000m), result.ledger.TaxFreeWithrawals[0]);
        Assert.Empty(result.ledger.LongTermCapitalGains);
        Assert.Empty(result.ledger.TaxableIraDistribution);
    }

    [Fact]
    public void RecordInvestmentSale_TaxableBrokerageAccount_RecordsCapitalGain()
    {
        // Arrange
        var ledger = CreateEmptyTaxLedger();
        var position = CreateTestPosition(5000m, 3000m);

        // Act
        var result = Tax.RecordInvestmentSale(ledger, TestDate, position, 
            McInvestmentAccountType.TAXABLE_BROKERAGE);

        // Assert
        Assert.Single(result.ledger.LongTermCapitalGains);
        Assert.Equal((TestDate, 2000m), result.ledger.LongTermCapitalGains[0]); // 5000 - 3000
        Assert.Empty(result.ledger.TaxFreeWithrawals);
        Assert.Empty(result.ledger.TaxableIraDistribution);
    }

    [Theory]
    [InlineData(McInvestmentAccountType.TRADITIONAL_401_K)]
    [InlineData(McInvestmentAccountType.TRADITIONAL_IRA)]
    public void RecordInvestmentSale_TaxDeferredAccounts_RecordsIraDistribution(McInvestmentAccountType accountType)
    {
        // Arrange
        var ledger = CreateEmptyTaxLedger();
        var position = CreateTestPosition(5000m, 3000m);

        // Act
        var result = Tax.RecordInvestmentSale(ledger, TestDate, position, accountType);

        // Assert
        Assert.Single(result.ledger.TaxableIraDistribution);
        Assert.Equal((TestDate, 5000m), result.ledger.TaxableIraDistribution[0]);
        Assert.Empty(result.ledger.LongTermCapitalGains);
        Assert.Empty(result.ledger.TaxFreeWithrawals);
    }

    [Theory]
    [InlineData(McInvestmentAccountType.PRIMARY_RESIDENCE)]
    [InlineData(McInvestmentAccountType.CASH)]
    public void RecordInvestmentSale_InvalidAccounts_ThrowsException(McInvestmentAccountType accountType)
    {
        // Arrange
        var ledger = CreateEmptyTaxLedger();
        var position = CreateTestPosition(5000m, 3000m);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => 
            Tax.RecordInvestmentSale(ledger, TestDate, position, accountType));
    }

    #endregion

    #region MeetRmdRequirements Tests

    [Fact]
    public void MeetRmdRequirements_WithNullInvestmentAccounts_ThrowsException()
    {
        // Arrange
        var ledger = CreateEmptyTaxLedger();
        var accounts = new BookOfAccounts { InvestmentAccounts = null };
        var model = TestDataManager.CreateTestModel();

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => 
            Tax.MeetRmdRequirements(ledger, TestDate, accounts, 72, model));
    }

    [Fact]
    public void MeetRmdRequirements_WithNoRmdRequired_ReturnsOriginalData()
    {
        // Arrange
        var ledger = CreateEmptyTaxLedger();
        var accounts = new BookOfAccounts { InvestmentAccounts = [] };
        var model = TestDataManager.CreateTestModel();
        var age = 65; // Below RMD age

        // Act
        var result = Tax.MeetRmdRequirements(ledger, TestDate, accounts, age, model);

        // Assert
        Assert.Equal(accounts, result.newBookOfAccounts);
        Assert.Equal(ledger, result.newLedger);
        Assert.Empty(result.messages);
    }

    #endregion
}