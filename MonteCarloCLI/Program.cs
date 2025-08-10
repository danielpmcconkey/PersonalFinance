using Lib;
using Lib.MonteCarlo;
using Lib.DataTypes.MonteCarlo;
using System.Diagnostics;
using System.Reflection;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using NodaTime;

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


if (MonteCarloConfig.ModelTrainingMode)
{
    logger.Info("Running in model training mode");
    
    logger.Info(logger.FormatBarSeparator('*'));
    logger.Info(logger.FormatHeading("Beginning Monte Carlo training session"));
    logger.Info(logger.FormatBarSeparator('*'));
    while(true)

    {
        SimulationTrigger.RunModelTrainingSession(
        logger, dan, investmentAccounts, debtAccounts, sAndP500HistoricalTrends);
        logger.Info("Training session completed. Starting another");
    }
}
else
{
    logger.Info("Running in single model mode");
    
    logger.Info("Pulling model champion from the database");
    McModel champion =DataStage.GetModelChampion(dan);
    
    // over-write the start and end dates from the DB champion model to use what's in the app config
    champion.SimStartDate = MonteCarloConfig.MonteCarloSimStartDate;
    champion.SimEndDate = MonteCarloConfig.MonteCarloSimEndDate;
    
    logger.Info(logger.FormatBarSeparator('*'));
    logger.Info(logger.FormatHeading("Beginning Monte Carlo single model session run"));
    logger.Info(logger.FormatBarSeparator('*'));
    var results = SimulationTrigger.RunSingleModelSession(
        logger, champion, dan, investmentAccounts, debtAccounts, sAndP500HistoricalTrends);
    logger.Info("Single model simulation of all lives completed");
}
logger.Info(logger.FormatBarSeparator('*'));
logger.Info(logger.FormatHeading("Exiting"));
logger.Info(logger.FormatBarSeparator('*'));

























