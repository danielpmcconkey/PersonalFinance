using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.WithdrawalStrategy;

public enum WithdrawalStrategyType
{
    BasicBuckets = 1,
}
public interface IWithdrawalStrategy
{
    (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)
        SellInvestmentsToDollarAmount(
            BookOfAccounts accounts, TaxLedger ledger, LocalDateTime currentDate, decimal amountToSell,
            LocalDateTime? minDateExclusive = null, LocalDateTime? maxDateInclusive = null,
            McInvestmentPositionType? positionTypeOverride = null, McInvestmentAccountType? accountTypeOverride = null
            );

    (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages)
        SellInvestmentsToRmdAmount(decimal amountNeeded, BookOfAccounts bookOfAccounts, TaxLedger taxLedger,
            LocalDateTime currentDate);
}
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
            results = SellHelper(amountToSellWithinRoom, results.accounts, results.ledger, currentDate, 
                positionTypes, _salesOrderWithRoom, accountTypeOverride, minDateExclusive, maxDateInclusive);
        }
        if (results.amountSold >= amountToSell) return results;
        
        // we don't have any more income room and we still have sellin to do. sell taxed and tax-free positions, up to
        // the amountNeeded
        var amountStillNeeded = amountToSell - results.amountSold;
        var noRoomResult = SellHelper(amountStillNeeded, results.accounts, results.ledger, 
            currentDate, positionTypes, _salesOrderWithNoRoom, accountTypeOverride, minDateExclusive, maxDateInclusive);
        
        results.amountSold += noRoomResult.amountSold;
        results.accounts = noRoomResult.accounts;
        results.ledger = noRoomResult.ledger;
        
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.AddRange(noRoomResult.messages);
        results.messages.Add(new ReconciliationMessage(
            currentDate, results.amountSold, $"Amount sold in investment accounts"));

        return results;
    }
    
    /// <summary>
    /// sells enough full positions of the provided position type to reach the amountToSell, but specifically in the RMD
    /// sales account order. It ignores the income "head room". It deposits proceeds into the cash account.
    /// </summary>
    /// <returns>the exact amount sold, a new book of accounts, and a new ledger</returns>
    public (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages) 
        SellInvestmentsToRmdAmount(decimal amountNeeded, BookOfAccounts bookOfAccounts, TaxLedger taxLedger, 
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
            salesOrder,
            null, 
            currentDate.PlusYears(-1));
        if (salesResult.amountSold >= amountNeeded) return salesResult;
        if (Math.Abs(salesResult.amountSold - amountNeeded) < 1m) return salesResult; // call it a wash due to floating point math
        
        // nothing's left to try. not sure how we got here
        throw new InvalidDataException("RMD: Nothing left to try. Not sure how we got here");
    }

    private static (McInvestmentPositionType positionType, McInvestmentAccountType accountType)[] CreateSalesOrder(
        McInvestmentPositionType[] positionTypes, McInvestmentAccountType[] accountTypes)
    {
        var size = accountTypes.Length * positionTypes.Length;
        var result = new (McInvestmentPositionType positionType, McInvestmentAccountType accountType)[size];
        for (var i = 0; i < accountTypes.Length; i++)
            for (var j = 0; j < positionTypes.Length; j++)
                result[i * positionTypes.Length + j] = (positionTypes[j], accountTypes[i]);
        return result;
    }

    private static (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage>messages) 
        SellHelper(decimal amountToSell, BookOfAccounts accounts, TaxLedger ledger, LocalDateTime currentDate, 
            McInvestmentPositionType[] positionTypes, McInvestmentAccountType[] standardSalesOrder,
            McInvestmentAccountType? accountTypeOverride, LocalDateTime? minDateExclusive, 
            LocalDateTime? maxDateInclusive)
    {
        var accountTypes = accountTypeOverride is null 
            ? standardSalesOrder
            : [(McInvestmentAccountType) accountTypeOverride];
        var salesOrderWithRoom = CreateSalesOrder(positionTypes, accountTypes);
        return InvestmentSales.SellInvestmentsToDollarAmount(accounts, ledger, currentDate, amountToSell, salesOrderWithRoom,
            minDateExclusive, maxDateInclusive);
    }
}