using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class AccountCashManagement
{
    public static BookOfAccounts DepositCash(BookOfAccounts accounts, decimal amount, LocalDateTime currentDate)
    {
        if (amount < 0) throw new ArgumentException("Amount must not be negative");
        if (amount == 0) return accounts;

        var result = AccountCopy.CopyBookOfAccounts(accounts);
        
        var totalCash = AccountCalculation.CalculateCashBalance(result);
        totalCash += amount;
        result = UpdateCashAccountBalance(accounts, totalCash, currentDate);
        
        if (StaticConfig.MonteCarloConfig.DebugMode == false) return result;
        
        Reconciliation.AddMessageLine(currentDate, amount, "Generic cash deposit");
        
        return result;
    }
    
    public static BookOfAccounts UpdateCashAccountBalance(BookOfAccounts accounts, decimal newBalance, LocalDateTime currentDate)
    {
        var result = AccountCopy.CopyBookOfAccounts(accounts);
        result.Cash.Positions = [
            new McInvestmentPosition(){
                Id = Guid.NewGuid(),
                Entry = currentDate,
                Price = 1m,
                Quantity = newBalance,
                InitialCost = 0,
                InvestmentPositionType = McInvestmentPositionType.SHORT_TERM,
                IsOpen = true,
                Name = "default cash account"}
        ];
        return result;
    }

    /// <summary>
    /// Withdraws cash if there is enough. If not, does nothing and returns the accounts back as-is
    /// </summary>
    public static (bool isSuccessful, BookOfAccounts newAccounts) TryWithdrawCash(
        BookOfAccounts accounts, decimal amount, LocalDateTime currentDate)
    {
        var totalCashOnHand = AccountCalculation.CalculateCashBalance(accounts);
        if (totalCashOnHand >= amount)
        {
            // we gots it. Update and return
            var newBalance = totalCashOnHand - amount;
            var newAccounts = UpdateCashAccountBalance(accounts, newBalance, currentDate);
            if (MonteCarloConfig.DebugMode == true)
            {
                Reconciliation.AddMessageLine(currentDate, amount, "Cash withdrawal");
            }
            return (true, newAccounts);
        }
        return (false, accounts);
    }

    /// <summary>
    /// deduct cash from the cash account. If not enough in the cash account, take from investments
    /// </summary>
    /// <returns>true if able to pay. false if not</returns>
    public static (bool isSuccessful, BookOfAccounts newAccounts, TaxLedger newLedger) WithdrawCash(
        BookOfAccounts accounts, decimal amount, LocalDateTime currentDate, TaxLedger taxLedger)
    {
        var tryResult = TryWithdrawCash(accounts, amount, currentDate);
        if (tryResult.isSuccessful == true) return (true, tryResult.newAccounts, taxLedger);
        
        // set up a return tuple
        (bool isSuccessful, BookOfAccounts newAccounts, TaxLedger newLedger) result = (
            false, AccountCopy.CopyBookOfAccounts(accounts), Tax.CopyTaxLedger(taxLedger));
        
        // we don't have enough cash. Let's try to sell some what we need
        var totalCashOnHand = AccountCalculation.CalculateCashBalance(accounts);
        var amountStillNeeded = amount - totalCashOnHand;
        
        // can we pull it from the mid bucket?
        var localResult = InvestmentSales.SellInvestmentsToDollarAmountByPositionType(
            amountStillNeeded, McInvestmentPositionType.MID_TERM, result.newAccounts, result.newLedger, currentDate);
        result.newAccounts = localResult.newBookOfAccounts;
        result.newLedger = localResult.newLedger;
        
        // now do we have enough?
        tryResult = TryWithdrawCash(accounts, amount, currentDate);
        if (tryResult.isSuccessful == true) return (true, tryResult.newAccounts, result.newLedger);
        
        // still not enough. try from the long bucket
        localResult = InvestmentSales.SellInvestmentsToDollarAmountByPositionType(
            amountStillNeeded, McInvestmentPositionType.LONG_TERM, result.newAccounts, result.newLedger, currentDate);
        result.newAccounts = localResult.newBookOfAccounts;
        result.newLedger = localResult.newLedger;
        
        // now do we have enough?
        tryResult = TryWithdrawCash(accounts, amount, currentDate);
        if (tryResult.isSuccessful == true) return (true, tryResult.newAccounts, result.newLedger);
        
        // we broke. update the account balance just in case. returning false here should result in a bankruptcy
        UpdateCashAccountBalance(result.newAccounts, 0, currentDate);
        return result;
    }
}