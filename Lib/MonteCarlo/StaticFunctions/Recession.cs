using System.Net.Sockets;
using Lib.DataTypes.MonteCarlo;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Recession
{
    public static (bool areWeInExtremeAusterityMeasures, LocalDateTime? lastExtremeAusterityMeasureEnd)
        CalculateExtremeAusterityMeasures(
            McModel simParameters, BookOfAccounts bookOfAccounts, RecessionStats recessionStats,
            LocalDateTime currentDate)
    {
        
        // set up the return tuple
        (bool areWeInExtremeAusterityMeasures, LocalDateTime? lastExtremeAusterityMeasureEnd) 
            results = (
                false, // assume false and let the logic below correct that assumption
                recessionStats.LastExtremeAusterityMeasureEnd);
        
        // see if we're in extreme austerity measures based on total net worth
        var netWorth = AccountCalculation.CalculateNetWorth(bookOfAccounts);
        if (netWorth <= simParameters.ExtremeAusterityNetWorthTrigger)
        {
            results.areWeInExtremeAusterityMeasures = true;
            
            /*
             * set the end date to now. if we stay below the line, the date will keep going up with it. so once we're
             * out of it, the date will stay in place as the end date of teh last event
             */
            results.lastExtremeAusterityMeasureEnd = currentDate;
        }
        else
        {
            /*
             * our net worth is greater than the trigger, but, if we were recently in extreme measures, we want to give
             * ourselves a little time to build up a cushion, so stay in it for a bit longer 
             */
            if (results.lastExtremeAusterityMeasureEnd >= currentDate.PlusYears(-1))
            {
                // set it to true, but don't move the date
                results.areWeInExtremeAusterityMeasures = true;
            }
        }
        return results;
    }
    
    public static bool WeLivinLarge(
        McModel simParameters, BookOfAccounts bookOfAccounts)
    {
        var netWorth = AccountCalculation.CalculateNetWorth(bookOfAccounts);
        return (netWorth >= simParameters.LivinLargeNetWorthTrigger) ? true : false;
    }
    
    
    public static (bool areWeInARecession, decimal recessionRecoveryPoint, decimal recessionDurationCounter)
        CalculateAreWeInARecession(
        RecessionStats currentStats, CurrentPrices currentPrices, McModel simParams)
    {
        (bool areWeInARecession, decimal recessionRecoveryPoint, decimal recessionDurationCounter) result = (
            currentStats.AreWeInARecession, 
                currentStats.RecessionRecoveryPoint, 
                currentStats.RecessionDurationCounter);
        // see if we're already in a recession based on prior checks
        if (currentStats.AreWeInARecession)
        {
            // we were previously in a down year. Let's see if we've made
            // it out yet
            if (currentPrices.CurrentLongTermInvestmentPrice >
                (currentStats.RecessionRecoveryPoint * simParams.RecessionRecoveryPointModifier))
            {
                // we've eclipsed the prior recovery point, we've made it
                // out. go ahead and set the recovery point to today's cost
                // just to keep it near a modern number
                result.areWeInARecession = false;
                result.recessionRecoveryPoint = currentPrices.CurrentLongTermInvestmentPrice;
                result.recessionDurationCounter = 0M;
            }
            else
            {
                // we're still in a dip. keep us here, but increment the down year counter
                result.recessionDurationCounter += 1m / 12m;
            }
        }
        else
        {
            // we weren't previously in a down year. check to see if stocks
            // have gone down year over year
            var numMonthsOfHistory = currentPrices.LongRangeInvestmentCostHistory.Count;
            // if we don't have 13 months of history, we can't check last
            // year's price and won't know if we need to do any rebalancing yet
            if (numMonthsOfHistory <= simParams.RecessionCheckLookBackMonths) return result; // not enough data yet
            
            var lookbackPrice = currentPrices.LongRangeInvestmentCostHistory[
                numMonthsOfHistory - simParams.RecessionCheckLookBackMonths - 1]; // the minus 1 is because the array is zero-indexed
            if (lookbackPrice > currentPrices.CurrentLongTermInvestmentPrice)
            {
                // prices are down year over year. Set the recovery point
                result.areWeInARecession = true;
                result.recessionRecoveryPoint =
                    (lookbackPrice > result.recessionRecoveryPoint) ? lookbackPrice : result.recessionRecoveryPoint;
            }
            else
            {
                // we're not in a down year update the recovery point if
                // it's a new high water mark
                result.recessionRecoveryPoint =
                    (currentPrices.CurrentLongTermInvestmentPrice > result.recessionRecoveryPoint)
                        ? currentPrices.CurrentLongTermInvestmentPrice
                        : result.recessionRecoveryPoint;
            }
        }
        return result;
    }
    
    
    public static RecessionStats CalculateRecessionStats(
        RecessionStats currentStats, CurrentPrices currentPrices, McModel simParams, BookOfAccounts bookOfAccounts,
        LocalDateTime currentDate)
    {
        var result = CopyRecessionStats(currentStats);
        
        var recessionResults = CalculateAreWeInARecession(
            currentStats, currentPrices, simParams);
        result.AreWeInARecession = recessionResults.areWeInARecession;
        result.RecessionRecoveryPoint = recessionResults.recessionRecoveryPoint;
        result.RecessionDurationCounter = recessionResults.recessionDurationCounter;
        
        // check to see if we're in extreme austerity measures
        var extremeAusterityResults =
            CalculateExtremeAusterityMeasures(simParams, bookOfAccounts, currentStats, currentDate);
        result.AreWeInExtremeAusterityMeasures = extremeAusterityResults.areWeInExtremeAusterityMeasures;
        result.LastExtremeAusterityMeasureEnd = extremeAusterityResults.lastExtremeAusterityMeasureEnd;
        
        // We livin' large yet?
        result.AreWeInLivinLargeMode = WeLivinLarge(simParams, bookOfAccounts);
        
        return result;
    }

    public static RecessionStats CopyRecessionStats(RecessionStats stats)
    {
        return new RecessionStats()
        {
            AreWeInARecession = stats.AreWeInARecession,
            RecessionDurationCounter = stats.RecessionDurationCounter,
            //AreWeInAusterityMeasures = stats.AreWeInAusterityMeasures,
            AreWeInExtremeAusterityMeasures = stats.AreWeInExtremeAusterityMeasures,
            LastExtremeAusterityMeasureEnd = stats.LastExtremeAusterityMeasureEnd,
            RecessionRecoveryPoint = stats.RecessionRecoveryPoint,
            AreWeInLivinLargeMode = stats.AreWeInLivinLargeMode,
        };
    }
}