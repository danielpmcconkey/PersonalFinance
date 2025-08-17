
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
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
            if(Model.GetHereditarySource() == source) count++;
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
            var result = Model.GetUnSeededRandomDate(min, max);
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
            var result = Model.GetUnSeededRandomDecimal(min, max);
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
        var modelA = Model.CreateRandomModel(_birthdate);
        var modelB = Model.CreateRandomModel(_birthdate);

        // Act
        var result = Model.MateAusterityRatio(modelA, modelB);

        // Assert
        Assert.InRange(result, ModelConstants.AusterityRatioMin, ModelConstants.AusterityRatioMax);
    }

    [Fact]
    public void MateDesiredMonthlySpendPostRetirement_ReturnsValidResult()
    {
        // Arrange
        var modelA = Model.CreateRandomModel(_birthdate);
        var modelB = Model.CreateRandomModel(_birthdate);

        // Act
        var result = Model.MateDesiredMonthlySpendPostRetirement(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.DesiredMonthlySpendPostRetirementMin,
            ModelConstants.DesiredMonthlySpendPostRetirementMax);
    }

    [Fact]
    public void MateDesiredMonthlySpendPreRetirement_ReturnsValidResult()
    {
        // Arrange
        var modelA = Model.CreateRandomModel(_birthdate);
        var modelB = Model.CreateRandomModel(_birthdate);

        // Act
        var result = Model.MateDesiredMonthlySpendPreRetirement(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.DesiredMonthlySpendPreRetirementMin,
            ModelConstants.DesiredMonthlySpendPreRetirementMax);
    }

    [Fact]
    public void MateExtremeAusterityNetWorthTrigger_ReturnsValidResult()
    {
        // Arrange
        var modelA = Model.CreateRandomModel(_birthdate);
        var modelB = Model.CreateRandomModel(_birthdate);

        // Act
        var result = Model.MateExtremeAusterityNetWorthTrigger(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.ExtremeAusterityNetWorthTriggerMin, 
            ModelConstants.ExtremeAusterityNetWorthTriggerMax);
    }

    [Fact]
    public void MateExtremeAusterityRatio_ReturnsValidResult()
    {
        // Arrange
        var modelA = Model.CreateRandomModel(_birthdate);
        var modelB = Model.CreateRandomModel(_birthdate);

        // Act
        var result = Model.MateExtremeAusterityRatio(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.ExtremeAusterityRatioMin, ModelConstants.ExtremeAusterityRatioMax);
    }

    [Fact]
    public void MateNumMonthsCashOnHand_ReturnsValidResult()
    {
        // Arrange
        var modelA = Model.CreateRandomModel(_birthdate);
        var modelB = Model.CreateRandomModel(_birthdate);

        // Act
        var result = Model.MateNumMonthsCashOnHand(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.NumMonthsCashOnHandMin, 
            ModelConstants.NumMonthsCashOnHandMax);
    }

    [Fact]
    public void MateNumMonthsMidBucketOnHand_ReturnsValidResult()
    {
        // Arrange
        var modelA = Model.CreateRandomModel(_birthdate);
        var modelB = Model.CreateRandomModel(_birthdate);

        // Act
        var result = Model.MateNumMonthsMidBucketOnHand(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.NumMonthsMidBucketOnHandMin, 
            ModelConstants.NumMonthsMidBucketOnHandMax);
    }

    [Fact]
    public void MateNumMonthsPriorToRetirementToBeginRebalance_ReturnsValidResult()
    {
        // Arrange
        var modelA = Model.CreateRandomModel(_birthdate);
        var modelB = Model.CreateRandomModel(_birthdate);

        // Act
        var result = Model.MateNumMonthsPriorToRetirementToBeginRebalance(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.NumMonthsPriorToRetirementToBeginRebalanceMin, 
            ModelConstants.NumMonthsPriorToRetirementToBeginRebalanceMax);
    }

    [Fact]
    public void MatePercent401KTraditional_ReturnsValidResult()
    {
        // Arrange
        var modelA = Model.CreateRandomModel(_birthdate);
        var modelB = Model.CreateRandomModel(_birthdate);

        // Act
        var result = Model.MatePercent401KTraditional(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.Percent401KTraditionalMin,
            ModelConstants.Percent401KTraditionalMax);
    }

    [Fact]
    public void MateRebalanceFrequency_ReturnsValidResult()
    {
        // Arrange
        var modelA = Model.CreateRandomModel(_birthdate);
        var modelB = Model.CreateRandomModel(_birthdate);

        // Act
        var result = Model.MateRebalanceFrequency(modelA, modelB);

        // Assert
        Assert.True(Enum.IsDefined(typeof(RebalanceFrequency), result));
    }

    [Fact]
    public void MateRecessionCheckLookBackMonths_ReturnsValidResult()
    {
        // Arrange
        var modelA = Model.CreateRandomModel(_birthdate);
        var modelB = Model.CreateRandomModel(_birthdate);

        // Act
        var result = Model.MateRecessionCheckLookBackMonths(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.RecessionCheckLookBackMonthsMin,
            ModelConstants.RecessionCheckLookBackMonthsMax);
    }

    [Fact]
    public void MateRecessionRecoveryPointModifier_ReturnsValidResult()
    {
        // Arrange
        var modelA = Model.CreateRandomModel(_birthdate);
        var modelB = Model.CreateRandomModel(_birthdate);

        // Act
        var result = Model.MateRecessionRecoveryPointModifier(modelA, modelB);

        // Assert
        Assert.InRange(
            result, ModelConstants.RecessionRecoveryPointModifierMin,
            ModelConstants.RecessionRecoveryPointModifierMax);
    }

    [Fact]
    public void MateRetirementDate_ReturnsValidResult()
    {
        // Arrange
        var modelA = Model.CreateRandomModel(_birthdate);
        var modelB = Model.CreateRandomModel(_birthdate);
        var minDate = _birthdate
            .PlusYears(ModelConstants.RetirementAgeMin.years)
            .PlusMonths(ModelConstants.RetirementAgeMin.months);
        var maxDate = _birthdate
            .PlusYears(ModelConstants.RetirementAgeMax.years)
            .PlusMonths(ModelConstants.RetirementAgeMax.months);

        // Act
        var result = Model.MateRetirementDate(modelA, modelB, _birthdate);

        // Assert
        Assert.InRange(result, minDate, maxDate);
    }

    [Fact]
    public void MateSocialSecurityStartDate_ReturnsValidResult()
    {
        // Arrange
        var modelA = Model.CreateRandomModel(_birthdate);
        var modelB = Model.CreateRandomModel(_birthdate);
        var minDate = _birthdate
            .PlusYears(ModelConstants.SocialSecurityElectionStartMin.years)
            .PlusMonths(ModelConstants.SocialSecurityElectionStartMin.months);
        var maxDate = _birthdate
            .PlusYears(ModelConstants.SocialSecurityElectionStartMax.years)
            .PlusMonths(ModelConstants.SocialSecurityElectionStartMax.months);

        // Act
        var result = Model.MateSocialSecurityStartDate(modelA, modelB, _birthdate);

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
            
            var modelA = Model.CreateRandomModel(_birthdate);
            var modelB = Model.CreateRandomModel(_birthdate);
            modelA.SocialSecurityStart = minDate.PlusMonths(-1);
            modelB.SocialSecurityStart = maxDate.PlusMonths(1);
            modelA.DesiredMonthlySpendPostRetirement = minSpend - 1;
            modelB.DesiredMonthlySpendPostRetirement = maxSpend + 1;

            // Act
            var dateResult = Model.MateSocialSecurityStartDate(modelA, modelB, _birthdate);
            var spendResult = Model.MateDesiredMonthlySpendPostRetirement(modelA, modelB);

            // Assert
            Assert.InRange(dateResult, minDate, maxDate);
            Assert.InRange(spendResult, minSpend, maxSpend);
        }
    }

    #endregion
}