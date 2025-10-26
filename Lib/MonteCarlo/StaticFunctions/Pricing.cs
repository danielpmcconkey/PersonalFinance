using System.Diagnostics;
using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Pricing
{
    private static Dictionary<int, Dictionary<LocalDateTime, decimal>> _hypotheticalPricingCache = [];
    public static int FetchMaxBlockStartFromHypotheticalDbLives()
    {
        using var context = new PgContext();
        var maxRunsPerBatch = 
            context
                .HypotheticalLifeTimeGrowthRate
                .Max(x => x.BlockStart);
        return maxRunsPerBatch;
    }
    public static Dictionary<LocalDateTime, decimal> CreateHypotheticalPricingForARun(int blockStart)
    {
        if (_hypotheticalPricingCache.TryGetValue(blockStart, out var run)) return  run;
        
        using var context = new PgContext();
        var hypotheticalLife = 
            context.HypotheticalLifeTimeGrowthRate
                .Where(x => x.BlockStart == blockStart)
                .OrderBy(x => x.Ordinal)
                .Select(x => x.InflationAdjustedGrowth)
                .ToArray();

        Dictionary<LocalDateTime, decimal> prices = [];
        // create first and last dates that will always be the same, even
        // if the simulation start and end dates change. this will allow
        // us to have an apples to apples comparison to models created years
        // apart
        var firstDateToCreate = MonteCarloConfig.MonteCarloSimStartDate;
        var lastDateToCreate = MonteCarloConfig.MonteCarloSimEndDate;
        
        var dateCursor = firstDateToCreate;
        var i = 0;
        
        while (dateCursor <= lastDateToCreate)
        {
            prices[dateCursor] = hypotheticalLife[i];
            dateCursor = dateCursor.PlusMonths(1);
            i++;
        }
        _hypotheticalPricingCache[blockStart] = prices;
        return prices;
    }
    
    public static CurrentPrices SetLongTermGrowthRateAndPrices(CurrentPrices prices, decimal longTermGrowthRate)
    {
        var result = Pricing.CopyPrices(prices);
        
        // long term growth
        result.CurrentLongTermGrowthRate = longTermGrowthRate;
        
        // calculate mid and short-term growth rates based on long-term growth rate
        var midTermGrowthRate = longTermGrowthRate * InvestmentConfig.MidTermGrowthRateModifier;
        var shortTermGrowthRate = longTermGrowthRate * InvestmentConfig.ShortTermGrowthRateModifier;
        
        // calculate the new prices
        result.CurrentLongTermInvestmentPrice += (result.CurrentLongTermInvestmentPrice * longTermGrowthRate);
        result.CurrentMidTermInvestmentPrice += (result.CurrentMidTermInvestmentPrice * midTermGrowthRate);
        result.CurrentShortTermInvestmentPrice += (result.CurrentShortTermInvestmentPrice * shortTermGrowthRate);
        
        // add to history
        result.LongRangeInvestmentCostHistory.Add(result.CurrentLongTermInvestmentPrice);
        return result;
    }

    public static CurrentPrices CopyPrices(CurrentPrices originalPrices)
    {
        return new CurrentPrices()
        {
            CurrentLongTermGrowthRate = originalPrices.CurrentLongTermGrowthRate,
            CurrentLongTermInvestmentPrice = originalPrices.CurrentLongTermInvestmentPrice,
            CurrentMidTermInvestmentPrice = originalPrices.CurrentMidTermInvestmentPrice,
            CurrentShortTermInvestmentPrice = originalPrices.CurrentShortTermInvestmentPrice,
            LongRangeInvestmentCostHistory = originalPrices.LongRangeInvestmentCostHistory,
        };
    }
}