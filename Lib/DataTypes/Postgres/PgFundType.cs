using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lib.DataTypes.Postgres;
[Table("fundtype", Schema = "personalfinance")]
[PrimaryKey(nameof(Id))]
public record PgFundType
{
    [Column("id")]
    public required int Id { get; init; }
    
    [Column("name", TypeName = "varchar(200)")]
    public required string Name { get; init; }

    public List<PgFund>? FundsOfType1 { get; init; } = [];
    public List<PgFund>? FundsOfType2 { get; init; } = [];
    public List<PgFund>? FundsOfType3 { get; init; } = [];
    public List<PgFund>? FundsOfType4 { get; init; } = [];
    public List<PgFund>? FundsOfType5 { get; init; } = [];
}