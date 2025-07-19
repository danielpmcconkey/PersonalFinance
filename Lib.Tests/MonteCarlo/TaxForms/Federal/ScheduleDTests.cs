using Xunit;
using Lib.DataTypes.MonteCarlo;
using NodaTime;
using System.Collections.Generic;
using Lib.MonteCarlo.TaxForms.Federal;

namespace Lib.Tests.MonteCarlo.TaxForms.Federal
{
    public class ScheduleDTests
    {
        [Fact]
        public void CalculateTotalShortTermCapitalGainsAndLosses_EmptyLedger_ReturnsZero()
        {
            // Arrange
            var ledger = new TaxLedger
            {
                ShortTermCapitalGains = new List<(LocalDateTime earnedDate, decimal amount)>()
            };

            // Act
            var result = ScheduleD.CalculateTotalShortTermCapitalGainsAndLosses(ledger, 2024);

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public void CalculateTotalShortTermCapitalGainsAndLosses_MultipleYears_ReturnsCorrectYearOnly()
        {
            // Arrange
            var ledger = new TaxLedger
            {
                ShortTermCapitalGains = new List<(LocalDateTime earnedDate, decimal amount)>
                {
                    (new LocalDateTime(2023, 12, 31, 0, 0), 1000m),
                    (new LocalDateTime(2024, 1, 1, 0, 0), 2000m),
                    (new LocalDateTime(2024, 12, 31, 0, 0), 3000m),
                    (new LocalDateTime(2025, 1, 1, 0, 0), 4000m)
                }
            };

            // Act
            var result = ScheduleD.CalculateTotalShortTermCapitalGainsAndLosses(ledger, 2024);

            // Assert
            Assert.Equal(5000m, result); // 2000m + 3000m
        }

        [Fact]
        public void CalculateTotalLongTermCapitalGainsAndLosses_EmptyLedger_ReturnsZero()
        {
            // Arrange
            var ledger = new TaxLedger
            {
                LongTermCapitalGains = new List<(LocalDateTime earnedDate, decimal amount)>()
            };

            // Act
            var result = ScheduleD.CalculateTotalLongTermCapitalGainsAndLosses(ledger, 2024);

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public void CalculateTotalLongTermCapitalGainsAndLosses_MultipleYears_ReturnsCorrectYearOnly()
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
            var result = ScheduleD.CalculateTotalLongTermCapitalGainsAndLosses(ledger, 2024);

            // Assert
            Assert.Equal(5000m, result); // 2000m + 3000m
        }

        [Theory]
        /*
         * these expectations were calculated using the "FedScheduleD" tab of the TaxTesting.ods file
         */
        [InlineData(1000, 20000, 21000, true, false)] // both positive
        [InlineData(-900, -1500, -2400, true, false)] // both small losses
        [InlineData(-3000, -4000, -3000, true, false)] // both big losses
        [InlineData(-900, 10000, 9100, true, false)] // short loss, long gain, overall gain
        [InlineData(10000, -900, 9100, true, false)] // short gain, long loss, overall gain
        [InlineData(-9000, 1000, -3000, true, false)] // short loss, long gain, overall loss
        [InlineData(1000, -9000, -3000, true, false)] // short gain, long loss, overall loss
        public void RunSummaryAndCalculateFinalValue_VariousScenarios(
            decimal shortTermGains,
            decimal longTermGains,
            decimal expectedCombinedGains,
            bool expectedLine20,
            bool expectedLine22)
        {
            // Arrange
            var ledger = new TaxLedger();

            // Act
            var result = ScheduleD.RunSummaryAndCalculateFinalValue(
                ledger,
                shortTermGains,
                longTermGains);

            // Assert
            Assert.Equal(expectedCombinedGains, result.line16Or21CombinedCapitalGains);
            Assert.Equal(expectedLine20, result.line20Are18And19BothZero);
            Assert.Equal(expectedLine22, result.line22DoYouHaveQualifiedDividendsOnCapitalGains);
        }

        
        [Theory]
        /*
         * these expectations were calculated using the "FedScheduleD" tab of the TaxTesting.ods file
         */
        [InlineData(1000, 20000, 21000, true, false)] // both positive
        [InlineData(-900, -1500, -2400, true, false)] // both small losses
        [InlineData(-3000, -4000, -3000, true, false)] // both big losses
        [InlineData(-900, 10000, 9100, true, false)] // short loss, long gain, overall gain
        [InlineData(10000, -900, 9100, true, false)] // short gain, long loss, overall gain
        [InlineData(-9000, 1000, -3000, true, false)] // short loss, long gain, overall loss
        [InlineData(1000, -9000, -3000, true, false)] // short gain, long loss, overall loss
        public void PopulateScheduleDAndCalculateFinalValue_CompleteCalculation(
            decimal shortTermGains,
            decimal longTermGains,
            decimal expectedCombinedGains,
            bool expectedLine20,
            bool expectedLine22)
        {
            // Arrange
            var ledger = new TaxLedger
            {
                ShortTermCapitalGains = new List<(LocalDateTime earnedDate, decimal amount)>
                {
                    (new LocalDateTime(2024, 6, 1, 0, 0), shortTermGains)
                },
                LongTermCapitalGains = new List<(LocalDateTime earnedDate, decimal amount)>
                {
                    (new LocalDateTime(2024, 6, 1, 0, 0), longTermGains)
                }
            };

            // Act
            var result = ScheduleD.PopulateScheduleDAndCalculateFinalValue(ledger, 2024);

            // Assert
            Assert.Equal(longTermGains, result.scheduleDLine15NetLongTermCapitalGain);
            Assert.Equal(expectedCombinedGains, result.scheduleDLine16CombinedCapitalGains);
            Assert.Equal(expectedLine20, result.scheduleDLine20Are18And19BothZero);
            Assert.Equal(expectedLine22, result.scheduleDLine22DoYouHaveQualifiedDividendsOnCapitalGains);
        }
    }
}