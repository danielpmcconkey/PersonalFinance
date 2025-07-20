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
        
        
        var expectedHeadroom = TaxConstants.Federal1040TaxTableBrackets[1].max 
                               - (24000m * TaxConstants.MaxSocialSecurityTaxPercent)
                               - 10000m
                               - 20000m
                               + TaxConstants.FederalStandardDeduction;
        
        
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
        
        
        var expectedHeadroom = TaxConstants.Federal1040TaxTableBrackets[1].max 
               - (TaxConstants.PlaceholderLastYearsSocialSecurityIncome * TaxConstants.MaxSocialSecurityTaxPercent)
               - 10000m 
               - 20000m
               + TaxConstants.FederalStandardDeduction;
        
        
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
        var result = TaxCalculation.CalculateSocialSecurityIncomeForYear(ledger, _testYear);

        // Assert
        Assert.Equal(socialSecurityIncome * TaxConstants.MaxSocialSecurityTaxPercent, result);
    }

   
    [Theory]
    /*
     * these expectations were calculated using the "NorthCarolinaTaxLiability" tab of the TaxTesting.ods file
     */
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
}