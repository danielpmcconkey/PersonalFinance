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
    

    [Theory]
    /*
     * these came from the TaxTesting.ods sheet titled "headroom calc"
     */
    [InlineData(2034,2,108700)]
    [InlineData(2034,3,108700)]
    [InlineData(2034,4,108700)]
    [InlineData(2034,5,108700)]
    [InlineData(2034,6,108700)]
    [InlineData(2034,7,108700)]
    [InlineData(2034,8,108700)]
    [InlineData(2034,9,108700)]
    [InlineData(2034,10,108700)]
    [InlineData(2034,11,108700)]
    [InlineData(2034,12,108700)]
    [InlineData(2035,1,78100)]
    [InlineData(2035,2,78100)]
    [InlineData(2035,3,78100)]
    [InlineData(2035,4,78100)]
    [InlineData(2035,5,78100)]
    [InlineData(2035,6,78100)]
    [InlineData(2035,7,78100)]
    [InlineData(2035,8,78100)]
    [InlineData(2035,9,78100)]
    [InlineData(2035,10,78100)]
    [InlineData(2035,11,78100)]
    [InlineData(2035,12,78100)]
    [InlineData(2036,1,47500)]
    [InlineData(2036,2,47500)]
    [InlineData(2036,3,47500)]
    [InlineData(2036,4,47500)]
    [InlineData(2036,5,47500)]
    [InlineData(2036,6,47500)]
    [InlineData(2036,7,47500)]
    [InlineData(2036,8,47500)]
    [InlineData(2036,9,47500)]
    [InlineData(2036,10,47500)]
    [InlineData(2036,11,47500)]
    [InlineData(2036,12,47500)]



    public void CalculateIncomeRoom_ReturnsCorrectAmount(int currentYear, int currentMonth, decimal expectedHeadroom)
    {
        
        // Arrange
        var currentDate = new LocalDateTime(currentYear, currentMonth, 1, 0, 0);
        var ledger = new TaxLedger();
        ledger.W2Income.Add((currentDate, 1000m));
        ledger.TaxableIraDistribution.Add((currentDate, 2000m));
        ledger.TaxableInterestReceived.Add((currentDate, 3000m));
        ledger.DividendsReceived.Add((currentDate, 4000m));
        ledger.QualifiedDividendsReceived.Add((currentDate, 200m));
        ledger.ShortTermCapitalGains.Add((currentDate, 5000));
        ledger.SocialSecurityElectionStartDate = new(2035,7,1,0,0);
        ledger.SocialSecurityWageMonthly = 6000m;
        
        // Act
        var result = TaxCalculation.CalculateIncomeRoom(ledger, currentDate);

        // Assert
        Assert.Equal(expectedHeadroom, result);
    }

    

    [Fact]
    public void CalculateIncomeRoom_NegativeRoom_ReturnsZero()
    {
        // Arrange
        var ledger = new TaxLedger();
        // should result in 6k * 12 * .85 ss income projection on the year
        ledger.SocialSecurityElectionStartDate = _baseDate.PlusYears(-2);
        ledger.SocialSecurityWageMonthly = 6000m;
        // add some long term cap gains
        ledger.W2Income.Add((_baseDate, 50000m));
        // add some IRA distributions
        ledger.TaxableIraDistribution.Add((_baseDate, 65000));


        var expectedHeadroom = 0;
        
        
        // Act
        var result = TaxCalculation.CalculateIncomeRoom(ledger, _baseDate);

        // Assert
        Assert.Equal(expectedHeadroom, result);
    }
    
    [Theory]
    [InlineData(2047, 0)]
    [InlineData(2048, 18867.92)]
    [InlineData(2049, 19607.84)]
    [InlineData(2050, 20325.2)]
    [InlineData(2051, 21097.05)]
    [InlineData(2052, 21834.06)]
    [InlineData(2053, 22727.27)]
    [InlineData(2054, 23696.68)]
    [InlineData(2055, 24752.48)]
    [InlineData(2056, 25773.2)]
    [InlineData(2057, 27027.03)]
    [InlineData(2058, 28248.59)]
    [InlineData(2059, 29761.9)]
    [InlineData(2060, 31250)]
    [InlineData(2061, 32894.74)]
    [InlineData(2062, 34722.22)]
    [InlineData(2063, 36496.35)]
    [InlineData(2064, 38759.69)]
    [InlineData(2065, 40983.61)]
    public void CalculateRmdRequirement_CalculatesCorrectly(int year, decimal expectatedRmdAmount)
    {
        // Arrange
        var ledger = new TaxLedger();
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        // we want $500k in tax deferred accounts
        accounts.Traditional401K.Positions = [
            TestDataManager.CreateTestInvestmentPosition(
                1000, 100, McInvestmentPositionType.LONG_TERM, true),
            TestDataManager.CreateTestInvestmentPosition(
                1000, 100, McInvestmentPositionType.LONG_TERM, true),
            TestDataManager.CreateTestInvestmentPosition(
                1000, 100, McInvestmentPositionType.LONG_TERM, true)
        ];
        accounts.TraditionalIra.Positions = [
            TestDataManager.CreateTestInvestmentPosition(
                1000, 100, McInvestmentPositionType.LONG_TERM, true),
            TestDataManager.CreateTestInvestmentPosition(
                1000, 100, McInvestmentPositionType.LONG_TERM, true)
        ];
        var currentDate = new LocalDateTime(year, 12, 1, 0, 0);
        
        int age = year - 1975;

        // Act
        var result = TaxCalculation.CalculateRmdRequirement(ledger, accounts, age);
        result = Math.Round(result, 2); // rounding to avoid floating point errors
        // Assert
        Assert.Equal(expectatedRmdAmount, result);
    }
    
   
    [Theory]
    /*
     * these expectations were calculated using the "NorthCarolinaTaxLiability" tab of the TaxTesting.ods file
     */
    [InlineData(10000,5, -5)]
    [InlineData(25000,10, -10)]
    [InlineData(62500,20, 1645)]
    [InlineData(156250,40, 5843.75)]
    [InlineData(390625,80, 16350.63)]
    [InlineData(976562.5,160, 42637.81)]
    public void CalculateNorthCarolinaTaxLiabilityForYear_CalculatesCorrectTax(
        decimal adjustedGrossIncome, decimal withholding, decimal expectation)
    {
        // Arrange
        var ledger = new TaxLedger();
        ledger.StateWithholdings.Add((_baseDate, withholding));
        var result = TaxCalculation.CalculateNorthCarolinaTaxLiabilityForYear(
            ledger, _baseDate.Year, adjustedGrossIncome);
        Assert.Equal(expectation, Math.Round(result, 2, MidpointRounding.AwayFromZero));
    }
}