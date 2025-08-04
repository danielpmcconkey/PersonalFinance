using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class AccountCashManagement
{
    public static (BookOfAccounts accounts, List<ReconciliationMessage> messages) DepositCash(
        BookOfAccounts accounts, decimal amount, LocalDateTime currentDate)
    {
        if (amount < 0) throw new ArgumentException("Amount must not be negative");
        if (amount == 0) return (accounts, []);

        (BookOfAccounts accounts, List<ReconciliationMessage> messages) result = 
            (AccountCopy.CopyBookOfAccounts(accounts), []);
        
        var totalCash = AccountCalculation.CalculateCashBalance(result.accounts);
        totalCash += amount;
        result.accounts = UpdateCashAccountBalance(accounts, totalCash, currentDate);
        
        if (!MonteCarloConfig.DebugMode) return result;
        
        result.messages.Add(new ReconciliationMessage(currentDate, amount, "Generic cash deposit"));
        
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
    public static (bool isSuccessful, BookOfAccounts newAccounts, List<ReconciliationMessage> messages) TryWithdrawCash(
        BookOfAccounts accounts, decimal amount, LocalDateTime currentDate)
    {
        var totalCashOnHand = AccountCalculation.CalculateCashBalance(accounts);
        if (totalCashOnHand < amount) return (false, accounts, []); // they hasn't gots it, precious
        
        // they gots it. Update and return
        (bool isSuccessful, BookOfAccounts newAccounts, List<ReconciliationMessage> messages) result = (
            true, accounts, []); // don't need to copy the accounts yet, we'll do it in the UpdateCashAccountBalance call
        
        var newBalance = totalCashOnHand - amount;
        result.newAccounts = UpdateCashAccountBalance(accounts, newBalance, currentDate);
        if (!MonteCarloConfig.DebugMode) return result;
        
        result.messages.Add(new ReconciliationMessage(currentDate, amount, "Cash withdrawal"));
        return result;
    }

    /// <summary>
    /// deduct cash from the cash account. If not enough in the cash account, take from investments
    /// </summary>
    /// <returns>true if able to pay. false if not</returns>
    public static (bool isSuccessful, BookOfAccounts newAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages) 
        WithdrawCash(BookOfAccounts accounts, decimal amount, LocalDateTime currentDate, TaxLedger taxLedger)
    {
        var tryResult = TryWithdrawCash(accounts, amount, currentDate);
        if (tryResult.isSuccessful == true) return (true, tryResult.newAccounts, taxLedger, []);
        
        // set up a return tuple
        (bool isSuccessful, BookOfAccounts newAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages)
            result = (false, AccountCopy.CopyBookOfAccounts(accounts), Tax.CopyTaxLedger(taxLedger), []);
        
        // we don't have enough cash. Let's try to sell some what we need
        var totalCashOnHand = AccountCalculation.CalculateCashBalance(accounts);
        var amountStillNeeded = amount - totalCashOnHand;
        
        // can we pull it from the mid bucket?
        var localResult = InvestmentSales.SellInvestmentsToDollarAmountByPositionType(
            amountStillNeeded, McInvestmentPositionType.MID_TERM, result.newAccounts, result.newLedger, currentDate);
        result.newAccounts = localResult.newBookOfAccounts;
        result.newLedger = localResult.newLedger;
        result.messages.AddRange(localResult.messages);
        
        // now do we have enough?
        tryResult = TryWithdrawCash(result.newAccounts, amount, currentDate);
        result.isSuccessful = tryResult.isSuccessful;
        result.newAccounts = tryResult.newAccounts;
        result.messages.AddRange(tryResult.messages);
        if (tryResult.isSuccessful) return result;
        
        // still not enough. try from the long bucket
        localResult = InvestmentSales.SellInvestmentsToDollarAmountByPositionType(
            amountStillNeeded, McInvestmentPositionType.LONG_TERM, result.newAccounts, result.newLedger, currentDate);
        result.newAccounts = localResult.newBookOfAccounts;
        result.newLedger = localResult.newLedger;
        result.messages.AddRange(localResult.messages);
        
        // now do we have enough?
        tryResult = TryWithdrawCash(result.newAccounts, amount, currentDate);
        result.isSuccessful = tryResult.isSuccessful;
        result.newAccounts = tryResult.newAccounts;
        result.messages.AddRange(tryResult.messages);
        if (tryResult.isSuccessful) return result;
        
        // we broke. update the account balance just in case. returning false here should result in a bankruptcy
        if(MonteCarloConfig.DebugMode) result.messages.Add(
            new ReconciliationMessage(currentDate, amount, "Cash withdrawal failed"));
        
        // set cash balance to zero just to make sure we don't cheat later
        UpdateCashAccountBalance(result.newAccounts, 0, currentDate);
        
        return result;
    }
}