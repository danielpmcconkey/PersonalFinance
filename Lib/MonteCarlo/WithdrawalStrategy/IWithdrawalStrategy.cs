using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
using NodaTime;

namespace Lib.MonteCarlo.WithdrawalStrategy;


public interface IWithdrawalStrategy
{
    (BookOfAccounts accounts, List<ReconciliationMessage> messages)
        InvestExcessCash(LocalDateTime currentDate, BookOfAccounts accounts, CurrentPrices prices,
            Model model, PgPerson person);
    /// <summary>
    /// Invest funds per the withdrawal strategy rules. Assumes that the cash has already been taken out of the cash
    /// account. This is because this method is sometimes used for investing extra cash, but also used to invest
    /// paycheck contributions (like 401k, HSA, etc.)
    /// </summary>
    (BookOfAccounts accounts, List<ReconciliationMessage> messages) 
        InvestFundsWithoutCashWithdrawal(BookOfAccounts accounts, LocalDateTime currentDate, decimal dollarAmount,
            McInvestmentAccountType accountType, CurrentPrices prices);

    (BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)
        RebalancePortfolio(LocalDateTime currentDate, BookOfAccounts accounts, RecessionStats recessionStats,
            CurrentPrices currentPrices, Model model, TaxLedger ledger, PgPerson person);
    
    (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)
        SellInvestmentsToDollarAmount(
            BookOfAccounts accounts, TaxLedger ledger, LocalDateTime currentDate, decimal amountToSell,
            LocalDateTime? minDateExclusive = null, LocalDateTime? maxDateInclusive = null,
            McInvestmentPositionType? positionTypeOverride = null, McInvestmentAccountType? accountTypeOverride = null
            );

    (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)
        SellInvestmentsToRmdAmount(decimal amountNeeded, BookOfAccounts bookOfAccounts, TaxLedger taxLedger,
            LocalDateTime currentDate);
    
    
}
