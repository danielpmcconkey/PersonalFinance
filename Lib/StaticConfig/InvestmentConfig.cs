using Lib.DataTypes.MonteCarlo;

namespace Lib.StaticConfig;


public static class InvestmentConfig
{
    public const decimal MidTermGrowthRateModifier = 0.5M; // half of the long term growth rate
    public const decimal ShortTermGrowthRateModifier = 0M; 
}