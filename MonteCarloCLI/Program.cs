using Lib;
using Lib.MonteCarlo;
using Lib.DataTypes.MonteCarlo;
using System.Diagnostics;

CorePackage corePackage = new CorePackage();
var logger = corePackage.Log;



logger.Info(logger.FormatBarSeparator('*'));
logger.Info(logger.FormatHeading("Beginning Monte Carlo run"));
logger.Info(logger.FormatBarSeparator('*'));



var dan = DataStage.GetPerson();
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


