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
        if (amount < 0) throw new ArgumentException("Withdrawal amount must not be negative");
        if (amount == 0) return (true, accounts, []);
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
    public static (bool isSuccessful, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) 
        WithdrawCash(BookOfAccounts accounts, decimal amount, LocalDateTime currentDate, TaxLedger taxLedger, 
            Lib.DataTypes.MonteCarlo.Model model)
    {
        var tryResult = TryWithdrawCash(accounts, amount, currentDate);
        if (tryResult.isSuccessful == true) return (true, tryResult.newAccounts, taxLedger, []);
        
        // We don't have enough cash. We'll have to try to sell some what we need
        
        // set up a return tuple
        (bool isSuccessful, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)
            result = (false, AccountCopy.CopyBookOfAccounts(accounts), Tax.CopyTaxLedger(taxLedger), []);
        
        var totalCashOnHand = AccountCalculation.CalculateCashBalance(accounts);
        var amountStillNeeded = amount - totalCashOnHand;
        
        // can we pull it from the mid-range bucket's long-term holdings?
        var localResult = model.WithdrawalStrategy.SellInvestmentsToDollarAmount(
            result.accounts, result.ledger, currentDate, amountStillNeeded, model, 
            minDateExclusive: null,
            maxDateInclusive: currentDate.PlusYears(-1),
            positionTypeOverride: McInvestmentPositionType.MID_TERM,
            accountTypeOverride: null);
        result.accounts = localResult.accounts;
        result.ledger = localResult.ledger;
        result.messages.AddRange(localResult.messages);
        
        // now do we have enough?
        tryResult = TryWithdrawCash(result.accounts, amount, currentDate);
        result.isSuccessful = tryResult.isSuccessful;
        result.accounts = tryResult.newAccounts;
        result.messages.AddRange(tryResult.messages);
        if (tryResult.isSuccessful) return result;
        
        // still not enough. try from the long-range bucket's long-term holdings
        totalCashOnHand = AccountCalculation.CalculateCashBalance(accounts);
        amountStillNeeded = amount - totalCashOnHand;
        
        localResult = model.WithdrawalStrategy.SellInvestmentsToDollarAmount(
            result.accounts, result.ledger, currentDate, amountStillNeeded, model, 
            minDateExclusive: null,
            maxDateInclusive: currentDate.PlusYears(-1),
            positionTypeOverride: McInvestmentPositionType.LONG_TERM,
            accountTypeOverride: null);
        result.accounts = localResult.accounts;
        result.ledger = localResult.ledger;
        result.messages.AddRange(localResult.messages);
        
        // now do we have enough?
        tryResult = TryWithdrawCash(result.accounts, amount, currentDate);
        result.isSuccessful = tryResult.isSuccessful;
        result.accounts = tryResult.newAccounts;
        result.messages.AddRange(tryResult.messages);
        if (tryResult.isSuccessful) return result;
        
        // It's getting ugly. Let's try for short-term capital gains hits on the mid bucket
        totalCashOnHand = AccountCalculation.CalculateCashBalance(accounts);
        amountStillNeeded = amount - totalCashOnHand;
        
        localResult = model.WithdrawalStrategy.SellInvestmentsToDollarAmount(
            result.accounts, result.ledger, currentDate, amountStillNeeded, model, 
            minDateExclusive: null,
            maxDateInclusive: null,
            positionTypeOverride: McInvestmentPositionType.MID_TERM,
            accountTypeOverride: null);
        result.accounts = localResult.accounts;
        result.ledger = localResult.ledger;
        result.messages.AddRange(localResult.messages);
        
        // now do we have enough?
        tryResult = TryWithdrawCash(result.accounts, amount, currentDate);
        result.isSuccessful = tryResult.isSuccessful;
        result.accounts = tryResult.newAccounts;
        result.messages.AddRange(tryResult.messages);
        if (tryResult.isSuccessful) return result;
        
        // Last chance. Let's try for no filters at all
        totalCashOnHand = AccountCalculation.CalculateCashBalance(accounts);
        amountStillNeeded = amount - totalCashOnHand;
        
        localResult = model.WithdrawalStrategy.SellInvestmentsToDollarAmount(
            result.accounts, result.ledger, currentDate, amountStillNeeded, model, 
            minDateExclusive: null,
            maxDateInclusive: null,
            positionTypeOverride: null,
            accountTypeOverride: null);
        result.accounts = localResult.accounts;
        result.ledger = localResult.ledger;
        result.messages.AddRange(localResult.messages);
        
        // now do we have enough?
        tryResult = TryWithdrawCash(result.accounts, amount, currentDate);
        result.isSuccessful = tryResult.isSuccessful;
        result.accounts = tryResult.newAccounts;
        result.messages.AddRange(tryResult.messages);
        if (tryResult.isSuccessful) return result;
        
        // we broke. update the account balance just in case. returning false here should result in a bankruptcy
        if(MonteCarloConfig.DebugMode) result.messages.Add(
            new ReconciliationMessage(currentDate, amount, "Cash withdrawal failed"));
        
        // set cash balance to zero just to make sure we don't cheat later
        UpdateCashAccountBalance(result.accounts, 0, currentDate);
        
        return result;
    }
}