using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.WithdrawalStrategy;
using Lib.StaticConfig;
using Lib.Utils;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public class Model
{
    #region utility functions

    
    /// <summary>
    /// A generic mating function for properties of all types.
    /// </summary>
    public static T MateProperty<T>(
        DataTypes.MonteCarlo.Model parentA,
        DataTypes.MonteCarlo.Model parentB,
        Func<DataTypes.MonteCarlo.Model, T> propertySelector,
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
        DataTypes.MonteCarlo.Model parentA,
        DataTypes.MonteCarlo.Model parentB,
        Func<DataTypes.MonteCarlo.Model, T> propertySelector,
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
            case HereditarySource.Random:
                if(MonteCarloConfig.IsNudgeModeOn) candidate = GenerateNudgeValue(
                    minValue, maxValue, propertySelector(parentA), propertySelector(parentB));
                else candidate = MathFunc.GenerateRandomBetween(minValue, maxValue);
                break;
            default:
                throw new InvalidDataException("Invalid HereditarySource");
        }

        // Ensure the resulting value always falls within [minValue, maxValue]. This is because we sometimes change the
        // min and max between training sessions and what was previously allowed might not be anymore.
        return MathFunc.ClampInclusive(candidate, minValue, maxValue);
    }
    
    
    /// <summary>
    /// this method was written by Claude. It returns a nudge handler of type T to help make the code for nudging values
    /// during model mating more DRY compliant
    /// </summary>
    private static INudgeHandler<T> GetNudgeHandler<T>() where T : IComparable<T>
    {
        if (typeof(T) == typeof(int))
            return (INudgeHandler<T>)new IntNudgeHandler();
        if (typeof(T) == typeof(decimal))
            return (INudgeHandler<T>)new DecimalNudgeHandler();
        if (typeof(T).FullName == "NodaTime.LocalDateTime")
            return (INudgeHandler<T>)new LocalDateTimeNudgeHandler();

        throw new NotSupportedException($"Type {typeof(T)} is not supported for nudge generation.");
    }


    /// <summary>
    /// Claude wrote this method. It returns a "nudged" value based on type. I used Claude to help make the
    /// code for nudging values during model mating more DRY compliant. Generally, the returned value should be half-way
    /// between ParentA's and ParentB's. If they're the same, then move 1 significant unit either up or down. For int
    /// values, that significant unit is 1. For decimals, it's 1% of the distance between min and max. For LocalDateTime
    /// values, it's 1 month.
    /// </summary>
    public static T GenerateNudgeValue<T>(T minValue, T maxValue, T parentAValue, T parentBValue)
        where T : IComparable<T>
    {
        var handler = GetNudgeHandler<T>();
        return handler.GenerateNudge(minValue, maxValue, parentAValue, parentBValue);
    }

    

    
    public static HereditarySource GetHereditarySource()
    {
        var diceRoll = MathFunc.GetUnSeededRandomInt(1, 10);
        return diceRoll switch
        {
            1 or 2 or 3 or 4  => HereditarySource.ParentA,
            5 or 6 or 7 or 8 => HereditarySource.ParentB,
            _ => HereditarySource.Random
        };
    }
    
    

    #endregion
    
    #region Model interface functions
    
    /*
     * these are the functions that other systems / classes should use. The other functions are public to facilitate
     * unit testing 
     */
    
    public static DataTypes.MonteCarlo.Model CreateRandomModel(LocalDateTime birthdate)
    {
        return new DataTypes.MonteCarlo.Model(){
            Id = Guid.NewGuid(),
            PersonId = Guid.Empty,
            ParentAId = Guid.Empty,
            ParentBId = Guid.Empty,
            ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Now),
            SimStartDate = MonteCarloConfig.MonteCarloSimStartDate,
            SimEndDate = MonteCarloConfig.MonteCarloSimEndDate,
            RetirementDate = MathFunc.GetUnSeededRandomDate( 
                birthdate
                    .PlusYears(ModelConstants.RetirementAgeMin.years)
                    .PlusMonths(ModelConstants.RetirementAgeMin.months),
                birthdate
                    .PlusYears(ModelConstants.RetirementAgeMax.years)
                    .PlusMonths(ModelConstants.RetirementAgeMax.months)
            ),
            SocialSecurityStart = MathFunc.GetUnSeededRandomDate(
                birthdate
                    .PlusYears(ModelConstants.SocialSecurityElectionStartMin.years)
                    .PlusMonths(ModelConstants.SocialSecurityElectionStartMin.months),
                birthdate
                    .PlusYears(ModelConstants.SocialSecurityElectionStartMax.years)
                    .PlusMonths(ModelConstants.SocialSecurityElectionStartMax.months)
            ),
            AusterityRatio = MathFunc.GetUnSeededRandomDecimal(
                ModelConstants.AusterityRatioMin, ModelConstants.AusterityRatioMax),
            ExtremeAusterityRatio = MathFunc.GetUnSeededRandomDecimal(
                ModelConstants.ExtremeAusterityRatioMin, ModelConstants.ExtremeAusterityRatioMax),
            ExtremeAusterityNetWorthTrigger = MathFunc.GetUnSeededRandomDecimal(
                ModelConstants.ExtremeAusterityNetWorthTriggerMin, ModelConstants.ExtremeAusterityNetWorthTriggerMax),
            LivinLargeRatio = MathFunc.GetUnSeededRandomDecimal(
                ModelConstants.LivinLargeRatioMin, ModelConstants.LivinLargeRatioMax),
            LivinLargeNetWorthTrigger = MathFunc.GetUnSeededRandomDecimal(
                ModelConstants.LivinLargeNetWorthTriggerMin, ModelConstants.LivinLargeNetWorthTriggerMax),
            RebalanceFrequency = GetRandomRebalanceFrequency(),
            NumMonthsCashOnHand = MathFunc.GetUnSeededRandomInt(
                ModelConstants.NumMonthsCashOnHandMin, ModelConstants.NumMonthsCashOnHandMax),
            NumMonthsMidBucketOnHand = MathFunc.GetUnSeededRandomInt(
                ModelConstants.NumMonthsMidBucketOnHandMin, ModelConstants.NumMonthsMidBucketOnHandMax),
            NumMonthsPriorToRetirementToBeginRebalance = MathFunc.GetUnSeededRandomInt(
                ModelConstants.NumMonthsPriorToRetirementToBeginRebalanceMin, 
                ModelConstants.NumMonthsPriorToRetirementToBeginRebalanceMax),
            RecessionCheckLookBackMonths = MathFunc.GetUnSeededRandomInt(
                ModelConstants.RecessionCheckLookBackMonthsMin, ModelConstants.RecessionCheckLookBackMonthsMax),
            RecessionRecoveryPointModifier = MathFunc.GetUnSeededRandomDecimal(
                ModelConstants.RecessionRecoveryPointModifierMin, ModelConstants.RecessionRecoveryPointModifierMax),
            DesiredMonthlySpendPreRetirement = MathFunc.GetUnSeededRandomDecimal(
                ModelConstants.DesiredMonthlySpendPreRetirementMin, ModelConstants.DesiredMonthlySpendPreRetirementMax),
            DesiredMonthlySpendPostRetirement = MathFunc.GetUnSeededRandomDecimal(
                ModelConstants.DesiredMonthlySpendPostRetirementMin, 
                ModelConstants.DesiredMonthlySpendPostRetirementMax),
            Percent401KTraditional = MathFunc.GetUnSeededRandomDecimal(
                ModelConstants.Percent401KTraditionalMin, ModelConstants.Percent401KTraditionalMax),
            Generation = 1,
            WithdrawalStrategyType = WithdrawalStrategyType.BasicBuckets // todo: randomize WithdrawalStrategyType
        };
    }
    
    public static DataTypes.MonteCarlo.Model FetchModelChampion()
    {
        
        using var context = new PgContext();
        var champ = context.McModels
                        .FirstOrDefault(x => x.Id == Guid.Parse(MonteCarloConfig.ChampionModelId)) ??
                    throw new InvalidDataException();
        return champ;
    }
    
    public static DataTypes.MonteCarlo.Model MateModels(DataTypes.MonteCarlo.Model a, DataTypes.MonteCarlo.Model b, LocalDateTime birthDate)
    {
        var newGuid = Guid.NewGuid();
        return new DataTypes.MonteCarlo.Model(){
        
            Id = Guid.NewGuid(),
            PersonId = a.PersonId,
            ParentAId = a.Id,
            ParentA = a,
            ChildrenA = a.ChildrenA,
            ParentBId = b.Id,
            ParentB = b,
            ChildrenB = b.ChildrenB,
            ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Now),
            SimStartDate = a.SimStartDate,
            SimEndDate = a.SimEndDate,
            RetirementDate = MateRetirementDate(a, b, birthDate),
            SocialSecurityStart = MateSocialSecurityStartDate(a, b, birthDate),
            AusterityRatio = MateAusterityRatio(a, b),
            ExtremeAusterityRatio = MateExtremeAusterityRatio(a, b),
            ExtremeAusterityNetWorthTrigger = MateExtremeAusterityNetWorthTrigger(a, b),
            LivinLargeRatio = MateLivinLargeRatio(a, b),
            LivinLargeNetWorthTrigger = MateLivinLargeNetWorthTrigger(a, b),
            RebalanceFrequency = MateRebalanceFrequency(a, b),
            NumMonthsCashOnHand = MateNumMonthsCashOnHand(a, b),
            NumMonthsMidBucketOnHand = MateNumMonthsMidBucketOnHand(a, b),
            NumMonthsPriorToRetirementToBeginRebalance = MateNumMonthsPriorToRetirementToBeginRebalance(a, b),
            RecessionCheckLookBackMonths = MateRecessionCheckLookBackMonths(a, b),
            RecessionRecoveryPointModifier = MateRecessionRecoveryPointModifier(a, b),
            DesiredMonthlySpendPreRetirement = MateDesiredMonthlySpendPreRetirement(a, b),
            DesiredMonthlySpendPostRetirement = MateDesiredMonthlySpendPostRetirement(a, b),
            Percent401KTraditional = MatePercent401KTraditional(a, b),
            Generation = Math.Max(a.Generation, b.Generation) + 1,
            WithdrawalStrategyType = MateWithdrawalStrategyType(a, b)
        };
    }
    
    #endregion
    

    #region Individual Property Mating Functions

    public static decimal MateAusterityRatio(DataTypes.MonteCarlo.Model a, DataTypes.MonteCarlo.Model b) =>
        MateNumericProperty(
            a, b,
            model => model.AusterityRatio,
            ModelConstants.AusterityRatioMin,
            ModelConstants.AusterityRatioMax);
    
    public static decimal MateDesiredMonthlySpendPostRetirement(DataTypes.MonteCarlo.Model a, DataTypes.MonteCarlo.Model b) =>
        MateNumericProperty(
            a, b,
            model => model.DesiredMonthlySpendPostRetirement,
            ModelConstants.DesiredMonthlySpendPostRetirementMin,
            ModelConstants.DesiredMonthlySpendPostRetirementMax);
    
    public static decimal MateDesiredMonthlySpendPreRetirement(DataTypes.MonteCarlo.Model a, DataTypes.MonteCarlo.Model b) =>
        MateNumericProperty(
            a, b,
            model => model.DesiredMonthlySpendPreRetirement,
            ModelConstants.DesiredMonthlySpendPreRetirementMin,
            ModelConstants.DesiredMonthlySpendPreRetirementMax);
    
    public static decimal MateExtremeAusterityNetWorthTrigger(DataTypes.MonteCarlo.Model a, DataTypes.MonteCarlo.Model b) =>
        MateNumericProperty(
            a, b,
            model => model.ExtremeAusterityNetWorthTrigger,
            ModelConstants.ExtremeAusterityNetWorthTriggerMin,
            ModelConstants.ExtremeAusterityNetWorthTriggerMax);
    
    public static decimal MateExtremeAusterityRatio(DataTypes.MonteCarlo.Model a, DataTypes.MonteCarlo.Model b) =>
        MateNumericProperty(
            a, b,
            model => model.ExtremeAusterityRatio,
            ModelConstants.ExtremeAusterityRatioMin,
            ModelConstants.ExtremeAusterityRatioMax);
    
    public static decimal MateLivinLargeNetWorthTrigger(DataTypes.MonteCarlo.Model a, DataTypes.MonteCarlo.Model b) =>
        MateNumericProperty(
            a, b,
            model => model.LivinLargeNetWorthTrigger,
            ModelConstants.LivinLargeNetWorthTriggerMin,
            ModelConstants.LivinLargeNetWorthTriggerMax);
    
    public static decimal MateLivinLargeRatio(DataTypes.MonteCarlo.Model a, DataTypes.MonteCarlo.Model b) =>
        MateNumericProperty(
            a, b,
            model => model.LivinLargeRatio,
            ModelConstants.LivinLargeRatioMin,
            ModelConstants.LivinLargeRatioMax);
    
    public static int MateNumMonthsCashOnHand(DataTypes.MonteCarlo.Model a, DataTypes.MonteCarlo.Model b) =>
        MateNumericProperty(
            a, b,
            model => model.NumMonthsCashOnHand,
            ModelConstants.NumMonthsCashOnHandMin,
            ModelConstants.NumMonthsCashOnHandMax);
    
    public static int MateNumMonthsMidBucketOnHand(DataTypes.MonteCarlo.Model a, DataTypes.MonteCarlo.Model b) =>
        MateNumericProperty(
            a, b,
            model => model.NumMonthsMidBucketOnHand,
            ModelConstants.NumMonthsMidBucketOnHandMin,
            ModelConstants.NumMonthsMidBucketOnHandMax);
    
    public static int MateNumMonthsPriorToRetirementToBeginRebalance(DataTypes.MonteCarlo.Model a, DataTypes.MonteCarlo.Model b) =>
        MateNumericProperty(
            a, b,
            model => model.NumMonthsPriorToRetirementToBeginRebalance,
            ModelConstants.NumMonthsPriorToRetirementToBeginRebalanceMin,
            ModelConstants.NumMonthsPriorToRetirementToBeginRebalanceMax);
    
    public static decimal MatePercent401KTraditional(DataTypes.MonteCarlo.Model a, DataTypes.MonteCarlo.Model b) =>
        MateNumericProperty(
            a, b,
            model => model.Percent401KTraditional,
            ModelConstants.Percent401KTraditionalMin,
            ModelConstants.Percent401KTraditionalMax);
    
    public static RebalanceFrequency MateRebalanceFrequency(DataTypes.MonteCarlo.Model a, DataTypes.MonteCarlo.Model b) =>
        MateProperty(
            a, b,
            model => model.RebalanceFrequency,
            () =>
            {
                var randomInt = MathFunc.GetUnSeededRandomInt(0, 3000);
                return randomInt switch
                {
                    < 1000 => RebalanceFrequency.MONTHLY,
                    < 2000 => RebalanceFrequency.QUARTERLY,
                    _ => RebalanceFrequency.YEARLY
                };
            });
    
    public static int MateRecessionCheckLookBackMonths(DataTypes.MonteCarlo.Model a, DataTypes.MonteCarlo.Model b) =>
        MateNumericProperty(
            a, b,
            model => model.RecessionCheckLookBackMonths,
            ModelConstants.RecessionCheckLookBackMonthsMin,
            ModelConstants.RecessionCheckLookBackMonthsMax);

    public static decimal MateRecessionRecoveryPointModifier(DataTypes.MonteCarlo.Model a, DataTypes.MonteCarlo.Model b) =>
        MateNumericProperty(
            a, b,
            model => model.RecessionRecoveryPointModifier,
            ModelConstants.RecessionRecoveryPointModifierMin,
            ModelConstants.RecessionRecoveryPointModifierMax);
    
    public static LocalDateTime MateRetirementDate(DataTypes.MonteCarlo.Model a, DataTypes.MonteCarlo.Model b, LocalDateTime birthDate) =>
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
    
    
    public static LocalDateTime MateSocialSecurityStartDate(DataTypes.MonteCarlo.Model a, DataTypes.MonteCarlo.Model b, LocalDateTime birthDate) =>
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
    public static WithdrawalStrategyType MateWithdrawalStrategyType(DataTypes.MonteCarlo.Model a, DataTypes.MonteCarlo.Model b) =>
        MateProperty(
            a, b,
            model => model.WithdrawalStrategyType,
            () =>
            {
                // todo: implement MateWithdrawalStrategyType
                var randomInt = MathFunc.GetUnSeededRandomInt(0, 3000);
                return randomInt switch
                {
                    < 1000 => WithdrawalStrategyType.BasicBuckets,
                    < 2000 => WithdrawalStrategyType.BasicBuckets,
                    _ => WithdrawalStrategyType.BasicBuckets
                };
            });
        

    #endregion


    private static  RebalanceFrequency GetRandomRebalanceFrequency()
    {
        return MathFunc.GetUnSeededRandomInt(0, 3000) switch
        {
            < 1000 => RebalanceFrequency.MONTHLY,
            < 2000 => RebalanceFrequency.QUARTERLY,
            _ => RebalanceFrequency.YEARLY
        };
    }
    
    
    
    

    

    

    
    

    
}