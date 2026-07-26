namespace Lib.DataTypes.MonteCarlo;

public struct CurrentPrices
{
    public CurrentPrices()
    {
    }

    public decimal CurrentEquityGrowthRate { get; set; } = 0m;
    public decimal CurrentEquityInvestmentPrice { get; set; } = 100m;
    public decimal CurrentMidTermInvestmentPrice { get; set; } = 100m;
    public decimal CurrentShortTermInvestmentPrice { get; set; } = 100m;
    public decimal CurrentTreasuryCoupon { get; set; } = 0.04m;
    /// <summary>
    /// Cumulative product of (1 + CpiGrowth) for each month elapsed. Starts at 1.0 (base year).
    /// Never reset mid-simulation. Multiply any base-year dollar amount by this value to get
    /// the inflation-adjusted nominal equivalent.
    /// </summary>
    public decimal CumulativeCpiMultiplier { get; set; } = 1.0m;
    /// <summary>
    /// The raw CpiGrowth value from the most recent HypotheticalLifeTimeGrowthRate.
    /// Used by projection functions (e.g. CalculateCashNeedForNMonths) to estimate future
    /// inflation without an extra parameter.
    /// </summary>
    public decimal CurrentCpiGrowthRate { get; set; } = 0m;
    public List<decimal> EquityCostHistory { get; set; } = [];
}