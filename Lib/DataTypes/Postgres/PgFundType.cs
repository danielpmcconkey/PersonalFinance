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

    public List<PgFund>? FundsOfInvestmentType { get; init; } = [];
    public List<PgFund>? FundsOfSizeType { get; init; } = [];
    public List<PgFund>? FundsOfIndexOrIndividualType { get; init; } = [];
    public List<PgFund>? FundsOfSectorType { get; init; } = [];
    public List<PgFund>? FundsOfRegionType { get; init; } = [];
    
    public List<PgFund>? FundsOfObjectiveType { get; init; } = [];
}