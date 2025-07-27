using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public class Model
{
    
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
    
    
    public static LocalDateTime MateRetirementDate(McModel a, McModel b, LocalDateTime birthDate)
    {
        var hereditarySource = GetHereditarySource();
        switch (hereditarySource)
        {
            case HereditarySource.ParentA:
                return a.RetirementDate;
            case HereditarySource.ParentB:
                return b.RetirementDate;
            case HereditarySource.Random:
                var min = birthDate
                    .PlusYears(ModelConstants.RetirementAgeMin.years)
                    .PlusMonths(ModelConstants.RetirementAgeMin.months);
                var max = birthDate
                    .PlusYears(ModelConstants.RetirementAgeMax.years)
                    .PlusMonths(ModelConstants.RetirementAgeMax.months);
                return GetUnSeededRandomDate(min, max);
            default:
                throw new InvalidDataException("Invalid HereditarySource");
        }
    }
    
    public static LocalDateTime MateSocialSecurityStartDate(McModel a, McModel b , LocalDateTime birthDate)
    {
        var hereditarySource = GetHereditarySource();
        switch (hereditarySource)
        {
            case HereditarySource.ParentA:
                return a.SocialSecurityStart;
            case HereditarySource.ParentB:
                return b.SocialSecurityStart;
            case HereditarySource.Random:
                var min = birthDate
                    .PlusYears(ModelConstants.SocialSecurityElectionStartMin.years)
                    .PlusMonths(ModelConstants.SocialSecurityElectionStartMin.months);
                var max = birthDate
                    .PlusYears(ModelConstants.SocialSecurityElectionStartMax.years)
                    .PlusMonths(ModelConstants.SocialSecurityElectionStartMax.months);
                return GetUnSeededRandomDate(min, max);
            default:
                throw new InvalidDataException("Invalid HereditarySource");
        }
    }
    
    public static decimal MateAusterityRatio(McModel a, McModel b)
    {
        
        var hereditarySource = GetHereditarySource();
        switch (hereditarySource)
        {
            case HereditarySource.ParentA:
                return a.AusterityRatio;
            case HereditarySource.ParentB:
                return b.AusterityRatio;
            case HereditarySource.Random:
                var min = ModelConstants.AusterityRatioMin;
                var max = ModelConstants.AusterityRatioMax;
                return GetUnSeededRandomDecimal(min, max);
            default:
                throw new InvalidDataException("Invalid HereditarySource");
        }
    }
    
    public static decimal MateExtremeAusterityRatio(McModel a, McModel b)
    {
        var hereditarySource = GetHereditarySource();
        switch (hereditarySource)
        {
            case HereditarySource.ParentA:
                return a.ExtremeAusterityRatio;
            case HereditarySource.ParentB:
                return b.ExtremeAusterityRatio;
            case HereditarySource.Random:
                var min = ModelConstants.ExtremeAusterityRatioMin;
                var max = ModelConstants.ExtremeAusterityRatioMax;
                return GetUnSeededRandomDecimal(min, max);
            default:
                throw new InvalidDataException("Invalid HereditarySource");
        }
    }
    
    public static decimal MateExtremeAusterityNetWorthTrigger(McModel a, McModel b)
    {
        
        var hereditarySource = GetHereditarySource();
        switch (hereditarySource)
        {
            case HereditarySource.ParentA:
                return a.ExtremeAusterityNetWorthTrigger;
            case HereditarySource.ParentB:
                return b.ExtremeAusterityNetWorthTrigger;
            case HereditarySource.Random:
                var min = ModelConstants.ExtremeAusterityNetWorthTriggerMin;
                var max = ModelConstants.ExtremeAusterityNetWorthTriggerMax;
                return GetUnSeededRandomDecimal(min, max);
            default:
                throw new InvalidDataException("Invalid HereditarySource");
        }
    }
    
    public static RebalanceFrequency MateRebalanceFrequency(McModel a, McModel b)
    {
            
        Func<RebalanceFrequency> getRandom = () =>
        {
            var randomInt = GetUnSeededRandomInt(0, 3000);
            return randomInt switch
            {
                < 1000 => RebalanceFrequency.MONTHLY,
                < 2000 => RebalanceFrequency.QUARTERLY,
                _ => RebalanceFrequency.YEARLY
            };
        };
        var hereditarySource = GetHereditarySource();
        return hereditarySource switch
        {
            HereditarySource.ParentA => a.RebalanceFrequency,
            HereditarySource.ParentB => b.RebalanceFrequency,
            HereditarySource.Random => getRandom(),
            _ => throw new InvalidDataException("Invalid HereditarySource")
        };
    }
    
    public static decimal MateDesiredMonthlySpendPreRetirement(McModel a, McModel b)
    {
        var hereditarySource = GetHereditarySource();
        switch (hereditarySource)
        {
            case HereditarySource.ParentA:
                return a.DesiredMonthlySpendPreRetirement;
            case HereditarySource.ParentB:
                return b.DesiredMonthlySpendPreRetirement;
            case HereditarySource.Random:
                var min = ModelConstants.DesiredMonthlySpendPreRetirementMin;
                var max = ModelConstants.DesiredMonthlySpendPreRetirementMax;
                return GetUnSeededRandomDecimal(min, max);
            default:
                throw new InvalidDataException("Invalid HereditarySource");
        }
    }
    
    public static decimal MateDesiredMonthlySpendPostRetirement(McModel a, McModel b)
    {
        var hereditarySource = GetHereditarySource();
        switch (hereditarySource)
        {
            case HereditarySource.ParentA:
                return a.DesiredMonthlySpendPostRetirement;
            case HereditarySource.ParentB:
                return b.DesiredMonthlySpendPostRetirement;
            case HereditarySource.Random:
                var min = ModelConstants.DesiredMonthlySpendPostRetirementMin;
                var max = ModelConstants.DesiredMonthlySpendPostRetirementMax;
                return GetUnSeededRandomDecimal(min, max);
            default:
                throw new InvalidDataException("Invalid HereditarySource");
        }
    }
    
    public static decimal MatePercent401KTraditional(McModel a, McModel b)
    {
        var hereditarySource = GetHereditarySource();
        switch (hereditarySource)
        {
            case HereditarySource.ParentA:
                return a.Percent401KTraditional;
            case HereditarySource.ParentB:
                return b.Percent401KTraditional;
            case HereditarySource.Random:
                var min = ModelConstants.Percent401KTraditionalMin;
                var max = ModelConstants.Percent401KTraditionalMax;
                return GetUnSeededRandomDecimal(min, max);
            default:
                throw new InvalidDataException("Invalid HereditarySource");
        }
    }
    
    public static int MateNumMonthsCashOnHand(McModel a, McModel b)
    {
        var hereditarySource = GetHereditarySource();
        switch (hereditarySource)
        {
            case HereditarySource.ParentA:
                return a.NumMonthsCashOnHand;
            case HereditarySource.ParentB:
                return b.NumMonthsCashOnHand;
            case HereditarySource.Random:
                var min = ModelConstants.NumMonthsCashOnHandMin;
                var max = ModelConstants.NumMonthsCashOnHandMax;
                return GetUnSeededRandomInt(min, max);
            default:
                throw new InvalidDataException("Invalid HereditarySource");
        }
    }
    
    public static int MateNumMonthsMidBucketOnHand(McModel a, McModel b)
    {
        var hereditarySource = GetHereditarySource();
        switch (hereditarySource)
        {
            case HereditarySource.ParentA:
                return a.NumMonthsMidBucketOnHand;
            case HereditarySource.ParentB:
                return b.NumMonthsMidBucketOnHand;
            case HereditarySource.Random:
                var min = ModelConstants.NumMonthsMidBucketOnHandMin;
                var max = ModelConstants.NumMonthsMidBucketOnHandMax;
                return GetUnSeededRandomInt(min, max);
            default:
                throw new InvalidDataException("Invalid HereditarySource");
        }
    }
    
    public static int MateNumMonthsPriorToRetirementToBeginRebalance(McModel a, McModel b)
    {
        var hereditarySource = GetHereditarySource();
        switch (hereditarySource)
        {
            case HereditarySource.ParentA:
                return a.NumMonthsPriorToRetirementToBeginRebalance;
            case HereditarySource.ParentB:
                return b.NumMonthsPriorToRetirementToBeginRebalance;
            case HereditarySource.Random:
                var min = ModelConstants.NumMonthsPriorToRetirementToBeginRebalanceMin;
                var max = ModelConstants.NumMonthsPriorToRetirementToBeginRebalanceMax;
                return GetUnSeededRandomInt(min, max);
            default:
                throw new InvalidDataException("Invalid HereditarySource");
        }
    }
    
    public static int MateRecessionCheckLookBackMonths(McModel a, McModel b)
    {
        var hereditarySource = GetHereditarySource();
        switch (hereditarySource)
        {
            case HereditarySource.ParentA:
                return a.RecessionCheckLookBackMonths;
            case HereditarySource.ParentB:
                return b.RecessionCheckLookBackMonths;
            case HereditarySource.Random:
                var min = ModelConstants.RecessionCheckLookBackMonthsMin;
                var max = ModelConstants.RecessionCheckLookBackMonthsMax;
                return GetUnSeededRandomInt(min, max);
            default:
                throw new InvalidDataException("Invalid HereditarySource");
        }
    }
    
    public static decimal MateRecessionRecoveryPointModifier(McModel a, McModel b)
    {
        var hereditarySource = GetHereditarySource();
        switch (hereditarySource)
        {
            case HereditarySource.ParentA:
                return a.RecessionRecoveryPointModifier;
            case HereditarySource.ParentB:
                return b.RecessionRecoveryPointModifier;
            case HereditarySource.Random:
                var min = ModelConstants.RecessionRecoveryPointModifierMin;
                var max = ModelConstants.RecessionRecoveryPointModifierMax;
                return GetUnSeededRandomDecimal(min, max);
            default:
                throw new InvalidDataException("Invalid HereditarySource");
        }
    }
    
    
    

    
}