namespace Lib;

public class CorePackage
{
    // use this as a collection of config values, logger, and other things shared by all

    public bool DebugMode;
    public Logger Log;
    public bool ShouldRunParallel;
    public string ReconFilePath;
    public NodaTime.LocalDateTime MonteCarloSimStartDate;
    public NodaTime.LocalDateTime MonteCarloSimEndDate;

    public CorePackage()
    {
        DebugMode = ConfigManager.ReadBoolSetting("IsDebugModeOn");
        ShouldRunParallel = ConfigManager.ReadBoolSetting("ShouldRunParallel");
        MonteCarloSimStartDate = ConfigManager.ReadDateSetting("MonteCarloSimStartDate");
        MonteCarloSimEndDate = ConfigManager.ReadDateSetting("MonteCarloSimEndDate");
        string logDir = ConfigManager.ReadStringSetting("LogDir");
        string timeSuffix = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
        string logFilePath = $"{logDir}MonteCarloLog{timeSuffix}.txt";
        Log = new(Lib.DataTypes.LogLevel.INFO, logFilePath);
        if (DebugMode)
        {
            ShouldRunParallel = false;
            Log = new(Lib.DataTypes.LogLevel.DEBUG, logFilePath);
            string outDir = ConfigManager.ReadStringSetting("OutputDir");
            ReconFilePath = $"{outDir}MonteCarloRecon{timeSuffix}.xlsx";
        }
        
        
    }

    

        
}