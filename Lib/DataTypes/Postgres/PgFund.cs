using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lib.DataTypes.Postgres;

[Table("fund", Schema = "personalfinance")]
[PrimaryKey(nameof(Symbol))]
public record PgFund()
{
    [Column("symbol", TypeName = "varchar(20)")]
    public required string Symbol { get; init; }
    
    [Column("name", TypeName = "varchar(200)")]
    public required string Name { get; init; }
    
    [Column("fundtype1")]
    public int? FundType1Id { get; init; }
    public PgFundType? FundType1 { get; init; }
    
    [Column("fundtype2")]
    public int? FundType2Id { get; init; }
    public PgFundType? FundType2 { get; init; }
    
    [Column("fundtype3")]
    public int? FundType3Id { get; init; }
    public PgFundType? FundType3 { get; init; }
    
    [Column("fundtype4")]
    public int? FundType4Id { get; init; }
    public PgFundType? FundType4 { get; init; }
    
    [Column("fundtype5")]
    public int? FundType5Id { get; init; }
    public PgFundType? FundType5 { get; init; }
    public required List<PgPosition> Positions { get; init; } = [];
    
}