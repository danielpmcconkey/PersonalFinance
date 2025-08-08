using System.Diagnostics;
using Lib.DataTypes;
using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Simulation
{
    
    /// <summary>
    /// This method was written by Claude. Matches Google Sheets' PERCENTILE (inclusive / PERCENTILE.INC) behavior
    /// </summary>
    /// <param name="sequence">values to compute percentile from (unsorted)</param>
    /// <param name="percentile">k in [0, 1], e.g. 0.10m, 0.25m, 0.50m, 0.75m, 0.90m</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static decimal CalculatePercentileValue(decimal[] sequence, decimal percentile)
    {
        /*
         * Claude prompt:
         * #file:Simulation.cs
         * help me re-write the `CalculatePercentileValue`. It currently does not produce the same result that Google
         * Sheets' PERCENTILE function does and I would like it to. The current `CalculatePercentileValue` function
         * produces very similar results, but not quite
         */
        if (sequence is null) throw new ArgumentNullException(nameof(sequence));
        if (sequence.Length == 0) throw new ArgumentException("Sequence must contain at least one value.", nameof(sequence));
        if (percentile < 0m || percentile > 1m)
            throw new ArgumentOutOfRangeException(nameof(percentile), "Percentile must be between 0 and 1 inclusive.");

        if (sequence.Length == 1) return sequence[0];

        // Work on a sorted copy (ascending)
        var values = new decimal[sequence.Length];
        Array.Copy(sequence, values, sequence.Length);
        Array.Sort(values);

        // Endpoints
        if (percentile == 0m) return values[0];
        if (percentile == 1m) return values[^1];

        // r = 1 + (n - 1) * k  (1-based rank)
        int n = values.Length;
        decimal r = 1m + (n - 1) * percentile;

        decimal floorR = decimal.Floor(r);
        int i = (int)floorR;              // 1-based lower index
        decimal frac = r - floorR;        // fractional part in [0, 1)

        int lower = i - 1;                // 0-based
        int upper = Math.Min(lower + 1, n - 1);

        // If rank is an integer, return that exact value
        if (frac == 0m) return values[lower];

        // Linear interpolation between the two surrounding ranks
        decimal a = values[lower];
        decimal b = values[upper];
        return a + (b - a) * frac;
    }
    
    public static SimSnapshot CreateSimSnapshot(MonteCarloSim sim)
    {
        return new SimSnapshot()
        {
            AreWeInARecession = sim.RecessionStats.AreWeInARecession,
            AreWeInExtremeAusterityMeasures = sim.RecessionStats.AreWeInExtremeAusterityMeasures,
            CurrentDateWithinSim = sim.CurrentDateInSim,
            IsBankrupt = sim.PgPerson.IsBankrupt,
            IsRetired = sim.PgPerson.IsRetired,
            NetWorth = AccountCalculation.CalculateNetWorth(sim.BookOfAccounts),
            TotalFunPointsSoFar = sim.LifetimeSpend.TotalFunPointsLifetime,
            TotalSpendSoFar = sim.LifetimeSpend.TotalSpendLifetime,
            TotalTaxSoFar = sim.TaxLedger.TotalTaxPaidLifetime
        };
    }

    // todo: UT SingleModelRunResultStatLineAtTime
    public static SingleModelRunResultStatLineAtTime CreateSingleModelRunResultStatLineAtTime(LocalDateTime date,
        decimal[] values)
    {
        var percentile10 = CalculatePercentileValue(values, 0.1m);
        var percentile25 = CalculatePercentileValue(values, 0.25m);
        var percentile50 = CalculatePercentileValue(values, 0.5m);
        var percentile75 = CalculatePercentileValue(values, 0.75m);
        var percentile90 = CalculatePercentileValue(values, 0.9m);
        return new SingleModelRunResultStatLineAtTime
        (
            date,
            percentile10,
            percentile25,
            percentile50,
            percentile75,
            percentile90
        );
    }

    public static decimal GetLastValueFromSingleModelRunResultBankruptcyRateAtTime(SingleModelRunResultBankruptcyRateAtTime[] statsArray)
    {
        if (statsArray.Length == 0) throw new ArgumentException("Stats array cannot be empty");

        // Get the last entry in the time series
        var lastEntry = statsArray.MaxBy(x => x.Date);
        if (lastEntry == null) throw new ArgumentException("Last entry cannot be null");
        return lastEntry.BankruptcyRate;
    }
    public static decimal GetLastValueFromSingleModelRunResultsOverTime(SingleModelRunResultStatLineAtTime[] statsArray, int percentile)
    {
        if (statsArray.Length == 0) throw new ArgumentException("Stats array cannot be empty");

        // Get the last entry in the time series
        var lastEntry = statsArray.MaxBy(x => x.Date);
        
        var type = typeof(SingleModelRunResult);
        var properties = type.GetProperties();
        
        // Dictionary to map percentile numbers to their corresponding property names in SingleModelRunResultStatLineAtTime
        var percentileMap = new Dictionary<int, string>
        {
            { 10, nameof(SingleModelRunResultStatLineAtTime.Percentile10) },
            { 25, nameof(SingleModelRunResultStatLineAtTime.Percentile25) },
            { 50, nameof(SingleModelRunResultStatLineAtTime.Percentile50) },
            { 75, nameof(SingleModelRunResultStatLineAtTime.Percentile75) },
            { 90, nameof(SingleModelRunResultStatLineAtTime.Percentile90) }
        };
        
        var percentileValue = (decimal)typeof(SingleModelRunResultStatLineAtTime)
            .GetProperty(percentileMap[percentile])!
            .GetValue(lastEntry)!;
        
        return percentileValue;
    }
    

    /// <summary>
    /// reads the output from running all lives on a single model and creates statistical views
    /// </summary>
    public static SingleModelRunResult InterpretSimulationResults(McModel model, List<SimSnapshot>[] allSnapshots)
    {
        // todo: unit test InterpretSimulationResults
        
        var minDate = MonteCarloConfig.MonteCarloSimStartDate;
        var maxDate = MonteCarloConfig.MonteCarloSimEndDate;
        var span = maxDate - minDate;
        var monthsCount = (span.Years * 12) + span.Months + 1;
        var netWorthStatsOverTime = new SingleModelRunResultStatLineAtTime[monthsCount];
        var totalFunPointsOverTime = new SingleModelRunResultStatLineAtTime[monthsCount];
        var totalSpendOverTime = new SingleModelRunResultStatLineAtTime[monthsCount];
        var totalTaxOverTime = new SingleModelRunResultStatLineAtTime[monthsCount];
        var bankruptcyRateOverTime = new SingleModelRunResultBankruptcyRateAtTime[monthsCount]; 
        var cursorDate = minDate;
        int i = 0;
        while (cursorDate <= maxDate)
        {
            var allSimSnapshotsThatDay = allSnapshots
                .SelectMany(x => x)
                .Where(y => y.CurrentDateWithinSim == cursorDate)
                .ToList();
            var allNetWorths = allSimSnapshotsThatDay.Select(x => x.NetWorth).ToArray();
            var allFun = allSimSnapshotsThatDay.Select(x => x.TotalFunPointsSoFar).ToArray();
            var allSpend = allSimSnapshotsThatDay.Select(x => x.TotalSpendSoFar).ToArray();
            var allTax = allSimSnapshotsThatDay.Select(x => x.TotalTaxSoFar).ToArray();
            var allBankruptcies = allSimSnapshotsThatDay.Select(x => x.IsBankrupt).ToArray();
            
            netWorthStatsOverTime[i] = CreateSingleModelRunResultStatLineAtTime(cursorDate, allNetWorths);
            totalFunPointsOverTime[i] = CreateSingleModelRunResultStatLineAtTime(cursorDate, allFun);
            totalSpendOverTime[i] = CreateSingleModelRunResultStatLineAtTime(cursorDate, allSpend);
            totalTaxOverTime[i] = CreateSingleModelRunResultStatLineAtTime(cursorDate, allTax);
            var bankruptcyRate = allBankruptcies.Count(x => x) / (decimal)allBankruptcies.Length;
            bankruptcyRateOverTime[i] = new SingleModelRunResultBankruptcyRateAtTime(cursorDate, bankruptcyRate);
            
            cursorDate = cursorDate.PlusMonths(1);
            i++;
        }

        var result= new SingleModelRunResult()
        {
            ModelId = model.Id,
            RunDate = LocalDateTime.FromDateTime(DateTime.Now),
            MajorVersion = ModelConstants.MajorVersion,
            MinorVersion = ModelConstants.MinorVersion,
            PatchVersion = ModelConstants.PatchVersion,
            NumLivesRun = MonteCarloConfig.NumLivesPerModelRun,
            AllSnapshots = allSnapshots,
            NetWorthStatsOverTime = netWorthStatsOverTime,
            TotalFunPointsOverTime = totalFunPointsOverTime,
            TotalSpendOverTime = totalSpendOverTime,
            TotalTaxOverTime = totalTaxOverTime,
            BankruptcyRateOverTime = bankruptcyRateOverTime,
            NetWorthAtEndOfSim10 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(netWorthStatsOverTime, 10),
            NetWorthAtEndOfSim25 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(netWorthStatsOverTime, 25),
            NetWorthAtEndOfSim50 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(netWorthStatsOverTime, 50),
            NetWorthAtEndOfSim75 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(netWorthStatsOverTime, 75),
            NetWorthAtEndOfSim90 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(netWorthStatsOverTime, 90),
            FunPointsAtEndOfSim10 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(totalFunPointsOverTime, 10),
            FunPointsAtEndOfSim25 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(totalFunPointsOverTime, 25),
            FunPointsAtEndOfSim50 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(totalFunPointsOverTime, 50),
            FunPointsAtEndOfSim75 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(totalFunPointsOverTime, 75),
            FunPointsAtEndOfSim90 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(totalFunPointsOverTime, 90),
            SpendAtEndOfSim10 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(totalSpendOverTime, 10),
            SpendAtEndOfSim25 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(totalSpendOverTime, 25),
            SpendAtEndOfSim50 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(totalSpendOverTime, 50),
            SpendAtEndOfSim75 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(totalSpendOverTime, 75),
            SpendAtEndOfSim90 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(totalSpendOverTime, 90),
            TaxAtEndOfSim10 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(totalTaxOverTime, 10),
            TaxAtEndOfSim25 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(totalTaxOverTime, 25),
            TaxAtEndOfSim50 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(totalTaxOverTime, 50),
            TaxAtEndOfSim75 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(totalTaxOverTime, 75),
            TaxAtEndOfSim90 = Simulation.GetLastValueFromSingleModelRunResultsOverTime(totalTaxOverTime, 90),
            BankruptcyRateAtEndOfSim = GetLastValueFromSingleModelRunResultBankruptcyRateAtTime(bankruptcyRateOverTime)
        };
        return result;
    }
    public static bool IsReconciliationPeriod(LocalDateTime currentDate)
    {
        if (!MonteCarloConfig.DebugMode) return false;
        if (currentDate < MonteCarloConfig.ReconciliationSimStartDate) return false;
        if (currentDate > MonteCarloConfig.ReconciliationSimEndDate) return false;
        return true;
    }

    /// <summary>
    /// reads any LocalDateTime and returns the first of the month closest to it
    /// </summary>
    public static LocalDateTime NormalizeDate(LocalDateTime providedDate)
    {
        var firstOfThisMonth = new LocalDateTime(providedDate.Year, providedDate.Month, 1, 0, 0);
        var firstOfNextMonth = firstOfThisMonth.PlusMonths(1);
        var timeSpanToThisFirst = providedDate - firstOfThisMonth;
        var timeSpanToNextFirst = firstOfNextMonth - providedDate;
        return (timeSpanToThisFirst.Days <= timeSpanToNextFirst.Days) ?
            firstOfThisMonth : // t2 is longer, return this first
            firstOfNextMonth; // t1 is longer than t2, return next first
    }

    public static (PgPerson person, McModel model) NormalizeDates(PgPerson person, McModel model)
    {
        // set up the return tuple
        (PgPerson newPerson, McModel newModel) result = (Person.CopyPerson(person, true), model);
        result.newPerson.BirthDate = NormalizeDate(person.BirthDate);
        result.newModel.RetirementDate = NormalizeDate(model.RetirementDate);
        result.newModel.SocialSecurityStart = NormalizeDate(model.SocialSecurityStart);
        result.newModel.SimEndDate = NormalizeDate(model.SimEndDate);
        result.newModel.SimStartDate = NormalizeDate(model.SimStartDate);
        return result;
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
    
    // todo: unit test ProcessPaycheck
    public static (BookOfAccounts accounts, TaxLedger ledger, LifetimeSpend spend, List<ReconciliationMessage> messages) 
        ProcessPaycheck(PgPerson person, LocalDateTime currentDate, BookOfAccounts accounts, TaxLedger ledger,
            LifetimeSpend spend, McModel simParams, CurrentPrices prices)
    {
        var paydayResult = Payday.ProcessPreRetirementPaycheck(
                person, currentDate, accounts, ledger, spend, simParams, prices);
        return (paydayResult.bookOfAccounts, paydayResult.ledger, paydayResult.spend, paydayResult.messages);
    }

    // todo: unit test ProcessSocialSecurityCheck
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
    
    public static  (LifetimeSpend spend, List<ReconciliationMessage> messages) RecordFunAndAnxiety(
        McModel simParams, PgPerson person, LocalDateTime currentDate, RecessionStats recessionStats,
        LifetimeSpend spend)
    {
        // set up the return tuple
        (LifetimeSpend spend, List<ReconciliationMessage> messages) results = (
                Spend.CopyLifetimeSpend(spend),
                []
            );
        
        var extraFun = 0.0m;
        var requiredSpend = Spend.CalculateMonthlyRequiredSpendWithoutDebt(simParams, person, currentDate);
        
        if (person.IsBankrupt) extraFun += ModelConstants.FunPenaltyBankruptcy;
        else
        {
            // ignore other penalties and bonuses if bankrupt. bankruptcy sucks regardless

            if (person.IsRetired)
            {
                extraFun += ModelConstants.FunBonusRetirement;
            }

            if (!person.IsRetired)
            {
                extraFun += requiredSpend * ModelConstants.FunPenaltyNotRetiredPercentOfRequiredSpend * -1;
            }

            if (person.IsRetired && recessionStats.AreWeInARecession)
            {
                extraFun += requiredSpend * ModelConstants.FunPenaltyRetiredInRecessionPercentOfRequiredSpend * -1;
            }

            if (person.IsRetired && recessionStats.AreWeInExtremeAusterityMeasures)
            {
                extraFun += requiredSpend * ModelConstants.FunPenaltyRetiredInExtremeAusterityPercentOfRequiredSpend *
                            -1;
            }
        }

        var spendResult = Spend.RecordFunPoints(spend, extraFun, currentDate);
        results.spend = spendResult.spend;
        
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.AddRange(spendResult.messages);
        return results;
    }
    
    public static CurrentPrices SetNewPrices(CurrentPrices prices, Dictionary<LocalDateTime, Decimal>  hypotheticalPrices,
        LocalDateTime currentDate)
    {
        if (!hypotheticalPrices.TryGetValue(currentDate, out var priceGrowthRate))
        {
            throw new InvalidDataException("CurrentDate not found in hypotheticalPrices");
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