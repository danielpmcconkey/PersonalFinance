using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.WithdrawalStrategy;

/// <summary>
/// Very similar to the BasicBucketsIncomeThreshold. However, on the withdrawal side, always pull from taxable bucket,
/// then tax deferred, and finally tax-free. This is done without regard to income threshold or where we are in the tax
/// brackets
/// </summary>
public class BasicBucketsTaxableFirst : IWithdrawalStrategy
{
    private static McInvestmentAccountType[] _salesOrder = [
        // tax on growth only
        McInvestmentAccountType.TAXABLE_BROKERAGE,
        // tax deferred
        McInvestmentAccountType.TRADITIONAL_401_K,
        McInvestmentAccountType.TRADITIONAL_IRA,
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

    public (BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)
        RebalancePortfolio(LocalDateTime currentDate, BookOfAccounts accounts, RecessionStats recessionStats,
            CurrentPrices currentPrices, Model model, TaxLedger ledger, PgPerson person)
    {
        return SharedWithdrawalFunctions.BasicBucketsRebalance(
            currentDate, accounts, recessionStats, currentPrices, model, ledger, person); 
    }

    public (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)
        SellInvestmentsToDollarAmount(
            BookOfAccounts accounts, TaxLedger ledger, LocalDateTime currentDate, decimal amountToSell,
            LocalDateTime? minDateExclusive = null, LocalDateTime? maxDateInclusive = null,
            McInvestmentPositionType? positionTypeOverride = null, McInvestmentAccountType? accountTypeOverride = null
        )
    {
        if (accounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        if (accounts.InvestmentAccounts.Count == 0) return (0, accounts, ledger, []);

        McInvestmentPositionType[] positionTypes = positionTypeOverride is null 
            ? [McInvestmentPositionType.LONG_TERM, McInvestmentPositionType.MID_TERM] 
            : [(McInvestmentPositionType) positionTypeOverride];
        var salesOrder = InvestmentSales.CreateSalesOrderAccountTypeFirst(
            positionTypes, _salesOrder);
        
        return InvestmentSales.SellInvestmentsToDollarAmount(accounts, ledger, currentDate, amountToSell, salesOrder,
            minDateExclusive, maxDateInclusive);
    }

    public (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) 
        SellInvestmentsToRmdAmount(
            decimal amountNeeded, BookOfAccounts accounts, TaxLedger ledger, LocalDateTime currentDate)
    {
        return SharedWithdrawalFunctions.BasicBucketsSellInvestmentsToRmdAmount(
            amountNeeded, accounts, ledger, currentDate);
    }

    #endregion
}