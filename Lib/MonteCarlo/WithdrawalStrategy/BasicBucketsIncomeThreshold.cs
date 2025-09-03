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
        return SharedWithdrawalFunctions.InvestExcessCashIntoLongTermBrokerage(
            currentDate, accounts, prices, model, person);
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
            LocalDateTime currentDate, BookOfAccounts accounts, RecessionStats recessionStats,
            CurrentPrices currentPrices, Model model, TaxLedger ledger, PgPerson person)
    {
       return SharedWithdrawalFunctions.BasicBucketsRebalance(
           currentDate, accounts, recessionStats, currentPrices, model, ledger, person); 
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
            decimal amountNeeded, BookOfAccounts accounts, TaxLedger ledger, LocalDateTime currentDate)
    {
       return SharedWithdrawalFunctions.BasicBucketsSellInvestmentsToRmdAmount(
           amountNeeded, accounts, ledger, currentDate);
    }
    
    #endregion

    #region private methods

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