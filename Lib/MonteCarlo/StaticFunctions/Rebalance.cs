using Lib.DataTypes.MonteCarlo;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Rebalance
{
    public static decimal MoveFromInvestmentToCash(BookOfAccounts bookOfAccounts, decimal cashNeeded, 
        McInvestmentPositionType positionType, LocalDateTime currentDate, TaxLedger taxLedger)
    {
        var cashSold = Investment.SellInvestment(
            bookOfAccounts, cashNeeded, positionType, currentDate, taxLedger);
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(null, cashSold, $"Rebalance: Selling {positionType} investment");
        }

        Account.DepositCash(bookOfAccounts, cashSold, currentDate);
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(currentDate, cashSold, "Rebalance: Depositing investment sales proceeds");
        }
        return cashSold;
    }
    /// <summary>
    /// move investments between long-term, mid-term, and cash, per retirement strategy defined in the sim parameters
    /// </summary>
    public static void RebalancePortfolio(LocalDateTime currentDate, BookOfAccounts bookOfAccounts,
        RecessionStats recessionStats, CurrentPrices currentPrices, McModel simParams, TaxLedger taxLedger)
    {
        // do our recession checking every month, regardless of whether
        // it's time to move money around. this gives us a finer grain for
        // determining down years

        // see if we're already in a recession based on prior checks
        if (recessionStats.AreWeInADownYear)
        {
            // we were previously in a down year. Let's see if we've made
            // it out yet
            if (currentPrices.CurrentLongTermInvestmentPrice >
                (recessionStats.RecessionRecoveryPoint * simParams.RecessionRecoveryPointModifier))
            {
                // we've eclipsed the prior recovery point, we've made it
                // out. go ahead and set the recovery point to today's cost
                // just to keep it near a modern number
                recessionStats.AreWeInADownYear = false;
                recessionStats.RecessionRecoveryPoint = currentPrices.CurrentLongTermInvestmentPrice;
                recessionStats.DownYearCounter = 0M;
            }
            else
            {
                // we're still in a dip. keep us here, but increment the down year counter
                decimal _downYearCounterIncrement() =>
                    simParams.RebalanceFrequency switch
                    {
                        RebalanceFrequency.MONTHLY => 1M / 12M,
                        RebalanceFrequency.QUARTERLY => 0.25M,
                        RebalanceFrequency.YEARLY => 1M,
                        _ => throw new NotImplementedException(),
                    };

                recessionStats.DownYearCounter += _downYearCounterIncrement();
            }
        }
        else
        {
            // we weren't previously in a down year. check to see if stocks
            // have gone down year over year
            var numMonthsOfHistory = currentPrices.LongRangeInvestmentCostHistory.Count;
            // if we don't have 13 months of history, we can't check last
            // year's price and won't know if we need to do any rebalancing yet
            if (numMonthsOfHistory < simParams.RecessionCheckLookBackMonths) return;
            var lookbackPrice = currentPrices.LongRangeInvestmentCostHistory[
                numMonthsOfHistory - simParams.RecessionCheckLookBackMonths];
            if (lookbackPrice > currentPrices.CurrentLongTermInvestmentPrice)
            {
                // prices are down year over year. Set the recovery point
                recessionStats.AreWeInADownYear = true;
                recessionStats.RecessionRecoveryPoint =
                    (lookbackPrice > recessionStats.RecessionRecoveryPoint) ? lookbackPrice : recessionStats.RecessionRecoveryPoint;
            }
            else
            {
                // we're not in a down year update the recovery point if
                // it's a new high water mark
                recessionStats.RecessionRecoveryPoint =
                    (currentPrices.CurrentLongTermInvestmentPrice > recessionStats.RecessionRecoveryPoint)
                        ? currentPrices.CurrentLongTermInvestmentPrice
                        : recessionStats.RecessionRecoveryPoint;
            }
        }

        // now check whether it's time to move funds
        bool isTime = false;
        int currentMonthNum = currentDate.Month - 1; // we want it zero-indexed to make the modulus easier
        if (
            (
                // check whether it's close enough to retirement to think about rebalancing
                currentDate >= simParams.RetirementDate
                    .PlusMonths(-1 * simParams.NumMonthsPriorToRetirementToBeginRebalance)
            ) &&
            (
                // check whether our frequency aligns to the calendar
                simParams.RebalanceFrequency is RebalanceFrequency.MONTHLY) ||
            (simParams.RebalanceFrequency is RebalanceFrequency.QUARTERLY
             && currentMonthNum % 3 == 0) ||
            (simParams.RebalanceFrequency is RebalanceFrequency.YEARLY
             && currentMonthNum % 12 == 0)
        )
        {
            isTime = true;
        }

        if (isTime)
        {
            // blammo. time to make the donuts.
            TopUpCashBucket(currentDate, bookOfAccounts, recessionStats, currentPrices, simParams, taxLedger);
            TopUpMidBucket(currentDate, bookOfAccounts, recessionStats, currentPrices, simParams, taxLedger);
        }
    }

    public static void TopUpCashBucket(LocalDateTime currentDate, BookOfAccounts bookOfAccounts,
        RecessionStats recessionStats, CurrentPrices currentPrices, McModel simParams, TaxLedger taxLedger) // todo: rename "top up" functions to something smarter
    {
        // if it's been a good year, sell long-term growth assets and
        // top-up cash. if it's been a bad year, sell mid-term assets to
        // top-up cash.


        // todo: there's a lot of DRY violation with the reconciliation lines

        int numMonths = simParams.NumMonthsCashOnHand;
        // subtract the number of months until retirement because you don't need to have it all at once
        if (currentDate < simParams.RetirementDate)
            numMonths -= (int)(Math.Round((simParams.RetirementDate - currentDate).Days / 30f, 0));

        if (numMonths <= 0) return;

        decimal totalCashWanted = numMonths * simParams.DesiredMonthlySpend;
        decimal cashOnHand = Account.CalculateCashBalance(bookOfAccounts);
        decimal cashNeeded = totalCashWanted - cashOnHand;
        if (cashNeeded > 0)
        {
            // need to pull from one of the buckets. 
            if (recessionStats.AreWeInADownYear)
            {
                // pull what we can from the mid-term bucket
                var cashSold = MoveFromInvestmentToCash(
                    bookOfAccounts, cashNeeded, McInvestmentPositionType.MID_TERM, currentDate, taxLedger);

                // pull any remaining from the long-term bucket
                cashNeeded = cashNeeded - cashSold;
                if (cashNeeded > 0)
                {
                    cashSold = MoveFromInvestmentToCash(
                        bookOfAccounts, cashNeeded, McInvestmentPositionType.LONG_TERM, currentDate, taxLedger);
                }
            }
            else
            {
                // pull what we can from the long-term bucket
                var cashSold = MoveFromInvestmentToCash(
                    bookOfAccounts, cashNeeded, McInvestmentPositionType.LONG_TERM, currentDate, taxLedger);

                // pull any remaining from the mid-term bucket
                cashNeeded = cashNeeded - cashSold;
                if (cashNeeded > 0)
                {
                    cashSold = MoveFromInvestmentToCash(
                        bookOfAccounts, cashNeeded, McInvestmentPositionType.MID_TERM, currentDate, taxLedger);
                }
            }
        }
    }

    public static void TopUpMidBucket(LocalDateTime currentDate, BookOfAccounts bookOfAccounts,
        RecessionStats recessionStats, CurrentPrices currentPrices, McModel simParams, TaxLedger taxLedger) // todo: rename "top up" functions to something smarter
    {
        // if it's been a good year, sell long-term growth assets and top-up mid-term.
        // if it's been a bad year, sit tight and hope the recession doesn't
        // outlast your mid-term bucket.

        int numMonths = simParams.NumMonthsMidBucketOnHand;
        // subtract the number of months until retirement because you don't need to have it all at once
        if (currentDate < simParams.RetirementDate)
            numMonths -= (int)(Math.Round((simParams.RetirementDate - currentDate).Days / 30f, 0));

        if (numMonths <= 0) return;

        decimal totalAmountWanted = numMonths * simParams.DesiredMonthlySpend;
        decimal amountOnHand = Account.CalculateMidBucketTotalBalance(bookOfAccounts);
        decimal amountNeeded = totalAmountWanted - amountOnHand;
        decimal maxAmountToPull =
            simParams.DesiredMonthlySpend *
            simParams.NumMonthsCashOnHand; // don't pull more in one go than the cash on hand goal
        amountNeeded = Math.Min(amountNeeded, maxAmountToPull);
        if (amountNeeded > 0)
        {
            // need to pull from one of the buckets. 
            if (recessionStats.AreWeInADownYear)
            {
                // rub some dirt on it, sissy
            }
            else
            {
                // pull what we can from the long-term bucket
                var cashSold = Investment.SellInvestment(
                    bookOfAccounts, amountNeeded, McInvestmentPositionType.LONG_TERM, currentDate, taxLedger);
                if (StaticConfig.MonteCarloConfig.DebugMode == true)
                {
                    Reconciliation.AddMessageLine(currentDate, cashSold, "Rebalance: Selling long-term investment");
                }

                // and invest it back into mid-term
                Investment.InvestFunds(bookOfAccounts, currentDate, cashSold, McInvestmentPositionType.MID_TERM, 
                    McInvestmentAccountType.TAXABLE_BROKERAGE, currentPrices);
                if (StaticConfig.MonteCarloConfig.DebugMode == true)
                {
                    Reconciliation.AddMessageLine(currentDate, cashSold, "Rebalance: adding investment sales to mid-term investment");
                }
            }
        }
    }
}