using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.WithdrawalStrategy;

public static class SharedWithdrawalFunctions
{
    #region Basic Buckets Shared Functions
    
    /// <summary>
    /// This is most of the basic bucket rebalance logic, but it calls the model's specific
    /// SellInvestmentsToDollarAmount when it comes time to sell investments
    /// </summary>
    public static (BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)  
        BasicBucketsRebalance(
            LocalDateTime currentDate, BookOfAccounts accounts, RecessionStats recessionStats,
            CurrentPrices currentPrices, Model model, TaxLedger ledger, PgPerson person)
    {
         if (!Rebalance.CalculateWhetherItsCloseEnoughToRetirementToRebalance(currentDate, model))
        {
            if (!MonteCarloConfig.DebugMode) return (accounts, ledger, []);
            return (accounts, ledger, [new ReconciliationMessage(
                currentDate, null, "Not close enough to retirement yet to rebalance")]);
        };
        if (!Rebalance.CalculateWhetherItsBucketRebalanceTime(currentDate, model))
        {
            if (!MonteCarloConfig.DebugMode) return (accounts, ledger, []);
            return (accounts, ledger, [new ReconciliationMessage(
                currentDate, null, "Not a rebalancing month")]);
        };
        
        // set up return tuple
        (BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) results = (
            AccountCopy.CopyBookOfAccounts(accounts), Tax.CopyTaxLedger(ledger), []);
        if (MonteCarloConfig.DebugMode)
        {
            results.messages.Add(new ReconciliationMessage(
                currentDate, null, "Rebalance: time to move funds"));
        }
        
        var cashNeededOnHand =
            Spend.CalculateCashNeedForNMonths(model, person, results.accounts, currentDate,
                model.NumMonthsCashOnHand);

        if (cashNeededOnHand > 0)
        {
            // top up your cash bucket by moving from mid or long, depending on recession stats 
            var rebalanceCashResults = BasicBucketsRebalanceMidOrLongToCash(
                currentDate, results.accounts, recessionStats, currentPrices, model, results.ledger,
                cashNeededOnHand);
            results.accounts = rebalanceCashResults.accounts;
            results.ledger = rebalanceCashResults.ledger;
            results.messages.AddRange(rebalanceCashResults.messages);
        }

        // now move from long to mid, depending on recession stats
        var rebalanceMidResults = BasicBucketsRebalanceLongToMid(
            currentDate, results.accounts, recessionStats, currentPrices, model, results.ledger,
            person);
        results.accounts = rebalanceMidResults.accounts;
        results.ledger = rebalanceMidResults.ledger;
        results.messages.AddRange(rebalanceMidResults.messages);
        
        return results;
    }
    
    private static (BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)
        BasicBucketsRebalanceLongToMid(
            LocalDateTime currentDate, BookOfAccounts accounts, RecessionStats recessionStats, CurrentPrices prices,
            Model model, TaxLedger ledger, PgPerson person)
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
            return (accounts, ledger, messages);
        }
        
        var numMonths = model.NumMonthsMidBucketOnHand; 
        if (numMonths <= 0) return (accounts, ledger, messages);
        
       
        // figure out how much we want to have in the mid-bucket
        decimal amountOnHand = AccountCalculation.CalculateMidBucketTotalBalance(accounts);
        var totalAmountNeeded = Spend.CalculateCashNeedForNMonths(model, person, accounts,
            currentDate, numMonths);
        decimal amountNeededToMove = totalAmountNeeded - amountOnHand;
        
        
        // do we stiLl need to pull anything?
        if (amountNeededToMove <= 0)
        {
            if(MonteCarloConfig.DebugMode) messages.Add(new ReconciliationMessage(
                currentDate, null, "We don't need to move anymore money to mid"));
            return (accounts, ledger, messages);
        }
        
        // not in a recession and we still need to move funds.
        
        // Set up the return tuple
        (BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) results = (
            AccountCopy.CopyBookOfAccounts(accounts),Tax.CopyTaxLedger(ledger), messages);
        
        // try to move without tax consequences
        var moveResult = Rebalance.MoveLongToMidWithoutTaxConsequences(
            amountNeededToMove, results.accounts, results.ledger, currentDate, prices);
        var amountMoved = moveResult.amountMoved;
        results.accounts = moveResult.accounts;
        results.ledger = moveResult.ledger;
        if(MonteCarloConfig.DebugMode) results.messages.AddRange(moveResult.messages);
        
        var amountToSell = Math.Max(0, amountNeededToMove - amountMoved);
        
        // first sell the long-term investments
        if (amountToSell > 0m)
        {
            var salesResults = model.WithdrawalStrategy.SellInvestmentsToDollarAmount(
                results.accounts, results.ledger, currentDate, amountToSell, null,
                currentDate.PlusYears(-1), McInvestmentPositionType.LONG_TERM, null);
            var amountSold = salesResults.amountSold;
            results.accounts = salesResults.accounts;
            results.ledger = salesResults.ledger;

            // now buy the mid-term investments, but you gotta first take out the cash
            var withdrawalResult = AccountCashManagement.TryWithdrawCash(
                results.accounts, amountSold, currentDate);
            if (withdrawalResult.isSuccessful == false)
                throw new InvalidDataException(
                    "Failed to withdraw cash to buy mid-term investment"); // shouldn't be here
            results.accounts = withdrawalResult.newAccounts;
            // now we can buy
            var buyResult = Investment.InvestFundsByAccountTypeAndPositionType(
                results.accounts, currentDate, amountSold, McInvestmentPositionType.MID_TERM, 
                McInvestmentAccountType.TAXABLE_BROKERAGE, prices);
            results.accounts = buyResult.accounts;
            
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
    
    
    private static (BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)
        BasicBucketsRebalanceMidOrLongToCash(LocalDateTime currentDate, BookOfAccounts accounts,
            RecessionStats recessionStats, CurrentPrices prices, Model model, TaxLedger ledger,
            decimal totalCashNeeded) 
    {
        /*
         * if it's been a good year, sell long-term growth assets and top-up cash. if it's been a bad year, sell
         * mid-term assets to top-up cash.
         */
        
        // set up the return tuple
        (BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) results = (
            AccountCopy.CopyBookOfAccounts(accounts), Tax.CopyTaxLedger(ledger), []);
        
        if(MonteCarloConfig.DebugMode)
            results.messages.Add(new ReconciliationMessage(
                currentDate, null, "Rebalance: moving mid and/or long to cash"));
        
        var totalCashOnHand = AccountCalculation.CalculateCashBalance(accounts);
        
        var cashNeededToBeMoved = totalCashNeeded - totalCashOnHand;
        
        if (cashNeededToBeMoved <= 0) return (accounts, ledger, []); // you got enough, bruv
        
        
        // need to pull from one of the buckets. 
        if (recessionStats.AreWeInARecession)
        {
            // pull what we can from the mid-term bucket, don't touch the long unless we have to (and we'll do that when
            // we try to withdraw cash)
            var localResults = model.WithdrawalStrategy.SellInvestmentsToDollarAmount(
                results.accounts, results.ledger, currentDate, cashNeededToBeMoved, null, 
                currentDate.PlusYears(-1), McInvestmentPositionType.MID_TERM, null);
            results.accounts = localResults.accounts;
            results.ledger = localResults.ledger;
            if (MonteCarloConfig.DebugMode)
            {
                results.messages.Add(new ReconciliationMessage(currentDate, null, "In a recession."));
                results.messages.AddRange(localResults.messages);
            }
            if(MonteCarloConfig.DebugMode)
                results.messages.Add(new ReconciliationMessage(
                    currentDate, null, "Rebalance: done moving mid to cash. Didn't sell long."));
            return results;
        }
        else
        {
            // not in a recession. pull what we can from the long-term bucket first, then from mid if we still need to
            var localResults = model.WithdrawalStrategy.SellInvestmentsToDollarAmount(results.accounts, results.ledger, 
                currentDate, cashNeededToBeMoved, null, currentDate.PlusYears(-1),
                McInvestmentPositionType.LONG_TERM, null);
            results.accounts = localResults.accounts;
            results.ledger = localResults.ledger;
            if (MonteCarloConfig.DebugMode)
            {
                results.messages.Add(new ReconciliationMessage(currentDate, null, "Reballanced long to cash. Not in a recession."));
                results.messages.AddRange(localResults.messages);
            }
            
            cashNeededToBeMoved = cashNeededToBeMoved - localResults.amountSold;
            if (cashNeededToBeMoved <= 0) return (results.accounts, results.ledger, []); // you got enough, bruv
            
            // still hungry, hungry, hippo. Sell from mid
            localResults = model.WithdrawalStrategy.SellInvestmentsToDollarAmount(results.accounts, results.ledger, 
                currentDate, cashNeededToBeMoved, null, currentDate.PlusYears(-1),
                McInvestmentPositionType.MID_TERM, null);
            results.accounts = localResults.accounts;
            results.ledger = localResults.ledger;
            if (MonteCarloConfig.DebugMode)
            {
                results.messages.Add(new ReconciliationMessage(currentDate, null, "Reballanced mid to cash. Not in a recession."));
                results.messages.AddRange(localResults.messages);
            }
        }
        if(MonteCarloConfig.DebugMode)
            results.messages.Add(new ReconciliationMessage(
                currentDate, null, "Rebalance: done moving mid and/or long to cash"));
        return results;
    }

    #endregion

    /// <summary>
    /// used for investing excess cash. let's you know how much excess you have to invest
    /// </summary>
    public static decimal CalculateExcessCash(LocalDateTime currentDate, BookOfAccounts accounts, 
        Model model, PgPerson person)
    {
        var reserveCashNeeded = 0m;
        if (Rebalance.CalculateWhetherItsCloseEnoughToRetirementToRebalance(currentDate, model))
        {
            reserveCashNeeded = Spend.CalculateCashNeedForNMonths(
                model, person, accounts, currentDate, model.NumMonthsCashOnHand);
        }
        else
        {
            // just add a month's worth of cash needed to keep the sim from accidentally going bankrupt
            const int numMonths = 1;
            var debtSpend = accounts.DebtAccounts
                .SelectMany(x => x.Positions
                    .Where(y => y.IsOpen))
                .Sum(x => x.MonthlyPayment);
            var cashNeededPerMonth = person.RequiredMonthlySpend + person.RequiredMonthlySpendHealthCare + debtSpend;

            reserveCashNeeded = cashNeededPerMonth * numMonths;
        }

        // check if we have any excess cash
        var cashOnHand = AccountCalculation.CalculateCashBalance(accounts);
        var totalInvestmentAmount = cashOnHand - reserveCashNeeded;
        return totalInvestmentAmount;
    }
    
    
    /// <summary>
    /// takes money out of cash account and puts it in a long-term position in the brokerage account
    /// </summary>
    public static (BookOfAccounts accounts, List<ReconciliationMessage> messages)
        InvestExcessCashIntoLongTermBrokerage(
            LocalDateTime currentDate, BookOfAccounts accounts, CurrentPrices prices, Model model, PgPerson person)
    {
        var totalInvestmentAmount = CalculateExcessCash(currentDate, accounts, model, person);

        if (totalInvestmentAmount <= 0)
        {
            if (!MonteCarloConfig.DebugMode)return (accounts, []);
            return (accounts, [new ReconciliationMessage(
                currentDate, null, "We don't enough spare cash to invest.")]);
        }
        
        // set up return tuple
        (BookOfAccounts accounts, List<ReconciliationMessage> messages) results = (
            AccountCopy.CopyBookOfAccounts(accounts), []);
    
        // we have excess cash, withdraw the cash so we can invest it
        var withdrawalResults = AccountCashManagement.TryWithdrawCash(
            accounts, totalInvestmentAmount, currentDate); 
        if (withdrawalResults.isSuccessful == false) throw new InvalidDataException("Failed to withdraw excess cash");
        results.accounts = withdrawalResults.newAccounts;
        
        // now put it in long-term brokerage
        var investResults = Investment.InvestFundsByAccountTypeAndPositionType(
            results.accounts, currentDate, totalInvestmentAmount, McInvestmentPositionType.LONG_TERM, 
            McInvestmentAccountType.TAXABLE_BROKERAGE, prices);
        results.accounts = investResults.accounts;
        
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.AddRange(withdrawalResults.messages);
        results.messages.AddRange(investResults.messages);
        results.messages.Add(new ReconciliationMessage(
            currentDate, totalInvestmentAmount,"Rebalance: added excess cash to long-term investment"));
        return results;
    }
    
    private static (decimal amountMoved, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)
        MoveFromInvestmentToCash(BookOfAccounts accounts, decimal cashNeeded, McInvestmentPositionType positionType,
            LocalDateTime currentDate, TaxLedger ledger, Lib.DataTypes.MonteCarlo.Model model)
    {
        // set up the return tuple
        (decimal amountMoved, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) results = (
            0m, AccountCopy.CopyBookOfAccounts(accounts), Tax.CopyTaxLedger(ledger), []);

        var salesResults = model.WithdrawalStrategy.SellInvestmentsToDollarAmount(
            accounts, ledger, currentDate, cashNeeded, null, currentDate.PlusYears(-1),
            positionType, null);
        results.amountMoved = salesResults.amountSold;
        results.accounts = salesResults.accounts;
        results.ledger = salesResults.ledger;
        
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.AddRange(salesResults.messages);
        results.messages.Add(new ReconciliationMessage(
            currentDate, results.amountMoved, $"Rebalance: Selling {positionType} investment"));
        return results;
    }
    
    
    
    

    
    
    public static (decimal amountSold, BookOfAccounts newAccounts, TaxLedger newLedger, 
        List<ReconciliationMessage> messages) 
        SellInOrder(decimal cashNeeded, McInvestmentPositionType[] pullOrder, BookOfAccounts bookOfAccounts,
            TaxLedger taxLedger, LocalDateTime currentDate, Model model)
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
                results.newAccounts, cashNeeded - results.amountSold, positionType, currentDate, 
                results.newLedger, model);
            results.amountSold += localResults.amountMoved;
            results.newAccounts = localResults.accounts;
            results.newLedger = localResults.ledger;
            results.messages.AddRange(localResults.messages);
        }
        return results;
    }
    
    /// <summary>
    /// sells enough full positions of the provided position type to reach the amountToSell, but specifically in the RMD
    /// sales account order. It ignores the income "head room". It deposits proceeds into the cash account.
    /// </summary>
    /// <returns>the exact amount sold, a new book of accounts, and a new ledger</returns>
    public static (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) 
        BasicBucketsSellInvestmentsToRmdAmount(decimal amountNeeded, BookOfAccounts bookOfAccounts, TaxLedger taxLedger, 
            LocalDateTime currentDate)
    {
        if (bookOfAccounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        if (bookOfAccounts.InvestmentAccounts.Count == 0) return (0, bookOfAccounts, taxLedger, []);
        
        (McInvestmentPositionType positionType, McInvestmentAccountType accountType)[] salesOrder = [
            (McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TRADITIONAL_401_K),
            (McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TRADITIONAL_IRA),
            (McInvestmentPositionType.MID_TERM, McInvestmentAccountType.TRADITIONAL_401_K),
            (McInvestmentPositionType.MID_TERM, McInvestmentAccountType.TRADITIONAL_IRA),
        ];
        
        var salesResult = InvestmentSales.SellInvestmentsToDollarAmount(
            AccountCopy.CopyBookOfAccounts(bookOfAccounts),
            Tax.CopyTaxLedger(taxLedger),
            currentDate, 
            amountNeeded, 
            salesOrder);
        if (salesResult.amountSold >= amountNeeded) return salesResult;
        if (Math.Abs(salesResult.amountSold - amountNeeded) < 1m) return salesResult; // call it a wash due to floating point math
        
        // nothing's left to try. not sure how we got here
        throw new InvalidDataException("RMD: Nothing left to try. Not sure how we got here");
    }
    
}