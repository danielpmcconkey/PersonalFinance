using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Pricing
{
    public static void SetLongTermGrowthRateAndPrices(CurrentPrices prices, decimal longTermGrowthRate)
    {
        prices.CurrentLongTermGrowthRate = longTermGrowthRate;
        prices.CurrentLongTermInvestmentPrice +=
            (prices.CurrentLongTermInvestmentPrice * prices.CurrentLongTermGrowthRate);

        var midTermGrowthRate = longTermGrowthRate * InvestmentConfig.MidTermGrowthRateModifier;
        var shortTermGrowthRate = longTermGrowthRate * InvestmentConfig.ShortTermGrowthRateModifier;

        prices.CurrentMidTermInvestmentPrice += (prices.CurrentMidTermInvestmentPrice * midTermGrowthRate);
        prices.CurrentShortTermInvestmentPrice += (prices.CurrentShortTermInvestmentPrice * shortTermGrowthRate);
    }
}