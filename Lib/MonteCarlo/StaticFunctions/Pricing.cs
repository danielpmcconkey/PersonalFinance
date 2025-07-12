using System.Diagnostics;
using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Pricing
{ 
    /// <summary>
    /// creates an array of pricing dictionaries where the dictionary key is the first of the month for every month
    /// between a date in the past and a date in the future (that I'm not expected to live to). The array represents one
    /// dictionary for every posible life to be simulated.
    ///
    /// This allows all run 0s to use the same pricing and all run 8675s to all use the same pricing. This gives us
    /// apples-to-apples comparisons when comparing retirement models. Though this is probably overkill. We create
    /// enough data to supply 10,000 runs, each going for 100 years. 
    /// </summary>
    /// <param name="actualHistoricalData">A decimal array of actual S&P 500 data to use as reference. The dates don't
    /// matter, only that the prices are in actual chronological order</param>
    public static Dictionary<LocalDateTime, decimal>[] CreateHypotheticalPricingForRuns(
        decimal [] actualHistoricalData)
    {
        var maxRunsPerBatch = MonteCarloConfig.MaxLivesPerBatch;

        var prices = new Dictionary<LocalDateTime, decimal>[maxRunsPerBatch];
        // create first and last dates that will always be the same, even
        // if the simulation start and end dates change. this will allow
        // us to have an apples to apples comparison to models created years
        // apart
        var firstDateToCreate = new LocalDateTime(2025,2,1,0,0);
        var lastDateToCreate = new LocalDateTime(2125,2,1,0,0); // I'll be 150. If I live that long, I'll have figured out my finances by then
        var historyDataMonthsCount = actualHistoricalData.Length;
        int seed = 0;
            

        for (int i = 0; i < maxRunsPerBatch; i++)
        {
            var dateCursor = firstDateToCreate;
            Dictionary<LocalDateTime, Decimal> thisRunsPrices = [];
            while(dateCursor <= lastDateToCreate)
            {
                Random rand = new Random(seed);
                int historicalTrendsPointer = rand.Next(0, historyDataMonthsCount);
                decimal thisPrice = actualHistoricalData[historicalTrendsPointer];
                thisRunsPrices[dateCursor] = thisPrice;
                dateCursor = dateCursor.PlusMonths(1);
                seed++;
            }
            prices[i] = thisRunsPrices;
        }
        return prices;
    }
    /// <summary>
    /// pulls the actual historical data from the DB
    /// </summary>
    public static decimal[] FetchSAndP500HistoricalTrends()
    {
        using var context = new PgContext();
        /*
         * this is an array of month over month growth of the S&P500 going
         * back to 1980. we use 1980 because the 50 years prior don't
         * reflect more modern behavior. I hope.
         * */
        var  historicalGrowthRates = context.HistoricalGrowthRates
            .Where(x => x.Year >= 1980)
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .Select(x => x.InflationAdjustedGrowth)
            .ToArray();
        return historicalGrowthRates;
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