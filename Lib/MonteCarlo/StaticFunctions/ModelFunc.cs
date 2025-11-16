using Lib.DataTypes;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.WithdrawalStrategy;
using Lib.StaticConfig;
using Lib.Utils;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public class ModelFunc
{
    #region utility functions

    
    /// <summary>
    /// A generic mating function for properties of all types.
    /// </summary>
    public static T MateProperty<T>(
        Model parentA,
        Model parentB,
        Func<Model, T> propertySelector,
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
        Model parentA,
        Model parentB,
        Func<Model, T> propertySelector,
        T minValue,
        T maxValue,
        bool nudgeMode
    ) where T : IComparable<T>
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
                if(nudgeMode) candidate = GenerateNudgeValue(
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
    
    public static Model CreateRandomModel(LocalDateTime birthdate, int clade)
    {
        return new Model(){
            Id = Guid.NewGuid(),
            Clade = clade,
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
            WithdrawalStrategyType = GetRandomWithdrawalStrategyType(),
            SixtyFortyLong = MathFunc.GetUnSeededRandomDecimal(
                ModelConstants.SixtyFortyLongMin, ModelConstants.SixtyFortyLongMax),
        };
    }
    
    public static Model FetchModelChampion(string? championOverride = null)
    {
        var champId = championOverride ?? MonteCarloConfig.ChampionModelId;
        using var context = new PgContext();
        var champ = context.McModels
                        .FirstOrDefault(x => x.Id == Guid.Parse(champId)) ??
                    throw new InvalidDataException($"Champion with ID {champId} doesn't exist.");
        return champ;
    }
    public static Model FetchModelPlusParentsPlusChildrenById(Guid id)
    {
        using var context = new PgContext();
        var champ = context.McModels
                        .Include(x => x.ParentA)
                        .Include(x => x.ParentB)
                        .Include(x => x.ChildrenA)
                        .Include(x => x.ChildrenB)
                        .FirstOrDefault(x => x.Id == id) ??
            
                    throw new InvalidDataException($"Champion with ID {id} doesn't exist.");
        return champ;
    }
    
    public static SingleModelRunResult? FetchMostRecentRunResultForModel(Model m)
    {
        using var context = new PgContext();
        return context.SingleModelRunResults
            .Where(x => x.ModelId == m.Id)
            .OrderByDescending(x => x.RunDate)
            .FirstOrDefault();
    }
    
    public static Model MateModels(Model a, Model b, LocalDateTime birthDate)
    {
        // When in nudgeMode, the model breeder will be way less drastic in its randomization. This used to be a global
        // config param, but I want to have it randomly set so we get a good blend of radical and conservative mutation
        var nudgeMode = (MathFunc.FlipACoin() == CoinFlip.Heads) ? true : false;
        var newGuid = Guid.NewGuid();
        return new Model(){
        
            Id = Guid.NewGuid(),
            Clade = a.Clade,
            PersonId = a.PersonId,
            ParentAId = a.Id,
            //ParentA = a,
            //ChildrenA = a.ChildrenA,
            ParentBId = b.Id,
            //ParentB = b,
            //ChildrenB = b.ChildrenB,
            ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Now),
            SimStartDate = a.SimStartDate,
            SimEndDate = a.SimEndDate,
            RetirementDate = MateRetirementDate(a, b, birthDate, nudgeMode),
            SocialSecurityStart = MateSocialSecurityStartDate(a, b, birthDate, nudgeMode),
            AusterityRatio = MateAusterityRatio(a, b, nudgeMode),
            ExtremeAusterityRatio = MateExtremeAusterityRatio(a, b, nudgeMode),
            ExtremeAusterityNetWorthTrigger = MateExtremeAusterityNetWorthTrigger(a, b, nudgeMode),
            LivinLargeRatio = MateLivinLargeRatio(a, b, nudgeMode),
            LivinLargeNetWorthTrigger = MateLivinLargeNetWorthTrigger(a, b, nudgeMode),
            RebalanceFrequency = MateRebalanceFrequency(a, b),
            NumMonthsCashOnHand = MateNumMonthsCashOnHand(a, b, nudgeMode),
            NumMonthsMidBucketOnHand = MateNumMonthsMidBucketOnHand(a, b, nudgeMode),
            NumMonthsPriorToRetirementToBeginRebalance = MateNumMonthsPriorToRetirementToBeginRebalance(a, b, nudgeMode),
            RecessionCheckLookBackMonths = MateRecessionCheckLookBackMonths(a, b, nudgeMode),
            RecessionRecoveryPointModifier = MateRecessionRecoveryPointModifier(a, b, nudgeMode),
            DesiredMonthlySpendPreRetirement = MateDesiredMonthlySpendPreRetirement(a, b, nudgeMode),
            DesiredMonthlySpendPostRetirement = MateDesiredMonthlySpendPostRetirement(a, b, nudgeMode),
            Percent401KTraditional = MatePercent401KTraditional(a, b, nudgeMode),
            Generation = Math.Max(a.Generation, b.Generation) + 1,
            WithdrawalStrategyType = MateWithdrawalStrategyType(a, b),
            SixtyFortyLong = MateSixtyFortyLong(a, b, nudgeMode),
        };
    }
    
    #endregion
    

    #region Individual Property Mating Functions

    public static decimal MateAusterityRatio(Model a, Model b, bool nudgeMode) =>
        MateNumericProperty(
            a, b,
            model => model.AusterityRatio,
            ModelConstants.AusterityRatioMin,
            ModelConstants.AusterityRatioMax,
            nudgeMode);
    
    public static decimal MateDesiredMonthlySpendPostRetirement(Model a, Model b, bool nudgeMode) =>
        MateNumericProperty(
            a, b,
            model => model.DesiredMonthlySpendPostRetirement,
            ModelConstants.DesiredMonthlySpendPostRetirementMin,
            ModelConstants.DesiredMonthlySpendPostRetirementMax,
            nudgeMode);
    
    public static decimal MateDesiredMonthlySpendPreRetirement(Model a, Model b, bool nudgeMode) =>
        MateNumericProperty(
            a, b,
            model => model.DesiredMonthlySpendPreRetirement,
            ModelConstants.DesiredMonthlySpendPreRetirementMin,
            ModelConstants.DesiredMonthlySpendPreRetirementMax,
            nudgeMode);
    
    public static decimal MateExtremeAusterityNetWorthTrigger(Model a, Model b, bool nudgeMode) =>
        MateNumericProperty(
            a, b,
            model => model.ExtremeAusterityNetWorthTrigger,
            ModelConstants.ExtremeAusterityNetWorthTriggerMin,
            ModelConstants.ExtremeAusterityNetWorthTriggerMax,
            nudgeMode);
    
    public static decimal MateExtremeAusterityRatio(Model a, Model b, bool nudgeMode) =>
        MateNumericProperty(
            a, b,
            model => model.ExtremeAusterityRatio,
            ModelConstants.ExtremeAusterityRatioMin,
            ModelConstants.ExtremeAusterityRatioMax,
            nudgeMode);
    
    public static decimal MateLivinLargeNetWorthTrigger(Model a, Model b, bool nudgeMode) =>
        MateNumericProperty(
            a, b,
            model => model.LivinLargeNetWorthTrigger,
            ModelConstants.LivinLargeNetWorthTriggerMin,
            ModelConstants.LivinLargeNetWorthTriggerMax,
            nudgeMode);
    
    public static decimal MateLivinLargeRatio(Model a, Model b, bool nudgeMode) =>
        MateNumericProperty(
            a, b,
            model => model.LivinLargeRatio,
            ModelConstants.LivinLargeRatioMin,
            ModelConstants.LivinLargeRatioMax,
            nudgeMode);
    
    public static int MateNumMonthsCashOnHand(Model a, Model b, bool nudgeMode) =>
        MateNumericProperty(
            a, b,
            model => model.NumMonthsCashOnHand,
            ModelConstants.NumMonthsCashOnHandMin,
            ModelConstants.NumMonthsCashOnHandMax,
            nudgeMode);
    
    public static int MateNumMonthsMidBucketOnHand(Model a, Model b, bool nudgeMode) =>
        MateNumericProperty(
            a, b,
            model => model.NumMonthsMidBucketOnHand,
            ModelConstants.NumMonthsMidBucketOnHandMin,
            ModelConstants.NumMonthsMidBucketOnHandMax,
            nudgeMode);
    
    public static int MateNumMonthsPriorToRetirementToBeginRebalance(Model a, Model b, bool nudgeMode) =>
        MateNumericProperty(
            a, b,
            model => model.NumMonthsPriorToRetirementToBeginRebalance,
            ModelConstants.NumMonthsPriorToRetirementToBeginRebalanceMin,
            ModelConstants.NumMonthsPriorToRetirementToBeginRebalanceMax,
            nudgeMode);
    
    public static decimal MatePercent401KTraditional(Model a, Model b, bool nudgeMode) =>
        MateNumericProperty(
            a, b,
            model => model.Percent401KTraditional,
            ModelConstants.Percent401KTraditionalMin,
            ModelConstants.Percent401KTraditionalMax,
            nudgeMode);
    
    public static RebalanceFrequency MateRebalanceFrequency(Model a, Model b) =>
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
    
    public static int MateRecessionCheckLookBackMonths(Model a, Model b, bool nudgeMode) =>
        MateNumericProperty(
            a, b,
            model => model.RecessionCheckLookBackMonths,
            ModelConstants.RecessionCheckLookBackMonthsMin,
            ModelConstants.RecessionCheckLookBackMonthsMax,
            nudgeMode);

    public static decimal MateRecessionRecoveryPointModifier(Model a, Model b, bool nudgeMode) =>
        MateNumericProperty(
            a, b,
            model => model.RecessionRecoveryPointModifier,
            ModelConstants.RecessionRecoveryPointModifierMin,
            ModelConstants.RecessionRecoveryPointModifierMax,
            nudgeMode);
    
    public static LocalDateTime MateRetirementDate(Model a, Model b, LocalDateTime birthDate, bool nudgeMode) =>
        MateNumericProperty(
            a, b,
            model => model.RetirementDate,
            birthDate
                    .PlusYears(ModelConstants.RetirementAgeMin.years)
                    .PlusMonths(ModelConstants.RetirementAgeMin.months),
            birthDate
                    .PlusYears(ModelConstants.RetirementAgeMax.years)
                    .PlusMonths(ModelConstants.RetirementAgeMax.months),
            nudgeMode
            );
    
    public static decimal MateSixtyFortyLong(Model a, Model b, bool nudgeMode) =>
        MateNumericProperty(
            a, b,
            model => model.SixtyFortyLong,
            ModelConstants.SixtyFortyLongMin,
            ModelConstants.SixtyFortyLongMax,
            nudgeMode);
    
    public static LocalDateTime MateSocialSecurityStartDate(Model a, Model b, LocalDateTime birthDate, bool nudgeMode) =>
        MateNumericProperty(
            a, b,
            model => model.SocialSecurityStart,
            birthDate
                .PlusYears(ModelConstants.SocialSecurityElectionStartMin.years)
                .PlusMonths(ModelConstants.SocialSecurityElectionStartMin.months),
            birthDate
                .PlusYears(ModelConstants.SocialSecurityElectionStartMax.years)
                .PlusMonths(ModelConstants.SocialSecurityElectionStartMax.months),
            nudgeMode
            );
    public static WithdrawalStrategyType MateWithdrawalStrategyType(Model a, Model b) =>
        MateProperty(
            a, b,
            model => model.WithdrawalStrategyType,
            () =>
            {
                var randomInt = MathFunc.GetUnSeededRandomInt(0, 4000);
                return randomInt switch
                {
                    < 1000 => WithdrawalStrategyType.BasicBucketsIncomeThreshold,
                    < 2000 => WithdrawalStrategyType.BasicBucketsTaxableFirst,
                    < 3000 => WithdrawalStrategyType.SixtyForty,
                    _ => WithdrawalStrategyType.NoMidIncomeThreshold
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
    
    private static  WithdrawalStrategyType GetRandomWithdrawalStrategyType()
    {
        var randomInt = MathFunc.GetUnSeededRandomInt(0, 4000);
        return randomInt switch
        {
            < 1000 => WithdrawalStrategyType.BasicBucketsIncomeThreshold,
            < 2000 => WithdrawalStrategyType.BasicBucketsTaxableFirst,
            < 3000 => WithdrawalStrategyType.SixtyForty,
            _ => WithdrawalStrategyType.NoMidIncomeThreshold
        };
    }
    
    
    
    

    

    

    
    

    
}