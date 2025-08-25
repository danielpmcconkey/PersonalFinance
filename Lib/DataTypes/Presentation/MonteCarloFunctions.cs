using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using Model = Lib.DataTypes.MonteCarlo.Model;

namespace Lib.DataTypes.Presentation;

public static class MonteCarloFunctions
{
    public static SingleModelRunResult RunMonteCarlo()
    {
        string logDir = ConfigManager.ReadStringSetting("LogDir");
        string timeSuffix = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
        string logFilePath = $"{logDir}MonteCarloLog{timeSuffix}.txt";
        var logger = new Logger(
            Lib.StaticConfig.MonteCarloConfig.LogLevel,
            logFilePath
        );

        logger.Info("Pulling person from the database");
        var danId = ConfigManager.ReadStringSetting("DanId");
        Guid danIdGuid = Guid.Parse(danId);
        var dan = Person.GetPersonById(danIdGuid);
        var investmentAccounts = AccountDbRead.FetchDbInvestmentAccountsByPersonId(danIdGuid);
        var debtAccounts = AccountDbRead.FetchDbDebtAccountsByPersonId(danIdGuid);


        logger.Info("Pulling historical pricing data");
        decimal[] sAndP500HistoricalTrends = Pricing.FetchSAndP500HistoricalTrends();
            
        logger.Info("Running in single model mode");
    
        logger.Info("Pulling model champion from the database");
        Model champion = Lib.MonteCarlo.StaticFunctions.Model.FetchModelChampion();
    
        // over-write the start and end dates from the DB champion model to use what's in the app config
        champion.SimStartDate = MonteCarloConfig.MonteCarloSimStartDate;
        champion.SimEndDate = MonteCarloConfig.MonteCarloSimEndDate;
    
        logger.Info(logger.FormatBarSeparator('*'));
        logger.Info(logger.FormatHeading("Beginning Monte Carlo single model session run"));
        logger.Info(logger.FormatBarSeparator('*'));
        var results = SimulationTrigger.RunSingleModelSession(
            logger, champion, dan, investmentAccounts, debtAccounts, sAndP500HistoricalTrends);
        logger.Info("Single model simulation of all lives completed");
        return results;
    }
}