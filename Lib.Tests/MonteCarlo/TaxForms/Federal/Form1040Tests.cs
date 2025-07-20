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
        const decimal testLedgerW2Income = 75000m;
        const decimal testLedgerTaxableInterestReceived = 1000m;
        const decimal testLedgerTaxableIraDistribution = 10000;
        const decimal testLedgerLongTermCapitalGains = 5000m;
        const decimal testLedgerShortTermCapitalGains = 2000m;
        const decimal testLedgerFederalWithholdings = 15000m;
        const decimal testLedgerSocialSecurityBenefits = 50000m;
        private TaxLedger CreateSampleLedger(int year)
        {
            return new TaxLedger
            {
                W2Income = 
                    [(new LocalDateTime(year, 6, 1, 0, 0), testLedgerW2Income)],
                TaxableInterestReceived = 
                    [(new LocalDateTime(year, 12, 31, 0, 0), testLedgerTaxableInterestReceived)],
                TaxableIraDistribution = 
                    [(new LocalDateTime(year, 12, 31, 0, 0), testLedgerTaxableIraDistribution)],
                LongTermCapitalGains = 
                    [(new LocalDateTime(year, 12, 31, 0, 0), testLedgerLongTermCapitalGains)],
                ShortTermCapitalGains = 
                    [(new LocalDateTime(year, 12, 31, 0, 0), testLedgerShortTermCapitalGains)],
                FederalWithholdings = [
                    (new LocalDateTime(year, 12, 31, 0, 0), testLedgerFederalWithholdings)],
                SocialSecurityIncome = 
                    [(new LocalDateTime(year, 12, 31, 0, 0), testLedgerSocialSecurityBenefits)]

            };
        }

        [Fact]
        public void CalculateTotalW2_ReturnsCorrectAmount()
        {
            // Arrange
            var ledger = CreateSampleLedger(2024);

            // Act
            var result = Form1040.CalculateTotalW2(ledger, 2024);

            // Assert
            Assert.Equal(testLedgerW2Income, result);
        }

        [Fact]
        public void CalculateLine1Z_IncludesAllW2Income()
        {
            // Arrange
            var ledger = CreateSampleLedger(2024);

            // Act
            var result = Form1040.CalculateLine1Z(ledger, 2024);

            // Assert
            Assert.Equal(testLedgerW2Income, result); // Only W2 income should be included
        }

        [Fact]
        public void CalculateTaxableInterestReceived_ReturnsCorrectAmount()
        {
            // Arrange
            var ledger = CreateSampleLedger(2024);

            // Act
            var result = Form1040.CalculateTaxableInterestReceived(ledger, 2024);

            // Assert
            Assert.Equal(testLedgerTaxableInterestReceived, result);
        }

        [Fact]
        public void CalculateCombinedIncome_SumsAllIncomeSources()
        {
            // Line 9 instructions are "add lines 1z, 2b, 3b, 4b, 5b, 6b, 7, and 8."
            // but we calculate 6b in the line 9 function and everything else here
            
            
            // Arrange
            var taxYear = 2024;
            var ledger = CreateSampleLedger(taxYear);
            var line1Z = Form1040.CalculateLine1Z(ledger, taxYear);
            var line2B = Form1040.CalculateTaxableInterestReceived(ledger, taxYear);
            var line3B = Form1040.CalculateOrdinaryDividendsEarned();
            var line4B = Form1040.CalculateTaxableIraDistributions(ledger, taxYear);
            var line5B = 0m; // Pensions and annuities
            var line7 = Form1040.CalculateCapitalGainOrLoss(ledger, taxYear);
            var line8 = 0m; // Additional income from Schedule 1, line 10 
            var expectedTotal = 
                line1Z + line2B + line3B + line4B + line5B + line7 + line8;
            
            

            // Act
            var result = Form1040.CalculateCombinedIncome(ledger, 2024);

            // Assert
            Assert.Equal(expectedTotal, result);
        }

        [Fact]
        public void CalculateLine9TotalIncome_SumsAllIncomeSources()
        {
            // Arrange
            var ledger = CreateSampleLedger(2024);
            var combinedIncome = Form1040.CalculateCombinedIncome(ledger, 2024);
            var taxableSocialSecurity = Form1040.CalculateTaxableSocialSecurityBenefits(
                ledger, 2024, combinedIncome, 0);
            var expectedTotal = combinedIncome + taxableSocialSecurity;

            // Act
            var result = Form1040.CalculateLine9TotalIncome(ledger, 2024);

            // Assert
            Assert.Equal(expectedTotal, result);
        }

        [Fact]
        public void CalculateLine15TaxableIncome_AppliesStandardDeduction()
        {
            // Arrange
            var ledger = CreateSampleLedger(2024);

            // Act
            var result = Form1040.CalculateLine15TaxableIncome(ledger, 2024);

            // Assert
            decimal totalIncome = Form1040.CalculateLine9TotalIncome(ledger, 2024);
            decimal expectedTaxableIncome = totalIncome - TaxConstants.FederalStandardDeduction;
            Assert.Equal(expectedTaxableIncome, result);
        }

        [Theory]
        [InlineData(35000, 3739.00)] // Under 100k, uses tax table
        [InlineData(150000, 23106)] // Over 100k, uses tax computation worksheet
        [InlineData(305175.79, 59327.19)] // Over 100k, uses tax computation worksheet, bracket 2
        public void CalculateTax_UsesCorrectCalculationMethod(decimal taxableIncome, decimal expectedTax)
        {
            // Arrange
            var ledger = new TaxLedger();

            // Act
            var result = Form1040.CalculateTax(ledger, 2024, taxableIncome, 0m);

            // Assert
            Assert.Equal(expectedTax, result, 2); // Allow for small rounding differences
        }

        [Fact]
        public void CalculateLine38AmountYouOwe_CalculatesCorrectLiability()
        {
            // Arrange
            var ledger = CreateSampleLedger(2024);

            // Act
            var (liability, taxableIncome) = Form1040.CalculateTaxLiability(ledger, 2024);

            // Assert
            Assert.True(liability < 15000m); // Should be less than withholdings
            Assert.True(taxableIncome > 0);
        }

        [Fact]
        public void CalculateLine33TotalPayments_IncludesWithholdings()
        {
            // Arrange
            var ledger = CreateSampleLedger(2024);

            // Act
            var result = Form1040.CalculateLine33TotalPayments(ledger, 2024);

            // Assert
            Assert.Equal(15000m, result); // Should match federal withholdings
        }

        [Theory]
        [InlineData(0)]
        [InlineData(100000)]
        [InlineData(200000)]
        public void CalculateTaxFromWorksheet_HandlesVariousIncomeLevels(decimal taxableIncome)
        {
            // Arrange
            var ledger = CreateSampleLedger(2024);
            
            // Act & Assert
            if (taxableIncome < 100000)
            {
                var exception = Assert.Throws<InvalidDataException>(() => 
                    Form1040.CalculateTaxFromWorksheet(ledger, 2024, taxableIncome, 0m));
                Assert.Equal("can't use the tax worksheet with income under 100k", exception.Message);
            }
            else
            {
                var result = Form1040.CalculateTaxFromWorksheet(ledger, 2024, taxableIncome, 0m);
                Assert.True(result > 0);
            }
        }

        [Fact]
        public void CalculateCapitalGainOrLoss_HandlesMultipleYears()
        {
            // Arrange
            var ledger = new TaxLedger
            {
                LongTermCapitalGains = new List<(LocalDateTime earnedDate, decimal amount)>
                {
                    (new LocalDateTime(2023, 12, 31, 0, 0), 1000m),
                    (new LocalDateTime(2024, 1, 1, 0, 0), 2000m),
                    (new LocalDateTime(2024, 12, 31, 0, 0), 3000m),
                    (new LocalDateTime(2025, 1, 1, 0, 0), 4000m)
                }
            };

            // Act
            var result = Form1040.CalculateCapitalGainOrLoss(ledger, 2024);

            // Assert
            Assert.Equal(5000m, result); // Should only include 2024 gains
        }
    }
}