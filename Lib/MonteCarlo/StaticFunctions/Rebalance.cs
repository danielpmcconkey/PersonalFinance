using Lib.DataTypes.MonteCarlo;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Rebalance
{
    #region Calculation functions

    public static int CalculateNumMonthsUntilRetirement(McModel simParams, LocalDateTime currentDate)
    {
        int numMonths = simParams.NumMonthsCashOnHand;
        // subtract the number of months until retirement because you don't need to have it all at once
        if (currentDate < simParams.RetirementDate)
            numMonths -= (int)(Math.Round((simParams.RetirementDate - currentDate).Days / 30f, 0));
        return numMonths;
    }
    
    public static bool CalculateWhetherItsBucketRebalanceTime(LocalDateTime currentDate, McModel simParams)
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

    #endregion Calculation functions
    
    #region asset movement functions
    
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
    
    public static (decimal amountSold, BookOfAccounts newAccounts, TaxLedger newLedger) SellInOrder(
        decimal cashNeeded, McInvestmentPositionType[] pullOrder, BookOfAccounts bookOfAccounts,
        TaxLedger taxLedger, LocalDateTime currentDate)
    {
        // set up return tuple
        (decimal amountSold, BookOfAccounts newAccounts, TaxLedger newLedger) results = (
            0m, AccountCopy.CopyBookOfAccounts(bookOfAccounts), Tax.CopyTaxLedger(taxLedger)); 
        
        // loop over the pull types until we have the bucks
        
        foreach (var positionType in pullOrder)
        {
            if (cashNeeded <= results.amountSold) return results;
            
            var localResults = MoveFromInvestmentToCash(
                bookOfAccounts, cashNeeded, McInvestmentPositionType.MID_TERM, currentDate, taxLedger);
            results.amountSold += localResults.amountMoved;
            results.newAccounts = localResults.newBookOfAccounts;
            results.newLedger = localResults.newLedger;
        }
        return results;
    }

    
    #endregion asset movement functions

    #region rebalance functions

    public static BookOfAccounts  InvestExcessCash(
        LocalDateTime currentDate, BookOfAccounts bookOfAccounts, RecessionStats recessionStats,
        CurrentPrices currentPrices, McModel simParams, TaxLedger taxLedger, decimal reserveCashNeeded)
    {
        var cashOnHand = AccountCalculation.CalculateCashBalance(bookOfAccounts);
        var totalInvestmentAmount = cashOnHand - reserveCashNeeded;

        if (totalInvestmentAmount <= 0) return bookOfAccounts;
        
        // we have excess cash, put it in long-term brokerage
        BookOfAccounts results = AccountCopy.CopyBookOfAccounts(bookOfAccounts);
        var investmentResults = Investment.InvestFunds(results, currentDate, totalInvestmentAmount,
            McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TAXABLE_BROKERAGE, currentPrices);
        
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(currentDate, totalInvestmentAmount, "Rebalance: adding excess cash to long-term investment");
        }

        return investmentResults;
    }
    
    public static (BookOfAccounts newBookOfAccounts, TaxLedger newLedger) RebalanceLongToMid(
        LocalDateTime currentDate, BookOfAccounts bookOfAccounts, RecessionStats recessionStats,
        CurrentPrices currentPrices, McModel simParams, TaxLedger taxLedger, McPerson person)
    {
        // if it's been a good year, sell long-term growth assets and top-up mid-term.
        // if it's been a bad year, sit tight and hope the recession doesn't
        // outlast your mid-term bucket.

        var numMonths = simParams.NumMonthsMidBucketOnHand; 
        if (numMonths <= 0) return (bookOfAccounts, taxLedger);
        
       
        // figure out how much we want to have in the mid-bucket
        decimal amountOnHand = AccountCalculation.CalculateMidBucketTotalBalance(bookOfAccounts);
        var totalAmountNeeded = Spend.CalculateCashNeedForNMonths(simParams, person, currentDate, numMonths);
        decimal amountNeededToMove = totalAmountNeeded - amountOnHand;
        
        
        // do we stiLl need to pull anything?
        if (amountNeededToMove <= 0) return (bookOfAccounts, taxLedger);
        
        // if we're in a recession, don't move from long to mid until we absolutely have to
        if (recessionStats.AreWeInADownYear) return (bookOfAccounts, taxLedger);
        
        // not in a recession. sell from long and buy mid
        
        // Set up the return tuple
        (BookOfAccounts newBookOfAccounts, TaxLedger newLedger) results = (
            AccountCopy.CopyBookOfAccounts(bookOfAccounts),Tax.CopyTaxLedger(taxLedger));
        
        // first sell the long-term investments
        var salesResults = SellInOrder(amountNeededToMove, [McInvestmentPositionType.LONG_TERM], results.newBookOfAccounts,
            results.newLedger, currentDate);
        var amountSold = salesResults.amountSold;
        results.newBookOfAccounts = salesResults.newAccounts;
        results.newLedger = salesResults.newLedger;
        
        // now buy the mid-term investments
        results.newBookOfAccounts = Investment.InvestFunds(results.newBookOfAccounts, currentDate, amountSold,
            McInvestmentPositionType.MID_TERM, McInvestmentAccountType.TAXABLE_BROKERAGE, currentPrices);
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(currentDate, amountSold, "Rebalance: adding investment sales to mid-term investment");
        }

        return results;
    }
    
    public static (BookOfAccounts newBookOfAccounts, TaxLedger newLedger) RebalanceMidToCash(
        LocalDateTime currentDate, BookOfAccounts bookOfAccounts, RecessionStats recessionStats,
        CurrentPrices currentPrices, McModel simParams, TaxLedger taxLedger, decimal totalCashNeeded) 
    {
        // if it's been a good year, sell long-term growth assets and
        // top-up cash. if it's been a bad year, sell mid-term assets to
        // top-up cash.
        
        var numMonths = CalculateNumMonthsUntilRetirement(simParams, currentDate);
        if (numMonths <= 0) return (bookOfAccounts, taxLedger);
        
        
        var totalCashOnHand = AccountCalculation.CalculateCashBalance(bookOfAccounts);
        
        var cashNeededToBeMoved = totalCashOnHand - totalCashNeeded;
        
        if (cashNeededToBeMoved <= 0) return (bookOfAccounts, taxLedger);
        
        // set up the return tuple
        (BookOfAccounts newBookOfAccounts, TaxLedger newLedger) results = (
            AccountCopy.CopyBookOfAccounts(bookOfAccounts), Tax.CopyTaxLedger(taxLedger));
        
        // need to pull from one of the buckets. 
        if (recessionStats.AreWeInADownYear)
        {
            // pull what we can from the mid-term bucket, then from long bucket
            McInvestmentPositionType[] pullOrder = [ McInvestmentPositionType.MID_TERM, McInvestmentPositionType.LONG_TERM ];
            var localResults = SellInOrder(cashNeededToBeMoved, pullOrder, bookOfAccounts, taxLedger, currentDate);
            results.newBookOfAccounts = localResults.newAccounts;
            results.newLedger = localResults.newLedger;
        }
        else
        {
            // pull what we can from the long-term bucket, then from mid
            McInvestmentPositionType[] pullOrder = [ McInvestmentPositionType.LONG_TERM, McInvestmentPositionType.MID_TERM ];
            var localResults = SellInOrder(cashNeededToBeMoved, pullOrder, bookOfAccounts, taxLedger, currentDate);
            results.newBookOfAccounts = localResults.newAccounts;
            results.newLedger = localResults.newLedger;
        }
        return results;
    }
    
    /// <summary>
    /// move investments between long-term, mid-term, and cash, per retirement strategy defined in the sim parameters
    /// </summary>
    public static (BookOfAccounts newBookOfAccounts, TaxLedger newLedger) RebalancePortfolio(LocalDateTime currentDate, BookOfAccounts bookOfAccounts,
        RecessionStats recessionStats, CurrentPrices currentPrices, McModel simParams, TaxLedger taxLedger, McPerson person)
    {
        // determine how much cash you need on hand
        var cashNeededTotal = Spend.CalculateCashNeedForNMonths(simParams, person, currentDate, simParams.NumMonthsCashOnHand);
        
        // set up return tuple
        (BookOfAccounts newBookOfAccounts, TaxLedger newLedger) results = (
            AccountCopy.CopyBookOfAccounts(bookOfAccounts), Tax.CopyTaxLedger(taxLedger));
        
        // move funds if it's time
        if (CalculateWhetherItsBucketRebalanceTime(currentDate, simParams))
        {
            var rebalanceCashResults = RebalanceMidToCash(
                currentDate, results.newBookOfAccounts, recessionStats, currentPrices, simParams, results.newLedger,
                cashNeededTotal);
            results.newBookOfAccounts = rebalanceCashResults.newBookOfAccounts;
            results.newLedger = rebalanceCashResults.newLedger;
            
            var rebalanceMidResults = RebalanceLongToMid(
                currentDate, results.newBookOfAccounts, recessionStats, currentPrices, simParams, results.newLedger,
                person);
            results.newBookOfAccounts = rebalanceMidResults.newBookOfAccounts;
            results.newLedger = rebalanceMidResults.newLedger;
        }
        // always check if you should invest excess cash
        var investCashResults = InvestExcessCash(
            currentDate, results.newBookOfAccounts, recessionStats, currentPrices, simParams, results.newLedger,
            cashNeededTotal);

        return results;
    }

    #endregion
    

   
}