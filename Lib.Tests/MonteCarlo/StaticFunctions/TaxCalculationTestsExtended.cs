using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using NodaTime;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class TaxCalculationTestsExtended
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
    
    private static TaxLedger CreateTaxLedgerWithData()
    {
        return new TaxLedger
        {
            W2Income = [(TestDate, 50000m), (TestDate.PlusMonths(1), 4000m)],
            SocialSecurityIncome = [(TestDate, 2000m)],
            TaxableIraDistribution = [(TestDate, 5000m)],
            LongTermCapitalGains = [(TestDate, 1000m)],
            ShortTermCapitalGains = [(TestDate, 500m)],
            FederalWithholdings = [(TestDate, 8000m)],
            StateWithholdings = [(TestDate, 2000m)],
            TaxFreeWithrawals = [(TestDate, 3000m)]
        };
    }
    
    #endregion

    #region CalculateW2IncomeForYear Tests

    [Fact]
    public void CalculateW2IncomeForYear_WithMatchingYear_ReturnsCorrectSum()
    {
        // Arrange
        var ledger = CreateTaxLedgerWithData();
        var targetYear = TestDate.Year;

        // Act
        var result = TaxCalculation.CalculateW2IncomeForYear(ledger, targetYear);

        // Assert
        Assert.Equal(54000m, result); // 50000 + 4000
    }

    [Fact]
    public void CalculateW2IncomeForYear_WithNonMatchingYear_ReturnsZero()
    {
        // Arrange
        var ledger = CreateTaxLedgerWithData();
        var targetYear = TestDate.Year - 1;

        // Act
        var result = TaxCalculation.CalculateW2IncomeForYear(ledger, targetYear);

        // Assert
        Assert.Equal(0m, result);
    }

    [Fact]
    public void CalculateW2IncomeForYear_WithEmptyLedger_ReturnsZero()
    {
        // Arrange
        var ledger = new TaxLedger { W2Income = [] };
        var targetYear = TestDate.Year;

        // Act
        var result = TaxCalculation.CalculateW2IncomeForYear(ledger, targetYear);

        // Assert
        Assert.Equal(0m, result);
    }

    #endregion

    #region CalculateRmdRateByAge Tests

    [Theory]
    [InlineData(70, 0.0)] // Below RMD age
    [InlineData(75, 24.6)] // Mid-range
    [InlineData(81, 19.4)] // High age
    [InlineData(110, 12.2)] // Beyond the maximum age, just return the highest value
    public void CalculateRmdRateByAge_WithVariousAges_ReturnsExpectedRates(int age, decimal expectedRate)
    {
        // Act
        var result = TaxCalculation.CalculateRmdRateByAge(age);

        // Assert
        Assert.Equal((decimal)expectedRate, result, 6); // 6 decimal precision
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(25)]
    public void CalculateRmdRateByAge_WithInvalidAge_ReturnsZero(int age)
    {
        // Act
        var result = TaxCalculation.CalculateRmdRateByAge(age);

        // Assert
        Assert.Equal(0m, result);
    }

    #endregion

    #region CalculateSocialSecurityIncomeForYear Tests

    [Fact]
    public void CalculateSocialSecurityIncomeForYear_WithMatchingYear_ReturnsCorrectSum()
    {
        // Arrange
        var ledger = CreateTaxLedgerWithData();
        var targetYear = TestDate.Year;

        // Act
        var result = TaxCalculation.CalculateSocialSecurityIncomeForYear(ledger, targetYear);

        // Assert
        Assert.Equal(2000m, result);
    }

    [Fact]
    public void CalculateSocialSecurityIncomeForYear_WithMultipleEntries_SumsCorrectly()
    {
        // Arrange
        var ledger = new TaxLedger
        {
            SocialSecurityIncome = [
                (TestDate, 1000m),
                (TestDate.PlusMonths(2), 1500m),
                (TestDate.PlusYears(1), 2000m) // Different year
            ]
        };

        // Act
        var result = TaxCalculation.CalculateSocialSecurityIncomeForYear(ledger, TestDate.Year);

        // Assert
        Assert.Equal(2500m, result); // Only current year entries
    }

    #endregion

    #region CalculateTaxableIraDistributionsForYear Tests

    [Fact]
    public void CalculateTaxableIraDistributionsForYear_WithMatchingYear_ReturnsCorrectSum()
    {
        // Arrange
        var ledger = CreateTaxLedgerWithData();
        var targetYear = TestDate.Year;

        // Act
        var result = TaxCalculation.CalculateTaxableIraDistributionsForYear(ledger, targetYear);

        // Assert
        Assert.Equal(5000m, result);
    }

    #endregion

    #region CalculateLongTermCapitalGainsForYear Tests

    [Fact]
    public void CalculateLongTermCapitalGainsForYear_WithMatchingYear_ReturnsCorrectSum()
    {
        // Arrange
        var ledger = CreateTaxLedgerWithData();
        var targetYear = TestDate.Year;

        // Act
        var result = TaxCalculation.CalculateLongTermCapitalGainsForYear(ledger, targetYear);

        // Assert
        Assert.Equal(1000m, result);
    }

    [Fact]
    public void CalculateLongTermCapitalGainsForYear_WithNegativeGains_HandlesCorrectly()
    {
        // Arrange
        var ledger = new TaxLedger
        {
            LongTermCapitalGains = [
                (TestDate, 2000m),
                (TestDate.PlusMonths(1), -500m) // Loss
            ]
        };

        // Act
        var result = TaxCalculation.CalculateLongTermCapitalGainsForYear(ledger, TestDate.Year);

        // Assert
        Assert.Equal(1500m, result); // 2000 - 500
    }

    #endregion

    #region CalculateShortTermCapitalGainsForYear Tests

    [Fact]
    public void CalculateShortTermCapitalGainsForYear_WithMatchingYear_ReturnsCorrectSum()
    {
        // Arrange
        var ledger = CreateTaxLedgerWithData();
        var targetYear = TestDate.Year;

        // Act
        var result = TaxCalculation.CalculateShortTermCapitalGainsForYear(ledger, targetYear);

        // Assert
        Assert.Equal(500m, result);
    }

    #endregion

    #region CalculateFederalWithholdingForYear Tests

    [Fact]
    public void CalculateFederalWithholdingForYear_WithMatchingYear_ReturnsCorrectSum()
    {
        // Arrange
        var ledger = CreateTaxLedgerWithData();
        var targetYear = TestDate.Year;

        // Act
        var result = TaxCalculation.CalculateFederalWithholdingForYear(ledger, targetYear);

        // Assert
        Assert.Equal(8000m, result);
    }

    #endregion

    #region CalculateStateWithholdingForYear Tests

    [Fact]
    public void CalculateStateWithholdingForYear_WithMatchingYear_ReturnsCorrectSum()
    {
        // Arrange
        var ledger = CreateTaxLedgerWithData();
        var targetYear = TestDate.Year;

        // Act
        var result = TaxCalculation.CalculateStateWithholdingForYear(ledger, targetYear);

        // Assert
        Assert.Equal(2000m, result);
    }

    #endregion

    #region CalculateRmdRequirement Tests

    

    [Fact]
    public void CalculateRmdRequirement_WithOnlyRothAccounts_ReturnsZero()
    {
        // Arrange
        var ledger = new TaxLedger();
        var accounts = new BookOfAccounts
        {
            InvestmentAccounts = [
                new McInvestmentAccount
                {
                    Id = Guid.NewGuid(),
                    Name = "test account",
                    AccountType = McInvestmentAccountType.ROTH_IRA,
                    Positions = [
                        TestDataManager.CreateTestInvestmentPosition(1m, 50000m, McInvestmentPositionType.LONG_TERM)
                    ]
                }
            ]
        };

        // Act
        var result = TaxCalculation.CalculateRmdRequirement(ledger, accounts, 75);

        // Assert
        Assert.Equal(0m, result);
    }

    #endregion
}