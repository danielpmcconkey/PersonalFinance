using Lib.DataTypes.MonteCarlo;
using NodaTime;

namespace Lib.MonteCarlo
{
    public static class ModelBreeder
    {
        public static McModel MateSimParameters(McModel a,
            McModel b)
        {
            var investmentAmounts = InvestmentAmounts(a, b);
            return new McModel()
            {
                Id = Guid.NewGuid(),
                PersonId = a.PersonId,
                ParentAId = a.Id,
                ParentBId = b.Id,
                ModelCreatedDate = LocalDateTime.FromDateTime(DateTime.Now),
                SimStartDate = a.SimStartDate,
                SimEndDate = a.SimEndDate,
                RetirementDate = RetirementDate(a, b),
                SocialSecurityStart = SocialSecurityStartDate(a, b),
                MonthlyInvest401kRoth = investmentAmounts.roth,
                MonthlyInvest401kTraditional = investmentAmounts.trad,
                MonthlyInvestBrokerage = investmentAmounts.brokerage,
                DesiredMonthlySpend = DesiredMonthlySpend(a, b),
                AusterityRatio = AusterityRatio(a, b),
                ExtremeAusterityRatio = ExtremeAusterityRatio(a, b),
                ExtremeAusterityNetWorthTrigger = ExtremeAusterityNetWorthTrigger(a, b),
                MonthlyInvestHSA = a.MonthlyInvestHSA,
                RebalanceFrequency = RebalanceFrequency(a, b),
                NumMonthsCashOnHand = NumMonthsCashOnHand(a, b),
                NumMonthsMidBucketOnHand = NumMonthsMidBucketOnHand(a, b),
                NumMonthsPriorToRetirementToBeginRebalance = NumMonthsPriorToRetirementToBeginRebalance(a, b),
                RecessionCheckLookBackMonths = RecessionCheckLookBackMonths(a, b),
                RecessionRecoveryPointModifier = RecessionRecoveryPointModifier(a, b),
                BatchResults = [],
            };
        }
        private static decimal RecessionRecoveryPointModifier(McModel a, McModel b)
        {
            Func<decimal> getRandom = () =>
            {
                int min = 80;
                int max = 120;

                var rngInt =  GetUnSeededRandomInt(min, max);
                var rngDecimal = rngInt * 0.01M;
                return rngDecimal;
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
        private static int RecessionCheckLookBackMonths(McModel a, McModel b)
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
        private static int NumMonthsPriorToRetirementToBeginRebalance(McModel a, McModel b)
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
        private static int NumMonthsMidBucketOnHand(McModel a, McModel b)
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
        private static int NumMonthsCashOnHand(McModel a, McModel b)
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
        private static RebalanceFrequency RebalanceFrequency(McModel a, McModel b)
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
        private static decimal ExtremeAusterityNetWorthTrigger(McModel a, McModel b)
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
        private static decimal ExtremeAusterityRatio(McModel a, McModel b)
        {
            Func<decimal> getRandom = () =>
            {
                int min = 40;
                int max = 90;

                return GetUnSeededRandomInt(min, max) * 0.01M;
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
        private static decimal AusterityRatio(McModel a, McModel b)
        {
            Func<decimal> getRandom = () =>
            {
                int min = 50;
                int max = 100;

                return GetUnSeededRandomInt(min, max) * 0.01M;
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
        private static decimal DesiredMonthlySpend(McModel a, McModel b)
        {
            Func<decimal> getRandom = () =>
            {
                int min = 8000;
                int max = 16000;
                
                return GetUnSeededRandomInt(min, max);
            };
            var heredetarySource = GetHeredetarySource();
            switch (heredetarySource)
            {
                case HeredetarySource.PARENT_A:
                    return a.DesiredMonthlySpend;
                case HeredetarySource.PARENT_B:
                    return b.DesiredMonthlySpend;
                case HeredetarySource.RANDOM:
                    return getRandom();
                default:
                    throw new NotImplementedException();
            }
        }
        private static LocalDateTime RetirementDate(McModel a, McModel b)
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
        private static LocalDateTime SocialSecurityStartDate(McModel a, McModel b)
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
        private static (decimal roth, decimal trad, decimal brokerage) InvestmentAmounts(
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

                    decimal monthlyBrokerageContributionBase = 500M * 26M / 12M;
                    var monthly401kContribution = (23500M + 7500M) / 12.0M;
                    int maxMonthlyRothContribution = (int)Math.Round(monthly401kContribution, 0);
                    decimal roth = GetUnSeededRandomInt(0, maxMonthlyRothContribution);
                    decimal trad = maxMonthlyRothContribution - roth;
                    decimal brokerage = monthlyBrokerageContributionBase + (trad * 0.162M);
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

        private static int GetUnSeededRandomInt(int minInclusive, int maxInclusive)
        {
            CryptoRandom cr = new CryptoRandom();
            return cr.Next(minInclusive, maxInclusive + 1);
        }
        private static HeredetarySource GetHeredetarySource()
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
    }
}