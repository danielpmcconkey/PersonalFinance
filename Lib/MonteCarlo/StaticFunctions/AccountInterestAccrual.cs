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

        var recordResult = Spend.RecordMultiSpend(result.newSpend, currentDate, null,
            null, amount, null, 
            null, null, null, null, 
            null);
        result.newSpend = recordResult.spend;
        
        if (!MonteCarloConfig.DebugMode) return result;
        
        result.messages.Add(new ReconciliationMessage(
            currentDate, -amount, $"Debt accrual for position {result.newPosition.Name}"));
        result.messages.AddRange(recordResult.messages);
        
        return result;
    }
    
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
            var localResult = AccrueInterestOnDebtAccount(currentDate, account, result.newSpend);
            result.newAccounts.DebtAccounts.Add(localResult.newAccount);
            result.newSpend = localResult.newSpend;
            result.messages.AddRange(localResult.messages);
        }
        return result;
    }
    
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
    
    // ── Mid-term quarterly dividends ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Accrues quarterly dividends on all mid-term positions across all investment accounts.
    /// Dividends are reinvested (position quantity increases). Taxable brokerage accounts also
    /// record the dividend in the TaxLedger for year-end tax calculation.
    /// Only runs in March, June, September, and December.
    /// </summary>
    public static (BookOfAccounts newAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages)
        AccrueMidTermDividends(LocalDateTime currentDate, BookOfAccounts bookOfAccounts, TaxLedger taxLedger)
    {
        (BookOfAccounts newAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages) result = (
            AccountCopy.CopyBookOfAccounts(bookOfAccounts), taxLedger, []);

        if (currentDate.Month is not (3 or 6 or 9 or 12))
            return result;
        if (result.newAccounts.InvestmentAccounts is null || result.newAccounts.InvestmentAccounts.Count == 0)
            return result;

        var updatedAccounts = new List<McInvestmentAccount>();
        foreach (var account in result.newAccounts.InvestmentAccounts)
        {
            if (account.AccountType is McInvestmentAccountType.PRIMARY_RESIDENCE or McInvestmentAccountType.CASH)
            {
                updatedAccounts.Add(account);
                continue;
            }
            var accountResult = AccrueMidTermDividendsOnAccount(currentDate, account, result.newLedger);
            updatedAccounts.Add(accountResult.newAccount);
            result.newLedger = accountResult.newLedger;
            result.messages.AddRange(accountResult.messages);
        }
        result.newAccounts.InvestmentAccounts = updatedAccounts;
        return result;
    }

    private static (McInvestmentAccount newAccount, TaxLedger newLedger, List<ReconciliationMessage> messages)
        AccrueMidTermDividendsOnAccount(LocalDateTime currentDate, McInvestmentAccount account, TaxLedger taxLedger)
    {
        if (account.Positions is null) throw new InvalidDataException("Positions is null");
        if (account.Positions.Count == 0) return (account, taxLedger, []);

        bool isTaxable = account.AccountType == McInvestmentAccountType.TAXABLE_BROKERAGE;

        (McInvestmentAccount newAccount, TaxLedger newLedger, List<ReconciliationMessage> messages) result = (
            AccountCopy.CopyInvestmentAccount(account), taxLedger, []);
        result.newAccount.Positions = [];

        foreach (var position in account.Positions)
        {
            if (position.InvestmentPositionType != McInvestmentPositionType.MID_TERM)
            {
                result.newAccount.Positions.Add(position);
                continue;
            }
            var positionResult = AccrueMidTermDividendsOnPosition(currentDate, position, result.newLedger, isTaxable);
            result.newAccount.Positions.Add(positionResult.newPosition);
            result.newLedger = positionResult.newLedger;
            result.messages.AddRange(positionResult.messages);
        }
        return result;
    }

    private static (McInvestmentPosition newPosition, TaxLedger newLedger, List<ReconciliationMessage> messages)
        AccrueMidTermDividendsOnPosition(LocalDateTime currentDate, McInvestmentPosition position,
            TaxLedger taxLedger, bool isTaxable)
    {
        (McInvestmentPosition newPosition, TaxLedger newLedger, List<ReconciliationMessage> messages) result = (
            AccountCopy.CopyInvestmentPosition(position), taxLedger, []);

        decimal dividendAmount = position.CurrentValue * (TaxConstants.MidTermAnnualDividendYield / 4m);
        if (dividendAmount == 0m) return result;

        // Reinvest: buy additional shares at the current price
        result.newPosition.Quantity += dividendAmount / position.Price;

        if (isTaxable)
        {
            result.newLedger.DividendsReceived.Add((currentDate, dividendAmount));
            result.newLedger.QualifiedDividendsReceived.Add(
                (currentDate, dividendAmount * TaxConstants.DividendPercentQualified));
        }

        if (!MonteCarloConfig.DebugMode) return result;

        result.messages.Add(new ReconciliationMessage(currentDate, dividendAmount,
            $"Mid-term dividend reinvested for position {position.Name}" +
            (isTaxable ? " (taxable — recorded in ledger)" : " (tax-advantaged)")));

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────

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
        
        
        var oldAmount = position.CurrentValue;
        
        var newPrice = position.InvestmentPositionType switch
        {
            McInvestmentPositionType.MID_TERM => (decimal)prices.CurrentMidTermInvestmentPrice,
            McInvestmentPositionType.SHORT_TERM => (decimal)prices.CurrentShortTermInvestmentPrice,
            _ => (decimal)prices.CurrentEquityInvestmentPrice
        };
        results.newPosition.Price = newPrice;
        
        // do the recon stuff, then return
        var newAmount =  results.newPosition.CurrentValue;
        results.newSpend.TotalInvestmentAccrualLifetime += (newAmount - oldAmount);
        results.messages.Add(new ReconciliationMessage(currentDate, newAmount - oldAmount,
            $"Interest accrual for position {results.newPosition.Name}"));
        

        return results;
    }
}