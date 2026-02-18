using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.Var;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Pricing
{
    private static VarModel? _varModelCache = null;
    private static Dictionary<int, Dictionary<LocalDateTime, HypotheticalLifeTimeGrowthRate>> _hypotheticalPricingCache = [];

    /// <summary>
    /// Loads historical growth data from the DB, fits a VAR(3) model, caches and returns it.
    /// Safe to call multiple times; fitting only happens once per process lifetime.
    /// </summary>
    public static VarModel LoadAndFitVarModel()
    {
        if (_varModelCache is not null) return _varModelCache;

        using var context = new PgContext();
        var observations = context.HistoricalGrowthData
            .Where(x => x.Year >= 1980 && x.SpGrowth != null && x.CpiGrowth != null && x.TreasuryGrowth != null)
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .Select(x => new double[] { (double)x.SpGrowth!.Value, (double)x.CpiGrowth!.Value, (double)x.TreasuryGrowth!.Value })
            .ToList();

        _varModelCache = VarFitter.Fit(observations);
        return _varModelCache;
    }

    /// <summary>
    /// Generates (or retrieves from cache) a full lifetime of hypothetical growth rates keyed by
    /// simulation date.  The same <paramref name="lifeIndex"/> always produces identical rates.
    /// </summary>
    public static Dictionary<LocalDateTime, HypotheticalLifeTimeGrowthRate> CreateHypotheticalPricingForARun(
        VarModel varModel, int lifeIndex)
    {
        if (_hypotheticalPricingCache.TryGetValue(lifeIndex, out var cached)) return cached;

        var firstDate = MonteCarloConfig.MonteCarloSimStartDate;
        var lastDate  = MonteCarloConfig.MonteCarloSimEndDate;
        int months = (lastDate.Year - firstDate.Year) * 12
                   + (lastDate.Month - firstDate.Month) + 1;

        var hypotheticalLife = VarLifetimeGenerator.Generate(varModel, lifeIndex, months);

        Dictionary<LocalDateTime, HypotheticalLifeTimeGrowthRate> prices = [];
        var dateCursor = firstDate;
        for (int i = 0; i < hypotheticalLife.Length; i++)
        {
            prices[dateCursor] = hypotheticalLife[i];
            dateCursor = dateCursor.PlusMonths(1);
        }

        _hypotheticalPricingCache[lifeIndex] = prices;
        return prices;
    }

    public static CurrentPrices SetLongTermGrowthRateAndPrices(CurrentPrices prices, HypotheticalLifeTimeGrowthRate rates)
    {
        var result = Pricing.CopyPrices(prices);

        // calculate new equity price
        result.CurrentEquityGrowthRate = rates.SpGrowth;
        result.CurrentEquityInvestmentPrice += (result.CurrentEquityInvestmentPrice * rates.SpGrowth);
        // add the new equity price to history
        result.EquityCostHistory.Add(result.CurrentEquityInvestmentPrice);
        // update bond coupon
        result.CurrentTreasuryCoupon += (result.CurrentTreasuryCoupon * rates.TreasuryGrowth);
        // calculate mid and short-term growth rates based on long-term growth rate
        var midTermGrowthRate   = rates * InvestmentConfig.MidTermGrowthRateModifier;
        var shortTermGrowthRate = rates * InvestmentConfig.ShortTermGrowthRateModifier;

        // calculate the new prices
        result.CurrentMidTermInvestmentPrice   += (result.CurrentMidTermInvestmentPrice   * midTermGrowthRate);
        result.CurrentShortTermInvestmentPrice += (result.CurrentShortTermInvestmentPrice * shortTermGrowthRate);

        return result;
    }

    public static CurrentPrices CopyPrices(CurrentPrices originalPrices)
    {
        return new CurrentPrices()
        {
            CurrentEquityGrowthRate        = originalPrices.CurrentEquityGrowthRate,
            CurrentEquityInvestmentPrice   = originalPrices.CurrentEquityInvestmentPrice,
            CurrentMidTermInvestmentPrice  = originalPrices.CurrentMidTermInvestmentPrice,
            CurrentShortTermInvestmentPrice = originalPrices.CurrentShortTermInvestmentPrice,
            EquityCostHistory              = originalPrices.EquityCostHistory,
        };
    }
}
