using NodaTime;

namespace Lib.DataTypes.MonteCarlo;

public record SimSnapshot
{
    public required LocalDateTime CurrentDateWithinSim {  get; set; }
    public required decimal NetWorth { get; set; }
    public required decimal TotalFunPointsSoFar { get; set; }
    public required decimal TotalSpendSoFar { get; set; }
    public required decimal TotalTaxSoFar { get; set; }
    public required bool IsBankrupt { get; set; }
    public required bool IsRetired { get; set; }
    public required bool AreWeInARecession { get; set; }
    public required bool AreWeInExtremeAusterityMeasures { get; set; }
    public required decimal TotalHealthCareSpendSoFar { get; set; }
    public required decimal TotalCapitalGainsSoFar { get; set; }
    public required decimal TotalIraDistributionsSoFar { get; set; }
    public required decimal TotalTaxFreeWithdrawalsSoFar { get; set; }
    public required decimal TotalFunSpendSoFar { get; set; }
    public required decimal TotalNotFunSpendSoFar { get; set; }
    
}