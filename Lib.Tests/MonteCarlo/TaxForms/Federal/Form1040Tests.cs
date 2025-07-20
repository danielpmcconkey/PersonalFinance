using Xunit;
using Lib.DataTypes.MonteCarlo;
using NodaTime;
using System.Collections.Generic;
using Lib.MonteCarlo.TaxForms.Federal;
using Lib.StaticConfig;

namespace Lib.Tests.MonteCarlo.TaxForms.Federal
{
    public class Form1040Tests
    {
        private TaxLedger CreateSampleLedger(
            int year, 
            decimal socialSecurityIncome, 
            decimal w2Income, 
            decimal taxableIraDistribution, 
            decimal taxableInterestReceived, 
            decimal dividendsReceivedTotal, 
            decimal qualifiedDividends, 
            decimal federalWithholdings, 
            decimal stateWithholdings, 
            decimal longTermCapitalGains, 
            decimal shortTermCapitalGains
            )
        {
            return new TaxLedger
            {
                W2Income = 
                    [(new LocalDateTime(year, 6, 1, 0, 0), w2Income)],
                TaxableInterestReceived = 
                    [(new LocalDateTime(year, 12, 31, 0, 0), taxableInterestReceived)],
                TaxableIraDistribution = 
                    [(new LocalDateTime(year, 12, 31, 0, 0), taxableIraDistribution)],
                LongTermCapitalGains = 
                    [(new LocalDateTime(year, 12, 31, 0, 0), longTermCapitalGains)],
                ShortTermCapitalGains = 
                    [(new LocalDateTime(year, 12, 31, 0, 0), shortTermCapitalGains)],
                FederalWithholdings = [
                    (new LocalDateTime(year, 12, 31, 0, 0), federalWithholdings)],
                SocialSecurityIncome = 
                    [(new LocalDateTime(year, 12, 31, 0, 0), socialSecurityIncome)],
                DividendsReceived = 
                    [(new LocalDateTime(year, 12, 31, 0, 0), dividendsReceivedTotal)],
                QualifiedDividendsReceived = 
                    [(new LocalDateTime(year, 12, 31, 0, 0), qualifiedDividends)],
                StateWithholdings = 
                    [(new LocalDateTime(year, 12, 31, 0, 0), stateWithholdings)],

            };
        }

        [Theory]
        /*
         * these testing scenarios are built using the TaxTest.ods spreadsheet tab named "Fed1040"
         */
// setup 1
        [InlineData(48000, 200000,0, 0, 2100, 2000, 73000, 7000, 125000, 1535,-16688.6)]
// setup 2
        [InlineData(48000, 50000,0, 0, 2100, 2000, 0, 0, 7000, -8000,5815)]
// all wages + withholding
        [InlineData(0, 324000,0, 0, 0, 0, 40000, 12000, 0, 0,16837)]
// early retirement
        [InlineData(48000, 0,60000, 0, 1000, 0, 0, 0, 10000, 0,8251)]
// lavish retirement
        [InlineData(48000, 0,100000, 0, 2100, 2000, 0, 0, 50000, -4500,21805)]
// broke
        [InlineData(32000, 0,0, 0, 0, 0, 0, 0, 0, 0,0)]
        public void CalculateTaxLiability_VariousScenarios_CalculatesCorrectly(
            decimal socialSecurityIncome, 
            decimal w2Income, 
            decimal taxableIraDistribution, 
            decimal taxableInterestReceived, 
            decimal dividendsReceivedTotal, 
            decimal qualifiedDividends, 
            decimal federalWithholdings, 
            decimal stateWithholdings, 
            decimal longTermCapitalGains, 
            decimal shortTermCapitalGains,
            decimal expectedTaxLiability)
        {
            // arrange
            var taxYear = 2024;
            var ledger = CreateSampleLedger(
                taxYear,
                socialSecurityIncome, 
                w2Income,
                taxableIraDistribution,
                taxableInterestReceived, 
                dividendsReceivedTotal,
                qualifiedDividends, 
                federalWithholdings,
                stateWithholdings,
                longTermCapitalGains,
                shortTermCapitalGains);
            // act
            var form1040 = new Form1040(ledger, taxYear);
            var result = form1040.CalculateTaxLiability();
            // assert
            Assert.Equal(expectedTaxLiability, result);
        }
    }
}