using Xunit;
using Lib.MonteCarlo.TaxForms.Federal;
using System;

namespace Lib.Tests.MonteCarlo.TaxForms.Federal
{
    public class TaxTableTests
    {
        [Theory]
        /*
         * these expectations were calculated using the "FedTaxTableWorksheet" tab of the TaxTesting.ods file
         */
        [InlineData(10000, 1000)]
        [InlineData(17500, 1750)]
        [InlineData(30625, 3211)]
        [InlineData(53593.75, 5967.25)]
        [InlineData(93789.06, 10790.69)]
        [InlineData(164130.86, 26214.79)]
        [InlineData(287229.01, 55019.96)]
        [InlineData(502650.77, 116677.27)]
        [InlineData(879638.85, 251591.87)]
        [InlineData(1539367.99, 495691.66)]
        [InlineData(2693893.98, 922866.27)]
        public void CalculatePreciseLiability_WithValidAmounts_ReturnsCorrectTax(decimal income, decimal expectedTax)
        {
            // Act
            decimal actualTax = TaxTable.CalculatePreciseLiability(income);

            // Assert
            Assert.Equal(expectedTax, Math.Round(actualTax, 2));
        }
        
        [Theory]
        [InlineData(23200, 2323)] // First bracket boundary
        [InlineData(50000, 5539)] // Middle income test
        [InlineData(50050, 5545)] // Test with amount just over $50 increment
        [InlineData(65000, 7339)] // Example from comments
        [InlineData(94300, 10858)] // Upper bracket boundary
        public void CalculateTaxOwed_WithValidAmounts_ReturnsCorrectTax(decimal income, decimal expectedTax)
        {
            // Act
            decimal actualTax = TaxTable.CalculateTaxOwed(income);

            // Assert
            Assert.Equal(expectedTax, Math.Round(actualTax, 2));
        }

        [Theory]
        [InlineData(0, 0)] // Zero income
        //[InlineData(50, 5)] // don't test below $3000. the table uses a different granularity and it's not likely to come up in this model
        [InlineData(99950, 12101)] // Just under maximum allowed
        public void CalculateTaxOwed_EdgeCases_ReturnsCorrectTax(decimal income, decimal expectedTax)
        {
            // Act
            decimal actualTax = TaxTable.CalculateTaxOwed(income);

            // Assert
            Assert.Equal(expectedTax, Math.Round(actualTax, 2));
        }

        [Theory]
        [InlineData(100001)]
        [InlineData(150000)]
        [InlineData(1000000)]
        public void CalculateTaxOwed_WithAmountOver100k_ThrowsInvalidDataException(decimal income)
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidDataException>(() => 
                TaxTable.CalculateTaxOwed(income));
            Assert.Equal("can't use the tax table with income over 100k", exception.Message);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-1000)]
        public void CalculateTaxOwed_WithNegativeAmount_ReturnsZero(decimal income)
        {
            // Act
            decimal actualTax = TaxTable.CalculateTaxOwed(income);

            // Assert
            Assert.Equal(0, actualTax);
        }

        [Theory]
        [InlineData(25000)] // Middle of first bracket
        [InlineData(50000)] // Middle of second bracket
        [InlineData(96900)] // Near top of brackets
        public void CalculateTaxOwed_ConsistentWith50DollarIncrements(decimal income)
        {
            // Arrange
            const decimal increment = 50m;
            
            // Act
            decimal tax1 = TaxTable.CalculateTaxOwed(income);
            decimal tax2 = TaxTable.CalculateTaxOwed(income + increment);

            // Assert
            // The difference between tax amounts for $50 increments should be reasonable
            decimal taxDifference = tax2 - tax1;
            Assert.True(taxDifference >= 0 && taxDifference <= increment * 0.37m, 
                $"Tax difference of {taxDifference} for {increment:C} income increment is not reasonable");
        }

        [Fact]
        public void CalculateTaxOwed_ExactTableGrainAmount_CalculatesAverage()
        {
            // Arrange
            decimal income = 65000m; // Exact multiple of 50

            // Act
            decimal actualTax = TaxTable.CalculateTaxOwed(income);
            decimal taxForLower = TaxTable.CalculateTaxOwed(income - 25);
            decimal taxForHigher = TaxTable.CalculateTaxOwed(income + 25);

            // Assert
            // The tax for the exact amount should be between the lower and higher amounts
            Assert.True(actualTax >= taxForLower && actualTax <= taxForHigher, 
                "Tax for exact table grain amount should be between lower and higher bounds");
        }

        [Theory]
        [InlineData(23850)] // First bracket boundary
        [InlineData(96950)] // Second bracket boundary
        public void CalculateTaxOwed_AtBracketBoundaries_TransitionsSmoothly(decimal income)
        {
            // Arrange
            const decimal delta = 1m;

            // Act
            decimal taxJustBelow = TaxTable.CalculateTaxOwed(income - delta);
            decimal taxAtBoundary = TaxTable.CalculateTaxOwed(income);
            decimal taxJustAbove = TaxTable.CalculateTaxOwed(income + delta);

            // Assert
            // Tax should transition smoothly at bracket boundaries
            Assert.True(taxJustBelow <= taxAtBoundary && taxAtBoundary <= taxJustAbove,
                "Tax should transition smoothly at bracket boundaries");
        }
    }
}