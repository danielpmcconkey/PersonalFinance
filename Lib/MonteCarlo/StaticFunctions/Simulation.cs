using System.Diagnostics;
using Lib.DataTypes;
using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Simulation
{
    
    /// <summary>
    /// reads any LocalDateTime and returns the first of the month closest to it
    /// </summary>
    private static LocalDateTime NormalizeDate(LocalDateTime providedDate)
    {
        // todo: figure out why we're not using NormalizeDate any more
        var firstOfThisMonth = new LocalDateTime(providedDate.Year, providedDate.Month, 1, 0, 0);
        var firstOfNextMonth = firstOfThisMonth.PlusMonths(1);
        var timeSpanToThisFirst = providedDate - firstOfThisMonth;
        var timeSpanToNextFirst = firstOfNextMonth - providedDate;
        return (timeSpanToThisFirst.Days <= timeSpanToNextFirst.Days) ?
            firstOfThisMonth : // t2 is longer, return this first
            firstOfNextMonth; // t1 is longer than t2, return next first
    }

    public static bool IsReconciliationPeriod(LocalDateTime currentDate)
    {
        if (!MonteCarloConfig.DebugMode) return false;
        if (currentDate < MonteCarloConfig.ReconciliationSimStartDate) return false;
        if (currentDate > MonteCarloConfig.ReconciliationSimEndDate) return false;
        return true;
    }

    public static (bool isSuccessful, BookOfAccounts accounts, TaxLedger ledger, LifetimeSpend spend, 
        List<ReconciliationMessage> messages) PayForStuff(McModel simParams, PgPerson person, LocalDateTime currentDate,
            RecessionStats recessionStats, TaxLedger ledger,
        LifetimeSpend spend, BookOfAccounts accounts)
    {
        var funSpend = Spend.CalculateMonthlyFunSpend(simParams, person, currentDate);
        var notFunSpend = Spend.CalculateMonthlyRequiredSpendWithoutDebt(simParams, person, currentDate);
        
        // required spend can't move. But your fun spend can go down if we're in a recession
        funSpend = Spend.CalculateRecessionSpendOverride(simParams, funSpend, recessionStats);
        
        var withdrawalAmount = funSpend + notFunSpend;
        
        // set up the return tuple
        (bool isSuccessful, BookOfAccounts accounts, TaxLedger ledger, LifetimeSpend spend
            , List<ReconciliationMessage> messages) results = (
            false, // default to false. only override if completely successful
            AccountCopy.CopyBookOfAccounts(accounts),
            Tax.CopyTaxLedger(ledger),
            Spend.CopyLifetimeSpend(spend),
            []
            );
        
        // first try the not-fun spend
        var notFunResult = SpendCash(
            notFunSpend, false, accounts, currentDate, ledger, spend, person);
        results.accounts = notFunResult.newAccounts;
        results.ledger = notFunResult.newLedger;
        results.spend = notFunResult.spend;
        results.messages.AddRange(notFunResult.messages);
        if (!notFunResult.isSuccessful) return results;
            
        // now try the fun spend
        var funResult = SpendCash(
            funSpend, true, results.accounts, currentDate, results.ledger, results.spend, person);
        results.accounts = funResult.newAccounts;
        results.ledger = funResult.newLedger;
        results.spend = funResult.spend;
        results.messages.AddRange(funResult.messages);
        if (!funResult.isSuccessful) return results;
        
        // all good; mark as successful and return
        results.isSuccessful = true;
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.Add(new ReconciliationMessage(currentDate, notFunSpend, "Monthly required spend"));
        results.messages.Add(new ReconciliationMessage(currentDate, funSpend, "Monthly fun spend"));
        
        return results;
    }
    
    public static (bool isSuccessful, BookOfAccounts accounts, TaxLedger ledger, LifetimeSpend spend,
        List<ReconciliationMessage> messages) PayTaxForYear(PgPerson person, LocalDateTime currentDate, 
            TaxLedger ledger, LifetimeSpend spend, BookOfAccounts accounts, int taxYear)
    {
        // set up the return tuple
        (bool isSuccessful, BookOfAccounts accounts, TaxLedger ledger, LifetimeSpend spend,
            List<ReconciliationMessage> messages) results = (
            false, // default to false. only override if completely successful
            AccountCopy.CopyBookOfAccounts(accounts),
            Tax.CopyTaxLedger(ledger),
            Spend.CopyLifetimeSpend(spend),
            []
        );
        
        // first figure out the liability
        var taxLiabilityResult = TaxCalculation.CalculateTaxLiabilityForYear(results.ledger, taxYear);
        var taxLiability = taxLiabilityResult.amount;
        results.messages.AddRange(taxLiabilityResult.messages);
        
        
        // if you have a refund, deposit it
        if (taxLiability < 0)
        {
            var refundAmount = -taxLiability;
            var refundResult = AccountCashManagement.DepositCash(results.accounts, refundAmount, currentDate);
            results.accounts = refundResult.accounts;
            results.messages.AddRange(refundResult.messages);
        }
        // if not, pay for it
        else
        {
            var notFunResult = SpendCash(
                taxLiability, false, results.accounts, currentDate, results.ledger, results.spend, person);
            results.accounts = notFunResult.newAccounts;
            results.ledger = notFunResult.newLedger;
            results.spend = notFunResult.spend;
            results.messages.AddRange(notFunResult.messages);
            if (!notFunResult.isSuccessful) return results;
        }
        
        // don't forget to record the tax liability, either way
        var recordResults = Tax.RecordTaxPaid(results.ledger, currentDate, taxLiability);
        results.ledger = recordResults.ledger;
        results.messages.AddRange(recordResults.messages);
        
        // mark it as successful and return
        results.isSuccessful = true;
        return results;
    }
    
    public static (BookOfAccounts accounts, TaxLedger ledger, LifetimeSpend spend, List<ReconciliationMessage> messages) 
        ProcessPaycheck(PgPerson person, LocalDateTime currentDate, BookOfAccounts accounts, TaxLedger ledger,
            LifetimeSpend spend, McModel simParams, CurrentPrices prices)
    {
        var paydayResult = Payday.ProcessPreRetirementPaycheck(
                person, currentDate, accounts, ledger, spend, simParams, prices);
        return (paydayResult.bookOfAccounts, paydayResult.ledger, paydayResult.spend, paydayResult.messages);
    }

    public static (BookOfAccounts accounts, TaxLedger ledger, LifetimeSpend spend, List<ReconciliationMessage> messages) 
        ProcessSocialSecurityCheck(PgPerson person, LocalDateTime currentDate, BookOfAccounts accounts,
            TaxLedger ledger, LifetimeSpend spend, McModel simParams)
    {
        var paydayResult = Payday.ProcessSocialSecurityCheck(
                person, currentDate, accounts, ledger, spend, simParams);
        return (paydayResult.bookOfAccounts, paydayResult.ledger, paydayResult.spend, paydayResult.messages);
    }
    
    public static (BookOfAccounts accounts, TaxLedger ledger, LifetimeSpend spend, List<ReconciliationMessage> messages)
        ProcessPayday (PgPerson person, LocalDateTime currentDate, BookOfAccounts accounts, TaxLedger ledger,
            LifetimeSpend spend, McModel simParams, CurrentPrices prices)
    {
        (BookOfAccounts accounts, TaxLedger ledger, LifetimeSpend spend, List<ReconciliationMessage> messages) results = 
            (AccountCopy.CopyBookOfAccounts(accounts),
                Tax.CopyTaxLedger(ledger),
                Spend.CopyLifetimeSpend(spend),
                []);
        
        // if still working, you get a paycheck
        if (!person.IsRetired)
        {
            if(MonteCarloConfig.DebugMode) results.messages.Add(new ReconciliationMessage(
                currentDate, null, "processing pre-retirement paycheck"));
            var paydayResult = ProcessPaycheck(
                person, currentDate, accounts, ledger, spend, simParams, prices);
            results.accounts = paydayResult.accounts;
            results.ledger = paydayResult.ledger;
            results.spend = paydayResult.spend;
            results.messages.AddRange(paydayResult.messages);
        }

        // if you aren't yet drawing SS, just return now
        if (currentDate < simParams.SocialSecurityStart) return results;
        
        // this isn't an "else" you can be working and draw SS
        if(MonteCarloConfig.DebugMode) results.messages.Add(new ReconciliationMessage(
            currentDate, null, "processing social security check"));
        var ssResult = ProcessSocialSecurityCheck(
            person, currentDate, accounts, ledger, spend, simParams);
        results.accounts = ssResult.accounts;
        results.ledger = ssResult.ledger;
        results.spend = ssResult.spend;
        results.messages.AddRange(ssResult.messages);
        return results;
    }
    
    public static CurrentPrices SetNewPrices(CurrentPrices prices, Dictionary<LocalDateTime, Decimal>  hypotheticalPrices,
        LocalDateTime currentDate)
    {
        if (!hypotheticalPrices.TryGetValue(currentDate, out var priceGrowthRate))
        {
            throw new InvalidDataException("CurrentDate not found in _hypotheticalPrices");
        }

        return Pricing.SetLongTermGrowthRateAndPrices(prices, priceGrowthRate);
    }
    
    public static (bool isRetired, PgPerson person) SetIsRetiredFlagIfNeeded(
        LocalDateTime currentDate, PgPerson person, McModel simParams)
    {
        if (person.IsRetired) return (true, person);
        if (currentDate < simParams.RetirementDate) return (false, person);
        
        // this is the day. copy the person, set the flag, and return 
        var personCopy = Person.CopyPerson(person, true);
        personCopy.IsRetired = true;
        return (true, personCopy);
        
    }
    
    public static (bool isSuccessful, BookOfAccounts newAccounts, TaxLedger newLedger, LifetimeSpend spend,
        List<ReconciliationMessage> messages) SpendCash(decimal amount, bool isFun, BookOfAccounts accounts, 
            LocalDateTime currentDate, TaxLedger ledger, LifetimeSpend spend, PgPerson person)
    {
        // set up the return tuple
        (bool isSuccessful, BookOfAccounts newAccounts, TaxLedger newLedger, LifetimeSpend spend,
            List<ReconciliationMessage> messages) results = (
            false, AccountCopy.CopyBookOfAccounts(accounts), Tax.CopyTaxLedger(ledger),
            Spend.CopyLifetimeSpend(spend), []);
        
        // try to withdraw the money
        var withdrawalResults = AccountCashManagement.WithdrawCash(
            results.newAccounts, amount, currentDate, results.newLedger);
        results.newAccounts = withdrawalResults.newAccounts;
        results.newLedger = withdrawalResults.newLedger;
        results.messages.AddRange(withdrawalResults.messages);
        if (!withdrawalResults.isSuccessful)
        {
            // let them declare bankruptcy upstream
            return results;
        }
        var recordResults = Spend.RecordSpend(results.spend, amount, currentDate);
        results.spend = recordResults.spend;
        results.messages.AddRange(recordResults.messages);
        
        if (isFun)
        {
            var funPoints = Spend.CalculateFunPointsForSpend(amount, person, currentDate);
            var funRecordResults = Spend.RecordFunPoints(results.spend, funPoints, currentDate);
            results.spend = funRecordResults.spend;
            results.messages.AddRange(funRecordResults.messages);
        }
        results.isSuccessful = true;
        return results;
    }

  



}