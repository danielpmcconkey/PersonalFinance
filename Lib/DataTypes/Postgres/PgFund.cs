using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lib.DataTypes.Postgres;

[Table("fund", Schema = "personalfinance")]
[PrimaryKey(nameof(Symbol))]
public record PgFund
{
    [Column("symbol", TypeName = "varchar(20)")]
    public required string Symbol { get; init; }
    
    [Column("name", TypeName = "varchar(200)")]
    public required string Name { get; init; }
    
    [Column("investment_type")]
    public required int InvestmentTypeId { get; init; }
    public required PgFundType InvestmentType { get; init; }
    
    [Column("size")]
    public required int SizeId { get; init; }
    public required PgFundType Size { get; init; }
    
    [Column("index_or_individual")]
    public required int IndexOrIndividualId { get; init; }
    public required PgFundType IndexOrIndividual { get; init; }
    
    [Column("sector")]
    public required int SectorId { get; init; }
    public required PgFundType Sector { get; init; }
    
    [Column("region")]
    public required int RegionId { get; init; }
    public required PgFundType Region { get; init; }
    
    [Column("objective")]
    public required int ObjectiveId { get; init; }
    public required PgFundType Objective { get; init; }
    public required List<PgPosition> Positions { get; init; } = [];
    
}