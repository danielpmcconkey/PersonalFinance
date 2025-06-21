using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
namespace Lib.DataTypes.MonteCarlo;

[Table("historicalgrowth", Schema = "personalfinance")]
[PrimaryKey(nameof(Year), nameof(Month))]
public record HistoricalGrowthRate
{
    [Column("year")]
    public required int Year { get; set; }
    
    [Column("month")]
    public required int Month { get; set; }
    
    [Column("spcurrentvalue", TypeName = "numeric(10,2)")]
    public required decimal SpCurrentValue { get; set; }
    
    [Column("sppriorvalue", TypeName = "numeric(10,2)")]
    public required decimal SpPriorValue { get; set; }
    
    [Column("spgrowth", TypeName = "numeric(6,5)")]
    public required decimal SpGrowth { get; set; }
    
    [Column("cpicurrentvalue", TypeName = "numeric(10,2)")]
    public required decimal CpiCurrentValue { get; set; }
    
    [Column("cpipriorvalue", TypeName = "numeric(10,2)")]
    public required decimal CpiPriorValue { get; set; }
    
    [Column("cpigrowth", TypeName = "numeric(6,5)")]
    public required decimal CpiGrowth { get; set; }
    
    [Column("inflation_adjusted_growth", TypeName = "numeric(6,5)")]
    public required decimal InflationAdjustedGrowth { get; set; }

    
}