using Lib.MonteCarlo.TaxForms.Federal;

namespace Lib.Tests.MonteCarlo.TaxForms.Federal;

public class QualifiedDividendsAndCapitalGainTaxWorksheetTests
{
    [Theory]
    /*
     * these expectations were calculated using the "QualifiedDividendsAndCapitalGainTaxWorksheet" tab of the
     * TaxTesting.ods file
     */
    [InlineData(105000, 125, 10000, 5000, 12847.75)] // setup scenario
    [InlineData(35000, 125, 5000, 6000, 3121)] // low income, low gains
    [InlineData(35000, 125, 185000, 215044, 0)] // low income, high gains
    [InlineData(175000, 125, 125000, 184000, 17663.5)] // high income, high gains
    [InlineData(403000, 125, 5000, 6000, 83461.75)] // high income, low gains
    [InlineData(35000, 125, 500, -3000, 3721)] // low income, loss gains
    [InlineData(308721, 125, -1123, -3000, 60166.79)] // high income loss gains

    public static void CalculateTaxOwed_VariousScenarios(
        decimal fed1040Line15, decimal fed1040Line3A, decimal scheduleDLine15NetLongTermCapitalGain, 
        decimal scheduleDLine16CombinedCapitalGains, decimal expectedTaxOwed)
    {
        // Act
        var result = QualifiedDividendsAndCapitalGainTaxWorksheet.CalculateTaxOwed(
            scheduleDLine15NetLongTermCapitalGain, scheduleDLine16CombinedCapitalGains, fed1040Line3A, fed1040Line15);
        
        // Assert
        Assert.Equal(expectedTaxOwed, result);
    }
}