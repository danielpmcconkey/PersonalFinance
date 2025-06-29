using Lib.DataTypes.MonteCarlo;

namespace Lib.StaticConfig;


public static class InvestmentConfig
{
    public static decimal MidTermGrowthRateModifier = 0.5M; // half of the long term growth rate
    public static decimal ShortTermGrowthRateModifier = 0M; 
    public static decimal MonteCarloSimMaxPositionValue = ConfigManager.ReadLongSetting("MonteCarloSimMaxPositionValue");
    
    /// <summary>
    /// when you sell investments during the Monte Carlo simulator, the simulator needs to know what order to sell things off in
    /// </summary>
    public static McInvestmentAccountType[] _salesOrderWithNoRoom = [
        // no tax, period
        McInvestmentAccountType.HSA,
        McInvestmentAccountType.ROTH_IRA,
        McInvestmentAccountType.ROTH_401_K,
        // tax on growth only
        McInvestmentAccountType.TAXABLE_BROKERAGE,
        // tax deferred
        McInvestmentAccountType.TRADITIONAL_401_K,
        McInvestmentAccountType.TRADITIONAL_IRA,
    ];
    public static McInvestmentAccountType[] _salesOrderWithRoom = [
        // tax deferred
        McInvestmentAccountType.TRADITIONAL_401_K,
        McInvestmentAccountType.TRADITIONAL_IRA,
        // tax on growth only
        McInvestmentAccountType.TAXABLE_BROKERAGE,
        // no tax, period
        McInvestmentAccountType.HSA,
        McInvestmentAccountType.ROTH_IRA,
        McInvestmentAccountType.ROTH_401_K,
    ];
    public static McInvestmentAccountType[] _salesOrderRmd = [
        // tax deferred
        McInvestmentAccountType.TRADITIONAL_401_K,
        McInvestmentAccountType.TRADITIONAL_IRA,
    ];
}