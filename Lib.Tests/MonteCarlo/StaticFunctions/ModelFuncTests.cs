
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using Lib.Utils;
using NodaTime;
using Xunit;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class ModelTests
{
    #region utility functions and private variables
    
    private static LocalDateTime _birthdate = new (1978, 3, 1, 0, 0);
    
    
    #endregion

   
    #region tests

    [Theory]
    [InlineData(HereditarySource.ParentA,38800,41200)]
    [InlineData(HereditarySource.ParentB,38800,41200)]
    [InlineData(HereditarySource.Random,19400,20600)]
    public void GetHeredetarySource_IsRandom(HereditarySource source, int expectedLow, int expectedHigh)
    {
        // Arrange
        var totalChecks = 100000;
        var count = 0;
        
        // Act
        for (int i = 0; i < totalChecks; i++)
        {
            if(ModelFunc.GetHereditarySource() == source) count++;
        }
        
        // Assert
        Assert.InRange(count, expectedLow, expectedHigh);
    }

    [Fact]
    public void GetUnSeededRandomDate_WillProduceMinAndMaxDate()
    {
        // Arrange
        var min = _birthdate;
        var max = _birthdate.PlusYears(10);
        var numTries = 100000;
        int countMin = 0;
        int countMax = 0;
        // Act
        for (int i = 0; i < numTries; i++)
        {
            var result = MathFunc.GetUnSeededRandomDate(min, max);
            if(result == min) countMin++;
            if(result == max) countMax++;
        }
        //Assert
        Assert.True(countMin > 500);
        Assert.True(countMax > 500);
        // but not too many
        Assert.True(countMin < 1500);
        Assert.True(countMax < 1500);
    }
    
    [Theory]
    [InlineData(0,4841,5200)] // 0 and 10 will only get half as many due to rounding
    [InlineData(1,9700,10300)]
    [InlineData(2,9700,10300)]
    [InlineData(3,9700,10300)]
    [InlineData(4,9700,10300)]
    [InlineData(5,9700,10300)]
    [InlineData(6,9700,10300)]
    [InlineData(7,9700,10300)]
    [InlineData(8,9700,10300)]
    [InlineData(9,9700,10300)]
    [InlineData(10,4850,5150)] // 0 and 10 will only get half as many due to rounding
    public void GetUnSeededRandomDecimal_IsRandom(int batch, int expectedLow, int expectedHigh)
    {
        var totalChecks = 100000;
        var min = 0.0m;
        var max = 10.0m;
        var divisor = 1.0m;
        var count = 0;
        
        // Act
        for (int i = 0; i < totalChecks; i++)
        {
            var result = MathFunc.GetUnSeededRandomDecimal(min, max);
            var rounded = (int)(Math.Round(result / divisor, 0));
            if(rounded == batch) count++;
        }
        
        // Assert
        Assert.InRange(count, expectedLow, expectedHigh);
    }
    
    [Fact]
    public void MateAusterityRatio_ReturnsValidResult()
    {
        // Arrange
        var modelA = ModelFunc.CreateRandomModel(_birthdate);
        var modelB = ModelFunc.CreateRandomModel(_birthdate);

        // Act
        var result = ModelFunc.MateAusterityRatio(modelA, modelB);

        // Assert
        Assert.InRange(result, ModelConstants.AusterityRatioMin, ModelConstants.AusterityRatioMax);
    }

    [Fact]
    public void MateDesiredMonthlySpendPostRetirement_ReturnsValidResult()
    {
        // Arrange
        var modelA = ModelFunc.CreateRandomModel(_birthdate);
        var modelB = ModelFunc.CreateRandomModel(_birthdate);

        // Act
        var result = ModelFunc.MateDesiredMonthlySpendPostRetirement(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.DesiredMonthlySpendPostRetirementMin,
            ModelConstants.DesiredMonthlySpendPostRetirementMax);
    }

    [Fact]
    public void MateDesiredMonthlySpendPreRetirement_ReturnsValidResult()
    {
        // Arrange
        var modelA = ModelFunc.CreateRandomModel(_birthdate);
        var modelB = ModelFunc.CreateRandomModel(_birthdate);

        // Act
        var result = ModelFunc.MateDesiredMonthlySpendPreRetirement(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.DesiredMonthlySpendPreRetirementMin,
            ModelConstants.DesiredMonthlySpendPreRetirementMax);
    }

    [Fact]
    public void MateExtremeAusterityNetWorthTrigger_ReturnsValidResult()
    {
        // Arrange
        var modelA = ModelFunc.CreateRandomModel(_birthdate);
        var modelB = ModelFunc.CreateRandomModel(_birthdate);

        // Act
        var result = ModelFunc.MateExtremeAusterityNetWorthTrigger(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.ExtremeAusterityNetWorthTriggerMin, 
            ModelConstants.ExtremeAusterityNetWorthTriggerMax);
    }

    [Fact]
    public void MateExtremeAusterityRatio_ReturnsValidResult()
    {
        // Arrange
        var modelA = ModelFunc.CreateRandomModel(_birthdate);
        var modelB = ModelFunc.CreateRandomModel(_birthdate);

        // Act
        var result = ModelFunc.MateExtremeAusterityRatio(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.ExtremeAusterityRatioMin, ModelConstants.ExtremeAusterityRatioMax);
    }

    [Fact]
    public void MateNumMonthsCashOnHand_ReturnsValidResult()
    {
        // Arrange
        var modelA = ModelFunc.CreateRandomModel(_birthdate);
        var modelB = ModelFunc.CreateRandomModel(_birthdate);

        // Act
        var result = ModelFunc.MateNumMonthsCashOnHand(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.NumMonthsCashOnHandMin, 
            ModelConstants.NumMonthsCashOnHandMax);
    }

    [Fact]
    public void MateNumMonthsMidBucketOnHand_ReturnsValidResult()
    {
        // Arrange
        var modelA = ModelFunc.CreateRandomModel(_birthdate);
        var modelB = ModelFunc.CreateRandomModel(_birthdate);

        // Act
        var result = ModelFunc.MateNumMonthsMidBucketOnHand(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.NumMonthsMidBucketOnHandMin, 
            ModelConstants.NumMonthsMidBucketOnHandMax);
    }

    [Fact]
    public void MateNumMonthsPriorToRetirementToBeginRebalance_ReturnsValidResult()
    {
        // Arrange
        var modelA = ModelFunc.CreateRandomModel(_birthdate);
        var modelB = ModelFunc.CreateRandomModel(_birthdate);

        // Act
        var result = ModelFunc.MateNumMonthsPriorToRetirementToBeginRebalance(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.NumMonthsPriorToRetirementToBeginRebalanceMin, 
            ModelConstants.NumMonthsPriorToRetirementToBeginRebalanceMax);
    }

    [Fact]
    public void MatePercent401KTraditional_ReturnsValidResult()
    {
        // Arrange
        var modelA = ModelFunc.CreateRandomModel(_birthdate);
        var modelB = ModelFunc.CreateRandomModel(_birthdate);

        // Act
        var result = ModelFunc.MatePercent401KTraditional(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.Percent401KTraditionalMin,
            ModelConstants.Percent401KTraditionalMax);
    }

    [Fact]
    public void MateRebalanceFrequency_ReturnsValidResult()
    {
        // Arrange
        var modelA = ModelFunc.CreateRandomModel(_birthdate);
        var modelB = ModelFunc.CreateRandomModel(_birthdate);

        // Act
        var result = ModelFunc.MateRebalanceFrequency(modelA, modelB);

        // Assert
        Assert.True(Enum.IsDefined(typeof(RebalanceFrequency), result));
    }

    [Fact]
    public void MateRecessionCheckLookBackMonths_ReturnsValidResult()
    {
        // Arrange
        var modelA = ModelFunc.CreateRandomModel(_birthdate);
        var modelB = ModelFunc.CreateRandomModel(_birthdate);

        // Act
        var result = ModelFunc.MateRecessionCheckLookBackMonths(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.RecessionCheckLookBackMonthsMin,
            ModelConstants.RecessionCheckLookBackMonthsMax);
    }

    [Fact]
    public void MateRecessionRecoveryPointModifier_ReturnsValidResult()
    {
        // Arrange
        var modelA = ModelFunc.CreateRandomModel(_birthdate);
        var modelB = ModelFunc.CreateRandomModel(_birthdate);

        // Act
        var result = ModelFunc.MateRecessionRecoveryPointModifier(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.RecessionRecoveryPointModifierMin,
            ModelConstants.RecessionRecoveryPointModifierMax);
    }

    [Fact]
    public void MateRetirementDate_ReturnsValidResult()
    {
        // Arrange
        var modelA = ModelFunc.CreateRandomModel(_birthdate);
        var modelB = ModelFunc.CreateRandomModel(_birthdate);
        var minDate = _birthdate
            .PlusYears(ModelConstants.RetirementAgeMin.years)
            .PlusMonths(ModelConstants.RetirementAgeMin.months);
        var maxDate = _birthdate
            .PlusYears(ModelConstants.RetirementAgeMax.years)
            .PlusMonths(ModelConstants.RetirementAgeMax.months);

        // Act
        var result = ModelFunc.MateRetirementDate(modelA, modelB, _birthdate);

        // Assert
        Assert.InRange(result, minDate, maxDate);
    }

    [Fact]
    public void MateSocialSecurityStartDate_ReturnsValidResult()
    {
        // Arrange
        var modelA = ModelFunc.CreateRandomModel(_birthdate);
        var modelB = ModelFunc.CreateRandomModel(_birthdate);
        var minDate = _birthdate
            .PlusYears(ModelConstants.SocialSecurityElectionStartMin.years)
            .PlusMonths(ModelConstants.SocialSecurityElectionStartMin.months);
        var maxDate = _birthdate
            .PlusYears(ModelConstants.SocialSecurityElectionStartMax.years)
            .PlusMonths(ModelConstants.SocialSecurityElectionStartMax.months);

        // Act
        var result = ModelFunc.MateSocialSecurityStartDate(modelA, modelB, _birthdate);

        // Assert
        Assert.InRange(result, minDate, maxDate);
    }
    

    [Fact]
    public void MateProperty_WhenParentsHaveAnOutOfBoundsProperty_ReturnsClampedResult()
    {
        // we're gonna loop over this and perform the same test multiple times because the MateProperty acts randomly
        // whether it's gonna take mom's value, dad's value, or a random value. And we want to ensure that, if mom's or
        // dad's is out of bounds, then the value gets clamped
        for (int i = 0; i < 100; i++)
        {
            // Arrange
            var minDate = _birthdate
                .PlusYears(ModelConstants.SocialSecurityElectionStartMin.years)
                .PlusMonths(ModelConstants.SocialSecurityElectionStartMin.months);
            var maxDate = _birthdate
                .PlusYears(ModelConstants.SocialSecurityElectionStartMax.years)
                .PlusMonths(ModelConstants.SocialSecurityElectionStartMax.months);
            var minSpend = ModelConstants.DesiredMonthlySpendPostRetirementMin;
            var maxSpend = ModelConstants.DesiredMonthlySpendPostRetirementMax;
            
            var modelA = ModelFunc.CreateRandomModel(_birthdate);
            var modelB = ModelFunc.CreateRandomModel(_birthdate);
            modelA.SocialSecurityStart = minDate.PlusMonths(-1);
            modelB.SocialSecurityStart = maxDate.PlusMonths(1);
            modelA.DesiredMonthlySpendPostRetirement = minSpend - 1;
            modelB.DesiredMonthlySpendPostRetirement = maxSpend + 1;

            // Act
            var dateResult = ModelFunc.MateSocialSecurityStartDate(modelA, modelB, _birthdate);
            var spendResult = ModelFunc.MateDesiredMonthlySpendPostRetirement(modelA, modelB);

            // Assert
            Assert.InRange(dateResult, minDate, maxDate);
            Assert.InRange(spendResult, minSpend, maxSpend);
        }
    }
    
    
    [Fact]
    public void GetNudgeHandler_WithIntPropertyAndSignificantDiff_ReturnsMiddleValue()
    {
        // Arrange
        var valueA = 8;
        var valueB = 4;
        var min = 2;
        var max = 10;
        var expected = 6;

        // Act
        var actual = ModelFunc.GenerateNudgeValue(min, max, valueA, valueB);;

        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    public void GetNudgeHandler_WithIntPropertyAndSameAtMax_ReturnsOneLessThanMax()
    {
        // Arrange
        var valueA = 10;
        var valueB = 10;
        var min = 2;
        var max = 10;
        var expected = 9;

        // Act
        var actual = ModelFunc.GenerateNudgeValue(min, max, valueA, valueB);;

        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    public void GetNudgeHandler_WithIntPropertyAndSameAboveMax_ReturnsOneLessThanMax()
    {
        // Arrange
        var valueA = 12;
        var valueB = 12;
        var min = 2;
        var max = 10;
        var expected = 9;

        // Act
        var actual = ModelFunc.GenerateNudgeValue(min, max, valueA, valueB);;

        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    public void GetNudgeHandler_WithIntPropertyAndSameAtMin_ReturnsOneMoreThanMin()
    {
        // Arrange
        var valueA = 2;
        var valueB = 2;
        var min = 2;
        var max = 10;
        var expected = 3;

        // Act
        var actual = ModelFunc.GenerateNudgeValue(min, max, valueA, valueB);;

        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    public void GetNudgeHandler_WithIntPropertyAndSameInMiddle_ReturnsOneMoreOrOneLessThanA()
    {
        // Arrange
        var valueA = 6;
        var valueB = 6;
        var min = 2;
        var max = 10;
        var count5 = 0;
        var count7 = 0;
        var expectedA = 7;
        var expectedB = 5;
        var numTests = 1000;
        var maxDiff = 1000 / 4;

        // Act
        for (int i = 0; i < numTests; i++)
        {
            var actual = ModelFunc.GenerateNudgeValue(min, max, valueA, valueB);;
            if(actual == expectedA) count5++;
            else if(actual == expectedB) count7++;
            else Assert.True(false); // this ain't right
        }
        var actualDiff = Math.Abs(count5 - count7);

        // Assert
        Assert.True(actualDiff < maxDiff);
    }
    
    
    
    
    [Fact]
    public void GetNudgeHandler_WithDecPropertyAndSignificantDiff_ReturnsMiddleValue()
    {
        // Arrange
        var valueA = 8m;
        var valueB = 4m;
        var min = 2m;
        var max = 10m;
        var expected = 6m;

        // Act
        var actual = ModelFunc.GenerateNudgeValue(min, max, valueA, valueB);;

        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    public void GetNudgeHandler_WithDecPropertyAndSameAtMax_ReturnsOneLessThanMax()
    {
        // Arrange
        var valueA = 10m;
        var valueB = 10m;
        var min = 2m;
        var max = 10m;
        var significance = (max - min) / 100m; // 1%
        var expected = max - significance;

        // Act
        var actual = ModelFunc.GenerateNudgeValue(min, max, valueA, valueB);;

        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    public void GetNudgeHandler_WithDecPropertyAndSameAboveMax_ReturnsOneLessThanMax()
    {
        // Arrange
        var valueA = 12m;
        var valueB = 12m;
        var min = 2m;
        var max = 10m;
        var significance = (max - min) / 100m; // 1%
        var expected = max - significance;

        // Act
        var actual = ModelFunc.GenerateNudgeValue(min, max, valueA, valueB);;

        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    public void GetNudgeHandler_WithDecPropertyAndSameAtMin_ReturnsOneMoreThanMin()
    {
        // Arrange
        var valueA = 2m;
        var valueB = 2m;
        var min = 2m;
        var max = 10m;
        var significance = (max - min) / 100m; // 1%
        var expected = min + significance;

        // Act
        var actual = ModelFunc.GenerateNudgeValue(min, max, valueA, valueB);;

        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    public void GetNudgeHandler_WithDecPropertyAndSameInMiddle_ReturnsOneMoreOrOneLessThanA()
    {
        // Arrange
        var valueA = 6m;
        var valueB = 6m;
        var min = 2m;
        var max = 10m;
        var significance = (max - min) / 100m; // 1%
        var countLower = 0;
        var countHigher = 0;
        var expectedA = Math.Round(valueA + significance, 2);
        var expectedB = Math.Round(valueA - significance, 2);
        var numTests = 1000;
        var maxDiff = 1000 / 4;

        // Act
        for (int i = 0; i < numTests; i++)
        {
            var actual = Math.Round(ModelFunc.GenerateNudgeValue(min, max, valueA, valueB), 2);
            if(actual == expectedA) countLower++;
            else if(actual == expectedB) countHigher++;
            else Assert.True(false); // this ain't right
        }
        var actualDiff = Math.Abs(countLower - countHigher);

        // Assert
        Assert.True(actualDiff < maxDiff);
    }
    
    
    
    
    [Fact]
    public void GetNudgeHandler_WithDatePropertyAndSignificantDiff_ReturnsMiddleValue()
    {
        // Arrange
        var valueA = new LocalDateTime(2025, 3, 1, 0, 0);
        var valueB = new LocalDateTime(2026, 3, 1, 0, 0);
        var min = new LocalDateTime(2024, 3, 1, 0, 0);
        var max = new LocalDateTime(2027, 3, 1, 0, 0);
        var expected = new LocalDateTime(2025, 9, 1, 0, 0);

        // Act
        var actual = ModelFunc.GenerateNudgeValue(min, max, valueA, valueB);;

        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    public void GetNudgeHandler_WithDatePropertyAndSameAtMax_ReturnsOneLessThanMax()
    {
        // Arrange
        var valueA = new LocalDateTime(2027, 3, 1, 0, 0);
        var valueB = new LocalDateTime(2027, 3, 1, 0, 0);
        var min = new LocalDateTime(2024, 3, 1, 0, 0);
        var max = new LocalDateTime(2027, 3, 1, 0, 0);
        var expected = max.PlusMonths(-1);

        // Act
        var actual = ModelFunc.GenerateNudgeValue(min, max, valueA, valueB);;

        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    public void GetNudgeHandler_WithDatePropertyAndSameAboveMax_ReturnsOneLessThanMax()
    {
        // Arrange
        var valueA = new LocalDateTime(2029, 3, 1, 0, 0);
        var valueB = new LocalDateTime(2029, 3, 1, 0, 0);
        var min = new LocalDateTime(2024, 3, 1, 0, 0);
        var max = new LocalDateTime(2027, 3, 1, 0, 0);
        var expected = max.PlusMonths(-1);

        // Act
        var actual = ModelFunc.GenerateNudgeValue(min, max, valueA, valueB);;

        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    public void GetNudgeHandler_WithDatePropertyAndSameAtMin_ReturnsOneMoreThanMin()
    {
        // Arrange
        var valueA = new LocalDateTime(2024, 3, 1, 0, 0);
        var valueB = new LocalDateTime(2024, 3, 1, 0, 0);
        var min = new LocalDateTime(2024, 3, 1, 0, 0);
        var max = new LocalDateTime(2027, 3, 1, 0, 0);
        var expected = min.PlusMonths(1);

        // Act
        var actual = ModelFunc.GenerateNudgeValue(min, max, valueA, valueB);;

        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    public void GetNudgeHandler_WithDatePropertyAndSameInMiddle_ReturnsOneMoreOrOneLessThanA()
    {
        // Arrange
        var valueA = new LocalDateTime(2025, 3, 1, 0, 0);
        var valueB = new LocalDateTime(2025, 3, 1, 0, 0);
        var min = new LocalDateTime(2024, 3, 1, 0, 0);
        var max = new LocalDateTime(2027, 3, 1, 0, 0);
        var countLower = 0;
        var countHigher = 0;
        var expectedA = valueA.PlusMonths(1);
        var expectedB = valueA.PlusMonths(-1);
        var numTests = 1000;
        var maxDiff = 1000 / 4;

        // Act
        for (int i = 0; i < numTests; i++)
        {
            var actual = ModelFunc.GenerateNudgeValue(min, max, valueA, valueB);
            if(actual == expectedA) countLower++;
            else if(actual == expectedB) countHigher++;
            else Assert.True(false); // this ain't right
        }
        var actualDiff = Math.Abs(countLower - countHigher);

        // Assert
        Assert.True(actualDiff < maxDiff);
    }

    #endregion
}