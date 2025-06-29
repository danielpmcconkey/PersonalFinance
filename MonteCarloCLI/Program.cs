using Lib;
using Lib.MonteCarlo;
using Lib.DataTypes.MonteCarlo;
using System.Diagnostics;
using System.Reflection;
using Lib.MonteCarlo.StaticFunctions;
using NodaTime;

string logDir = ConfigManager.ReadStringSetting("LogDir");
string timeSuffix = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
string logFilePath = $"{logDir}MonteCarloLog{timeSuffix}.txt";
var logger = new Logger(
    Lib.StaticConfig.MonteCarloConfig.LogLevel,
    logFilePath
);





// you are here. you just made the sim logic not show any errors and now you need
// to correct the entry into the simulator and then finally test this refactoring
// the logger you instantiate above should be passed into next-level classes






var danId = ConfigManager.ReadStringSetting("DanId");
Guid danIdGuid = Guid.Parse(danId);
var dan = Person.GetPersonById(danIdGuid);









logger.Info(logger.FormatBarSeparator('*'));
logger.Info(logger.FormatHeading("Beginning Monte Carlo run"));
logger.Info(logger.FormatBarSeparator('*'));




McModel champion = DataStage.GetModelChampion();

// over-write the start and end dates from the DB champion model to use what's in the app config
champion.SimStartDate = corePackage.MonteCarloSimStartDate;
champion.SimEndDate = corePackage.MonteCarloSimEndDate;

decimal[] sAndP500HistoricalTrends = DataStage.GetSAndP500HistoricalTrends();


Stopwatch stopwatch = Stopwatch.StartNew();

SimulationModeler modeler = new SimulationModeler(corePackage,
    dan, sAndP500HistoricalTrends);

//modeler.Train(1000, startDate, endDate);
modeler.RunSingle(1000, champion);

stopwatch.Stop();
var duration = stopwatch.Elapsed;
logger.Info(logger.FormatTimespanDisplay("Full session completed",duration));


