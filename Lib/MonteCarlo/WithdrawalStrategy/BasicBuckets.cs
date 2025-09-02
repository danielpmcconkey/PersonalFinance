using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.WithdrawalStrategy;

public class BasicBuckets : IWithdrawalStrategy
{
    private static McInvestmentAccountType[] _salesOrderWithNoRoom = [
        // no tax, period
        McInvestmentAccountType.HSA,
        McInvestmentAccountType.ROTH_IRA,
        McInvestmentAccountType.ROTH_401_K,
        // tax on growth only
        McInvestmentAccountType.TAXABLE_BROKERAGE,
        // tax deferred
        McInvestmentAccountType.TRADITIONAL_401_K,
        McInvestmentAccountType.TRADITIONAL_IRA,
    ];
    private static McInvestmentAccountType[] _salesOrderWithRoom = [
        // tax deferred
        McInvestmentAccountType.TRADITIONAL_401_K,
        McInvestmentAccountType.TRADITIONAL_IRA,
        // tax on growth only
        McInvestmentAccountType.TAXABLE_BROKERAGE,
        // no tax, period
        McInvestmentAccountType.HSA,
        McInvestmentAccountType.ROTH_IRA,
        McInvestmentAccountType.ROTH_401_K,
    ];

    #region Interface implementation

    /// <summary>
    /// takes money out of cash account and puts it in a long-term position in the brokerage account
    /// </summary>
    public (BookOfAccounts accounts, List<ReconciliationMessage> messages)
        InvestExcessCash(
            LocalDateTime currentDate, BookOfAccounts accounts, CurrentPrices prices, Model model, PgPerson person)
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
        var investResults = InvestFundsWithoutCashWithdrawal(results.accounts, currentDate, 
            totalInvestmentAmount, McInvestmentAccountType.TAXABLE_BROKERAGE, prices);
        results.accounts = investResults.accounts;
        
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.AddRange(withdrawalResults.messages);
        results.messages.AddRange(investResults.messages);
        results.messages.Add(new ReconciliationMessage(
            currentDate, totalInvestmentAmount,"Rebalance: added excess cash to long-term investment"));
        return results;
    }

    /// <summary>
    /// The basic buckets strategy always puts money into long-term investments 
    /// </summary>
    public (BookOfAccounts accounts, List<ReconciliationMessage> messages) 
        InvestFundsWithoutCashWithdrawal(
            BookOfAccounts accounts, LocalDateTime currentDate, decimal dollarAmount,
            McInvestmentAccountType accountType, CurrentPrices prices)
    {
        return Investment.InvestFundsByAccountTypeAndPositionType(
            accounts, currentDate, dollarAmount, McInvestmentPositionType.LONG_TERM, accountType,
            prices);
    }

    /// <summary>
    /// move investments between long-term, mid-term, and cash, per retirement strategy defined in the sim parameters
    /// </summary>
    public (BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) 
        RebalancePortfolio(
        LocalDateTime currentDate, BookOfAccounts accounts, RecessionStats recessionStats, CurrentPrices currentPrices,
        Model model, TaxLedger ledger, PgPerson person)
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
            var rebalanceCashResults = RebalanceMidOrLongToCash(
                currentDate, results.accounts, recessionStats, currentPrices, model, results.ledger,
                cashNeededOnHand);
            results.accounts = rebalanceCashResults.accounts;
            results.ledger = rebalanceCashResults.ledger;
            results.messages.AddRange(rebalanceCashResults.messages);
        }

        // now move from long to mid, depending on recession stats
        var rebalanceMidResults = RebalanceLongToMid(
            currentDate, results.accounts, recessionStats, currentPrices, model, results.ledger,
            person);
        results.accounts = rebalanceMidResults.accounts;
        results.ledger = rebalanceMidResults.ledger;
        results.messages.AddRange(rebalanceMidResults.messages);
        
        return results;
    }

    /// <summary>
    /// Tries to do a tax-targeted sale of investments. If there's income room, sell tax deferred or taxed investments
    /// until you've run out of income room. Once you've exhausted your income room, sell tax free. Income room here
    /// means that you've jumped up to the higher tax bracket. In 2024 tax terms, that's 96k, when you jump from 12% to
    /// 22%. However, if accountTypeOverride is specified, the sales order will be ignored.
    /// </summary>
    /// <returns>
    /// Note, the amountSold in the return tuple may not be the same as the amountToSell value in the input
    /// parameters. This is because you many not have enough in the accounts. Handling that discrepancy should be up to
    /// the consuming method
    /// </returns>
    /// <exception cref="InvalidDataException"></exception>
    public (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage>messages)
        SellInvestmentsToDollarAmount(
            BookOfAccounts accounts, TaxLedger ledger, LocalDateTime currentDate, decimal amountToSell,
            LocalDateTime? minDateExclusive, LocalDateTime? maxDateInclusive, 
            McInvestmentPositionType? positionTypeOverride = null, McInvestmentAccountType? accountTypeOverride = null)
    {
        if (accounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        if (accounts.InvestmentAccounts.Count == 0) return (0, accounts, ledger, []);
        
        var oneYearAgo = currentDate.PlusYears(-1);
        
        // set up the return tuple
        (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) 
            results = (
                0M, 
                AccountCopy.CopyBookOfAccounts(accounts), 
                Tax.CopyTaxLedger(ledger), 
                []
                );
        var incomeRoom = TaxCalculation.CalculateIncomeRoom(ledger, currentDate);
        McInvestmentPositionType[] positionTypes = positionTypeOverride is null 
            ? [McInvestmentPositionType.LONG_TERM, McInvestmentPositionType.MID_TERM] 
            : [(McInvestmentPositionType) positionTypeOverride];

        if (incomeRoom > 0)
        {
            // we have income room. sell tax deferred positions, up to the incomeRoom amount
            var amountToSellWithinRoom = Math.Min(amountToSell, incomeRoom);
            var accountTypesWithRoom = accountTypeOverride is null ?
                _salesOrderWithRoom : 
                new [] { (McInvestmentAccountType) accountTypeOverride };
            results = SellHelper2(amountToSellWithinRoom, results.accounts, results.ledger, currentDate, 
                positionTypes, accountTypesWithRoom, true, minDateExclusive, maxDateInclusive);
        }
        if (results.amountSold >= amountToSell) return results;
        
        // we don't have any more income room and we still have sellin to do. sell taxed and tax-free positions, up to
        // the amountNeeded
        var amountStillNeeded = amountToSell - results.amountSold;
        var accountTypesNoRoom = accountTypeOverride is null ?
            _salesOrderWithNoRoom : 
            new [] { (McInvestmentAccountType) accountTypeOverride };
        var noRoomResult = SellHelper2(amountStillNeeded, results.accounts, results.ledger, 
            currentDate, positionTypes, accountTypesNoRoom, true, minDateExclusive, maxDateInclusive);
        
        results.amountSold += noRoomResult.amountSold;
        results.accounts = noRoomResult.accounts;
        results.ledger = noRoomResult.ledger;
        
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.AddRange(noRoomResult.messages);
        results.messages.Add(new ReconciliationMessage(
            currentDate, results.amountSold, $"Amount sold in investment accounts"));

        return results;
    }
    
    public (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) 
        SellInvestmentsToRmdAmount(
            decimal amountNeeded, BookOfAccounts accounts, TaxLedger taxLedger, LocalDateTime currentDate)
    {
       return SharedWithdrawalFunctions.SellInvestmentsToRmdAmountStandardBucketsStrat(
           amountNeeded, accounts, taxLedger, currentDate);
    }
    
    #endregion

    #region private methods
    
    private (BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)
        RebalanceLongToMid(LocalDateTime currentDate, BookOfAccounts accounts, RecessionStats recessionStats,
            CurrentPrices prices, DataTypes.MonteCarlo.Model model, TaxLedger ledger, PgPerson person)
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
            var salesResults = SellInvestmentsToDollarAmount(
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

    private (BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)
        RebalanceMidOrLongToCash(LocalDateTime currentDate, BookOfAccounts accounts,
            RecessionStats recessionStats, CurrentPrices prices, DataTypes.MonteCarlo.Model model, TaxLedger ledger,
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
            var localResults = SellInvestmentsToDollarAmount(results.accounts, results.ledger,
                currentDate, cashNeededToBeMoved, null, currentDate.PlusYears(-1), 
                McInvestmentPositionType.MID_TERM, null);
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
            var localResults = SellInvestmentsToDollarAmount(results.accounts, results.ledger, 
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
            localResults = SellInvestmentsToDollarAmount(results.accounts, results.ledger, 
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
    

    private static (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage>messages) 
        SellHelper2(decimal amountToSell, BookOfAccounts accounts, TaxLedger ledger, LocalDateTime currentDate, 
            McInvestmentPositionType[] positionTypes, McInvestmentAccountType[] accountTypes, bool orderByAccountType,
            LocalDateTime? minDateExclusive, LocalDateTime? maxDateInclusive)
    {
        var salesOrder = orderByAccountType ?
            InvestmentSales.CreateSalesOrderAccountTypeFirst(positionTypes, accountTypes) :
            InvestmentSales.CreateSalesOrderPositionTypeFirst(positionTypes, accountTypes);
        return InvestmentSales.SellInvestmentsToDollarAmount(accounts, ledger, currentDate, amountToSell, salesOrder,
            minDateExclusive, maxDateInclusive);
    }

    #endregion

    
}