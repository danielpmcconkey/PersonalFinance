using Lib.DataTypes.MonteCarlo;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public class Model
{
    public static HeredetarySource GetHeredetarySource()
    {
        int diceRoll = GetUnSeededRandomInt(1, 10);
        switch (diceRoll)
        {
            case 1:
            case 2:
            case 3:
            case 4:
                return HeredetarySource.PARENT_A;
            case 5:
            case 6:
            case 7:
            case 8:
                return HeredetarySource.PARENT_B;
            case 9:
            case 10:
                return HeredetarySource.RANDOM;
            default:
                throw new NotImplementedException();
        }
    }
    
    public static int GetUnSeededRandomInt(int minInclusive, int maxInclusive)
    {
        CryptoRandom cr = new CryptoRandom();
        return cr.Next(minInclusive, maxInclusive + 1);
    }
    
    public static (decimal roth, decimal trad, decimal brokerage) MateInvestmentAmounts(
        McModel a, McModel b)
    {
        Func<(decimal roth, decimal trad, decimal brokerage)>
            getRandom = () =>
            {
                // today, I put $500 per paycheck into my brokerage account. if I
                // moved some of my 401k from Roth to traditional, I'd have extra
                // money in my paycheck that I could put in. Those dollars are
                // taxed at 16.2%, or at least that's the difference I think I'd
                // make in my paycheck

                int monthlyBrokerageContributionBase = 5000000 * 26 / 12;
                int monthly401kContribution = (235000000 + 75000000) / 12;
                int maxMonthlyRothContribution = monthly401kContribution;
                decimal roth = GetUnSeededRandomInt(0, maxMonthlyRothContribution);
                decimal trad = maxMonthlyRothContribution - roth;
                decimal brokerage = monthlyBrokerageContributionBase + (trad * 1620);
                return (roth, trad, brokerage);
            };
        decimal monthlyRoth401kContribution = 0;
        decimal monthlyTraditional401kContribution = 0;
        decimal monthlyBrokerageContribution = 0;
        var heredetarySource = GetHeredetarySource();
        switch (heredetarySource)
        {
            case HeredetarySource.PARENT_A:
                monthlyRoth401kContribution = a.MonthlyInvest401kRoth;
                monthlyTraditional401kContribution = a.MonthlyInvest401kTraditional;
                monthlyBrokerageContribution = a.MonthlyInvestBrokerage;
                break;
            case HeredetarySource.PARENT_B:
                monthlyRoth401kContribution = b.MonthlyInvest401kRoth;
                monthlyTraditional401kContribution = b.MonthlyInvest401kTraditional;
                monthlyBrokerageContribution = b.MonthlyInvestBrokerage;
                break;
            case HeredetarySource.RANDOM:
                var investments = getRandom();
                monthlyRoth401kContribution = investments.roth;
                monthlyTraditional401kContribution = investments.trad;
                monthlyBrokerageContribution = investments.brokerage;
                break;
            default:
                throw new NotImplementedException();
        }

        return (monthlyRoth401kContribution,
            monthlyTraditional401kContribution, monthlyBrokerageContribution);
    }

    public static decimal MateAusterityRatio(McModel a, McModel b)
    {
        Func<decimal> getRandom = () =>
        {
            int min = 5000;
            int max = 10000;

            return GetUnSeededRandomInt(min, max);
        };
        var heredetarySource = GetHeredetarySource();
        switch (heredetarySource)
        {
            case HeredetarySource.PARENT_A:
                return a.AusterityRatio;
            case HeredetarySource.PARENT_B:
                return b.AusterityRatio;
            case HeredetarySource.RANDOM:
                return getRandom();
            default:
                throw new NotImplementedException();
        }
    }
    
    public static decimal MateRequiredMonthlySpend(McModel a, McModel b)
    {
        Func<decimal> getRandom = () =>
        {
            // todo: think about the min and max values in MateRequiredMonthlySpend
            int min = 8000;
            int max = 16000;
                
            return GetUnSeededRandomInt(min, max);
        };
        var heredetarySource = GetHeredetarySource();
        switch (heredetarySource)
        {
            case HeredetarySource.PARENT_A:
                return a.RequiredMonthlySpend;
            case HeredetarySource.PARENT_B:
                return b.RequiredMonthlySpend;
            case HeredetarySource.RANDOM:
                return getRandom();
            default:
                throw new NotImplementedException();
        }
    }
    public static decimal MateRequiredMonthlySpendHealthCare(McModel a, McModel b)
    {
        Func<decimal> getRandom = () =>
        {
            // todo: think about the min and max values in MateRequiredMonthlySpendHealthCare
            int min = 8000;
            int max = 16000;
                
            return GetUnSeededRandomInt(min, max);
        };
        var heredetarySource = GetHeredetarySource();
        switch (heredetarySource)
        {
            case HeredetarySource.PARENT_A:
                return a.RequiredMonthlySpendHealthCare;
            case HeredetarySource.PARENT_B:
                return b.RequiredMonthlySpendHealthCare;
            case HeredetarySource.RANDOM:
                return getRandom();
            default:
                throw new NotImplementedException();
        }
    }
    public static decimal MateDesiredMonthlySpendPreRetirement(McModel a, McModel b)
    {
        Func<decimal> getRandom = () =>
        {
            // todo: think about the min and max values in MateDesiredMonthlySpendPreRetirement
            int min = 8000;
            int max = 16000;
                
            return GetUnSeededRandomInt(min, max);
        };
        var heredetarySource = GetHeredetarySource();
        switch (heredetarySource)
        {
            case HeredetarySource.PARENT_A:
                return a.DesiredMonthlySpendPreRetirement;
            case HeredetarySource.PARENT_B:
                return b.DesiredMonthlySpendPreRetirement;
            case HeredetarySource.RANDOM:
                return getRandom();
            default:
                throw new NotImplementedException();
        }
    }
    
    public static decimal MateDesiredMonthlySpendPostRetirement(McModel a, McModel b)
    {
        Func<decimal> getRandom = () =>
        {
            // todo: think about the min and max values in MateDesiredMonthlySpendPostRetirement
            int min = 8000;
            int max = 16000;
                
            return GetUnSeededRandomInt(min, max);
        };
        var heredetarySource = GetHeredetarySource();
        switch (heredetarySource)
        {
            case HeredetarySource.PARENT_A:
                return a.DesiredMonthlySpendPostRetirement;
            case HeredetarySource.PARENT_B:
                return b.DesiredMonthlySpendPostRetirement;
            case HeredetarySource.RANDOM:
                return getRandom();
            default:
                throw new NotImplementedException();
        }
    }
    
    public static decimal MateExtremeAusterityRatio(McModel a, McModel b)
    {
        Func<decimal> getRandom = () =>
        {
            int min = 4000;
            int max = 9000;

            return GetUnSeededRandomInt(min, max);
        };
        var heredetarySource = GetHeredetarySource();
        switch (heredetarySource)
        {
            case HeredetarySource.PARENT_A:
                return a.ExtremeAusterityRatio;
            case HeredetarySource.PARENT_B:
                return b.ExtremeAusterityRatio;
            case HeredetarySource.RANDOM:
                return getRandom();
            default:
                throw new NotImplementedException();
        }
    }
    
    public static decimal MateExtremeAusterityNetWorthTrigger(McModel a, McModel b)
    {
        Func<decimal> getRandom = () =>
        {
            int min = 500000;
            int max = 2000000;

            return GetUnSeededRandomInt(min, max);
        };
        var heredetarySource = GetHeredetarySource();
        switch (heredetarySource)
        {
            case HeredetarySource.PARENT_A:
                return a.ExtremeAusterityNetWorthTrigger;
            case HeredetarySource.PARENT_B:
                return b.ExtremeAusterityNetWorthTrigger;
            case HeredetarySource.RANDOM:
                return getRandom();
            default:
                throw new NotImplementedException();
        }
    }
    
    public static McModel MateModels(McModel a, McModel b)
    {
        var investmentAmounts = MateInvestmentAmounts(a, b);
        return new McModel()
        {
            Id = Guid.NewGuid(),
            PersonId = a.PersonId,
            ParentAId = a.Id,
            ParentBId = b.Id,
            ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Now),
            SimStartDate = a.SimStartDate,
            SimEndDate = a.SimEndDate,
            RetirementDate = MateRetirementDate(a, b),
            SocialSecurityStart = MateSocialSecurityStartDate(a, b),
            MonthlyInvest401kRoth = investmentAmounts.roth,
            MonthlyInvest401kTraditional = investmentAmounts.trad,
            MonthlyInvestBrokerage = investmentAmounts.brokerage,
            RequiredMonthlySpend = MateRequiredMonthlySpend(a, b),
            RequiredMonthlySpendHealthCare = MateRequiredMonthlySpendHealthCare(a, b),
            DesiredMonthlySpendPreRetirement = MateDesiredMonthlySpendPreRetirement(a, b),
            DesiredMonthlySpendPostRetirement = MateDesiredMonthlySpendPostRetirement(a, b),
            AusterityRatio = MateAusterityRatio(a, b),
            ExtremeAusterityRatio = MateExtremeAusterityRatio(a, b),
            ExtremeAusterityNetWorthTrigger = MateExtremeAusterityNetWorthTrigger(a, b),
            MonthlyInvestHSA = a.MonthlyInvestHSA,
            RebalanceFrequency = MateRebalanceFrequency(a, b),
            NumMonthsCashOnHand = MateNumMonthsCashOnHand(a, b),
            NumMonthsMidBucketOnHand = MateNumMonthsMidBucketOnHand(a, b),
            NumMonthsPriorToRetirementToBeginRebalance = MateNumMonthsPriorToRetirementToBeginRebalance(a, b),
            RecessionCheckLookBackMonths = MateRecessionCheckLookBackMonths(a, b),
            RecessionRecoveryPointModifier = MateRecessionRecoveryPointModifier(a, b),
        };
    }
    
    public static RebalanceFrequency MateRebalanceFrequency(McModel a, McModel b)
    {
            
        Func<RebalanceFrequency> getRandom = () =>
        {
            RebalanceFrequency[] rebalanceFrequencyOptions = [
                Lib.DataTypes.MonteCarlo.RebalanceFrequency.MONTHLY,
                Lib.DataTypes.MonteCarlo.RebalanceFrequency.QUARTERLY,
                Lib.DataTypes.MonteCarlo.RebalanceFrequency.YEARLY,
            ];

            return rebalanceFrequencyOptions[
                GetUnSeededRandomInt(0, rebalanceFrequencyOptions.Length - 1)];
        };
        var heredetarySource = GetHeredetarySource();
        switch (heredetarySource)
        {
            case HeredetarySource.PARENT_A:
                return a.RebalanceFrequency;
            case HeredetarySource.PARENT_B:
                return b.RebalanceFrequency;
            case HeredetarySource.RANDOM:
                return getRandom();
            default:
                throw new NotImplementedException();
        }
    }
    
    public static int MateNumMonthsCashOnHand(McModel a, McModel b)
    {
        Func<int> getRandom = () =>
        {
            int min = 3;
            int max = 48;

            return GetUnSeededRandomInt(min, max);
        };
        var heredetarySource = GetHeredetarySource();
        switch (heredetarySource)
        {
            case HeredetarySource.PARENT_A:
                return a.NumMonthsCashOnHand;
            case HeredetarySource.PARENT_B:
                return b.NumMonthsCashOnHand;
            case HeredetarySource.RANDOM:
                return getRandom();
            default:
                throw new NotImplementedException();
        }
    }
    
    public static int MateNumMonthsMidBucketOnHand(McModel a, McModel b)
    {
        Func<int> getRandom = () =>
        {
            int min = 3;
            int max = 60;

            return GetUnSeededRandomInt(min, max);
        };
        var heredetarySource = GetHeredetarySource();
        switch (heredetarySource)
        {
            case HeredetarySource.PARENT_A:
                return a.NumMonthsMidBucketOnHand;
            case HeredetarySource.PARENT_B:
                return b.NumMonthsMidBucketOnHand;
            case HeredetarySource.RANDOM:
                return getRandom();
            default:
                throw new NotImplementedException();
        }
    }
    
    public static int MateNumMonthsPriorToRetirementToBeginRebalance(McModel a, McModel b)
    {
        Func<int> getRandom = () =>
        {
            int min = 3;
            int max = 84;

            return GetUnSeededRandomInt(min, max);
        };
        var heredetarySource = GetHeredetarySource();
        switch (heredetarySource)
        {
            case HeredetarySource.PARENT_A:
                return a.NumMonthsPriorToRetirementToBeginRebalance;
            case HeredetarySource.PARENT_B:
                return b.NumMonthsPriorToRetirementToBeginRebalance;
            case HeredetarySource.RANDOM:
                return getRandom();
            default:
                throw new NotImplementedException();
        }
    }
    
    public static int MateRecessionCheckLookBackMonths(McModel a, McModel b)
    {
        Func<int> getRandom = () =>
        {
            int min = 3;
            int max = 36;

            return GetUnSeededRandomInt(min, max);
        };
        var heredetarySource = GetHeredetarySource();
        switch (heredetarySource)
        {
            case HeredetarySource.PARENT_A:
                return a.RecessionCheckLookBackMonths;
            case HeredetarySource.PARENT_B:
                return b.RecessionCheckLookBackMonths;
            case HeredetarySource.RANDOM:
                return getRandom();
            default:
                throw new NotImplementedException();
        }
    }
    
    public static decimal MateRecessionRecoveryPointModifier(McModel a, McModel b)
    {
        Func<decimal> getRandom = () =>
        {
            int min =  8000; 
            int max = 12000;

            var rngInt =  GetUnSeededRandomInt(min, max);
                
            return (decimal)rngInt;
        };
        var heredetarySource = GetHeredetarySource();
        switch (heredetarySource)
        {
            case HeredetarySource.PARENT_A:
                return a.RecessionRecoveryPointModifier;
            case HeredetarySource.PARENT_B:
                return b.RecessionRecoveryPointModifier;
            case HeredetarySource.RANDOM:
                return getRandom();
            default:
                throw new NotImplementedException();
        }
    }
    
    public static LocalDateTime MateRetirementDate(McModel a, McModel b)
    {
        Func<LocalDateTime> getRandom = () =>
        {
            LocalDateTime min = new LocalDateTime(2034, 7, 1, 0, 0);
            LocalDateTime max = new LocalDateTime(2037, 2, 1, 0, 0);
            var numDays = (max - min).Days;
            double daysPerMonth = 365.25 / 12;
            double numMonths = numDays / daysPerMonth;
            return min.PlusMonths(GetUnSeededRandomInt(0, (int)Math.Round(numMonths, 0)));
        };
        var heredetarySource = GetHeredetarySource();
        switch (heredetarySource)
        {
            case HeredetarySource.PARENT_A:
                return a.RetirementDate;
            case HeredetarySource.PARENT_B:
                return b.RetirementDate;
            case HeredetarySource.RANDOM:
                return getRandom();
            default:
                throw new NotImplementedException();
        }
    }
    
    public static LocalDateTime MateSocialSecurityStartDate(McModel a, McModel b)
    {
        Func<LocalDateTime> getRandom = () =>
        {
            LocalDateTime min = new LocalDateTime(2037, 2, 1, 0, 0);
            LocalDateTime max = new LocalDateTime(2042, 2, 1, 0, 0);
            var numDays = (max - min).Days;//.TotalDays;
            double daysPerMonth = 365.25 / 12;
            double numMonths = numDays / daysPerMonth;
            return min.PlusMonths(GetUnSeededRandomInt(0, (int)Math.Round(numMonths, 0)));
        };
        var heredetarySource = GetHeredetarySource();
        switch (heredetarySource)
        {
            case HeredetarySource.PARENT_A:
                return a.SocialSecurityStart;
            case HeredetarySource.PARENT_B:
                return b.SocialSecurityStart;
            case HeredetarySource.RANDOM:
                return getRandom();
            default:
                throw new NotImplementedException();
        }
    }
}