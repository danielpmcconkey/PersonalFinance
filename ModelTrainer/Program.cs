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



logger.Info("Running in model training mode");

logger.Info(logger.FormatBarSeparator('*'));
logger.Info(logger.FormatHeading("Beginning Monte Carlo training session"));
logger.Info(logger.FormatHeading($"Version {ModelConstants.MajorVersion}.{ModelConstants.MinorVersion}.{ModelConstants.PatchVersion}"));
logger.Info(logger.FormatBarSeparator('*'));
var keepRunning = true;
int cladeCounter = 0;
int[] activeClades = [9, 8, 6, 3, 1, 2];
while(keepRunning)
{
    var cladePosition = cladeCounter % activeClades.Length;
    var clade = activeClades[cladePosition];
    logger.Info($"Clade: {clade}");
    SimulationTrigger.RunModelTrainingSession(logger, dan, investmentAccounts, debtAccounts, clade);
    logger.Info("Training session completed.");
    
    if (MonteCarloConfig.RunTrainingInLoop)
    {
        logger.Info("Starting another training session");
    }
    else
    {
        logger.Info("Not looping.");
        keepRunning = false;
    }

    cladeCounter++;
    if(cladePosition == activeClades.Length - 1)
    {
        // only clean up after you've gone through an entire loop of clades
        logger.Info("Cleaning up unneeded model training data.");
        SimulationTrigger.CleanUpModelAndRunResultsData();
    }
}
logger.Info(logger.FormatBarSeparator('*'));
logger.Info(logger.FormatHeading("Exiting"));
logger.Info(logger.FormatBarSeparator('*'));

























