using Lib.DataTypes;
using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Rebalance
{
    #region Calculation functions
    
    public static bool CalculateWhetherItsBucketRebalanceTime(LocalDateTime currentDate, DataTypes.MonteCarlo.Model model)
    {
        // check whether our frequency aligns to the calendar
        // monthly is a free-bee
        if(model.RebalanceFrequency == RebalanceFrequency.MONTHLY) return true;
        // quarterly and yearly need to be determined
        var currentMonthNum = currentDate.Month - 1; // we want it zero-indexed to make the modulus easier
        
        var modulus = model.RebalanceFrequency switch
        {
            RebalanceFrequency.MONTHLY => 1, // we already met this case
            RebalanceFrequency.QUARTERLY => 3,
            RebalanceFrequency.YEARLY => 12,
            _ => 0 // shouldn't happen but we get warnings if we don't have a default
        };
        return currentMonthNum % modulus == 0;
    }
    
    public static bool CalculateWhetherItsCloseEnoughToRetirementToRebalance(LocalDateTime currentDate, DataTypes.MonteCarlo.Model model)
    {
        // check whether it's close enough to retirement to think about rebalancing
        var rebalanceBegin = model.RetirementDate
            .PlusMonths(-1 * model.NumMonthsPriorToRetirementToBeginRebalance);
        return currentDate >= rebalanceBegin;
    }

    #endregion Calculation functions
    
    #region asset movement functions
    
    public static (decimal amountMoved, BookOfAccounts newBookOfAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages)
        MoveFromInvestmentToCash(BookOfAccounts bookOfAccounts, decimal cashNeeded, 
            McInvestmentPositionType positionType, LocalDateTime currentDate, TaxLedger taxLedger)
    {
        // set up the return tuple
        (decimal amountMoved, BookOfAccounts newBookOfAccounts, TaxLedger newLedger,
            List<ReconciliationMessage> messages) results = (
            0m, AccountCopy.CopyBookOfAccounts(bookOfAccounts), Tax.CopyTaxLedger(taxLedger), []);
        
        var salesResults = InvestmentSales.SellInvestmentsToDollarAmountByPositionType(
            cashNeeded, positionType, results.newBookOfAccounts, results.newLedger, currentDate);
        results.amountMoved = salesResults.amountSold;
        results.newBookOfAccounts = salesResults.newBookOfAccounts;
        results.newLedger = salesResults.newLedger;
        
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.AddRange(salesResults.messages);
        results.messages.Add(new ReconciliationMessage(
            currentDate, results.amountMoved, $"Rebalance: Selling {positionType} investment"));
        return results;
    }
    
    public static (decimal amountSold, BookOfAccounts newAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages) 
        SellInOrder(decimal cashNeeded, McInvestmentPositionType[] pullOrder, BookOfAccounts bookOfAccounts,
            TaxLedger taxLedger, LocalDateTime currentDate)
    {
        if(cashNeeded <= 0) return (0m, bookOfAccounts, taxLedger, []);
        // set up return tuple
        (decimal amountSold, BookOfAccounts newAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages) 
            results = (0m, AccountCopy.CopyBookOfAccounts(bookOfAccounts), Tax.CopyTaxLedger(taxLedger), []); 
        
        // loop over the pull types until we have the bucks
        
        foreach (var positionType in pullOrder)
        {
            if (cashNeeded <= results.amountSold) return results;
            
            var localResults = MoveFromInvestmentToCash(
                results.newAccounts, cashNeeded - results.amountSold, positionType, currentDate, results.newLedger);
            results.amountSold += localResults.amountMoved;
            results.newAccounts = localResults.newBookOfAccounts;
            results.newLedger = localResults.newLedger;
            results.messages.AddRange(localResults.messages);
        }
        return results;
    }
    
    public static (decimal amountMoved, BookOfAccounts newAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages) 
        MoveLongToMidWithoutTaxeConsequences(decimal amountToMove, BookOfAccounts bookOfAccounts, TaxLedger taxLedger, 
            LocalDateTime currentDate, CurrentPrices prices)
    {
        /*
         * with either tax deferred or tax free accounts, you can sell and buy inside the account without tax
         * consequences. during rebalance from long to mid, this is ideal as it doesn't create any income
         */
        // set up return tuple
        (decimal amountMoved, BookOfAccounts newAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages) 
            results = (0m, AccountCopy.CopyBookOfAccounts(bookOfAccounts), Tax.CopyTaxLedger(taxLedger), []); 
        
        // pull all eligible positions
        var query = from account in results.newAccounts.InvestmentAccounts
                where (account.AccountType is McInvestmentAccountType.TRADITIONAL_401_K || 
                       account.AccountType is McInvestmentAccountType.TRADITIONAL_IRA) 
                from position in account.Positions
                where (position.IsOpen &&
                       position.InvestmentPositionType == McInvestmentPositionType.LONG_TERM)
                orderby (position.Entry)
                select (account, position)
            ;
        
        // loop over the pull types until we have the bucks
        
        foreach (var (account, position) in query)
        {
            if (results.amountMoved >= amountToMove) break;
            results.amountMoved += position.CurrentValue;
            
            // set it to mid-term
            position.InvestmentPositionType = McInvestmentPositionType.MID_TERM;
            
            // change its price
            var oldValue = position.CurrentValue;
            var newPrice = prices.CurrentMidTermInvestmentPrice;
            var newQuantity = oldValue / newPrice;
            position.Price = newPrice;
            position.Quantity = newQuantity;
            var newValue = position.CurrentValue;
            if(Math.Round(oldValue, 4) != Math.Round(newValue, 4))
                throw new InvalidDataException("Failed to set price of long-term investment to mid-term");
        }
        return results;
    }

    
    #endregion asset movement functions

    #region rebalance functions

    /// <summary>
    /// takes money out of cash account and puts it in a long-term position in the brokerage account
    /// </summary>
    public static (BookOfAccounts newBookOfAccounts, List<ReconciliationMessage> messages)
        InvestExcessCash(LocalDateTime currentDate, BookOfAccounts bookOfAccounts, CurrentPrices currentPrices,
            DataTypes.MonteCarlo.Model model, PgPerson person)
    {
        var reserveCashNeeded = 0m;
        if (CalculateWhetherItsCloseEnoughToRetirementToRebalance(currentDate, model))
        {
            reserveCashNeeded = Spend.CalculateCashNeedForNMonths(
                model, person, bookOfAccounts, currentDate, model.NumMonthsCashOnHand);
        }
        else
        {
            // just add a month's worth of cash needed to keep the sim from accidentally going bankrupt
            const int numMonths = 1;
            var debtSpend = bookOfAccounts.DebtAccounts
                .SelectMany(x => x.Positions
                    .Where(y => y.IsOpen))
                .Sum(x => x.MonthlyPayment);
            var cashNeededPerMonth = person.RequiredMonthlySpend + person.RequiredMonthlySpendHealthCare + debtSpend;

            reserveCashNeeded = cashNeededPerMonth * numMonths;
        }

        // check if we have any excess cash
        var cashOnHand = AccountCalculation.CalculateCashBalance(bookOfAccounts);
        var totalInvestmentAmount = cashOnHand - reserveCashNeeded;

        if (totalInvestmentAmount <= 0)
        {
            if (!MonteCarloConfig.DebugMode)return (bookOfAccounts, []);
            return (bookOfAccounts, [new ReconciliationMessage(
                currentDate, null, "We don't enough spare cash to invest.")]);
        }
        
        // set up return tuple
        (BookOfAccounts newBookOfAccounts, List<ReconciliationMessage> messages) results = (
            AccountCopy.CopyBookOfAccounts(bookOfAccounts), []);
    
        // we have excess cash, withdraw the cash so we can invest it
        var withdrawalResults = AccountCashManagement.TryWithdrawCash(
            bookOfAccounts, totalInvestmentAmount, currentDate); 
        if (withdrawalResults.isSuccessful == false) throw new InvalidDataException("Failed to withdraw excess cash");
        results.newBookOfAccounts = withdrawalResults.newAccounts;
        
        // now put it in long-term brokerage
        var investResults = Investment.InvestFunds(results.newBookOfAccounts, currentDate, totalInvestmentAmount,
            McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TAXABLE_BROKERAGE, currentPrices);
        results.newBookOfAccounts = investResults.accounts;
        
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.AddRange(withdrawalResults.messages);
        results.messages.AddRange(investResults.messages);
        results.messages.Add(new ReconciliationMessage(
            currentDate, totalInvestmentAmount,"Rebalance: added excess cash to long-term investment"));
        return results;
    }
    
    public static (BookOfAccounts newBookOfAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages)
        RebalanceLongToMid(LocalDateTime currentDate, BookOfAccounts bookOfAccounts, RecessionStats recessionStats,
            CurrentPrices currentPrices, DataTypes.MonteCarlo.Model model, TaxLedger taxLedger, PgPerson person)
    {
        // if it's been a good year, sell long-term growth assets and top-up mid-term.
        // if it's been a bad year, sit tight and hope the recession doesn't
        // outlast your mid-term bucket.

        List<ReconciliationMessage> messages = [];

        if(MonteCarloConfig.DebugMode)
            messages.Add(new ReconciliationMessage(
                currentDate, null, "Rebalance: moving long to mid"));
        
        // if we're in a recession, don't move from long to mid until we absolutely have to
        if (recessionStats.AreWeInARecession)
        {
            if(MonteCarloConfig.DebugMode) messages.Add(
                new ReconciliationMessage(currentDate, null, "In a recession; not moving long to mid."));
            return (bookOfAccounts, taxLedger, messages);
        }
        
        var numMonths = model.NumMonthsMidBucketOnHand; 
        if (numMonths <= 0) return (bookOfAccounts, taxLedger, messages);
        
       
        // figure out how much we want to have in the mid-bucket
        decimal amountOnHand = AccountCalculation.CalculateMidBucketTotalBalance(bookOfAccounts);
        var totalAmountNeeded = Spend.CalculateCashNeedForNMonths(model, person, bookOfAccounts,
            currentDate, numMonths);
        decimal amountNeededToMove = totalAmountNeeded - amountOnHand;
        
        
        // do we stiLl need to pull anything?
        if (amountNeededToMove <= 0)
        {
            if(MonteCarloConfig.DebugMode) messages.Add(new ReconciliationMessage(currentDate, null, "We don't need to move anymore money to mid"));
            return (bookOfAccounts, taxLedger, messages);
        }
        
        // not in a recession and we still need to move funds.
        
        // Set up the return tuple
        (BookOfAccounts newBookOfAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages) results = (
            AccountCopy.CopyBookOfAccounts(bookOfAccounts),Tax.CopyTaxLedger(taxLedger), messages);
        
        // try to move without tax consequences
        var moveResult = MoveLongToMidWithoutTaxeConsequences(
            amountNeededToMove, results.newBookOfAccounts, results.newLedger, currentDate, currentPrices);
        var amountMoved = moveResult.amountMoved;
        results.newBookOfAccounts = moveResult.newAccounts;
        results.newLedger = moveResult.newLedger;
        if(MonteCarloConfig.DebugMode) results.messages.AddRange(moveResult.messages);
        
        var amountToSell = Math.Max(0, amountNeededToMove - amountMoved);
        
        // first sell the long-term investments
        if (amountToSell > 0m)
        {
            var salesResults = SellInOrder(amountToSell, [McInvestmentPositionType.LONG_TERM],
                results.newBookOfAccounts,
                results.newLedger, currentDate);
            var amountSold = salesResults.amountSold;
            results.newBookOfAccounts = salesResults.newAccounts;
            results.newLedger = salesResults.newLedger;

            // now buy the mid-term investments, but you gotta first take out the cash
            var withdrawalResult = AccountCashManagement.TryWithdrawCash(
                results.newBookOfAccounts, amountSold, currentDate);
            if (withdrawalResult.isSuccessful == false)
                throw new InvalidDataException(
                    "Failed to withdraw cash to buy mid-term investment"); // shouldn't be here
            results.newBookOfAccounts = withdrawalResult.newAccounts;
            // now we can buy
            var buyResult = Investment.InvestFunds(results.newBookOfAccounts, currentDate, amountSold,
                McInvestmentPositionType.MID_TERM, McInvestmentAccountType.TAXABLE_BROKERAGE, currentPrices);
            results.newBookOfAccounts = buyResult.accounts;
            
            if(MonteCarloConfig.DebugMode)
            {
                results.messages.AddRange(salesResults.messages);
                results.messages.AddRange(withdrawalResult.messages);
                results.messages.AddRange(buyResult.messages);
            }
        }

        if(!MonteCarloConfig.DebugMode) return results;
        results.messages.Add(new ReconciliationMessage(
            currentDate, null, "Rebalance: done moving long to mid"));
        return results;
    }
    
    public static (BookOfAccounts newBookOfAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages)
        RebalanceMidOrLongToCash(LocalDateTime currentDate, BookOfAccounts bookOfAccounts,
            RecessionStats recessionStats, CurrentPrices currentPrices, DataTypes.MonteCarlo.Model model, TaxLedger taxLedger,
            decimal totalCashNeeded) 
    {
        /*
         * if it's been a good year, sell long-term growth assets and top-up cash. if it's been a bad year, sell
         * mid-term assets to top-up cash.
         */
        
        // set up the return tuple
        (BookOfAccounts newBookOfAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages) results = (
            AccountCopy.CopyBookOfAccounts(bookOfAccounts), Tax.CopyTaxLedger(taxLedger), []);
        
        if(MonteCarloConfig.DebugMode)
            results.messages.Add(new ReconciliationMessage(
                currentDate, null, "Rebalance: moving mid and/or long to cash"));
        
        var totalCashOnHand = AccountCalculation.CalculateCashBalance(bookOfAccounts);
        
        var cashNeededToBeMoved = totalCashNeeded - totalCashOnHand;
        
        if (cashNeededToBeMoved <= 0) return (bookOfAccounts, taxLedger, []); // you got enough, bruv
        
        
        // need to pull from one of the buckets. 
        if (recessionStats.AreWeInARecession)
        {
            
            // pull what we can from the mid-term bucket, then from long bucket
            McInvestmentPositionType[] pullOrder = [ McInvestmentPositionType.MID_TERM, McInvestmentPositionType.LONG_TERM ];
            var localResults = 
                SellInOrder(cashNeededToBeMoved, pullOrder, bookOfAccounts, taxLedger, currentDate);
            results.newBookOfAccounts = localResults.newAccounts;
            results.newLedger = localResults.newLedger;
            if (MonteCarloConfig.DebugMode)
            {
                results.messages.Add(new ReconciliationMessage(currentDate, null, "In a recession."));
                results.messages.AddRange(localResults.messages);
            }
        }
        else
        {
            // pull what we can from the long-term bucket, then from mid
            McInvestmentPositionType[] pullOrder = [ McInvestmentPositionType.LONG_TERM, McInvestmentPositionType.MID_TERM ];
            var localResults = SellInOrder(cashNeededToBeMoved, pullOrder, bookOfAccounts, taxLedger, currentDate);
            results.newBookOfAccounts = localResults.newAccounts;
            results.newLedger = localResults.newLedger;
            if (MonteCarloConfig.DebugMode)
            {
                results.messages.Add(
                    new ReconciliationMessage(currentDate, null, "Not in a recession."));
                results.messages.AddRange(localResults.messages);
            }
        }
        if(MonteCarloConfig.DebugMode)
            results.messages.Add(new ReconciliationMessage(
                currentDate, null, "Rebalance: done moving mid and/or long to cash"));
        return results;
    }
    
    /// <summary>
    /// move investments between long-term, mid-term, and cash, per retirement strategy defined in the sim parameters
    /// </summary>
    public static (BookOfAccounts newBookOfAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages) 
        RebalancePortfolio(LocalDateTime currentDate, BookOfAccounts bookOfAccounts, RecessionStats recessionStats,
            CurrentPrices currentPrices, DataTypes.MonteCarlo.Model model, TaxLedger taxLedger, PgPerson person)
    {
        if (!CalculateWhetherItsCloseEnoughToRetirementToRebalance(currentDate, model))
        {
            if (!MonteCarloConfig.DebugMode) return (bookOfAccounts, taxLedger, []);
            return (bookOfAccounts, taxLedger, [new ReconciliationMessage(
                currentDate, null, "Not close enough to retirement yet to rebalance")]);
        };
        if (!CalculateWhetherItsBucketRebalanceTime(currentDate, model))
        {
            if (!MonteCarloConfig.DebugMode) return (bookOfAccounts, taxLedger, []);
            return (bookOfAccounts, taxLedger, [new ReconciliationMessage(
                currentDate, null, "Not a rebalancing month")]);
        };

        // set up return tuple
        (BookOfAccounts newBookOfAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages) results = (
            AccountCopy.CopyBookOfAccounts(bookOfAccounts), Tax.CopyTaxLedger(taxLedger), []);
        if (MonteCarloConfig.DebugMode)
        {
            results.messages.Add(new ReconciliationMessage(
                currentDate, null, "Rebalance: time to move funds"));
        }
        
        var cashNeededOnHand =
            Spend.CalculateCashNeedForNMonths(model, person, results.newBookOfAccounts, currentDate,
                model.NumMonthsCashOnHand);

        if (cashNeededOnHand > 0)
        {
            // top up your cash bucket by moving from mid or long, depending on recession stats 
            var rebalanceCashResults = RebalanceMidOrLongToCash(
                currentDate, results.newBookOfAccounts, recessionStats, currentPrices, model, results.newLedger,
                cashNeededOnHand);
            results.newBookOfAccounts = rebalanceCashResults.newBookOfAccounts;
            results.newLedger = rebalanceCashResults.newLedger;
            results.messages.AddRange(rebalanceCashResults.messages);
        }

        // now move from long to mid, depending on recession stats
        var rebalanceMidResults = RebalanceLongToMid(
            currentDate, results.newBookOfAccounts, recessionStats, currentPrices, model, results.newLedger,
            person);
        results.newBookOfAccounts = rebalanceMidResults.newBookOfAccounts;
        results.newLedger = rebalanceMidResults.newLedger;
        results.messages.AddRange(rebalanceMidResults.messages);
        
        return results;
    }

    #endregion
}