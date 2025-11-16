using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.WithdrawalStrategy;

/// <summary>
/// This strategy mimics the tax-optimized strategy from the BasicBucketsIncomeThreshold strategy, but without having a
/// mid bucket
/// </summary>
public class NoMidIncomeThreshold : IWithdrawalStrategy
{
     #region Interface implementation

    /// <summary>
    /// takes money out of cash account and puts it in a long-term position in the brokerage account
    /// </summary>
    public (BookOfAccounts accounts, List<ReconciliationMessage> messages)
        InvestExcessCash(
            LocalDateTime currentDate, BookOfAccounts accounts, CurrentPrices prices, Model model, PgPerson person)
    {
        return SharedWithdrawalFunctions.InvestExcessCashIntoLongTermBrokerage(
            currentDate, accounts, prices, model, person);
    }

    /// <summary>
    /// The basic buckets strategy always puts money into long-term investments 
    /// </summary>
    public (BookOfAccounts accounts, List<ReconciliationMessage> messages) 
        InvestFundsWithoutCashWithdrawal(
            BookOfAccounts accounts, LocalDateTime currentDate, decimal dollarAmount,
            McInvestmentAccountType accountType, CurrentPrices prices, Model model)
    {
        return Investment.InvestFundsByAccountTypeAndPositionType(
            accounts, currentDate, dollarAmount, McInvestmentPositionType.LONG_TERM, accountType,
            prices);
    }

    
    /// <summary>
    /// just manages the cash bucket amount. always withdraw from long bucket. period
    /// </summary>
    public (BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) 
        RebalancePortfolio(
            LocalDateTime currentDate, BookOfAccounts accounts, RecessionStats recessionStats,
            CurrentPrices currentPrices, Model model, TaxLedger ledger, PgPerson person)
    {
       // return SharedWithdrawalFunctions.BasicBucketsRebalance(
       //     currentDate, accounts, recessionStats, currentPrices, model, ledger, person); 
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
       
       // check if we have enough cash already
       var cashNeeded =
           Spend.CalculateCashNeedForNMonths(model, person, accounts, currentDate, model.NumMonthsCashOnHand);
       var cashWeHave = AccountCalculation.CalculateCashBalance(accounts);
       var cashNeededToBeMoved = cashNeeded - cashWeHave;
       if (cashNeededToBeMoved <= 0)
       {
           if (!MonteCarloConfig.DebugMode) return (accounts, ledger, []);
           return (accounts, ledger, [new ReconciliationMessage(
               currentDate, null, "We already have enough cash")]);
       }
       
       // gotta sell stuff. set up the return tuple
       (BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) results = (
           AccountCopy.CopyBookOfAccounts(accounts), Tax.CopyTaxLedger(ledger), []);
       if (MonteCarloConfig.DebugMode)
       {
           results.messages.Add(new ReconciliationMessage(
               currentDate, null, "Rebalance: time to move funds"));
       }
       var sellResults = model.WithdrawalStrategy.SellInvestmentsToDollarAmount(results.accounts, results.ledger, 
           currentDate, cashNeededToBeMoved, model, null, currentDate.PlusYears(-1),
           McInvestmentPositionType.LONG_TERM, null);
       results.accounts = sellResults.accounts;
       results.ledger = sellResults.ledger;
       if (MonteCarloConfig.DebugMode)
       {
           results.messages.Add(new ReconciliationMessage(currentDate, null, "Rebalanced long to cash."));
           results.messages.AddRange(sellResults.messages);
       }

       return results;
    }

    
    public (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage>messages)
        SellInvestmentsToDollarAmount(
            BookOfAccounts accounts, TaxLedger ledger, LocalDateTime currentDate, decimal amountToSell, Model model,
            LocalDateTime? minDateExclusive, LocalDateTime? maxDateInclusive, 
            McInvestmentPositionType? positionTypeOverride = null, McInvestmentAccountType? accountTypeOverride = null)
    {
        return SharedWithdrawalFunctions.IncomeThreasholdSellInvestmentsToDollarAmount(
            accounts, ledger, currentDate, amountToSell, model, minDateExclusive, maxDateInclusive, 
            positionTypeOverride, accountTypeOverride);
    }
    
    public (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) 
        SellInvestmentsToRmdAmount(
            decimal amountNeeded, BookOfAccounts accounts, TaxLedger ledger, LocalDateTime currentDate, Model model)
    {
        if (accounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        if (accounts.InvestmentAccounts.Count == 0) return (0, accounts, ledger, []);
        
        (McInvestmentPositionType positionType, McInvestmentAccountType accountType)[] salesOrder = [
            (McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TRADITIONAL_401_K),
            (McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TRADITIONAL_IRA),
        ];
        
        var salesResult = InvestmentSales.SellInvestmentsToDollarAmount(
            AccountCopy.CopyBookOfAccounts(accounts),
            Tax.CopyTaxLedger(ledger),
            currentDate, 
            amountNeeded, 
            salesOrder);
        if (salesResult.amountSold >= amountNeeded) return salesResult;
        if (Math.Abs(salesResult.amountSold - amountNeeded) < 1m) return salesResult; // call it a wash due to floating point math
        
        // nothing's left to try. not sure how we got here
        throw new InvalidDataException("RMD: Nothing left to try. Not sure how we got here");
    }
    
    #endregion
}