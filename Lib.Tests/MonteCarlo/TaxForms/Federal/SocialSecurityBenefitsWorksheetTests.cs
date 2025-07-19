using Xunit;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.TaxForms.Federal;
using System;
using System.Collections.Generic;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.Tests.MonteCarlo.TaxForms.Federal
{
    public class SocialSecurityBenefitsWorksheetTests
    {
        [Fact]
        public void CalculateLine1SocialSecurityIncome_WithNoIncome_ReturnsZero()
        {
            // Arrange
            var ledger = new TaxLedger
            {
                SocialSecurityIncome = new List<(LocalDateTime earnedDate, decimal amount)>()
            };

            // Act
            var result = SocialSecurityBenefitsWorksheet.CalculateLine1SocialSecurityIncome(ledger, 2024);

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public void CalculateLine1SocialSecurityIncome_WithSingleYearIncome_ReturnsTotalForYear()
        {
            // Arrange
            var ledger = new TaxLedger
            {
                SocialSecurityIncome = new List<(LocalDateTime earnedDate, decimal amount)>
                {
                    (new LocalDateTime(2024, 1, 1, 0, 0), 1000m),
                    (new LocalDateTime(2024, 6, 1, 0, 0), 2000m),
                    (new LocalDateTime(2024, 12, 31, 0, 0), 3000m),
                }
            };

            // Act
            var result = SocialSecurityBenefitsWorksheet.CalculateLine1SocialSecurityIncome(ledger, 2024);

            // Assert
            Assert.Equal(6000m, result);
        }

        [Fact]
        public void CalculateLine1SocialSecurityIncome_WithMultipleYears_ReturnsOnlyRequestedYear()
        {
            // Arrange
            var ledger = new TaxLedger
            {
                SocialSecurityIncome = new List<(LocalDateTime earnedDate, decimal amount)>
                {
                    (new LocalDateTime(2023, 12, 31, 0, 0), 1000m),
                    (new LocalDateTime(2024, 1, 1, 0, 0), 2000m),
                    (new LocalDateTime(2025, 1, 1, 0, 0), 3000m),
                }
            };

            // Act
            var result = SocialSecurityBenefitsWorksheet.CalculateLine1SocialSecurityIncome(ledger, 2024);

            // Assert
            Assert.Equal(2000m, result);
        }

        [Theory]
        /*
         * these expectations were calculated using the "FedSocialSecurityBenefitsWorksheet" tab of the TaxTesting.ods
         * file
         */
        [InlineData(500, 10000, 100, 0)]
        [InlineData(625, 12500, 125, 0)]
        [InlineData(781.25, 15625, 156.25, 0)]
        [InlineData(976.56, 19531.25, 195.31, 0)]
        [InlineData(1220.7, 24414.06, 244.14, 0)]
        [InlineData(1525.88, 30517.58, 305.18, 3989.02)]
        [InlineData(1907.35, 38146.98, 381.48, 11076.68)]
        [InlineData(2384.19, 47683.73, 476.85, 21695.86)]
        [InlineData(2980.24, 59604.66, 596.06, 30398.45)]
        [InlineData(3725.3, 74505.83, 745.08, 37998.06)]
        [InlineData(4656.63, 93132.29, 931.35, 47497.63)]
        [InlineData(5820.79, 116415.36, 1164.19, 59372.06)]
        [InlineData(7275.99, 145519.2, 1455.24, 74215.1)]
        [InlineData(9094.99, 181899, 1819.05, 92768.9)]
        public void CalculateTaxableSocialSecurityBenefits_VariousScenarios(
            decimal monthlySocialSecurityWage,
            decimal combinedIncomeFrom1040,
            decimal taxExemptInterest,
            decimal expectedTaxableAmount)
        {
            // Arrange
            const int taxYear = 2024;
            List<(LocalDateTime earnedDate, decimal amount)> socialSecurityIncome = [];
            for (int i = 1; i <= 12; i++)
            {
                var date = new LocalDateTime(taxYear, i, 1, 0, 0);
                var amount = monthlySocialSecurityWage;
                socialSecurityIncome.Add((date, amount));
            }
            var ledger = new TaxLedger
            {
                 
                SocialSecurityIncome = socialSecurityIncome,
            };

            // Act
            var result = SocialSecurityBenefitsWorksheet.CalculateTaxableSocialSecurityBenefits(
                ledger, 
                2024, 
                combinedIncomeFrom1040, 
                taxExemptInterest);

            // Assert
            Assert.Equal(expectedTaxableAmount, Math.Round(result, 2, MidpointRounding.AwayFromZero));
        }

        [Fact]
        public void CalculateTaxableSocialSecurityBenefits_MaximumTaxableAmount()
        {
            // Arrange
            decimal socialSecurityBenefits = 50000m;
            var ledger = new TaxLedger
            {
                SocialSecurityIncome = new List<(LocalDateTime earnedDate, decimal amount)>
                {
                    (new LocalDateTime(2024, 1, 1, 0, 0), socialSecurityBenefits)
                }
            };

            // Act
            var result = SocialSecurityBenefitsWorksheet.CalculateTaxableSocialSecurityBenefits(
                ledger,
                2024,
                100000m, // High combined income
                0m);

            // Assert
            decimal maximumTaxable = socialSecurityBenefits * TaxConstants.MaxSocialSecurityTaxPercent;
            Assert.Equal(maximumTaxable, result);
        }

        [Theory]
        [InlineData(0, 0, 0)] // Zero case
        [InlineData(-1000, 0, 0)] // Negative combined income
        [InlineData(0, -1000, 0)] // Negative line2A
        public void CalculateTaxableSocialSecurityBenefits_EdgeCases(
            decimal combinedIncomeFrom1040,
            decimal line2AFrom1040,
            decimal expectedResult)
        {
            // Arrange
            var ledger = new TaxLedger
            {
                SocialSecurityIncome = new List<(LocalDateTime earnedDate, decimal amount)>
                {
                    (new LocalDateTime(2024, 1, 1, 0, 0), 10000m)
                }
            };

            // Act
            var result = SocialSecurityBenefitsWorksheet.CalculateTaxableSocialSecurityBenefits(
                ledger,
                2024,
                combinedIncomeFrom1040,
                line2AFrom1040);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void CalculateTaxableSocialSecurityBenefits_WithLine2AIncome()
        {
            // Arrange
            const int taxYear = 2024;
            List<(LocalDateTime earnedDate, decimal amount)> socialSecurityIncome = [];
            for (int i = 1; i <= 12; i++)
            {
                var date = new LocalDateTime(taxYear, i, 1, 0, 0);
                var amount = 1666.67m;
                socialSecurityIncome.Add((date, amount));
            }
            var ledger = new TaxLedger
            {
                 
                SocialSecurityIncome = socialSecurityIncome,
            };
            decimal combinedIncome = 30000m;
            decimal line2AIncome = 5000m;

            // Act
            var resultWithLine2A = SocialSecurityBenefitsWorksheet.CalculateTaxableSocialSecurityBenefits(
                ledger, 2024, combinedIncome, line2AIncome);
            var resultWithoutLine2A = SocialSecurityBenefitsWorksheet.CalculateTaxableSocialSecurityBenefits(
                ledger, 2024, combinedIncome, 0m);

            // Assert
            Assert.True(resultWithLine2A >= resultWithoutLine2A, 
                "Additional Line 2A income should increase or maintain taxable benefits");
            
            Assert.Equal(6850.02m, Math.Round(resultWithLine2A,2, MidpointRounding.AwayFromZero));
            Assert.Equal(4000.01m, Math.Round(resultWithoutLine2A,2, MidpointRounding.AwayFromZero));
        }
    }
}