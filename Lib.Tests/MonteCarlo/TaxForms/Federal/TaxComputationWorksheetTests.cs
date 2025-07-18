using Xunit;
using Lib.MonteCarlo.TaxForms.Federal;
using System;

namespace Lib.Tests.MonteCarlo.TaxForms.Federal
{
    public class TaxComputationWorksheetTests
    {
        [Theory]
        [InlineData(100000, 12106)]
        [InlineData(125000, 17606)]
        [InlineData(156250, 24481)]
        [InlineData(195312.5, 33074.75)]
        [InlineData(244140.63, 44678.75)]
        [InlineData(305175.79, 59327.19)]
        [InlineData(381469.74, 77637.74)]
        [InlineData(476837.18, 107960.9)]
        [InlineData(596046.48, 149365.77)]
        [InlineData(745058.1, 201797)]
        [InlineData(931322.63, 270714.87)]
        public void CalculateTaxOwed_WithValidAmounts_ReturnsCorrectTax(decimal income, decimal expectedTax)
        {
            // Act
            decimal actualTax = TaxComputationWorksheet.CalculateTaxOwed(income);

            // Assert
            Assert.Equal(expectedTax, Math.Round(actualTax, 2));
        }

        [Theory]
        [InlineData(99999)] // Just below minimum threshold
        [InlineData(0)]
        [InlineData(-1000)]
        public void CalculateTaxOwed_WithAmountBelowMinimumThreshold_ThrowsInvalidDataException(decimal income)
        {
            // Act & Assert
            Assert.Throws<InvalidDataException>(() => TaxComputationWorksheet.CalculateTaxOwed(income));
        }

        [Theory]
        [InlineData(201050, 34337)]
        [InlineData(383900, 78221)]
        public void CalculateTaxOwed_AtExactBracketBoundaries_ReturnsCorrectTax(decimal income, decimal expectedTax)
        {
            // Arrange
            
            // Act
            decimal actualTax = TaxComputationWorksheet.CalculateTaxOwed(income);

            // Assert
            Assert.Equal(expectedTax, actualTax);
        }

        [Fact]
        public void CalculateTaxOwed_WithVeryLargeIncome_CalculatesCorrectly()
        {
            // Arrange
            decimal income = decimal.MaxValue / 2; // Using a very large number
            decimal expectedTax = (income * 0.37m) - 73874.5m;

            // Act
            decimal actualTax = TaxComputationWorksheet.CalculateTaxOwed(income);

            // Assert
            Assert.Equal(expectedTax, actualTax);
        }
    }
}