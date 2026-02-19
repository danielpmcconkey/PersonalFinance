using System.ComponentModel.DataAnnotations.Schema;

namespace Lib.DataTypes.MonteCarlo;

[Table("historicalgrowth", Schema = "personalfinance")]
public record HistoricalDataPoint
{
    [Column("year")]
    public int Year { get; init; }

    [Column("month")]
    public int Month { get; init; }

    [Column("sp_growth")]
    public decimal? SpGrowth { get; init; }

    [Column("cpi_growth")]
    public decimal? CpiGrowth { get; init; }

    [Column("treasury_growth")]
    public decimal? TreasuryGrowth { get; init; }

    [Column("treasury_current_value")]
    public decimal? TreasuryCurrentValue { get; init; }
}
