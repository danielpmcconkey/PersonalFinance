namespace Lib.DataTypes.MonteCarlo;

public class CurrentPrices
{
    public decimal CurrentLongTermGrowthRate { get; set; }
    public decimal CurrentLongTermInvestmentPrice { get; set; } = 100m;
    public decimal CurrentMidTermInvestmentPrice { get; set; } = 100m;
    public decimal CurrentShortTermInvestmentPrice { get; set; } = 100m;
    public List<decimal> LongRangeInvestmentCostHistory { get; set; } = [];
}