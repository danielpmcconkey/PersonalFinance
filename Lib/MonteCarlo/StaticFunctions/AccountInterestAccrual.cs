using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class AccountInterestAccrual
{
        public static (BookOfAccounts newAccounts, LifetimeSpend newSpend) AccrueInterest(
        LocalDateTime currentDate, BookOfAccounts bookOfAccounts, CurrentPrices prices, LifetimeSpend lifetimeSpend)
    {
        var newAccounts = AccountCopy.CopyBookOfAccounts(bookOfAccounts);
        var newSpend = Simulation.CopyLifetimeSpend(lifetimeSpend);
        
        var results = AccrueInterestOnInvestmentAccounts(currentDate, newAccounts, prices, newSpend);
        newAccounts = results.newAccounts;
        newSpend = results.newSpend;
        
        results = AccrueInterestOnDebtAccounts(currentDate, newAccounts, newSpend);
        newAccounts = results.newAccounts;
        newSpend = results.newSpend;
        
        return (newAccounts, newSpend);
    }
    
    public static (McDebtAccount newAccount, LifetimeSpend newSpend) AccrueInterestOnDebtAccount
        (LocalDateTime currentDate, McDebtAccount account, LifetimeSpend lifetimeSpend)
    {
        if(account.Positions is null) throw new InvalidDataException("Positions is null");
        if(account.Positions.Count == 0) return (account, lifetimeSpend);
        
        // set up the return tuple
        (McDebtAccount newAccount, LifetimeSpend newSpend)  result = (
            AccountCopy.CopyDebtAccount(account), Simulation.CopyLifetimeSpend(lifetimeSpend));
        
        /*
         * for debt accounts, we just need to update the balances according to the apr
         */
        
        result.newAccount.Positions = [];
        
        foreach (var p in account.Positions)
        {
            var localResult = AccrueInterestOnDebtPosition(currentDate, p, result.newSpend);
            result.newAccount.Positions.Add(localResult.newPosition);
            result.newSpend = localResult.newSpend;
        }
        return result;
    }
    
    public static (McDebtPosition newPosition, LifetimeSpend newSpend) AccrueInterestOnDebtPosition(
        LocalDateTime currentDate, McDebtPosition position, LifetimeSpend lifetimeSpend)
    {
        if (position.IsOpen == false) return (position, lifetimeSpend);
        
        /*
        * for debt accounts, we just need to update the balances according to the apr         
        */

        (McDebtPosition newPosition, LifetimeSpend newSpend) result = 
            (AccountCopy.CopyDebtPosition(position), Simulation.CopyLifetimeSpend(lifetimeSpend));;
        
        decimal amount = position.CurrentBalance * (position.AnnualPercentageRate / 12);
        
        result.newPosition.CurrentBalance += amount;

        result.newSpend.TotalDebtAccrualLifetime += amount;
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcileInterestAccrual)
        {
            Reconciliation.AddMessageLine(currentDate, -1 * amount, $"Debt accrual for position {result.newPosition.Name}");
        }
        return result;
    }
    
    public static (BookOfAccounts newAccounts, LifetimeSpend newSpend) AccrueInterestOnDebtAccounts(
        LocalDateTime currentDate, BookOfAccounts bookOfAccounts, LifetimeSpend lifetimeSpend)
    {
        if (bookOfAccounts.DebtAccounts is null) throw new InvalidDataException("DebtAccounts is null");
        if (bookOfAccounts.DebtAccounts.Count == 0) return (bookOfAccounts, lifetimeSpend);
        
        // set up the return tuple
        (BookOfAccounts newAccounts, LifetimeSpend newSpend) result = 
            (AccountCopy.CopyBookOfAccounts(bookOfAccounts), Simulation.CopyLifetimeSpend(lifetimeSpend));
        
        result.newAccounts.DebtAccounts = [];
        foreach (var account in bookOfAccounts.DebtAccounts)
        {
            var localResult = AccrueInterestOnDebtAccount(currentDate, account, lifetimeSpend);
            result.newAccounts.DebtAccounts.Add(localResult.newAccount);
            result.newSpend = localResult.newSpend;
        }
        return result;
    }
    
    public static (BookOfAccounts newAccounts, LifetimeSpend newSpend) AccrueInterestOnInvestmentAccounts(
        LocalDateTime currentDate, BookOfAccounts bookOfAccounts, CurrentPrices prices, LifetimeSpend lifetimeSpend)
    {
        if (bookOfAccounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        if (bookOfAccounts.InvestmentAccounts.Count == 0) return (bookOfAccounts, lifetimeSpend);

        // set up the return tuple
        (BookOfAccounts newAccounts, LifetimeSpend newSpend) results = (
            AccountCopy.CopyBookOfAccounts(bookOfAccounts), Simulation.CopyLifetimeSpend(lifetimeSpend));
        results.newAccounts.InvestmentAccounts = [];
        
        // add the cash and primary residence accounts back as-is
        results.newAccounts.InvestmentAccounts.AddRange(bookOfAccounts.InvestmentAccounts
            .Where(x => x.AccountType is 
                (McInvestmentAccountType.PRIMARY_RESIDENCE or McInvestmentAccountType.CASH)));
        
        foreach (var account in bookOfAccounts.InvestmentAccounts
            .Where(x => x.AccountType is not 
                (McInvestmentAccountType.PRIMARY_RESIDENCE or McInvestmentAccountType.CASH)))
                     
        {
            var localResult = AccrueInterestOnInvestmentAccount(currentDate, account, prices, results.newSpend);;
            results.newAccounts.InvestmentAccounts.Add(localResult.newAccount);
            results.newSpend = localResult.newSpend;
        }
        return results;
    }
    
    public static (McInvestmentAccount newAccount, LifetimeSpend newSpend) AccrueInterestOnInvestmentAccount(
        LocalDateTime currentDate, McInvestmentAccount account, CurrentPrices prices, LifetimeSpend lifetimeSpend)
    {
        if (account.Positions is null) throw new InvalidDataException("Positions is null");
        if (account.Positions.Count == 0) return (account, lifetimeSpend);
        
        // set up the return tuple
        (McInvestmentAccount newAccount, LifetimeSpend newSpend) results = (
            AccountCopy.CopyInvestmentAccount(account), Simulation.CopyLifetimeSpend(lifetimeSpend));
        results.newAccount.Positions = [];
        
        foreach (var p in account.Positions)
        {
            var localResult = AccrueInterestOnInvestmentPosition(currentDate, p, prices, results.newSpend);
            results.newAccount.Positions.Add(localResult.newPosition);
            results.newSpend = localResult.newSpend;
        }
        return results;
    }
    
    public static (McInvestmentPosition newPosition, LifetimeSpend newSpend) AccrueInterestOnInvestmentPosition(
        LocalDateTime currentDate, McInvestmentPosition position, CurrentPrices prices, LifetimeSpend lifetimeSpend)
    {
        /*
         * for investment accounts, if all accounts have previously normalized, we should only have to update the
         * price on all positions
         */
        
        // set up the return tuple
        (McInvestmentPosition newPosition, LifetimeSpend newSpend) results = (
            AccountCopy.CopyInvestmentPosition(position), Simulation.CopyLifetimeSpend(lifetimeSpend));
        
        
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
        Reconciliation.AddMessageLine(currentDate, newAmount - oldAmount,
            $"Interest accrual for position {results.newPosition.Name}");
        

        return results;
    }
}