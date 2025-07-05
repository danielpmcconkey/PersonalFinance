using Lib.DataTypes.MonteCarlo;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Rebalance
{
    public static bool IsRebalanceTime(LocalDateTime currentDate, McModel simParams)
    {
        // check whether it's time to move funds
        bool isTime = false;
        int currentMonthNum = currentDate.Month - 1; // we want it zero-indexed to make the modulus easier
        return (
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
        );
    }
    public static (decimal amountMoved, BookOfAccounts newBookOfAccounts, TaxLedger newLedger)
        MoveFromInvestmentToCash(BookOfAccounts bookOfAccounts, decimal cashNeeded, 
            McInvestmentPositionType positionType, LocalDateTime currentDate, TaxLedger taxLedger)
    {
        // set up the return tuple
        (decimal amountMoved, BookOfAccounts newBookOfAccounts, TaxLedger newLedger) results = (
            0m, AccountCopy.CopyBookOfAccounts(bookOfAccounts), Tax.CopyTaxLedger(taxLedger));
        
        var salesResults = InvestmentSales.SellInvestmentsToDollarAmountByPositionType(
            cashNeeded, positionType, results.newBookOfAccounts, results.newLedger, currentDate);
        results.amountMoved = salesResults.amountSold;
        results.newBookOfAccounts = salesResults.newBookOfAccounts;
        results.newLedger = salesResults.newLedger;
        
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(null, results.amountMoved, $"Rebalance: Selling {positionType} investment");
        }

        return results;
    }
    
    
    /// <summary>
    /// move investments between long-term, mid-term, and cash, per retirement strategy defined in the sim parameters
    /// </summary>
    public static (BookOfAccounts newBookOfAccounts, TaxLedger newLedger) RebalancePortfolio(LocalDateTime currentDate, BookOfAccounts bookOfAccounts,
        RecessionStats recessionStats, CurrentPrices currentPrices, McModel simParams, TaxLedger taxLedger)
    {
        if(IsRebalanceTime(currentDate, simParams) == false) return (bookOfAccounts, taxLedger);
        
        // blammo. time to make the donuts.
        
        // set up return tuple
        (BookOfAccounts newBookOfAccounts, TaxLedger newLedger) results = (
            AccountCopy.CopyBookOfAccounts(bookOfAccounts), Tax.CopyTaxLedger(taxLedger));
        
        TopUpCashBucket(currentDate, bookOfAccounts, recessionStats, currentPrices, simParams, taxLedger);
        TopUpMidBucket(currentDate, bookOfAccounts, recessionStats, currentPrices, simParams, taxLedger);
        
        return results;
    }

    public static (BookOfAccounts newBookOfAccounts, TaxLedger newLedger) TopUpCashBucket(
        LocalDateTime currentDate, BookOfAccounts bookOfAccounts, RecessionStats recessionStats,
        CurrentPrices currentPrices, McModel simParams, TaxLedger taxLedger) // todo: rename "top up" functions to something smarter
    {
        // if it's been a good year, sell long-term growth assets and
        // top-up cash. if it's been a bad year, sell mid-term assets to
        // top-up cash.
        
        
        
        
        
        
        
        
        
        
        /*
         * you are here. in the middle of refactoring to make this whole thing work a lot more functional. then you need to write unit tests
         */
        
        
        
        
        
        
        
        
        
        
        
        
        


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