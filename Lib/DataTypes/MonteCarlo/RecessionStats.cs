using NodaTime;

namespace Lib.DataTypes.MonteCarlo;

public struct RecessionStats
{
    public RecessionStats()
    {
    }

    public bool AreWeInADownYear { get; set; } = false;
    public decimal DownYearCounter { get; set; } = 0M;
    public bool AreWeInAusterityMeasures { get; set; } = false;
    public bool AreWeInExtremeAusterityMeasures { get; set; } = false;
    public LocalDateTime? LastExtremeAusterityMeasureEnd { get; set; } = null;
    public decimal RecessionRecoveryPoint { get; set; } = 0M;
}