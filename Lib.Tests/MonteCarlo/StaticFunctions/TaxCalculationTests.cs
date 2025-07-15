using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class TaxCalculationTests
{
    private readonly int _testYear = 2025;
    private readonly LocalDateTime _baseDate = new(2025, 1, 1, 0, 0);
    //
    // private TaxLedger CreateTestLedger(
    //     List<(LocalDateTime date, decimal amount)>? capitalGains = null,
    //     List<(LocalDateTime date, decimal amount)>? ordinaryIncome = null,
    //     List<(LocalDateTime date, decimal amount)>? socialSecurityIncome = null)
    // {
    //     return new TaxLedger
    //     {
    //         LongTermCapitalGains = capitalGains ?? new List<(LocalDateTime date, decimal amount)>(),
    //         OrdinaryIncome = ordinaryIncome ?? new List<(LocalDateTime date, decimal amount)>(),
    //         SocialSecurityIncome = socialSecurityIncome ?? new List<(LocalDateTime date, decimal amount)>(),
    //         RmdDistributions = new Dictionary<int, decimal>(),
    //         IncomeTarget = incomeTarget
    //     };
    // }

    
    //
    // [Fact]
    // public void CalculateEarnedIncomeForYear_CorrectlyCalculatesTotal()
    // {
    //     // Arrange
    //     
    //     var ledger = new TaxLedger();
    //     ledger = Tax.RecordSocialSecurityIncome(ledger, _baseDate, 24000m);
    //     ledger = Tax.RecordIncome(ledger, _baseDate, 50000m);
    //     ledger = Tax.RecordLongTermCapitalGain(ledger, _baseDate, 3000m);
    //
    //     // Act
    //     var result = TaxCalculation.CalculateEarnedIncomeForYear(ledger, _testYear);
    //
    //     // Assert
    //     var expectedSocialSecurityTaxable = 24000m * TaxConstants.MaxSocialSecurityTaxPercent;
    //     var expectedTotal = 50000m + expectedSocialSecurityTaxable - TaxConstants._standardDeduction;
    //     Assert.Equal(expectedTotal, result);
    // }

    [Fact]
    public void CalculateIncomeRoom_ReturnsCorrectAmount()
    {
        // Arrange
        var ledger = new TaxLedger();
        // add last year's social security wages
        ledger.SocialSecurityIncome.Add((_baseDate.PlusYears(-1), 24000m));
        // add some long term cap gains
        ledger.LongTermCapitalGains.Add((_baseDate, 10000m));
        // add some IRA distributions
        ledger.TaxableIraDistribution.Add((_baseDate, 20000m));
        
        
        var expectedHeadroom = TaxConstants._incomeTaxBrackets[1].max 
                               - (24000m * TaxConstants.MaxSocialSecurityTaxPercent)
                               - 10000m
                               - 20000m
                               + TaxConstants._standardDeduction;
        
        
        // Act
        var result = TaxCalculation.CalculateIncomeRoom(ledger, _testYear);

        // Assert
        Assert.Equal(expectedHeadroom, result);
    }

    [Fact]
    public void CalculateIncomeRoom_WithNoPriorSocialSecurityIncome_UsesPlaceholderValue()
    {
        // Arrange
        var ledger = new TaxLedger();
        // add some long term cap gains
        ledger.LongTermCapitalGains.Add((_baseDate, 10000m));
        // add some IRA distributions
        ledger.TaxableIraDistribution.Add((_baseDate, 20000m));
        
        
        var expectedHeadroom = TaxConstants._incomeTaxBrackets[1].max 
               - (TaxConstants.PlaceholderLastYearsSocialSecurityIncome * TaxConstants.MaxSocialSecurityTaxPercent)
               - 10000m 
               - 20000m
               + TaxConstants._standardDeduction;
        
        
        // Act
        var result = TaxCalculation.CalculateIncomeRoom(ledger, _testYear);

        // Assert
        Assert.Equal(expectedHeadroom, result);
    }

    [Fact]
    public void CalculateIncomeRoom_NegativeRoom_ReturnsZero()
    {
        // Arrange
        var ledger = new TaxLedger();
        // add last year's social security wages
        ledger.SocialSecurityIncome.Add((_baseDate.PlusYears(-1), 48000m));
        // add some long term cap gains
        ledger.LongTermCapitalGains.Add((_baseDate, 50000m));
        // add some IRA distributions
        ledger.TaxableIraDistribution.Add((_baseDate, 65000));


        var expectedHeadroom = 0;
        
        
        // Act
        var result = TaxCalculation.CalculateIncomeRoom(ledger, _testYear);

        // Assert
        Assert.Equal(expectedHeadroom, result);
    }

    [Theory]
    [InlineData(2055, true)]  // Assuming this year has an RMD rate
    [InlineData(1900, false)] // Assuming this year doesn't have an RMD rate
    public void CalculateRmdRateByYear_ReturnsExpectedResult(int year, bool shouldHaveRate)
    {
        // Act
        var result = TaxCalculation.CalculateRmdRateByYear(year);

        // Assert
        Assert.Equal(shouldHaveRate, result.HasValue);
    }

    [Fact]
    public void CalculateTaxableSocialSecurityIncome_AppliesCorrectPercentage()
    {
        // Arrange
        var socialSecurityIncome = 24000m;
        var ledger = new TaxLedger();
        ledger.SocialSecurityIncome.Add((_baseDate, socialSecurityIncome));

        // Act
        var result = TaxCalculation.CalculateTaxableSocialSecurityIncomeForYear(ledger, _testYear);

        // Assert
        Assert.Equal(socialSecurityIncome * TaxConstants.MaxSocialSecurityTaxPercent, result);
    }

    // [Theory]
    // [InlineData(10500, 1050)]
    // [InlineData(23625, 2371)]
    // [InlineData(53156.25, 5914.75)]
    // [InlineData(119601.56, 16418.34)]
    // [InlineData(269103.52, 50669.84)]
    // [InlineData(605482.91, 111770.12)]
    // [InlineData(1362336.55, 345730.65)]
    // public void CalculateTaxOnOrdinaryIncomeForYear_CalculatesCorrectTax(
    //     decimal income, decimal expectedLiability)
    // {
    //     // Arrange
    //     var ledger = CreateTestLedger();
    //
    //     // Act
    //     var result = TaxCalculation.CalculateTaxOnOrdinaryIncomeForYear(income, ledger, _testYear);
    //
    //     // Assert
    //     Assert.Equal(expectedLiability, result);
    // }

    [Theory]
    [InlineData(10000, 10000, 798)]
    [InlineData(25000, 30000, 2194.5)]
    [InlineData(62500, 90000, 6084.75)]
    [InlineData(156250, 270000, 17007.38)]
    [InlineData(390625, 810000, 47904.94)]
    [InlineData(976562.5, 2430000, 135921.84)]
    public void CalculateNorthCarolinaTaxLiabilityForYear_CalculatesCorrectTax(
        decimal earnedIncome, decimal totalCapitalGains, decimal expectation)
    {
        var result = TaxCalculation.CalculateNorthCarolinaTaxLiabilityForYear(
            earnedIncome + totalCapitalGains);
        Assert.Equal(expectation, Math.Round(result, 2));
    }

    //
    // [Fact]
    // public void CalculateTaxLiabilityForYear_IncludesAllComponents()
    // {
    //     /*
    //      * we've tested the individual components enough. Let's just make sure the orchestrating method brings them
    //      * all together
    //      */
    //     
    //     // Arrange
    //     var ledger = new TaxLedger();
    //     ledger.LongTermCapitalGains.Add((_baseDate, 130000m));
    //     ledger.OrdinaryIncome.Add((_baseDate, 90000m));
    //     ledger.SocialSecurityIncome.Add((_baseDate, 40000m));
    //
    //     // Act
    //     
    //     // first call the individual functions
    //     var earnedIncome = TaxCalculation.CalculateEarnedIncomeForYear(ledger, _baseDate.Year);
    //     var totalCapitalGains = TaxCalculation.CalculateCapitalGainsForYear(ledger, _baseDate.Year);
    //     var expected = TaxCalculation.CalculateTaxOnOrdinaryIncomeForYear(earnedIncome, ledger, _baseDate.Year);
    //     expected += TaxCalculation.CalculateTaxOnCapitalGainsForYear(totalCapitalGains, earnedIncome, ledger, _baseDate.Year);
    //     expected += TaxCalculation.CalculateNorthCarolinaTaxLiabilityForYear(earnedIncome, totalCapitalGains);
    //     var result = TaxCalculation.CalculateTaxLiabilityForYear(ledger, _baseDate.Year);
    //
    //     // Assert
    //     Assert.Equal(expected, result);
    // }
    /*
     * you are here. AI made all the functions below and you are vetting them. you've done that for all the above
     */
    // [Theory]
    // [InlineData(75000, 10000, 0)] 
    // [InlineData(85000, 10000, 142.5)]
    // [InlineData(175000, 10000, 1500.0)]
    // [InlineData(600000, 100000, 20000)]
    // [InlineData(100000, 600000, 95812.5)]
    // public void CalculateTaxOnCapitalGainsForYear_HandlesThresholds(
    //     decimal earnedIncome, decimal capitalGains, decimal expectation)
    // {
    //     // https://www.irs.gov/taxtopics/tc409
    //     // https://www.forbes.com/advisor/taxes/capital-gains-tax-calculator/
    //     
    //     // Arrange
    //     var ledger = CreateTestLedger();
    //
    //     // Act
    //     var result = TaxCalculation.CalculateTaxOnCapitalGainsForYear(
    //         capitalGains, earnedIncome, ledger, _testYear);
    //
    //     // Assert
    //     Assert.Equal(expectation, Math.Round(result, 2));
    // }
}