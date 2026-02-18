using Lib;
using Lib.MonteCarlo;
using Lib.DataTypes.MonteCarlo;
using System.Diagnostics;
using System.Reflection;
using Lib.MonteCarlo.StaticFunctions;
using Lib.MonteCarlo.Var;
using Lib.StaticConfig;
using Lib.Utils;
using NodaTime;
using Model = Lib.DataTypes.MonteCarlo.Model;

string logDir = MonteCarloConfig.LogOutputDirectory;
string timeSuffix = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
string logFilePath = $"{logDir}MonteCarloLog{timeSuffix}.txt";
var logger = new Logger(
    MonteCarloConfig.LogLevel,
    logFilePath
);


// ── VAR diagnostic mode ───────────────────────────────────────────────────────────────────────
// When GenerateVarDiagnostics is true in appsettings.json (or the DB config table), fit the
// VAR model and emit a self-contained HTML file showing 5 hypothetical lifetime trajectories
// overlaid against actual historical data.  Open the file in any browser to visually validate
// that the synthetic series look like plausible economic histories.
// Set GenerateVarDiagnostics back to false to resume normal simulation runs.
if (ConfigManager.ReadBoolSetting("GenerateVarDiagnostics"))
{
    var diagOutputPath = ConfigManager.ReadStringSetting("VarDiagnosticsOutputPath");
    logger.Info($"VAR diagnostic mode — fitting model and writing charts to: {diagOutputPath}");

    using var diagContext = new PgContext();
    var historicalObs = diagContext.HistoricalGrowthData
        .Where(x => x.Year >= 1980 && x.SpGrowth != null && x.CpiGrowth != null && x.TreasuryGrowth != null)
        .OrderBy(x => x.Year).ThenBy(x => x.Month)
        .Select(x => new double[] { (double)x.SpGrowth!.Value, (double)x.CpiGrowth!.Value, (double)x.TreasuryGrowth!.Value })
        .ToList();

    var diagVarModel = VarFitter.Fit(historicalObs);
    VarDiagnosticsWriter.Write(diagOutputPath, diagVarModel, historicalObs);

    logger.Info($"Done. Open {diagOutputPath} in a browser to review the charts.");
    return;
}
// ─────────────────────────────────────────────────────────────────────────────────────────────

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
var test = MonteCarloConfig.ShouldCheckIncomeInversion;

logger.Info(logger.FormatBarSeparator('*'));
logger.Info(logger.FormatHeading("Beginning Monte Carlo single model session run"));
logger.Info(logger.FormatHeading($"Version {ModelConstants.MajorVersion}.{ModelConstants.MinorVersion}.{ModelConstants.PatchVersion}"));
logger.Info(logger.FormatBarSeparator('*'));


/*
 * use this code to run a specific life for a specific model
 */
// var model = ModelFunc.FetchModelChampion("cd8d33dc-6850-4779-a499-48c6ddba34c3"); // 4
// // var model = ModelFunc.FetchModelChampion("a65e5d0b-f920-402e-9f00-27f041b5aec6"); // 3
// var lifePointer = 195;
// var prices = Pricing.CreateHypotheticalPricingForARun(lifePointer);
// var newPerson = Person.CopyPerson(dan, false);
// LifeSimulator sim = new(logger, model, newPerson, investmentAccounts, debtAccounts, prices, lifePointer);
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

























