using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class AccountDebtPayment
{

    public static (McDebtPosition newPosition, decimal totalCredited, List<ReconciliationMessage> messages)
        CreditDebtPosition(McDebtPosition position, LocalDateTime currentDate, 
            Dictionary<Guid, decimal> debtPayDownAmounts)
    {
        if (position.IsOpen == false) return (position, 0m, []);
        if (position.CurrentBalance <= 0) return (position, 0m, []);

        // set up the return tuple
        (McDebtPosition newPosition, decimal totalCredited, List<ReconciliationMessage> messages) result = (
            AccountCopy.CopyDebtPosition(position), 0m, []);


        if (debtPayDownAmounts.TryGetValue(position.Id, out var payment) == false)
        {
            // this shouldn't happen
            throw new InvalidDataException($"Could not find debt payment for position {position.Id}");
        }

        result.newPosition.CurrentBalance -= payment;
        result.totalCredited += payment;

        if (MonteCarloConfig.DebugMode)
        {
            result.messages.Add(new ReconciliationMessage(
                currentDate, payment, $"Paying down loan position {position.Name}"));
        }

        if (result.newPosition.CurrentBalance > 0) return result;
        
        result.newPosition.CurrentBalance = 0;
        result.newPosition.IsOpen = false;
        
        if (!MonteCarloConfig.DebugMode) return result;
        
        result.messages.Add(new ReconciliationMessage(
                currentDate, 0, $"Paid off loan position {position.Name}"));
        return result;
    }

    public static (McDebtAccount newAccount, decimal totalCredited, List<ReconciliationMessage> messages) 
        CreditDebtAccount(McDebtAccount account, LocalDateTime currentDate,
            Dictionary<Guid, decimal> debtPayDownAmounts)
    {
        if (account.Positions is null) throw new InvalidDataException("Positions is null");
        if (account.Positions.Count == 0) return (account, 0m, []);
        
        // set up the return tuple
        (McDebtAccount newAccount, decimal totalCredited, List<ReconciliationMessage> messages) result = (
            AccountCopy.CopyDebtAccount(account), 0m, []);
        
        // create an empty positions list
        result.newAccount.Positions = [];
        
        foreach (var position in account.Positions)
        {
            var creditResult = CreditDebtPosition(position, currentDate, debtPayDownAmounts);
            result.newAccount.Positions.Add(creditResult.newPosition);
            result.totalCredited += creditResult.totalCredited;
            result.messages.AddRange(creditResult.messages);
        }

        return result;
    }

    public static (bool isSuccessful, BookOfAccounts newBookOfAccounts, TaxLedger newLedger, LifetimeSpend newSpend, List<ReconciliationMessage> messages) 
        PayDownLoans(
            BookOfAccounts accounts, LocalDateTime currentDate, TaxLedger taxLedger, LifetimeSpend lifetimeSpend)
    {
        if (accounts.DebtAccounts is null) throw new InvalidDataException("DebtAccounts is null");
        
        var debtPayDownAmounts = AccountCalculation.CalculateDebtPaydownAmounts(accounts.DebtAccounts);
        var totalDebtPayment = debtPayDownAmounts.Sum(x => x.Value);
        if (totalDebtPayment <= 0) return (true, accounts, taxLedger, lifetimeSpend, []);
        
        // set up the return tuple
        (bool isSuccessful, BookOfAccounts newBookOfAccounts, TaxLedger newLedger, LifetimeSpend newSpend,
            List<ReconciliationMessage> messages) result = (
            false,
            AccountCopy.CopyBookOfAccounts(accounts),
            Tax.CopyTaxLedger(taxLedger),
            lifetimeSpend,
            []);
        
        // withdraw the cash first; this keeps us from needing to pass the book of accounts around
        var cashWithdrawalResult = AccountCashManagement.WithdrawCash(
            result.newBookOfAccounts, totalDebtPayment, currentDate, result.newLedger);
        result.newBookOfAccounts = cashWithdrawalResult.newAccounts;
        result.newLedger = cashWithdrawalResult.newLedger;
        result.messages.AddRange(cashWithdrawalResult.messages);
        if (cashWithdrawalResult.isSuccessful == false)
        {
            // let them declare bankruptcy upstream
            return result;
        }
        
        // cash has already been debited. update each position
        decimal totalCredited = 0m; // just to keep track that we spent only what we withdrew
        result.newBookOfAccounts.DebtAccounts = [];
        foreach (var account in accounts.DebtAccounts)
        {
            var creditResult = CreditDebtAccount(account, currentDate, debtPayDownAmounts);
            result.newBookOfAccounts.DebtAccounts.Add(creditResult.newAccount);
            result.messages.AddRange(creditResult.messages);
            totalCredited += creditResult.totalCredited;
        }

        if (Math.Abs(totalCredited - totalDebtPayment) > 1m)
        {
            throw new InvalidDataException($"Total debt payment {totalDebtPayment} does not match total credited {totalCredited}");       
        }

        var recordResult =  Spend.RecordMultiSpend(result.newSpend, currentDate, null,
            null, null, null, 
            totalCredited, null, null, null, 
            null);
        result.newSpend = recordResult.spend;
        result.messages.AddRange(recordResult.messages);
        
        result.isSuccessful = true;
        return result;
    }
}