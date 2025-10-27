using System.Diagnostics;
using Lib.DataTypes;
using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
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

    public static void SaveModelToDb(Model model)
    {
        using var context = new PgContext();
        if(model.ParentA is not null) context.Attach(model.ParentA);
        if(model.ParentB is not null) context.Attach(model.ParentB);
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
    public static (List<SimSnapshot> snapshots, LocalDateTime firstIncomeInflection)[] 
        ExecuteSingleModelAllLives(Logger logger, DataTypes.MonteCarlo.Model model, PgPerson person, 
            List<McInvestmentAccount> investmentAccounts, List<McDebtAccount> debtAccounts)
    {
        const int skip = 5;
        var numLivesPerModelRun = Pricing.FetchMaxBlockStartFromHypotheticalDbLives() / skip;
        var runs = new (List<SimSnapshot> snapshots, LocalDateTime firstIncomeInflection)[numLivesPerModelRun];

        if(MonteCarloConfig.ShouldRunParallel) Parallel.For(0, numLivesPerModelRun, i =>
        {
            var pointer = i * skip;
            var newPerson = Person.CopyPerson(person, false);
            var prices = Pricing.CreateHypotheticalPricingForARun(pointer);
            LifeSimulator sim = new(logger, model, newPerson, investmentAccounts, debtAccounts, prices, pointer);
            runs[i] = sim.Run();
        });
        else for(var i = 0; i < numLivesPerModelRun; i += skip)
        {
            var pointer = i * skip;
            var newPerson = Person.CopyPerson(person, false);
            var prices = Pricing.CreateHypotheticalPricingForARun(pointer);
            LifeSimulator sim = new(logger, model, newPerson, investmentAccounts, debtAccounts, prices, pointer);
            runs[i] = sim.Run();
        }
        return runs;
    }
    
    /// <summary>
    /// Trigger the simulator for a single run after you've created all the default accounts, parameters and pricing.
    /// Can be used by RunSingle and Train.
    /// </summary>
    public static (List<SimSnapshot> snapshots, LocalDateTime firstIncomeInflection) ExecuteSingleModelSingleLife(Logger logger, DataTypes.MonteCarlo.Model model, PgPerson person, 
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
        Logger logger, DataTypes.MonteCarlo.Model model, PgPerson person, List<McInvestmentAccount> investmentAccounts,
        List<McDebtAccount> debtAccounts)
    {   
        Stopwatch stopwatch = Stopwatch.StartNew();
        logger.Info("Running all lives for a single model");
        
        /*
         * run all sim lives
         */
        var allLivesRuns = ExecuteSingleModelAllLives(
            logger, model, person, investmentAccounts, debtAccounts);
        
        stopwatch.Stop();
        var duration = stopwatch.Elapsed;
        logger.Info(logger.FormatTimespanDisplay("Ran all lives for a single model", duration));
        
        logger.Info("Batching up the results into something meaningful");
        
        var results = Simulation.InterpretSimulationResults(model, allLivesRuns, -1, person);
        
        return results;
    }
    
    /// <summary>
    /// pull the recent champions from the DB, mate them to one another, write the best results back to the DB
    /// </summary>
    /// <returns></returns>
    public static void RunModelTrainingSession(
        Logger logger, PgPerson person, List<McInvestmentAccount> investmentAccounts, List<McDebtAccount> debtAccounts, 
        int clade)
    {   
        
        /*
         * pull the current champs from the DB
         */
        
        var startDate = MonteCarloConfig.MonteCarloSimStartDate;
        var endDate = MonteCarloConfig.MonteCarloSimEndDate;
        
       
        logger.Info("Pulling model champions from DB");
        var allModels = FetchOrCreateModelsForTraining(person, clade);
        int maxCounter = FetchMaxRunResult();
        
        /*
         * breed and run
         */
        //List<(McModel model, SingleModelRunResult result)> results = [];
        for(int i1 = 0; i1 < allModels.Count; i1++)
        {
            for (int i2 = 0; i2 < allModels.Count; i2++)
            {
                logger.Info($"running {i1} bred with {i2}");
                var offspring = ModelFunc.MateModels(allModels[i1], allModels[i2], person.BirthDate);
                offspring.SimStartDate = startDate;
                offspring.SimEndDate = endDate;
                SaveModelToDb(offspring);
                var allLivesRuns2 = ExecuteSingleModelAllLives(
                    logger, offspring, person, investmentAccounts, debtAccounts);
                var modelResults = Simulation.InterpretSimulationResults(
                    offspring, allLivesRuns2, ++maxCounter, person);
                SaveSingleModelRunResultsToDb(modelResults);
                modelResults = null; // trying to ensure that we clear up the memory
            }
        }
    }

    public static void CleanUpModelAndRunResultsData()
    {
        using var context = new PgContext();
    
        var cutoffDate = LocalDate.FromDateTime(DateTime.Now).PlusDays(-1);
        
        // Find childless models older than the cutoff date
        var childlessModels = context.McModels
            .Where(model => model.ModelCreatedDate.Date <= cutoffDate)
            .Where(model => 
                !context.McModels.Any(child => child.ParentAId == model.Id) &&
                !context.McModels.Any(child => child.ParentBId == model.Id))
            .Where(model => !context.ModelChampions.Any(champion => champion.ModelId == model.Id))
            .ToList(); // Execute the query to get the models

    
        if (childlessModels.Count == 0)
            return; // Nothing to clean up
        
        // Get the model IDs for deleting run results
        var modelIds = childlessModels.Select(m => m.Id).ToList();
    
        // Delete associated run results first (to maintain referential integrity)
        var runResultsToDelete = context.SingleModelRunResults
            .Where(result => modelIds.Contains(result.ModelId))
            .ToList();
        
        context.SingleModelRunResults.RemoveRange(runResultsToDelete);
    
        // Delete the childless models
        context.McModels.RemoveRange(childlessModels);
    
        // Save all changes
        context.SaveChanges();
    }

    public static List<DataTypes.MonteCarlo.Model> FetchModelsForTrainingByVersion(
        PgPerson person, int majorVersion, int minorVersion, int clade)
    {
        var maxFromDb = MonteCarloConfig.NumberOfModelsToPull;
        using var context = new PgContext();

        var query = 
            $"""

             select 
                 m.id, personid, parenta, parentb, modelcreateddate, simstartdate, simenddate, retirementdate,
                 socialsecuritystart, austerityratio, extremeausterityratio, livinlargeratio, livinlargenetworthtrigger,
                 extremeausteritynetworthtrigger, rebalancefrequency, nummonthscashonhand, nummonthsmidbucketonhand,
                 nummonthspriortoretirementtobeginrebalance, recessionchecklookbackmonths,
                 recessionrecoverypointmodifier, desiredmonthlyspendpreretirement, desiredmonthlyspendpostretirement,
                 percent401ktraditional, generation , withdrawalstrategy, sixtyfortylong, clade
             from personalfinance.singlemodelrunresult r 
                 left join personalfinance.montecarlomodel m on r.modelid = m.id 
             where m.id is not null 
               and majorversion = {majorVersion}  
               and minorversion = {minorVersion} 
               and r.bankruptcyrateatendofsim <= 0 
               and clade = {clade}
             group by 
                 m.id, personid, parenta, parentb, modelcreateddate, simstartdate, simenddate, retirementdate,
                 socialsecuritystart, austerityratio, extremeausterityratio, livinlargeratio, livinlargenetworthtrigger,
                 extremeausteritynetworthtrigger, rebalancefrequency, nummonthscashonhand, nummonthsmidbucketonhand, 
                 nummonthspriortoretirementtobeginrebalance, recessionchecklookbackmonths, 
                 recessionrecoverypointmodifier, desiredmonthlyspendpreretirement, desiredmonthlyspendpostretirement,
                  percent401ktraditional, generation , withdrawalstrategy, sixtyfortylong, clade
             order by 
                 max(r.funpointsatendofsim50) desc, 
                 min(r.bankruptcyrateatendofsim) asc, 
                 max(r.networthatendofsim50) desc, 
                 max(r.funpointsatendofsim90) desc,
                 max(networthatendofsim90) desc 
                                 limit {maxFromDb}
            """;
        return context.McModels.FromSqlRaw(query).ToList();
    }
    public static (int majorVersion, int minorVersion) FetchLatestVersionOfTrainingRuns(int clade)
    {
        var maxFromDb = MonteCarloConfig.NumberOfModelsToPull;
        using var context = new PgContext();

        var latestResult = (from models in context.McModels
            join results in context.SingleModelRunResults on models.Id equals results.ModelId
            where models.Clade == clade
            orderby results.MajorVersion descending,  results.MinorVersion descending
            select results).FirstOrDefault();
        
        return latestResult is null ? 
            (ModelConstants.MajorVersion, ModelConstants.MinorVersion) :
            (latestResult.MajorVersion, latestResult.MinorVersion);
    }

    public static List<DataTypes.MonteCarlo.Model> FetchOrCreateModelsForTraining(PgPerson person, int clade)
    {
        var currentChamps = FetchModelsForTrainingByVersion(
            person, ModelConstants.MajorVersion, ModelConstants.MinorVersion, clade);
        var dbCount = currentChamps.Count();
        if (dbCount == 0)
        {
            // we might've just changed the version. Try to pull models from the prior version
            var latestVersion = FetchLatestVersionOfTrainingRuns(clade);
            currentChamps = FetchModelsForTrainingByVersion(
                person, latestVersion.majorVersion, latestVersion.minorVersion, clade);
            dbCount = currentChamps.Count();
        }
        
        // now create the new random models
        using var context = new PgContext();
        var numNew = Math.Max(0, MonteCarloConfig.NumberOfModelsToBreed - dbCount);
        var allModels = currentChamps.ToList();
        for (int i = 0; i < numNew; i++)
        {
            var newModel = ModelFunc.CreateRandomModel(person.BirthDate, clade);
            allModels.Add(newModel);
            context.McModels.Add(newModel);
        }
        context.SaveChanges();
        return allModels;
    }

    public static int FetchMaxRunResult()
    {
        using var context = new PgContext();
        var query = "select * from personalfinance.singlemodelrunresult order by counter desc limit 1";
        var maxRun = context.SingleModelRunResults.FromSqlRaw(query).First();
        return maxRun.Counter;
    }
}