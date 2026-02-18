namespace Lib.DataTypes.MonteCarlo;

public record HypotheticalLifeTimeGrowthRate
{
    public decimal SpGrowth { get; init; }
    public decimal CpiGrowth { get; init; }
    public decimal TreasuryGrowth { get; init; }

    public static decimal operator *(HypotheticalLifeTimeGrowthRate r, decimal m) => r.SpGrowth * m;
}
