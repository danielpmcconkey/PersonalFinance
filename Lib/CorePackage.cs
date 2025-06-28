using Lib.DataTypes.MonteCarlo;

namespace Lib;

public class CorePackage
{
    // use this as a collection of config values, logger, and other things shared by all

    
    
    
    public MonteCarloSim MonteCarloSim;
    


    public CorePackage()
    {
        
        
        
        string logDir = ConfigManager.ReadStringSetting("LogDir");
        string timeSuffix = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
        string logFilePath = $"{logDir}MonteCarloLog{timeSuffix}.txt";
        Log = new(Lib.DataTypes.LogLevel.INFO, logFilePath);
        if (DebugMode)
        {
            ShouldRunParallel = false;
            Log = new(Lib.DataTypes.LogLevel.DEBUG, logFilePath);
            
        }
        
        
    }

    

        
}