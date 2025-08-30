using System.Diagnostics;
using Lib.DataTypes;
using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
using Lib.StaticConfig;
using Lib.Utils;
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
    
    public static SimSnapshot CreateSimSnapshot(SimData simData)
    {
        return new SimSnapshot()
        {
            AreWeInARecession = simData.RecessionStats.AreWeInARecession,
            AreWeInExtremeAusterityMeasures = simData.RecessionStats.AreWeInExtremeAusterityMeasures,
            CurrentDateWithinSim = simData.CurrentDateInSim,
            IsBankrupt = simData.PgPerson.IsBankrupt,
            IsRetired = simData.PgPerson.IsRetired,
            NetWorth = AccountCalculation.CalculateNetWorth(simData.BookOfAccounts),
            TotalFunPointsSoFar = simData.LifetimeSpend.TotalFunPointsLifetime,
            TotalSpendSoFar = simData.LifetimeSpend.TotalSpendLifetime,
            TotalTaxSoFar = simData.TaxLedger.TotalTaxPaidLifetime,
            TotalHealthCareSpendSoFar = simData.LifetimeSpend.TotalLifetimeHealthCareSpend,
            TotalCapitalGainsSoFar = simData.TaxLedger.ShortTermCapitalGains.Sum(x => x.amount) 
                                     + simData.TaxLedger.LongTermCapitalGains.Sum(x => x.amount),
            TotalIraDistributionsSoFar = simData.TaxLedger.TaxableIraDistribution.Sum(x => x.amount),
            TotalTaxFreeWithdrawalsSoFar = simData.TaxLedger.TaxFreeWithrawals.Sum(x => x.amount),
            TotalFunSpendSoFar = simData.LifetimeSpend.TotalLifetimeFunSpend,
            TotalNotFunSpendSoFar = simData.LifetimeSpend.TotalLifetimeRequiredSpend,
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

    public static SingleModelRunResultStatLineAtTime[] CreateYearlyMarkersFromSimSnapshots( 
        int[] orderedYears, SingleModelRunResultStatLineAtTime[] specificValuesOverTime)
    {
        
        var result = new SingleModelRunResultStatLineAtTime[orderedYears.Length];
        var totalsAfterLastYear = new SingleModelRunResultStatLineAtTime(
            new LocalDateTime(1900, 12, 31, 0, 0),
            0, 0, 0, 0, 0);
        for (int i = 0; i < orderedYears.Length; i++)
        {
            var statsInYear = specificValuesOverTime
                .Where(x => x.Date.Year == orderedYears[i])
                .ToArray();
            var date = new LocalDateTime(orderedYears[i], 12, 31, 0, 0);
            var thisYears10Lifetime = GetLastValueFromSingleModelRunResultsOverTime(statsInYear, 10);
            var thisYears25Lifetime = GetLastValueFromSingleModelRunResultsOverTime(statsInYear, 25);
            var thisYears50Lifetime = GetLastValueFromSingleModelRunResultsOverTime(statsInYear, 50);
            var thisYears75Lifetime = GetLastValueFromSingleModelRunResultsOverTime(statsInYear, 75);
            var thisYears90Lifetime = GetLastValueFromSingleModelRunResultsOverTime(statsInYear, 90);
            
            var thisYearsStatLine = new SingleModelRunResultStatLineAtTime(
                date,
                thisYears10Lifetime - totalsAfterLastYear.Percentile10,
                thisYears25Lifetime - totalsAfterLastYear.Percentile25,
                thisYears50Lifetime - totalsAfterLastYear.Percentile50,
                thisYears75Lifetime - totalsAfterLastYear.Percentile75,
                thisYears90Lifetime - totalsAfterLastYear.Percentile90
            );
            result[i] = thisYearsStatLine;
            totalsAfterLastYear = new SingleModelRunResultStatLineAtTime(
                date,
                thisYears10Lifetime,
                thisYears25Lifetime,
                thisYears50Lifetime,
                thisYears75Lifetime,
                thisYears90Lifetime
            );
        }
        return result;
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
    public static SingleModelRunResult InterpretSimulationResults(DataTypes.MonteCarlo.Model model, List<SimSnapshot>[] allSnapshots)
    {
        // todo: unit test InterpretSimulationResults
        
        var maxDate = MonteCarloConfig.MonteCarloSimEndDate;
        var minDate = (MonteCarloConfig.ModelTrainingMode == true)? maxDate : MonteCarloConfig.MonteCarloSimStartDate;
        var span = maxDate - minDate;
        var monthsCount = (span.Years * 12) + span.Months + 1;
        var netWorthStatsOverTime = new SingleModelRunResultStatLineAtTime[monthsCount];
        var totalFunPointsOverTime = new SingleModelRunResultStatLineAtTime[monthsCount];
        var totalSpendOverTime = new SingleModelRunResultStatLineAtTime[monthsCount];
        var totalTaxOverTime = new SingleModelRunResultStatLineAtTime[monthsCount];
        var totalHealthSpendOverTime = new SingleModelRunResultStatLineAtTime[monthsCount];
        var totalIraDistributionsOverTime = new SingleModelRunResultStatLineAtTime[monthsCount];
        var totalCapitalGainsOverTime = new SingleModelRunResultStatLineAtTime[monthsCount];
        var totalTaxFreeWithdrawalsOverTime = new SingleModelRunResultStatLineAtTime[monthsCount];
        var totalFunSpendOverTime = new SingleModelRunResultStatLineAtTime[monthsCount];
        var totalNotFunSpendOverTime = new SingleModelRunResultStatLineAtTime[monthsCount];
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
            var allFunPoints = allSimSnapshotsThatDay.Select(x => x.TotalFunPointsSoFar).ToArray();
            var allSpend = allSimSnapshotsThatDay.Select(x => x.TotalSpendSoFar).ToArray();
            var allTax = allSimSnapshotsThatDay.Select(x => x.TotalTaxSoFar).ToArray();
            var allBankruptcies = allSimSnapshotsThatDay.Select(x => x.IsBankrupt).ToArray();
            var allHealthSpend = allSimSnapshotsThatDay.Select(x => x.TotalHealthCareSpendSoFar).ToArray();
            var allIraDistributions = allSimSnapshotsThatDay.Select(x => x.TotalIraDistributionsSoFar).ToArray();
            var allCapitalGains = allSimSnapshotsThatDay.Select(x => x.TotalCapitalGainsSoFar).ToArray();
            var allTaxFreeWithdrawals = allSimSnapshotsThatDay.Select(x => x.TotalTaxFreeWithdrawalsSoFar).ToArray();
            var allFunSpend = allSimSnapshotsThatDay.Select(x => x.TotalFunSpendSoFar).ToArray();
            var allNotFunSpend = allSimSnapshotsThatDay.Select(x => x.TotalNotFunSpendSoFar).ToArray();
            
            netWorthStatsOverTime[i] = CreateSingleModelRunResultStatLineAtTime(cursorDate, allNetWorths);
            totalFunPointsOverTime[i] = CreateSingleModelRunResultStatLineAtTime(cursorDate, allFunPoints);
            totalSpendOverTime[i] = CreateSingleModelRunResultStatLineAtTime(cursorDate, allSpend);
            totalTaxOverTime[i] = CreateSingleModelRunResultStatLineAtTime(cursorDate, allTax);
            totalHealthSpendOverTime[i] = CreateSingleModelRunResultStatLineAtTime(cursorDate, allHealthSpend);
            totalIraDistributionsOverTime[i] = CreateSingleModelRunResultStatLineAtTime(cursorDate, allIraDistributions);
            totalCapitalGainsOverTime[i] = CreateSingleModelRunResultStatLineAtTime(cursorDate, allCapitalGains);
            totalTaxFreeWithdrawalsOverTime[i] = CreateSingleModelRunResultStatLineAtTime(cursorDate, allTaxFreeWithdrawals);
            totalFunSpendOverTime[i] = CreateSingleModelRunResultStatLineAtTime(cursorDate, allFunSpend);
            totalNotFunSpendOverTime[i] = CreateSingleModelRunResultStatLineAtTime(cursorDate, allNotFunSpend);
            
            var bankruptcyRate = allBankruptcies.Count(x => x) / (decimal)allBankruptcies.Length;
            bankruptcyRateOverTime[i] = new SingleModelRunResultBankruptcyRateAtTime(cursorDate, bankruptcyRate);
            
            cursorDate = cursorDate.PlusMonths(1);
            i++;
        }
        
        var orderedYears = allSnapshots
            .SelectMany(x => x)
            .Select(x => x.CurrentDateWithinSim.Year)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

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
            FunPointsByYear = CreateYearlyMarkersFromSimSnapshots(orderedYears, totalFunPointsOverTime),
            SpendByYear = CreateYearlyMarkersFromSimSnapshots(orderedYears, totalSpendOverTime),
            TaxByYear = CreateYearlyMarkersFromSimSnapshots(orderedYears, totalTaxOverTime),
            
            HealthSpendByYear = CreateYearlyMarkersFromSimSnapshots(orderedYears, totalHealthSpendOverTime),
            IraDistributionsByYear = CreateYearlyMarkersFromSimSnapshots(orderedYears, totalIraDistributionsOverTime),
            CapitalGainsByYear = CreateYearlyMarkersFromSimSnapshots(orderedYears, totalCapitalGainsOverTime),
            TaxFreeWithdrawalsByYear = CreateYearlyMarkersFromSimSnapshots(orderedYears, totalTaxFreeWithdrawalsOverTime),
            FunSpendByYear = CreateYearlyMarkersFromSimSnapshots(orderedYears, totalFunSpendOverTime),
            NotFunSpendByYear = CreateYearlyMarkersFromSimSnapshots(orderedYears, totalNotFunSpendOverTime),
            
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

    public static (PgPerson person, DataTypes.MonteCarlo.Model model) NormalizeDates(PgPerson person, DataTypes.MonteCarlo.Model model)
    {
        // set up the return tuple
        (PgPerson newPerson, DataTypes.MonteCarlo.Model newModel) result = (Person.CopyPerson(person, true), model);
        result.newPerson.BirthDate = DateFunc.NormalizeDate(person.BirthDate);
        result.newModel.RetirementDate = DateFunc.NormalizeDate(model.RetirementDate);
        result.newModel.SocialSecurityStart = DateFunc.NormalizeDate(model.SocialSecurityStart);
        result.newModel.SimEndDate = DateFunc.NormalizeDate(model.SimEndDate);
        result.newModel.SimStartDate = DateFunc.NormalizeDate(model.SimStartDate);
        return result;
    }

    // todo: write a UT to make sure that PayForStuff records the health spend
    public static (bool isSuccessful, BookOfAccounts accounts, TaxLedger ledger, LifetimeSpend spend, 
        List<ReconciliationMessage> messages) PayForStuff(DataTypes.MonteCarlo.Model model, PgPerson person, LocalDateTime currentDate,
            RecessionStats recessionStats, TaxLedger ledger,
        LifetimeSpend spend, BookOfAccounts accounts)
    {
        var funSpend = Spend.CalculateMonthlyFunSpend(model, person, currentDate);
        var requiredSpendResults = Spend.CalculateMonthlyRequiredSpendWithoutDebt(model, person, currentDate);
        var notFunSpend = requiredSpendResults.TotalSpend;
        
        // required spend can't move. But your fun spend can go down if we're in a recession or up if we livin' large
        funSpend = Spend.CalculateSpendOverride(model, funSpend, recessionStats);
        
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
        
        // record the spends
        var funPointsToRecord = Spend.CalculateFunPointsForSpend(funSpend, person, currentDate);
        var totalSpend = funSpend + notFunSpend;
        var recordResults = Spend.RecordMultiSpend(results.spend, currentDate, totalSpend,
            null, null, null, 
            null, funPointsToRecord, requiredSpendResults.HealthSpend, funSpend, 
            notFunSpend);
        results.spend = recordResults.spend;
        results.messages.AddRange(recordResults.messages);
        
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
            var recordSpendResults = Spend.RecordMultiSpend(results.spend, currentDate, null,
                null, null, null, 
                null, null, null, null, 
                taxLiability);
            results.spend = recordSpendResults.spend;
            results.messages.AddRange(recordSpendResults.messages);
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
            LifetimeSpend spend, DataTypes.MonteCarlo.Model model, CurrentPrices prices)
    {
        var paydayResult = Payday.ProcessPreRetirementPaycheck(
                person, currentDate, accounts, ledger, spend, model, prices);
        return (paydayResult.bookOfAccounts, paydayResult.ledger, paydayResult.spend, paydayResult.messages);
    }

    // todo: unit test ProcessSocialSecurityCheck
    public static (BookOfAccounts accounts, TaxLedger ledger, LifetimeSpend spend, List<ReconciliationMessage> messages) 
        ProcessSocialSecurityCheck(PgPerson person, LocalDateTime currentDate, BookOfAccounts accounts,
            TaxLedger ledger, LifetimeSpend spend, DataTypes.MonteCarlo.Model model)
    {
        var paydayResult = Payday.ProcessSocialSecurityCheck(
                person, currentDate, accounts, ledger, spend, model);
        return (paydayResult.bookOfAccounts, paydayResult.ledger, paydayResult.spend, paydayResult.messages);
    }
    
    public static (BookOfAccounts accounts, TaxLedger ledger, LifetimeSpend spend, List<ReconciliationMessage> messages)
        ProcessPayday (PgPerson person, LocalDateTime currentDate, BookOfAccounts accounts, TaxLedger ledger,
            LifetimeSpend spend, DataTypes.MonteCarlo.Model model, CurrentPrices prices)
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
                person, currentDate, accounts, ledger, spend, model, prices);
            results.accounts = paydayResult.accounts;
            results.ledger = paydayResult.ledger;
            results.spend = paydayResult.spend;
            results.messages.AddRange(paydayResult.messages);
        }

        // if you aren't yet drawing SS, just return now
        if (currentDate < model.SocialSecurityStart) return results;
        
        // this isn't an "else" you can be working and draw SS
        if(MonteCarloConfig.DebugMode) results.messages.Add(new ReconciliationMessage(
            currentDate, null, "processing social security check"));
        var ssResult = ProcessSocialSecurityCheck(
            person, currentDate, accounts, ledger, spend, model);
        results.accounts = ssResult.accounts;
        results.ledger = ssResult.ledger;
        results.spend = ssResult.spend;
        results.messages.AddRange(ssResult.messages);
        return results;
    }
    
    public static  (LifetimeSpend spend, List<ReconciliationMessage> messages) RecordFunAndAnxiety(
        DataTypes.MonteCarlo.Model model, PgPerson person, LocalDateTime currentDate, RecessionStats recessionStats,
        LifetimeSpend spend, BookOfAccounts accounts)
    {
        // set up the return tuple
        (LifetimeSpend spend, List<ReconciliationMessage> messages) results = (
                Spend.CopyLifetimeSpend(spend),
                []
            );
        
        var extraFun = 0.0m;
        var requiredSpend = Spend.CalculateMonthlyRequiredSpend(
            model, person, currentDate, accounts)
            .TotalSpend;
        
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

        var spendResult = Spend.RecordMultiSpend(results.spend, currentDate, null,
            null, null, null, 
            null, extraFun, null, null, 
            null);
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
        LocalDateTime currentDate, PgPerson person, DataTypes.MonteCarlo.Model model)
    {
        if (person.IsRetired) return (true, person);
        if (currentDate < model.RetirementDate) return (false, person);
        
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

        results.isSuccessful = true;
        return results;
    }
}