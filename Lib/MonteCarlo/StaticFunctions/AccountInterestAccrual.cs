using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class AccountInterestAccrual
{
    public static (BookOfAccounts newAccounts, LifetimeSpend newSpend, List<ReconciliationMessage> messages) 
        AccrueInterest(LocalDateTime currentDate, BookOfAccounts bookOfAccounts, CurrentPrices prices,
            LifetimeSpend lifetimeSpend)
    {
        (BookOfAccounts newAccounts, LifetimeSpend newSpend, List<ReconciliationMessage> messages) results = (
            AccountCopy.CopyBookOfAccounts(bookOfAccounts),
            Spend.CopyLifetimeSpend(lifetimeSpend),
            []);
        
        
        var investResults = AccrueInterestOnInvestmentAccounts(
            currentDate, results.newAccounts, prices, results.newSpend);
        results.newAccounts = investResults.newAccounts;
        results.newSpend = investResults.newSpend;
        results.messages.AddRange(investResults.messages);
        
        
        var debtResults = AccrueInterestOnDebtAccounts(currentDate, results.newAccounts,
            results.newSpend);
        results.newAccounts = debtResults.newAccounts;
        results.newSpend = debtResults.newSpend;
        results.messages.AddRange(debtResults.messages);
        
        return results;
    }
// todo: check if there's a unit test on AccrueInterestOnDebtAccount
    public static (McDebtAccount newAccount, LifetimeSpend newSpend, List<ReconciliationMessage> messages) 
        AccrueInterestOnDebtAccount(LocalDateTime currentDate, McDebtAccount account, LifetimeSpend lifetimeSpend)
    {
        if(account.Positions is null) throw new InvalidDataException("Positions is null");
        if(account.Positions.Count == 0) return (account, lifetimeSpend, []);
        
        // set up the return tuple
        (McDebtAccount newAccount, LifetimeSpend newSpend, List<ReconciliationMessage> messages)  result = (
            AccountCopy.CopyDebtAccount(account), Spend.CopyLifetimeSpend(lifetimeSpend), []);
        
        /*
         * for debt accounts, we just need to update the balances according to the apr
         */
        
        result.newAccount.Positions = [];
        
        foreach (var p in account.Positions)
        {
            var localResult = AccrueInterestOnDebtPosition(currentDate, p, result.newSpend);
            result.newAccount.Positions.Add(localResult.newPosition);
            result.newSpend = localResult.newSpend;
            result.messages.AddRange(localResult.messages);
        }
        return result;
    }
    
    public static (McDebtPosition newPosition, LifetimeSpend newSpend, List<ReconciliationMessage> messages)
        AccrueInterestOnDebtPosition(LocalDateTime currentDate, McDebtPosition position, LifetimeSpend lifetimeSpend)
    {
        if (position.IsOpen == false) return (position, lifetimeSpend, []);
        
        /*
        * for debt accounts, we just need to update the balances according to the apr         
        */

        (McDebtPosition newPosition, LifetimeSpend newSpend, List<ReconciliationMessage> messages) result = 
            (AccountCopy.CopyDebtPosition(position), Spend.CopyLifetimeSpend(lifetimeSpend), []);
        
        decimal amount = position.CurrentBalance * (position.AnnualPercentageRate / 12);
        
        result.newPosition.CurrentBalance += amount;

        result.newSpend.TotalDebtAccrualLifetime += amount;
        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcileInterestAccrual) return result;
        
        result.messages.Add(new ReconciliationMessage(
            currentDate, amount, $"Debt accrual for position {result.newPosition.Name}"));
        
        return result;
    }
    // todo: check if there's a unit test on AccrueInterestOnDebtAccounts
    public static (BookOfAccounts newAccounts, LifetimeSpend newSpend, List<ReconciliationMessage> messages)
        AccrueInterestOnDebtAccounts(LocalDateTime currentDate, BookOfAccounts bookOfAccounts,
            LifetimeSpend lifetimeSpend)
    {
        if (bookOfAccounts.DebtAccounts is null) throw new InvalidDataException("DebtAccounts is null");
        if (bookOfAccounts.DebtAccounts.Count == 0) return (bookOfAccounts, lifetimeSpend, []);
        
        // set up the return tuple
        (BookOfAccounts newAccounts, LifetimeSpend newSpend, List<ReconciliationMessage> messages) result = 
            (AccountCopy.CopyBookOfAccounts(bookOfAccounts), Spend.CopyLifetimeSpend(lifetimeSpend), []);
        
        result.newAccounts.DebtAccounts = [];
        foreach (var account in bookOfAccounts.DebtAccounts)
        {
            var localResult = AccrueInterestOnDebtAccount(currentDate, account, lifetimeSpend);
            result.newAccounts.DebtAccounts.Add(localResult.newAccount);
            result.newSpend = localResult.newSpend;
            result.messages.AddRange(localResult.messages);
        }
        return result;
    }
    
    // todo: check if there's a unit test on AccrueInterestOnInvestmentAccounts
    public static (BookOfAccounts newAccounts, LifetimeSpend newSpend, List<ReconciliationMessage> messages) 
        AccrueInterestOnInvestmentAccounts(LocalDateTime currentDate, BookOfAccounts bookOfAccounts,
            CurrentPrices prices, LifetimeSpend lifetimeSpend)
    {
        if (bookOfAccounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        if (bookOfAccounts.InvestmentAccounts.Count == 0) return (bookOfAccounts, lifetimeSpend, []);

        // set up the return tuple
        (BookOfAccounts newAccounts, LifetimeSpend newSpend, List<ReconciliationMessage> messages) results = (
            AccountCopy.CopyBookOfAccounts(bookOfAccounts), Spend.CopyLifetimeSpend(lifetimeSpend), []);
        
        // start with a clean, empty list. We'll rebuild it back, one at a time
        results.newAccounts.InvestmentAccounts = [];
        
        // add the cash and primary residence accounts back as-is
        results.newAccounts.InvestmentAccounts.AddRange(bookOfAccounts.InvestmentAccounts
            .Where(x => x.AccountType is 
                (McInvestmentAccountType.PRIMARY_RESIDENCE or McInvestmentAccountType.CASH)));
        
        // iterate through remaining and add the copied / updated account back
        foreach (var account in bookOfAccounts.InvestmentAccounts
            .Where(x => x.AccountType is not 
                (McInvestmentAccountType.PRIMARY_RESIDENCE or McInvestmentAccountType.CASH)))
                     
        {
            var localResult = AccrueInterestOnInvestmentAccount(currentDate, account, prices, results.newSpend);;
            results.newAccounts.InvestmentAccounts.Add(localResult.newAccount);
            results.newSpend = localResult.newSpend;
            results.messages.AddRange(localResult.messages);
        }
        return results;
    }
    // todo: check if there's a unit test on AccrueInterestOnInvestmentAccount
    public static (McInvestmentAccount newAccount, LifetimeSpend newSpend, List<ReconciliationMessage> messages) 
        AccrueInterestOnInvestmentAccount(LocalDateTime currentDate, McInvestmentAccount account, 
            CurrentPrices prices, LifetimeSpend lifetimeSpend)
    {
        if (account.Positions is null) throw new InvalidDataException("Positions is null");
        if (account.Positions.Count == 0) return (account, lifetimeSpend, []);
        
        // set up the return tuple
        (McInvestmentAccount newAccount, LifetimeSpend newSpend, List<ReconciliationMessage> messages) results = (
            AccountCopy.CopyInvestmentAccount(account), Spend.CopyLifetimeSpend(lifetimeSpend), []);
        results.newAccount.Positions = [];
        
        foreach (var p in account.Positions)
        {
            var localResult = AccrueInterestOnInvestmentPosition(currentDate, p, prices, results.newSpend);
            results.newAccount.Positions.Add(localResult.newPosition);
            results.newSpend = localResult.newSpend;
            results.messages.AddRange(localResult.messages);
        }
        return results;
    }
    
    public static (McInvestmentPosition newPosition, LifetimeSpend newSpend, List<ReconciliationMessage> messages) 
        AccrueInterestOnInvestmentPosition(LocalDateTime currentDate, McInvestmentPosition position, 
            CurrentPrices prices, LifetimeSpend lifetimeSpend)
    {
        /*
         * for investment accounts, if all accounts have previously normalized, we should only have to update the
         * price on all positions
         */
        
        // set up the return tuple
        (McInvestmentPosition newPosition, LifetimeSpend newSpend, List<ReconciliationMessage> messages) results = (
            AccountCopy.CopyInvestmentPosition(position), Spend.CopyLifetimeSpend(lifetimeSpend), []);
        
        
        var oldAmount = (StaticConfig.MonteCarloConfig.DebugMode == false) ? 0 : position.CurrentValue;
        
        var newPrice = position.InvestmentPositionType switch
        {
            McInvestmentPositionType.MID_TERM => (decimal)prices.CurrentMidTermInvestmentPrice,
            McInvestmentPositionType.SHORT_TERM => (decimal)prices.CurrentShortTermInvestmentPrice,
            _ => (decimal)prices.CurrentLongTermInvestmentPrice
        };
        results.newPosition.Price = newPrice;
        
        // return here unless we're reconciling
        if (MonteCarloConfig.DebugMode == false || MonteCarloConfig.ShouldReconcileInterestAccrual == false)
            return results;
        
        // do the recon stuff, then return
        var newAmount =  results.newPosition.CurrentValue;
        results.newSpend.TotalInvestmentAccrualLifetime += newAmount - oldAmount;
        results.messages.Add(new ReconciliationMessage(currentDate, newAmount - oldAmount,
            $"Interest accrual for position {results.newPosition.Name}"));
        

        return results;
    }
}