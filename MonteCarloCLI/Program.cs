using Lib;
using Lib.MonteCarlo;
using Lib.DataTypes.MonteCarlo;
using System.Diagnostics;
using System.Reflection;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using Lib.Utils;
using NodaTime;
using Model = Lib.DataTypes.MonteCarlo.Model;

string logDir = ConfigManager.ReadStringSetting("LogDir");
string timeSuffix = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
string logFilePath = $"{logDir}MonteCarloLog{timeSuffix}.txt";
var logger = new Logger(
    Lib.StaticConfig.MonteCarloConfig.LogLevel,
    logFilePath
);

SimulationTrigger.CleanUpModelAndRunResultsData();
logger.Info("Pulling person from the database");
var danId = ConfigManager.ReadStringSetting("DanId");
Guid danIdGuid = Guid.Parse(danId);
var dan = Person.GetPersonById(danIdGuid);
var investmentAccounts = AccountDbRead.FetchDbInvestmentAccountsByPersonId(danIdGuid);
var debtAccounts = AccountDbRead.FetchDbDebtAccountsByPersonId(danIdGuid);


logger.Info("Running in single model mode");

logger.Info("Pulling model champion from the database");
Model champion = ModelFunc.FetchModelChampion();

// over-write the start and end dates from the DB champion model to use what's in the app config
champion.SimStartDate = MonteCarloConfig.MonteCarloSimStartDate;
champion.SimEndDate = MonteCarloConfig.MonteCarloSimEndDate;

logger.Info(logger.FormatBarSeparator('*'));
logger.Info(logger.FormatHeading("Beginning Monte Carlo single model session run"));
logger.Info(logger.FormatHeading($"Version {ModelConstants.MajorVersion}.{ModelConstants.MinorVersion}.{ModelConstants.PatchVersion}"));
logger.Info(logger.FormatBarSeparator('*'));


/*
 * use this code to run a specific life for a specific model
 */
// var model = ModelFunc.FetchModelChampion("7758072c-b455-4352-ace7-6ab2bc478b0f");
// var hypotheticalPrices = Pricing.CreateHypotheticalPricingForRuns(sAndP500HistoricalTrends);
// var newPerson = Person.CopyPerson(dan, false);
// var i = 3;
// LifeSimulator sim = new(logger, model, newPerson, investmentAccounts, debtAccounts, hypotheticalPrices[i], i);
// var runResults = sim.Run();
/*
 * end code to run a specific life for a specific model
 */

var results = SimulationTrigger.RunSingleModelSession(
    logger, champion, dan, investmentAccounts, debtAccounts);
logger.Info("Single model simulation of all lives completed");

logger.Info(logger.FormatBarSeparator('*'));
logger.Info(logger.FormatHeading("Exiting"));
logger.Info(logger.FormatBarSeparator('*'));

























