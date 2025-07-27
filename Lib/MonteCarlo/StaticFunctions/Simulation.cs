using System.Diagnostics;
using Lib.DataTypes;
using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Simulation
{
    #region sim copy functions

    public static LifetimeSpend CopyLifetimeSpend(LifetimeSpend lifetimeSpend)
    {
        return new LifetimeSpend()
        {
            TotalDebtAccrualLifetime = lifetimeSpend.TotalDebtAccrualLifetime,
            TotalDebtPaidLifetime = lifetimeSpend.TotalDebtPaidLifetime,
            TotalInvestmentAccrualLifetime = lifetimeSpend.TotalInvestmentAccrualLifetime,
            TotalSocialSecurityWageLifetime = lifetimeSpend.TotalSocialSecurityWageLifetime,
            TotalSpendLifetime = lifetimeSpend.TotalSpendLifetime,
        };
    }
    #endregion sim copy functions
    public static NetWorthMeasurement CalculatePercentileValue(NetWorthMeasurement[] sequence,
        decimal percentile)
    {
        // assumes the list is already sorted
        /*
         * length of sequence = 15
         * percentile = .7
         * target row = 0.7 * 15 = 10.5
         * target row = 11 (rounded to nearest int)
         * target row = 10 (zero-indexed)
         *
         * */
        int numRows = sequence.Length;
        decimal targetRowDecimal = numRows * percentile;
        int targetRowInt = (int)(Math.Round(targetRowDecimal, 0));
        return sequence[targetRowInt];
    }
    
    /// <summary>
    /// reads the output from running all lives on a single model and creates statistical views
    /// </summary>
    public static SimulationAllLivesResult InterpretSimulationResults(List<NetWorthMeasurement>[] allMeasurements)
    {
        throw new NotImplementedException();
        // List<SimulationAllLivesResult> batchResults = [];
        // var minDate = allMeasurements.Min(x => x.MeasuredDate);
        // var maxDate = allMeasurements.Max(x => x.MeasuredDate);
        // LocalDateTime dateCursor = minDate;
        // int totalBankruptcies = 0;
        // while (dateCursor <= maxDate)
        // {
        //     // get all the total spend measurements for this date
        //     NetWorthMeasurement[] valuesAtDate = allMeasurements
        //         .Where(x => x.MeasuredDate == dateCursor)
        //         .OrderBy(x => x.TotalSpend)
        //         .ToArray();
        //
        //     // total bankruptcies is a running list of all bankruptcies so
        //     // far. it will grow as the date cursor moves forward
        //     totalBankruptcies += valuesAtDate.Where(x => x.NetWorth <= 0).Count();
        //     var simAt90PercentileSpend = CalculatePercentileValue(valuesAtDate, 0.9M);
        //     var simAt75PercentileSpend = CalculatePercentileValue(valuesAtDate, 0.75M);
        //     var simAt50PercentileSpend = CalculatePercentileValue(valuesAtDate, 0.5M);
        //     var simAt25PercentileSpend = CalculatePercentileValue(valuesAtDate, 0.25M);
        //     var simAt10PercentileSpend = CalculatePercentileValue(valuesAtDate, 0.1M);
        //     batchResults.Add(new SimulationAllLivesResult()
        //     {
        //         Id = Guid.NewGuid(),
        //         ModelId = _mcModel.Id,
        //         MeasuredDate = dateCursor,
        //         NetWorthAt90thPercentile = simAt90PercentileSpend.NetWorth,
        //         NetWorthAt75thPercentile = simAt75PercentileSpend.NetWorth,
        //         NetWorthAt50thPercentile = simAt50PercentileSpend.NetWorth,
        //         NetWorthAt25thPercentile = simAt25PercentileSpend.NetWorth,
        //         NetWorthAt10thPercentile = simAt10PercentileSpend.NetWorth,
        //         SpendAt90thPercentile = simAt90PercentileSpend.TotalSpend,
        //         SpendAt75thPercentile = simAt75PercentileSpend.TotalSpend,
        //         SpendAt50thPercentile = simAt50PercentileSpend.TotalSpend,
        //         SpendAt25thPercentile = simAt25PercentileSpend.TotalSpend,
        //         SpendAt10thPercentile = simAt10PercentileSpend.TotalSpend,
        //         TaxesAt90thPercentile = simAt90PercentileSpend.TotalTax,
        //         TaxesAt75thPercentile = simAt75PercentileSpend.TotalTax,
        //         TaxesAt50thPercentile = simAt50PercentileSpend.TotalTax,
        //         TaxesAt25thPercentile = simAt25PercentileSpend.TotalTax,
        //         TaxesAt10thPercentile = simAt10PercentileSpend.TotalTax,
        //         BankruptcyRate = (1.0M * totalBankruptcies) / (1.0M * allMeasurements.Count),
        //     });
        //     dateCursor = dateCursor.PlusMonths(1);
        // }
        // return batchResults;
    }
    
    /// <summary>
    /// Trigger the simulator for a single run after you've created all the default accounts, parameters and pricing.
    /// Can be used by RunSingle and Train.
    /// </summary>
    
    public static List<NetWorthMeasurement> ExecuteSingleModelSingleLife(Logger logger, McModel model, PgPerson person, 
        List<McInvestmentAccount> investmentAccounts, List<McDebtAccount> debtAccounts,
        Dictionary<LocalDateTime, Decimal> hypotheticalPrices)
    {
        LifeSimulator sim = new LifeSimulator(
            logger, model, person, investmentAccounts, debtAccounts, hypotheticalPrices);
        return sim.Run();
    }

    /// <summary>
    /// runs all simulation lives for a single model
    /// </summary>
    /// <returns>an array of NetWorthMeasurement lists. Each element in the array represents one simulated life</returns>
    public static List<NetWorthMeasurement>[] ExecuteSingleModelAllLives(Logger logger,
        McModel model, PgPerson person, List<McInvestmentAccount> investmentAccounts, List<McDebtAccount> debtAccounts,
        Dictionary<LocalDateTime, decimal>[] allPricingDicts)
    {
        int numLivesPerModelRun = MonteCarloConfig.NumLivesPerModelRun;
        List<NetWorthMeasurement>[] runs = new List<NetWorthMeasurement>[numLivesPerModelRun];
        
        if(MonteCarloConfig.ShouldRunParallel) Parallel.For(0, numLivesPerModelRun, i =>
        {
            var newPerson = Person.CopyPerson(person, false);
            LifeSimulator sim = new(logger, model, newPerson, investmentAccounts, debtAccounts, allPricingDicts[i]);
            runs[i] = sim.Run();
        });
        else for(int i = 0; i < numLivesPerModelRun; i++)
        {
            var newPerson = Person.CopyPerson(person, false);
            LifeSimulator sim = new(logger, model, newPerson, investmentAccounts, debtAccounts, allPricingDicts[i]);
            runs[i] = sim.Run();
        }
        return runs;
    }

    
    /// <summary>
    /// provides results from a pre-determined model. It still runs that
    /// moddel numSimulations times, each over a different "randomized"
    /// set of price simulations
    /// </summary>
    public static SimulationAllLivesResult RunSingleModelSession(
        Logger logger, McModel simParams, PgPerson person, List<McInvestmentAccount> investmentAccounts,
        List<McDebtAccount> debtAccounts, decimal[] historicalPrices)
    {   
        Stopwatch stopwatch = Stopwatch.StartNew();
        logger.Debug("Creating simulation pricing");
        
        /*
         * create the pricing for all lives
         */
        var hypotheticalPrices = Pricing.CreateHypotheticalPricingForRuns(historicalPrices);
        
        stopwatch.Stop();
        var duration = stopwatch.Elapsed;
        logger.Debug(logger.FormatTimespanDisplay("Created simulation pricing", duration));
        
        
        stopwatch = Stopwatch.StartNew();
        logger.Debug("Running all lives for a single model");
        
        /*
         * run all sim lives
         */
        var allLivesRuns = ExecuteSingleModelAllLives(
            logger, simParams, person, investmentAccounts, debtAccounts, hypotheticalPrices);
        
        stopwatch.Stop();
        logger.Debug(logger.FormatTimespanDisplay("Ran all lives for a single model", duration));
        
        /*
         * batch up the results into something meaningful
         */
        var results = InterpretSimulationResults(allLivesRuns);
        return results;
    }
    
    /// <summary>
    /// pull the recent champions from the DB, mate them to one another, write the best results back to the DB
    /// </summary>
    /// <returns></returns>
    public static void RunModelTrainingSession(
        Logger logger, PgPerson person, List<McInvestmentAccount> investmentAccounts, List<McDebtAccount> debtAccounts, 
        decimal[] historicalPrices)
    {   
        Stopwatch stopwatch = Stopwatch.StartNew();
        logger.Debug("Creating simulation pricing");
        
        /*
         * create the pricing for all lives
         */
        var hypotheticalPrices = Pricing.CreateHypotheticalPricingForRuns(historicalPrices);
        
        stopwatch.Stop();
        var duration = stopwatch.Elapsed;
        logger.Debug(logger.FormatTimespanDisplay("Created simulation pricing", duration));
        
        
        /*
         * pull the current champs from the DB
         */
        throw new NotImplementedException();
        var startDate = MonteCarloConfig.MonteCarloSimStartDate;
        var endDate = MonteCarloConfig.MonteCarloSimEndDate;
        McModel[] currentChamps = [
                // todo: pull model champs from the database
                            ];
        
        
        /*
         * breed and run
         */
        List<(McModel simParams, SimulationAllLivesResult result)> results = [];
        for(int i1 = 0; i1 < currentChamps.Length; i1++)
        {
            for (int i2 = 0; i2 < currentChamps.Length; i2++)
            {
                logger.Info($"running {i1} bred with {i2}");
                var offspring = Model.MateModels(currentChamps[i1], currentChamps[i2], person.BirthDate);
                var allLivesRuns2 = ExecuteSingleModelAllLives(
                    logger, offspring, person, investmentAccounts, debtAccounts, hypotheticalPrices);
                var modelResults = InterpretSimulationResults(allLivesRuns2);
                
                results.Add((offspring, modelResults));
            }
        }
        
        /*
         * write results back to the db
         */
        throw new NotImplementedException();
    }
}