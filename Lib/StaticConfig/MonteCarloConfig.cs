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
    public static long MonteCarloSimMaxPositionValue;

    static MonteCarloConfig()
    {
        MonteCarloSimStartDate = ConfigManager.ReadDateSetting("MonteCarloSimStartDate");
        MonteCarloSimEndDate = ConfigManager.ReadDateSetting("MonteCarloSimEndDate");
        MonteCarloSimMaxPositionValue = ConfigManager.ReadLongSetting("MonteCarloSimMaxPositionValue");
        DebugMode = ConfigManager.ReadBoolSetting("IsDebugModeOn");
        ShouldRunParallel = ConfigManager.ReadBoolSetting("ShouldRunParallel");
        ReconOutputDirectory = ConfigManager.ReadStringSetting("ReconOutputDir");
        LogOutputDirectory = ConfigManager.ReadStringSetting("LogOutputDir");
    }
}