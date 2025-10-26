using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lib.MonteCarlo.StaticFunctions;
using Lib.MonteCarlo.WithdrawalStrategy;
using NodaTime;

namespace Lib.DataTypes.MonteCarlo;

[Table("vw_hypothetical_lifetime_growth_rates", Schema = "personalfinance")]
[PrimaryKey(nameof(BlockStart), nameof(Ordinal))]
public record HypotheticalLifeTimeGrowthRate
{
    [Column("block_start", TypeName = "int")]
    public required int BlockStart { get; init; }
    
    [Column("ordinal", TypeName = "int")]
    public required int Ordinal { get; init; }
    
    // [Column("spgrowth", TypeName = "numeric(6,5)")]
    // public required decimal SpGrowth { get; init; }
    //
    // [Column("cpigrowth", TypeName = "numeric(6,5)")]
    // public required decimal CpiGrowth { get; init; }
    
    [Column("inflation_adjusted_growth", TypeName = "numeric(6,5)")]
    public required decimal InflationAdjustedGrowth { get; init; }
}