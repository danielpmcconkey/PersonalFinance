using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public class Model
{
    #region utility functions

    
    /// <summary>
    /// A generic mating function for properties of all types.
    /// </summary>
    public static T MateProperty<T>(
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
    /// This is used to generically provide a random value between min and max by detecting whether the property is an
    /// int or a decimal. Uses the MateProperty function above to increase DRY compliance
    /// </summary>
    private static T MateNumericProperty<T>(
        McModel parentA,
        McModel parentB,
        Func<McModel, T> propertySelector,
        T minValue,
        T maxValue) where T : IComparable<T>
    {
        // Normalize bounds if they were provided in reverse order.
        if (Comparer<T>.Default.Compare(minValue, maxValue) > 0)
        {
            (minValue, maxValue) = (maxValue, minValue);
        }

        T candidate;
        switch (GetHereditarySource())
        {
            case HereditarySource.ParentA:
                candidate = propertySelector(parentA);
                break;
            case HereditarySource.ParentB:
                candidate = propertySelector(parentB);
                break;
            default:
                candidate = GenerateRandomBetween(minValue, maxValue);
                break;
        }

        // Ensure the resulting value always falls within [minValue, maxValue]. This is because we sometimes change the
        // min and max between training sessions and what was previously allowed might not be anymore.
        return ClampInclusive(candidate, minValue, maxValue);
    }
    
    /// <summary>
    /// Helper for generating a random value in [minValue, maxValue] for supported types.
    /// </summary>
    private static T GenerateRandomBetween<T>(T minValue, T maxValue) where T : IComparable<T>
    {
        // Normalize bounds if they were provided in reverse order.
        if (Comparer<T>.Default.Compare(minValue, maxValue) > 0)
        {
            (minValue, maxValue) = (maxValue, minValue);
        }

        if (typeof(T) == typeof(int))
        {
            var result = GetUnSeededRandomInt((int)(object)minValue, (int)(object)maxValue);
            return (T)(object)result;
        }

        if (typeof(T) == typeof(decimal))
        {
            var result = GetUnSeededRandomDecimal((decimal)(object)minValue, (decimal)(object)maxValue);
            return (T)(object)result;
        }

        if (typeof(T).FullName == "NodaTime.LocalDateTime")
        {
            var result = GetUnSeededRandomDate((NodaTime.LocalDateTime)(object)minValue, (NodaTime.LocalDateTime)(object)maxValue);
            return (T)(object)result;
        }

        throw new NotSupportedException($"Type {typeof(T)} is not supported for random generation in MateNumericProperty.");
    }
    /// <summary>
    ///  Generic inclusive clamp used for ints, decimals, and LocalDateTime (and any IComparable<T>).
    /// </summary>
    private static T ClampInclusive<T>(T value, T minValue, T maxValue) where T : IComparable<T>
    {
        // Normalize bounds if they were provided in reverse order.
        if (Comparer<T>.Default.Compare(minValue, maxValue) > 0)
        {
            (minValue, maxValue) = (maxValue, minValue);
        }

        if (Comparer<T>.Default.Compare(value, minValue) < 0) return minValue;
        if (Comparer<T>.Default.Compare(value, maxValue) > 0) return maxValue;
        return value;
    }


    
    public static HereditarySource GetHereditarySource()
    {
        var diceRoll = GetUnSeededRandomInt(1, 10);
        return diceRoll switch
        {
            1 or 2 or 3 or 4  => HereditarySource.ParentA,
            5 or 6 or 7 or 8 => HereditarySource.ParentB,
            _ => HereditarySource.Random
        };
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
            ParentAId = Guid.Empty,
            ParentBId = Guid.Empty,
            PersonId = Guid.NewGuid(),
            ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Now),
            SimStartDate = MonteCarloConfig.MonteCarloSimStartDate,
            SimEndDate = MonteCarloConfig.MonteCarloSimEndDate,
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
                ModelConstants.Percent401KTraditionalMin, ModelConstants.Percent401KTraditionalMax),
            LivinLargeRatio = GetUnSeededRandomDecimal(
                ModelConstants.LivinLargeRatioMin, ModelConstants.LivinLargeRatioMax),
            LivinLargeNetWorthTrigger = GetUnSeededRandomDecimal(
                ModelConstants.LivinLargeNetWorthTriggerMin, ModelConstants.LivinLargeNetWorthTriggerMax),
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
            LivinLargeRatio = MateLivinLargeRatio(a, b),
            LivinLargeNetWorthTrigger = MateLivinLargeNetWorthTrigger(a, b),
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
    
    public static decimal MateLivinLargeNetWorthTrigger(McModel a, McModel b) =>
        MateNumericProperty(
            a, b,
            model => model.LivinLargeNetWorthTrigger,
            ModelConstants.LivinLargeNetWorthTriggerMin,
            ModelConstants.LivinLargeNetWorthTriggerMax);
    
    public static decimal MateLivinLargeRatio(McModel a, McModel b) =>
        MateNumericProperty(
            a, b,
            model => model.LivinLargeRatio,
            ModelConstants.LivinLargeRatioMin,
            ModelConstants.LivinLargeRatioMax);
    
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
        MateNumericProperty(
            a, b,
            model => model.RetirementDate,
            birthDate
                    .PlusYears(ModelConstants.RetirementAgeMin.years)
                    .PlusMonths(ModelConstants.RetirementAgeMin.months),
            birthDate
                    .PlusYears(ModelConstants.RetirementAgeMax.years)
                    .PlusMonths(ModelConstants.RetirementAgeMax.months)
            );
    
    
    public static LocalDateTime MateSocialSecurityStartDate(McModel a, McModel b, LocalDateTime birthDate) =>
        MateNumericProperty(
            a, b,
            model => model.SocialSecurityStart,
            birthDate
                .PlusYears(ModelConstants.SocialSecurityElectionStartMin.years)
                .PlusMonths(ModelConstants.SocialSecurityElectionStartMin.months),
            birthDate
                .PlusYears(ModelConstants.SocialSecurityElectionStartMax.years)
                .PlusMonths(ModelConstants.SocialSecurityElectionStartMax.months)
            );
        

    #endregion
    
    
    
    

    

    

    
    

    
}