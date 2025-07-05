using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class AccountDebtPayment
{
    
    public static (bool isSuccessful, McDebtPosition newPosition, BookOfAccounts newBookOfAccounts, TaxLedger newLedger, LifetimeSpend newSpend)
        PayDownDebtPosition(McDebtPosition position, BookOfAccounts bookOfAccounts, LocalDateTime currentDate,
            TaxLedger taxLedger, LifetimeSpend lifetimeSpend)
    {
        if (position.IsOpen == false) return (true, position, bookOfAccounts, taxLedger, lifetimeSpend);
        if (position.MonthlyPayment <= 0) return (true, position, bookOfAccounts, taxLedger, lifetimeSpend);
        
        // set up the return tuple
        (bool isSuccessful, McDebtPosition newPosition, BookOfAccounts newBookOfAccounts, TaxLedger newLedger, LifetimeSpend newSpend) result = (
            false,
            AccountCopy.CopyDebtPosition(position),
            AccountCopy.CopyBookOfAccounts(bookOfAccounts),
            Tax.CopyTaxLedger(taxLedger),
            lifetimeSpend);
        
        // calculate the payment amount
        decimal amount = position.MonthlyPayment;
        if (amount > position.CurrentBalance) amount = position.CurrentBalance;
        
        // deduct the cash, if we have it
        var localResult = AccountCashManagement.WithdrawCash(result.newBookOfAccounts, amount, currentDate, result.newLedger);
        if (localResult.isSuccessful == false)
        {
            // let them declare bankruptcy upstream
            return result;
        }
        result.newPosition.CurrentBalance -= amount;
        result.newSpend.TotalDebtPaidLifetime += amount;
        result.newLedger = localResult.newLedger;
        result.newBookOfAccounts = localResult.newAccounts;
        result.isSuccessful = true;

        if (MonteCarloConfig.DebugMode)
        {
            Reconciliation.AddMessageLine(currentDate, amount, $"Pay down loan {result.newPosition.Name}");
        }
        if(result.newPosition.CurrentBalance <= 0)
        {
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(currentDate, 0, $"Paid off loan {result.newPosition.Name}");
            }
            result.newPosition.CurrentBalance = 0;
            result.newPosition.IsOpen = false;
        }

        return result;
    }

    public static (bool isSuccessful, McDebtAccount newAccount, BookOfAccounts newBookOfAccounts, TaxLedger newLedger, LifetimeSpend newSpend)
        PayDownDebtAccount(McDebtAccount account, BookOfAccounts bookOfAccounts, LocalDateTime currentDate,
            TaxLedger taxLedger, LifetimeSpend lifetimeSpend)
    {
        if (account.Positions is null) throw new InvalidDataException("Positions is null");
        if (account.Positions.Count == 0) return (true, account, bookOfAccounts, taxLedger, lifetimeSpend);
        
        // set up the return tuple
        (bool isSuccessful, McDebtAccount newAccount, BookOfAccounts newBookOfAccounts, TaxLedger newLedger,
            LifetimeSpend newSpend) result = (
                false,
                AccountCopy.CopyDebtAccount(account),
                AccountCopy.CopyBookOfAccounts(bookOfAccounts),
                Tax.CopyTaxLedger(taxLedger),
                Simulation.CopyLifetimeSpend(lifetimeSpend)
            );
        
        
        List<McDebtPosition> newPositions = [];
            
        foreach (var p in result.newAccount.Positions)
        {
            var localResult = PayDownDebtPosition(p, result.newBookOfAccounts, currentDate, result.newLedger, result.newSpend);
            if (localResult.isSuccessful == false)
            {
                // let them declare bankruptcy upstream
                return result;
            }
            newPositions.Add(localResult.newPosition);
            result.newBookOfAccounts = localResult.newBookOfAccounts;
            result.newLedger = localResult.newLedger;
            result.newSpend = localResult.newSpend;
        }
        result.newAccount.Positions = newPositions;
        result.isSuccessful = true;
        return result;
    }

    public static (bool isSuccessful, BookOfAccounts newBookOfAccounts, TaxLedger newLedger, LifetimeSpend newSpend) 
        PayDownLoans(
            BookOfAccounts accounts, LocalDateTime currentDate, McPerson person, TaxLedger taxLedger, LifetimeSpend lifetimeSpend)
    {
        if (accounts.DebtAccounts is null) throw new InvalidDataException("DebtAccounts is null");
        
        // set up the return tuple
        (bool isSuccessful, BookOfAccounts newBookOfAccounts, TaxLedger newLedger, LifetimeSpend newSpend) result = (
            false,
            AccountCopy.CopyBookOfAccounts(accounts),
            Tax.CopyTaxLedger(taxLedger),
            lifetimeSpend);

        List<McDebtAccount> newDebtAccounts = [];
        
        foreach (var account in accounts.DebtAccounts)
        {
            var localResult = PayDownDebtAccount(
                account, result.newBookOfAccounts, currentDate, result.newLedger, result.newSpend);
            if (localResult.isSuccessful == false)
            {
                // let them declare bankruptcy upstream
                return result;
            }
            newDebtAccounts.Add(localResult.newAccount);
            result.newBookOfAccounts = localResult.newBookOfAccounts;
            result.newLedger = localResult.newLedger;
            result.newSpend = localResult.newSpend;
        }
        result.newBookOfAccounts.DebtAccounts = newDebtAccounts;
        result.isSuccessful = true;
        return result;
    }
    
    
}