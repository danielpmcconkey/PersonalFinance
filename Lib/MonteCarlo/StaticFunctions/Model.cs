using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public class Model
{
    #region utility functions

    
    /// <summary>
    /// A generic mating function for properties of all types. This is here for DRY compliance
    /// </summary>
    private static T MateProperty<T>(
        McModel parentA,
        McModel parentB,
        Func<McModel, T> propertySelector,
        Func<T> randomValueGenerator)
    {
        var hereditarySource = GetHereditarySource();
        return hereditarySource switch
        {
            HereditarySource.ParentA => propertySelector(parentA),
            HereditarySource.ParentB => propertySelector(parentB),
            HereditarySource.Random => randomValueGenerator(),
            _ => throw new InvalidDataException("Invalid HereditarySource")
        };
    }
    /// <summary>
    /// This is used to generially provide a random value between min and max by detecting whether the property is an
    /// int or a decimal. Uses the MateProperty function above to increase DRY compliance
    /// </summary>
    private static T MateNumericProperty<T>(
        McModel parentA,
        McModel parentB,
        Func<McModel, T> propertySelector,
        T minValue,
        T maxValue) where T : struct
    {
        return MateProperty(
            parentA,
            parentB,
            propertySelector,
            () => typeof(T) == typeof(decimal) 
                ? (T)(object)GetUnSeededRandomDecimal((decimal)(object)minValue, (decimal)(object)maxValue)
                : (T)(object)GetUnSeededRandomInt((int)(object)minValue, (int)(object)maxValue)
        );
    }
    
    public static HereditarySource GetHereditarySource()
    {
        int diceRoll = GetUnSeededRandomInt(1, 10);
        switch (diceRoll)
        {
            case 1:
            case 2:
            case 3:
            case 4:
                return HereditarySource.ParentA;
            case 5:
            case 6:
            case 7:
            case 8:
                return HereditarySource.ParentB;
            case 9:
            case 10:
                return HereditarySource.Random;
            default:
                throw new NotImplementedException();
        }
    }
    
    public static int GetUnSeededRandomInt(int minInclusive, int maxInclusive)
    {
        var rand = new Random();
        return rand.Next(minInclusive, maxInclusive + 1);
    }
    public static decimal GetUnSeededRandomDecimal(decimal minInclusive, decimal maxInclusive)
    {
        var rand = new Random();
        return (decimal)rand.NextDouble() * (maxInclusive - minInclusive) + minInclusive;
    }
    public static LocalDateTime GetUnSeededRandomDate(LocalDateTime min, LocalDateTime max)
    {
        var span = max - min;
        var totalMonths = span.Months + (12 * span.Years);
        var addlMonthsOverMin = GetUnSeededRandomInt(0, totalMonths);
        var newDate = min.PlusMonths(addlMonthsOverMin);
        return newDate;
    }
    
    

    #endregion
    
    #region Model interface functions
    
    /*
     * these are the functions that other systems / classes should use. The other functions are public to facilitate
     * unit testing 
     */
    
    public static McModel CreateRandomModel(LocalDateTime birthdate)
    {
        return new McModel
        {
            Id = Guid.NewGuid(),
            ParentAId = Guid.NewGuid(),
            ParentBId = Guid.NewGuid(),
            PersonId = Guid.NewGuid(),
            ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Now),
            SimStartDate = LocalDateTime.FromDateTime(DateTime.Now),
            SimEndDate = LocalDateTime.FromDateTime(DateTime.Now.AddYears(30)),
            RetirementDate = Model.GetUnSeededRandomDate(
                birthdate
                    .PlusYears(ModelConstants.RetirementAgeMin.years)
                    .PlusMonths(ModelConstants.RetirementAgeMin.months), 
                birthdate
                    .PlusYears(ModelConstants.RetirementAgeMax.years)
                    .PlusMonths(ModelConstants.RetirementAgeMax.months)
                ),
            SocialSecurityStart = Model.GetUnSeededRandomDate(
                birthdate
                    .PlusYears(ModelConstants.SocialSecurityElectionStartMin.years)
                    .PlusMonths(ModelConstants.SocialSecurityElectionStartMin.months), 
                birthdate
                    .PlusYears(ModelConstants.SocialSecurityElectionStartMax.years)
                    .PlusMonths(ModelConstants.SocialSecurityElectionStartMax.months)
            ),
            AusterityRatio = Model.GetUnSeededRandomDecimal(
                ModelConstants.AusterityRatioMin, ModelConstants.AusterityRatioMax),
            ExtremeAusterityRatio = Model.GetUnSeededRandomDecimal(
                ModelConstants.ExtremeAusterityRatioMin, ModelConstants.ExtremeAusterityRatioMax),
            ExtremeAusterityNetWorthTrigger = Model.GetUnSeededRandomDecimal(
                ModelConstants.ExtremeAusterityNetWorthTriggerMin, ModelConstants.ExtremeAusterityNetWorthTriggerMax),
            RebalanceFrequency = GetUnSeededRandomInt(0, 3000) switch
            {
                < 1000 => RebalanceFrequency.MONTHLY,
                < 2000 => RebalanceFrequency.QUARTERLY,
                _ => RebalanceFrequency.YEARLY
            },
            NumMonthsCashOnHand = Model.GetUnSeededRandomInt(
                ModelConstants.NumMonthsCashOnHandMin, ModelConstants.NumMonthsCashOnHandMax),
            NumMonthsMidBucketOnHand = Model.GetUnSeededRandomInt(
                ModelConstants.NumMonthsMidBucketOnHandMin, ModelConstants.NumMonthsMidBucketOnHandMax),
            NumMonthsPriorToRetirementToBeginRebalance = Model.GetUnSeededRandomInt(
                ModelConstants.NumMonthsPriorToRetirementToBeginRebalanceMin, ModelConstants.NumMonthsPriorToRetirementToBeginRebalanceMax),
            RecessionCheckLookBackMonths = Model.GetUnSeededRandomInt(
                ModelConstants.RecessionCheckLookBackMonthsMin, ModelConstants.RecessionCheckLookBackMonthsMax),
            RecessionRecoveryPointModifier = Model.GetUnSeededRandomDecimal(
                ModelConstants.RecessionRecoveryPointModifierMin, ModelConstants.RecessionRecoveryPointModifierMax),
            DesiredMonthlySpendPreRetirement = Model.GetUnSeededRandomDecimal(
                ModelConstants.DesiredMonthlySpendPreRetirementMin, ModelConstants.DesiredMonthlySpendPreRetirementMax),
            DesiredMonthlySpendPostRetirement = Model.GetUnSeededRandomDecimal(
                ModelConstants.DesiredMonthlySpendPostRetirementMin, ModelConstants.DesiredMonthlySpendPostRetirementMax),
            Percent401KTraditional = Model.GetUnSeededRandomDecimal(
                ModelConstants.Percent401KTraditionalMin, ModelConstants.Percent401KTraditionalMax)
        };
    }
    
    public static McModel MateModels(McModel a, McModel b, LocalDateTime birthDate)
    {
        return new McModel()
        {
            Id = Guid.NewGuid(),
            PersonId = a.PersonId,
            ParentAId = a.Id,
            ParentBId = b.Id,
            ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Now),
            SimStartDate = a.SimStartDate,
            SimEndDate = a.SimEndDate,
            RetirementDate = MateRetirementDate(a, b, birthDate),
            SocialSecurityStart = MateSocialSecurityStartDate(a, b, birthDate),
            AusterityRatio = MateAusterityRatio(a, b),
            ExtremeAusterityRatio = MateExtremeAusterityRatio(a, b),
            ExtremeAusterityNetWorthTrigger = MateExtremeAusterityNetWorthTrigger(a, b),
            RebalanceFrequency = MateRebalanceFrequency(a, b),
            NumMonthsCashOnHand = MateNumMonthsCashOnHand(a, b),
            NumMonthsMidBucketOnHand = MateNumMonthsMidBucketOnHand(a, b),
            NumMonthsPriorToRetirementToBeginRebalance = MateNumMonthsPriorToRetirementToBeginRebalance(a, b),
            RecessionCheckLookBackMonths = MateRecessionCheckLookBackMonths(a, b),
            RecessionRecoveryPointModifier = MateRecessionRecoveryPointModifier(a, b),
            DesiredMonthlySpendPreRetirement = MateDesiredMonthlySpendPreRetirement(a, b),
            DesiredMonthlySpendPostRetirement = MateDesiredMonthlySpendPostRetirement(a, b),
            Percent401KTraditional = MatePercent401KTraditional(a, b),
        };
    }
    
    #endregion
    

    #region Individual Property Mating Functions

    public static decimal MateAusterityRatio(McModel a, McModel b) =>
        MateNumericProperty(
            a, b,
            model => model.AusterityRatio,
            ModelConstants.AusterityRatioMin,
            ModelConstants.AusterityRatioMax);
    
    public static decimal MateDesiredMonthlySpendPostRetirement(McModel a, McModel b) =>
        MateNumericProperty(
            a, b,
            model => model.DesiredMonthlySpendPostRetirement,
            ModelConstants.DesiredMonthlySpendPostRetirementMin,
            ModelConstants.DesiredMonthlySpendPostRetirementMax);
    
    public static decimal MateDesiredMonthlySpendPreRetirement(McModel a, McModel b) =>
        MateNumericProperty(
            a, b,
            model => model.DesiredMonthlySpendPreRetirement,
            ModelConstants.DesiredMonthlySpendPreRetirementMin,
            ModelConstants.DesiredMonthlySpendPreRetirementMax);
    
    public static decimal MateExtremeAusterityNetWorthTrigger(McModel a, McModel b) =>
        MateNumericProperty(
            a, b,
            model => model.ExtremeAusterityNetWorthTrigger,
            ModelConstants.ExtremeAusterityNetWorthTriggerMin,
            ModelConstants.ExtremeAusterityNetWorthTriggerMax);
    
    public static decimal MateExtremeAusterityRatio(McModel a, McModel b) =>
        MateNumericProperty(
            a, b,
            model => model.ExtremeAusterityRatio,
            ModelConstants.ExtremeAusterityRatioMin,
            ModelConstants.ExtremeAusterityRatioMax);
    
    public static int MateNumMonthsCashOnHand(McModel a, McModel b) =>
        MateNumericProperty(
            a, b,
            model => model.NumMonthsCashOnHand,
            ModelConstants.NumMonthsCashOnHandMin,
            ModelConstants.NumMonthsCashOnHandMax);
    
    public static int MateNumMonthsMidBucketOnHand(McModel a, McModel b) =>
        MateNumericProperty(
            a, b,
            model => model.NumMonthsMidBucketOnHand,
            ModelConstants.NumMonthsMidBucketOnHandMin,
            ModelConstants.NumMonthsMidBucketOnHandMax);
    
    public static int MateNumMonthsPriorToRetirementToBeginRebalance(McModel a, McModel b) =>
        MateNumericProperty(
            a, b,
            model => model.NumMonthsPriorToRetirementToBeginRebalance,
            ModelConstants.NumMonthsPriorToRetirementToBeginRebalanceMin,
            ModelConstants.NumMonthsPriorToRetirementToBeginRebalanceMax);
    
    public static decimal MatePercent401KTraditional(McModel a, McModel b) =>
        MateNumericProperty(
            a, b,
            model => model.Percent401KTraditional,
            ModelConstants.Percent401KTraditionalMin,
            ModelConstants.Percent401KTraditionalMax);
    
    public static RebalanceFrequency MateRebalanceFrequency(McModel a, McModel b) =>
        MateProperty(
            a, b,
            model => model.RebalanceFrequency,
            () =>
            {
                var randomInt = GetUnSeededRandomInt(0, 3000);
                return randomInt switch
                {
                    < 1000 => RebalanceFrequency.MONTHLY,
                    < 2000 => RebalanceFrequency.QUARTERLY,
                    _ => RebalanceFrequency.YEARLY
                };
            });
    
    public static int MateRecessionCheckLookBackMonths(McModel a, McModel b) =>
        MateNumericProperty(
            a, b,
            model => model.RecessionCheckLookBackMonths,
            ModelConstants.RecessionCheckLookBackMonthsMin,
            ModelConstants.RecessionCheckLookBackMonthsMax);

    public static decimal MateRecessionRecoveryPointModifier(McModel a, McModel b) =>
        MateNumericProperty(
            a, b,
            model => model.RecessionRecoveryPointModifier,
            ModelConstants.RecessionRecoveryPointModifierMin,
            ModelConstants.RecessionRecoveryPointModifierMax);
    
    public static LocalDateTime MateRetirementDate(McModel a, McModel b, LocalDateTime birthDate) =>
        MateProperty(
            a, b,
            model => model.RetirementDate,
            () =>
            {
                var min = birthDate
                    .PlusYears(ModelConstants.RetirementAgeMin.years)
                    .PlusMonths(ModelConstants.RetirementAgeMin.months);
                var max = birthDate
                    .PlusYears(ModelConstants.RetirementAgeMax.years)
                    .PlusMonths(ModelConstants.RetirementAgeMax.months);
                return GetUnSeededRandomDate(min, max);
            });
    
    public static LocalDateTime MateSocialSecurityStartDate(McModel a, McModel b, LocalDateTime birthDate) =>
        MateProperty(
            a, b,
            model => model.SocialSecurityStart,
            () =>
            {
                var min = birthDate
                    .PlusYears(ModelConstants.SocialSecurityElectionStartMin.years)
                    .PlusMonths(ModelConstants.SocialSecurityElectionStartMin.months);
                var max = birthDate
                    .PlusYears(ModelConstants.SocialSecurityElectionStartMax.years)
                    .PlusMonths(ModelConstants.SocialSecurityElectionStartMax.months);
                return GetUnSeededRandomDate(min, max);
            });

    #endregion
    
    
    
    

    

    

    
    

    
}