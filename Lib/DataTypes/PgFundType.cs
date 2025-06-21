using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lib.DataTypes;
[Table("fundtype", Schema = "personalfinance")]
[PrimaryKey(nameof(Id))]
public record PgFundType
{
    [Column("id")]
    public required int Id { get; set; }
    
    [Column("name", TypeName = "varchar(200)")]
    public required string Name { get; set; }

    public List<PgFund>? FundsOfType1 { get; set; } = [];
    public List<PgFund>? FundsOfType2 { get; set; } = [];
    public List<PgFund>? FundsOfType3 { get; set; } = [];
    public List<PgFund>? FundsOfType4 { get; set; } = [];
    public List<PgFund>? FundsOfType5 { get; set; } = [];
}