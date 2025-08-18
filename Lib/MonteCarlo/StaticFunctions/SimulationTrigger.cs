using System.Diagnostics;
using Lib.DataTypes;
using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using Microsoft.EntityFrameworkCore;
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
        logger.Info(logger.FormatTimespanDisplay("Created simulation pricing", duration));
        
        
        /*
         * pull the current champs from the DB
         */
        
        var startDate = MonteCarloConfig.MonteCarloSimStartDate;
        var endDate = MonteCarloConfig.MonteCarloSimEndDate;
        
        logger.Info(logger.FormatTimespanDisplay("Pulling model champions from DB", duration));
        var allModels = FetchOrCreateModelsForTraining(person);
        
        /*
         * breed and run
         */
        //List<(McModel simParams, SingleModelRunResult result)> results = [];
        for(int i1 = 0; i1 < allModels.Count; i1++)
        {
            for (int i2 = 0; i2 < allModels.Count; i2++)
            {
                logger.Info($"running {i1} bred with {i2}");
                var offspring = Model.MateModels(allModels[i1], allModels[i2], person.BirthDate);
                offspring.SimStartDate = startDate;
                offspring.SimEndDate = endDate;
                SaveModelToDb(offspring);
                var allLivesRuns2 = ExecuteSingleModelAllLives(
                    logger, offspring, person, investmentAccounts, debtAccounts, hypotheticalPrices);
                var modelResults = Simulation.InterpretSimulationResults(offspring, allLivesRuns2);
                SaveSingleModelRunResultsToDb(modelResults);
                modelResults = null; // trying to ensure that we clear up the memory
            }
        }
    }

    public static List<McModel> FetchModelsForTrainingByVersion(PgPerson person, int majorVersion, int minorVersion)
    {
        var maxFromDb = MonteCarloConfig.NumberOfModelsToPull;
        using var context = new PgContext();

        var query = " select m.id, personid, parenta, parentb, modelcreateddate, simstartdate, simenddate," +
                    " retirementdate, socialsecuritystart, austerityratio, extremeausterityratio," +
                    " extremeausteritynetworthtrigger, rebalancefrequency, nummonthscashonhand," +
                    " nummonthsmidbucketonhand, nummonthspriortoretirementtobeginrebalance," +
                    " recessionchecklookbackmonths, recessionrecoverypointmodifier, desiredmonthlyspendpreretirement" +
                    ", desiredmonthlyspendpostretirement, percent401ktraditional " +
                    "from personalfinance.singlemodelrunresult r " +
                    "left join personalfinance.montecarlomodel m on r.modelid = m.id" +
                    " where m.id is not null " +
                    $"and majorversion = {majorVersion} " +
                    $" and minorversion = {minorVersion} " +
                    $"and r.bankruptcyrateatendofsim <= 0.1 " +
                    "group by m.id, personid, parenta, parentb, modelcreateddate, simstartdate, simenddate" +
                    ", retirementdate, socialsecuritystart, austerityratio, extremeausterityratio" +
                    ", extremeausteritynetworthtrigger, rebalancefrequency, nummonthscashonhand" +
                    ", nummonthsmidbucketonhand, nummonthspriortoretirementtobeginrebalance" +
                    ", recessionchecklookbackmonths, recessionrecoverypointmodifier" +
                    ", desiredmonthlyspendpreretirement, desiredmonthlyspendpostretirement, percent401ktraditional " +
                    "order by max(r.funpointsatendofsim50) desc, min(r.bankruptcyrateatendofsim) asc " +
                    $"limit {maxFromDb}";
        return context.McModels.FromSqlRaw(query).ToList();
    }
    public static (int majorVersion, int minorVersion) FetchLatestVersionOfTrainingRuns()
    {
        var maxFromDb = MonteCarloConfig.NumberOfModelsToPull;
        using var context = new PgContext();

        var latestResult =
            context.SingleModelRunResults
                .OrderByDescending(x => x.MajorVersion)
                .ThenByDescending(x => x.MinorVersion)
                .FirstOrDefault();
        return latestResult is null ? 
            (ModelConstants.MajorVersion, ModelConstants.MinorVersion) :
            (latestResult.MajorVersion, latestResult.MinorVersion);
    }

    public static List<McModel> FetchOrCreateModelsForTraining(PgPerson person)
    {
        
        var currentChamps = FetchModelsForTrainingByVersion(
            person, ModelConstants.MajorVersion, ModelConstants.MinorVersion);
        var dbCount = currentChamps.Count();
        if (dbCount == 0)
        {
            // we might've just changed the version. Try to pull models from the prior version
            var latestVersion = FetchLatestVersionOfTrainingRuns();
            currentChamps = FetchModelsForTrainingByVersion(
                person, latestVersion.majorVersion, latestVersion.minorVersion);
            dbCount = currentChamps.Count();
        }
        
        // now create the new random models
        using var context = new PgContext();
        var numNew = Math.Max(0, MonteCarloConfig.NumberOfModelsToBreed - dbCount);
        var allModels = currentChamps.ToList();
        for (int i = 0; i < numNew; i++)
        {
            var newModel = Model.CreateRandomModel(person.BirthDate);
            allModels.Add(newModel);
            context.McModels.Add(newModel);
        }
        context.SaveChanges();
        return allModels;
    }
}