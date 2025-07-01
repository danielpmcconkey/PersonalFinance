﻿using Lib;
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

logger.Info("Pulling historical pricing data");
decimal[] sAndP500HistoricalTrends = Pricing.FetchSAndP500HistoricalTrends();


if (MonteCarloConfig.ModelTrainingMode)
{
    logger.Info("Running in model training mode");
    throw new NotImplementedException();
    logger.Info(logger.FormatBarSeparator('*'));
    logger.Info(logger.FormatHeading("Beginning Monte Carlo training session"));
    logger.Info(logger.FormatBarSeparator('*'));
}
else
{
    logger.Info("Running in single model mode");
    
    logger.Info("Pulling model champion from the database");
    McModel champion = DataStage.GetModelChampion();
    // over-write the start and end dates from the DB champion model to use what's in the app config
    champion.SimStartDate = MonteCarloConfig.MonteCarloSimStartDate;
    champion.SimEndDate = MonteCarloConfig.MonteCarloSimEndDate;
    
    logger.Info(logger.FormatBarSeparator('*'));
    logger.Info(logger.FormatHeading("Beginning Monte Carlo run"));
    logger.Info(logger.FormatBarSeparator('*'));
    var results = Simulation.RunSingleModelSession(logger, champion, dan, sAndP500HistoricalTrends);
    logger.Info("Single model simulation of all lives completed");
    if (MonteCarloConfig.DebugMode)
    {
        logger.Info("Writing reconciliation data to spreadsheet");
        Reconciliation.ExportToSpreadsheet();
    }
}
logger.Info(logger.FormatBarSeparator('*'));
logger.Info(logger.FormatHeading("Exiting"));
logger.Info(logger.FormatBarSeparator('*'));

























