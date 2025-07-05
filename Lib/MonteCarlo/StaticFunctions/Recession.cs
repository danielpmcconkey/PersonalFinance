using System.Net.Sockets;
using Lib.DataTypes.MonteCarlo;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Recession
{
    public static RecessionStats CalculateRecessionStats(RecessionStats currentStats, CurrentPrices currentPrices, McModel simParams)
    {
        var result = CopyRecessionStats(currentStats);
        // see if we're already in a recession based on prior checks
        if (currentStats.AreWeInADownYear)
        {
            // we were previously in a down year. Let's see if we've made
            // it out yet
            if (currentPrices.CurrentLongTermInvestmentPrice >
                (currentStats.RecessionRecoveryPoint * simParams.RecessionRecoveryPointModifier))
            {
                // we've eclipsed the prior recovery point, we've made it
                // out. go ahead and set the recovery point to today's cost
                // just to keep it near a modern number
                result.AreWeInADownYear = false;
                result.RecessionRecoveryPoint = currentPrices.CurrentLongTermInvestmentPrice;
                result.DownYearCounter = 0M;
            }
            else
            {
                // we're still in a dip. keep us here, but increment the down year counter
                result.DownYearCounter += 1m / 12m;
            }
        }
        else
        {
            // we weren't previously in a down year. check to see if stocks
            // have gone down year over year
            var numMonthsOfHistory = currentPrices.LongRangeInvestmentCostHistory.Count;
            // if we don't have 13 months of history, we can't check last
            // year's price and won't know if we need to do any rebalancing yet
            if (numMonthsOfHistory < simParams.RecessionCheckLookBackMonths) return result; // not enough data yet
            
            var lookbackPrice = currentPrices.LongRangeInvestmentCostHistory[
                numMonthsOfHistory - simParams.RecessionCheckLookBackMonths];
            if (lookbackPrice > currentPrices.CurrentLongTermInvestmentPrice)
            {
                // prices are down year over year. Set the recovery point
                result.AreWeInADownYear = true;
                result.RecessionRecoveryPoint =
                    (lookbackPrice > result.RecessionRecoveryPoint) ? lookbackPrice : result.RecessionRecoveryPoint;
            }
            else
            {
                // we're not in a down year update the recovery point if
                // it's a new high water mark
                result.RecessionRecoveryPoint =
                    (currentPrices.CurrentLongTermInvestmentPrice > result.RecessionRecoveryPoint)
                        ? currentPrices.CurrentLongTermInvestmentPrice
                        : result.RecessionRecoveryPoint;
            }
        }
        return result;
    }

    public static RecessionStats CopyRecessionStats(RecessionStats stats)
    {
        return new RecessionStats()
        {
            AreWeInADownYear = stats.AreWeInADownYear,
            DownYearCounter = stats.DownYearCounter,
            AreWeInAusterityMeasures = stats.AreWeInAusterityMeasures,
            AreWeInExtremeAusterityMeasures = stats.AreWeInExtremeAusterityMeasures,
            LastExtremeAusterityMeasureEnd = stats.LastExtremeAusterityMeasureEnd,
            RecessionRecoveryPoint = stats.RecessionRecoveryPoint,
        };
    }
}