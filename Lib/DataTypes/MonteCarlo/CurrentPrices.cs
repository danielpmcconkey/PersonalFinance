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
    public decimal CurrentCpi { get; set; } = 1.00m; // set it to $1 to start
    public List<decimal> EquityCostHistory { get; set; } = [];
}