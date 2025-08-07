using System.Diagnostics;
using Lib.DataTypes;
using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;


/// <summary>
/// This class houses functions that deal with a sim in aggregate. They either trigger LifeSimulation runs or process
/// the results of such runs
/// </summary>
public class SimulationTrigger
{
    #region DB functions

    public static void SaveModelToDb(McModel model)
    {
        using var context = new PgContext();
        context.McModels.Add(model);
        context.SaveChanges();
    }
    public static void SaveSingleModelRunResultsToDb(Lib.DataTypes.MonteCarlo.SingleModelRunResult result)
    {
        using var context = new PgContext();
        context.SingleModelRunResults.Add(result);
        context.SaveChanges();
    }
    
    #endregion
    
    /// <summary>
    /// runs all simulation lives for a single model
    /// </summary>
    /// <returns>an array of SimSnapshot lists. Each element in the array represents one simulated life</returns>
    public static List<SimSnapshot>[] ExecuteSingleModelAllLives(Logger logger,
        McModel model, PgPerson person, List<McInvestmentAccount> investmentAccounts, List<McDebtAccount> debtAccounts,
        Dictionary<LocalDateTime, decimal>[] allPricingDicts)
    {
        int numLivesPerModelRun = MonteCarloConfig.NumLivesPerModelRun;
        List<SimSnapshot>[] runs = new List<SimSnapshot>[numLivesPerModelRun];
        
        if(MonteCarloConfig.ShouldRunParallel) Parallel.For(0, numLivesPerModelRun, i =>
        {
            var newPerson = Person.CopyPerson(person, false);
            LifeSimulator sim = new(logger, model, newPerson, investmentAccounts, debtAccounts, allPricingDicts[i], i);
            runs[i] = sim.Run();
        });
        else for(int i = 0; i < numLivesPerModelRun; i++)
        {
            var newPerson = Person.CopyPerson(person, false);
            LifeSimulator sim = new(logger, model, newPerson, investmentAccounts, debtAccounts, allPricingDicts[i], i);
            runs[i] = sim.Run();
        }
        return runs;
    }
    
    /// <summary>
    /// Trigger the simulator for a single run after you've created all the default accounts, parameters and pricing.
    /// Can be used by RunSingle and Train.
    /// </summary>
    public static List<SimSnapshot> ExecuteSingleModelSingleLife(Logger logger, McModel model, PgPerson person, 
        List<McInvestmentAccount> investmentAccounts, List<McDebtAccount> debtAccounts,
        Dictionary<LocalDateTime, Decimal> hypotheticalPrices)
    {
        LifeSimulator sim = new LifeSimulator(
            logger, model, person, investmentAccounts, debtAccounts, hypotheticalPrices, 0);
        return sim.Run();
    }

    
    
    /// <summary>
    /// provides results from a pre-determined model. It still runs that
    /// moddel numSimulations times, each over a different "randomized"
    /// set of price simulations
    /// </summary>
    public static SingleModelRunResult RunSingleModelSession(
        Logger logger, McModel simParams, PgPerson person, List<McInvestmentAccount> investmentAccounts,
        List<McDebtAccount> debtAccounts, decimal[] historicalPrices)
    {   
        Stopwatch stopwatch = Stopwatch.StartNew();
        logger.Info("Creating simulation pricing");
        
        /*
         * create the pricing for all lives
         */
        var hypotheticalPrices = Pricing.CreateHypotheticalPricingForRuns(historicalPrices);
        
        stopwatch.Stop();
        var duration = stopwatch.Elapsed;
        logger.Info(logger.FormatTimespanDisplay("Created simulation pricing", duration));
        
        
        stopwatch = Stopwatch.StartNew();
        logger.Info("Running all lives for a single model");
        
        /*
         * run all sim lives
         */
        var allLivesRuns = ExecuteSingleModelAllLives(
            logger, simParams, person, investmentAccounts, debtAccounts, hypotheticalPrices);
        
        stopwatch.Stop();
        logger.Info(logger.FormatTimespanDisplay("Ran all lives for a single model", duration));
        
        logger.Info("Batching up the results into something meaningful");
        
        var results = Simulation.InterpretSimulationResults(simParams, allLivesRuns);
        
        logger.Info("Saving model run results to the database");
        SaveSingleModelRunResultsToDb(results);
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
        List<(McModel simParams, SingleModelRunResult result)> results = [];
        for(int i1 = 0; i1 < currentChamps.Length; i1++)
        {
            for (int i2 = 0; i2 < currentChamps.Length; i2++)
            {
                logger.Info($"running {i1} bred with {i2}");
                var offspring = Model.MateModels(currentChamps[i1], currentChamps[i2], person.BirthDate);
                var allLivesRuns2 = ExecuteSingleModelAllLives(
                    logger, offspring, person, investmentAccounts, debtAccounts, hypotheticalPrices);
                var modelResults = Simulation.InterpretSimulationResults(offspring, allLivesRuns2);
                
                results.Add((offspring, modelResults));
            }
        }
        
        /*
         * write results back to the db
         */
        throw new NotImplementedException();
    }
}