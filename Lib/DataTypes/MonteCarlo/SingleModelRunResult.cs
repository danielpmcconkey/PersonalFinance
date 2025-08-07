using Lib.MonteCarlo.StaticFunctions;
using NodaTime;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lib.DataTypes.MonteCarlo;

[Table("singlemodelrunresult", Schema = "personalfinance")]
[PrimaryKey(nameof(ModelId), nameof(RunDate))]
public record SingleModelRunResult
{
    [Column("modelid")]
    public required Guid ModelId { get; set; }
    
    [Column("rundate")]
    public required LocalDateTime RunDate {  get; set; }
    
    [NotMapped]
    public required SingleModelRunResultStatLineAtTime[] NetWorthStatsOverTime { get; init; }
    [NotMapped]
    public required SingleModelRunResultStatLineAtTime[] TotalFunPointsOverTime { get; init; }
    [NotMapped]
    public required SingleModelRunResultStatLineAtTime[] TotalSpendOverTime { get; init; }
    [NotMapped]
    public required SingleModelRunResultStatLineAtTime[] TotalTaxOverTime { get; init; }
    [NotMapped]
    public required SingleModelRunResultBankruptcyRateAtTime[] BankruptcyRateOverTime { get; init; }
    [NotMapped]
    public required List<SimSnapshot>[] AllSnapshots { get; init; }
    
    [Column("networthatendofsim10", TypeName = "numeric(18,4)")]
    public required decimal NetWorthAtEndOfSim10 { get; init; }
    
    [Column("networthatendofsim25", TypeName = "numeric(18,4)")]
    public required decimal NetWorthAtEndOfSim25 { get; init; }
    
    [Column("networthatendofsim50", TypeName = "numeric(18,4)")]
    public required decimal NetWorthAtEndOfSim50 { get; init; }
    
    [Column("networthatendofsim75", TypeName = "numeric(18,4)")]
    public required decimal NetWorthAtEndOfSim75 { get; init; }
    
    [Column("networthatendofsim90", TypeName = "numeric(18,4)")]
    public required decimal NetWorthAtEndOfSim90 { get; init; }
    
    
    
    [Column("funpointsatendofsim10", TypeName = "numeric(18,4)")]
    public required decimal FunPointsAtEndOfSim10 { get; init; }
    
    [Column("funpointsatendofsim25", TypeName = "numeric(18,4)")]
    public required decimal FunPointsAtEndOfSim25 { get; init; }
    
    [Column("funpointsatendofsim50", TypeName = "numeric(18,4)")]
    public required decimal FunPointsAtEndOfSim50 { get; init; }
    
    [Column("funpointsatendofsim75", TypeName = "numeric(18,4)")]
    public required decimal FunPointsAtEndOfSim75 { get; init; }
    
    [Column("funpointsatendofsim90", TypeName = "numeric(18,4)")]
    public required decimal FunPointsAtEndOfSim90 { get; init; }
    
    
    [Column("spendatendofsim10", TypeName = "numeric(18,4)")]
    public required decimal SpendAtEndOfSim10 { get; init; }
    
    [Column("spendatendofsim25", TypeName = "numeric(18,4)")]
    public required decimal SpendAtEndOfSim25 { get; init; }
    
    [Column("spendatendofsim50", TypeName = "numeric(18,4)")]
    public required decimal SpendAtEndOfSim50 { get; init; }
    
    [Column("spendatendofsim75", TypeName = "numeric(18,4)")]
    public required decimal SpendAtEndOfSim75 { get; init; }
    
    [Column("spendatendofsim90", TypeName = "numeric(18,4)")]
    public required decimal SpendAtEndOfSim90 { get; init; }
    
    
    [Column("taxatendofsim10", TypeName = "numeric(18,4)")]
    public required decimal TaxAtEndOfSim10 { get; init; }
    [Column("taxatendofsim25", TypeName = "numeric(18,4)")]
    public required decimal TaxAtEndOfSim25 { get; init; }
    [Column("taxatendofsim50", TypeName = "numeric(18,4)")]
    public required decimal TaxAtEndOfSim50 { get; init; }
    [Column("taxatendofsim75", TypeName = "numeric(18,4)")]
    public required decimal TaxAtEndOfSim75 { get; init; }
    [Column("taxatendofsim90", TypeName = "numeric(18,4)")]
    public required decimal TaxAtEndOfSim90 { get; init; }
    
    
    
    [Column("bankruptcyrateatendofsim", TypeName = "numeric(6,4)")]
    public required decimal BankruptcyRateAtEndOfSim { get; init; }
}

public record SingleModelRunResultStatLineAtTime(
    LocalDateTime Date, 
    decimal Percentile10, 
    decimal Percentile25, 
    decimal Percentile50, 
    decimal Percentile75, 
    decimal Percentile90
    );

public record SingleModelRunResultBankruptcyRateAtTime(
    LocalDateTime Date, 
    decimal BankruptcyRate
);
