namespace Lib.DataTypes.MonteCarlo;

public class CurrentPrices
{
    public long? CurrentLongTermGrowthRate { get; set; }
    public long? CurrentLongTermInvestmentPrice { get; set; }
    public long? CurrentMidTermInvestmentPrice { get; set; }
    public long? CurrentShortTermInvestmentPrice { get; set; }

    public CurrentPrices(
        long? currentLongTermGrowthRate = null,
        long? currentLongTermInvestmentPrice = null,
        long? currentMidTermInvestmentPrice = null,
        long? currentShortTermInvestmentPrice = null)
    {
        CurrentLongTermGrowthRate = currentLongTermGrowthRate;
        CurrentLongTermInvestmentPrice = currentLongTermInvestmentPrice;
        CurrentMidTermInvestmentPrice = currentMidTermInvestmentPrice;
        CurrentShortTermInvestmentPrice = currentShortTermInvestmentPrice;
    }
}