using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.WithdrawalStrategy;

/// <summary>
/// This is the OG strategy. You have cash bucket, mid-term bucket, and long-term bucket. Every rebalance period, you
/// try to make sure you have enough cash for the next N months. You do this by pulling from the long bucket in good
/// years and pulling from the mid bucket in not-so good years. During the good years, you also top up the mid bucket
/// from the long bucket. The idea is that you never have to sell your long-term stocks during a down period.
///
/// You also have a withdrawal strategy that tries to pull from tax-deferred funds until you've reached the expensive
/// tax bracket, then try to pull from capital gains or tax free above the income room threshold. 
/// </summary>
public class BasicBucketsIncomeThreshold : IWithdrawalStrategy
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
    /// move investments between long-term, mid-term, and cash, per retirement strategy defined in the sim parameters
    /// </summary>
    public (BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) 
        RebalancePortfolio(
            LocalDateTime currentDate, BookOfAccounts accounts, RecessionStats recessionStats,
            CurrentPrices currentPrices, Model model, TaxLedger ledger, PgPerson person)
    {
       return SharedWithdrawalFunctions.BasicBucketsRebalance(
           currentDate, accounts, recessionStats, currentPrices, model, ledger, person); 
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
       return SharedWithdrawalFunctions.BasicBucketsSellInvestmentsToRmdAmount(
           amountNeeded, accounts, ledger, currentDate);
    }
    
    #endregion

    #region private methods

    

    #endregion

    
}