using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lib.DataTypes;

[Table("fund", Schema = "personalfinance")]
[PrimaryKey(nameof(Symbol))]
public record PgFund()
{
    [Column("symbol", TypeName = "varchar(20)")]
    public required string Symbol { get; set; }
    
    [Column("name", TypeName = "varchar(200)")]
    public required string Name { get; set; }
    
    [Column("fundtype1")]
    public int? FundType1Id { get; set; }
    public PgFundType? FundType1 { get; set; }
    
    [Column("fundtype2")]
    public int? FundType2Id { get; set; }
    public PgFundType? FundType2 { get; set; }
    
    [Column("fundtype3")]
    public int? FundType3Id { get; set; }
    public PgFundType? FundType3 { get; set; }
    
    [Column("fundtype4")]
    public int? FundType4Id { get; set; }
    public PgFundType? FundType4 { get; set; }
    
    [Column("fundtype5")]
    public int? FundType5Id { get; set; }
    public PgFundType? FundType5 { get; set; }
    
}