using Lib.DataTypes;
using NodaTime;

namespace Lib.StaticConfig;

/// <summary>
/// This should be used only for values that would transcend all instances of a simulation run
/// </summary>
public static class MonteCarloConfig
{
    public static bool DebugMode;
    public static bool ShouldRunParallel;
    public static string ReconOutputDirectory;
    public static string LogOutputDirectory;
    public static LocalDateTime MonteCarloSimStartDate;
    public static LocalDateTime MonteCarloSimEndDate;
    public static LogLevel LogLevel = LogLevel.INFO;
    /// <summary>
    /// determines whether you are running a single model through a simulation or whether you are pitting models against
    /// each other to determine the best results
    /// </summary>
    public static bool ModelTrainingMode;
    /// <summary>
    /// This is the number of lives you want to run this time. this is different from the MaxLivesPerBatch because you
    /// may only want to run 100 lives today, but you always want to create pricing for the max lives. This ensures
    /// that, if you run 100 or 23000 lives, life 78 always uses the same hypothetical pricing 
    /// </summary>
    public static int NumLivesPerModelRun;
    /// <summary>
    /// all runs use the MaxLivesPerBatch to create the hypothetics pricing
    /// array. We build that array out to the max you'd ever want to run
    /// things at so that we know we always using the same "random" pricing
    /// for every run. Run 1 for every batch will use the same hypothetical
    /// pricing. Run 7 will use the same pricing. But run 7 will be 
    /// different from run 1
    /// </summary>
    public static int MaxLivesPerBatch;

    public static bool ShouldReconcileInterestAccrual;
    public static bool ShouldReconcileTaxCalcs;
    public static bool ShouldReconcileAccountCleanUp;
    public static bool ShouldReconcileRmd;
    public static bool ShouldReconcileLoanPaydown;
    public static bool ShouldReconcilePayingForStuff;
    public static bool ShouldReconcilePayDay;
    public static bool ShouldReconcileRebalancing;
    public static bool ShouldReconcilePricingGrowth;
    public static LocalDateTime ReconciliationSimStartDate;
    public static LocalDateTime ReconciliationSimEndDate;

    static MonteCarloConfig()
    {
        MonteCarloSimStartDate = ConfigManager.ReadDateSetting("MonteCarloSimStartDate");
        MonteCarloSimEndDate = ConfigManager.ReadDateSetting("MonteCarloSimEndDate");
        DebugMode = ConfigManager.ReadBoolSetting("IsDebugModeOn");
        ShouldRunParallel = ConfigManager.ReadBoolSetting("ShouldRunParallel");
        ReconOutputDirectory = ConfigManager.ReadStringSetting("ReconOutputDir");
        LogOutputDirectory = ConfigManager.ReadStringSetting("LogOutputDir");
        MaxLivesPerBatch = ConfigManager.ReadIntSetting("MaxLivesPerBatch");
        NumLivesPerModelRun = Math.Min(ConfigManager.ReadIntSetting("NumLivesPerModelRun"), MaxLivesPerBatch);
        ModelTrainingMode = ConfigManager.ReadBoolSetting("ModelTrainingMode");
        ShouldReconcileInterestAccrual = ConfigManager.ReadBoolSetting("ShouldReconcileInterestAccrual");
        ShouldReconcileTaxCalcs = ConfigManager.ReadBoolSetting("ShouldReconcileTaxCalcs");
        ShouldReconcileAccountCleanUp = ConfigManager.ReadBoolSetting("ShouldReconcileAccountCleanUp");
        ShouldReconcileRmd = ConfigManager.ReadBoolSetting("ShouldReconcileRmd");
        ShouldReconcileLoanPaydown = ConfigManager.ReadBoolSetting("ShouldReconcileLoanPaydown");
        ShouldReconcilePayingForStuff = ConfigManager.ReadBoolSetting("ShouldReconcilePayingForStuff");
        ShouldReconcilePayDay = ConfigManager.ReadBoolSetting("ShouldReconcilePayDay");
        ShouldReconcileRebalancing = ConfigManager.ReadBoolSetting("ShouldReconcileRebalancing");
        ShouldReconcilePricingGrowth = ConfigManager.ReadBoolSetting("ShouldReconcilePricingGrowth");
        ReconciliationSimStartDate = ConfigManager.ReadDateSetting("ReconciliationSimStartDate");
        ReconciliationSimEndDate = ConfigManager.ReadDateSetting("ReconciliationSimEndDate");
        if (DebugMode) LogLevel = LogLevel.DEBUG;
    }
}