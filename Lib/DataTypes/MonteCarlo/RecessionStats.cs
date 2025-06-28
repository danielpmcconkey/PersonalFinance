using NodaTime;

namespace Lib.DataTypes.MonteCarlo;

public class RecessionStats
{
    public bool? AreWeInADownYear { get; set; }
    public long? DownYearCounter { get; set; }
    public bool? AreWeInAusterityMeasures { get; set; }
    public bool? AreWeInExtremeAusterityMeasures { get; set; }
    public LocalDateTime? LastExtremeAusterityMeasureEnd { get; set; }
    public long? RecessionRecoveryPoint { get; set; }

    public RecessionStats(
        bool? areWeInADownYear = null,
        long? downYearCounter = null,
        bool? areWeInAusterityMeasures = null,
        bool? areWeInExtremeAusterityMeasures = null,
        LocalDateTime? lastExtremeAusterityMeasureEnd = null,
        long? recessionRecoveryPoint = null
    )
    {
        AreWeInADownYear = areWeInADownYear;
        DownYearCounter = downYearCounter;
        AreWeInAusterityMeasures = areWeInAusterityMeasures;
        AreWeInExtremeAusterityMeasures = areWeInExtremeAusterityMeasures;
        LastExtremeAusterityMeasureEnd = lastExtremeAusterityMeasureEnd;
        RecessionRecoveryPoint = recessionRecoveryPoint;
    }
}