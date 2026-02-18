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
        // ── Why 1980? The structural-break argument ───────────────────────────────────────────
        // The historicalgrowth table almost certainly contains SP500 data back to ~1957 and CPI
        // data into the 1940s.  Including that earlier history might seem attractive — more data
        // generally improves OLS estimates, and the 1970s stagflation decade (high inflation +
        // weak equities) is currently invisible to the model.
        //
        // However, VAR assumes the data comes from a stationary process: the statistical
        // relationships between variables (SP500 ↔ CPI ↔ treasury) must be stable across the
        // entire sample window.  The pre-1980 economy had fundamentally different structural
        // characteristics that permanently altered those relationships:
        //
        //   • Bretton Woods gold standard (collapsed August 1971) — before this, exchange rates
        //     and monetary policy operated under entirely different constraints.
        //   • Nixon price controls (1971–1974) — artificially suppressed CPI during the period,
        //     then caused a sharp re-release of inflation when they ended.
        //   • Oil embargoes and supply shocks (1973, 1979) — produced stagflation patterns with
        //     no modern parallel in terms of duration and severity.
        //   • Fed money-supply targeting (pre-Volcker) vs. modern interest-rate targeting —
        //     the transmission mechanism between Fed policy and treasury/equity markets changed
        //     structurally when Volcker pivoted in 1979–1982.
        //
        // Mixing pre- and post-1980 data in one VAR introduces structural breaks: periods where
        // the cross-asset relationships changed permanently.  This would produce coefficient
        // estimates that are a blended average of two incompatible regimes, degrading accuracy
        // for the modern regime that actually governs future markets.
        //
        // The 1980 cutoff is the earliest year where modern monetary policy, floating exchange
        // rates, open capital markets, and the current inflation-targeting framework are broadly
        // in effect.  It is a single-line edit if you want to experiment (change >= 1980 to
        // >= 1970 etc.).  The VarDiagnosticsWriter HTML tool can help you visually evaluate
        // whether including earlier data makes synthetic lifetime trajectories more or less
        // realistic before committing to a different cutoff.
        // ─────────────────────────────────────────────────────────────────────────────────────
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
