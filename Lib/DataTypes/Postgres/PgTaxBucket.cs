using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using Lib.DataTypes.MonteCarlo;

namespace Lib.DataTypes.Postgres;
[Table("taxbucket", Schema = "personalfinance")]
[PrimaryKey(nameof(Id))]
public record PgTaxBucket
{
    [Column("id")]
    public required int Id { get; set; }
    
    [Column("name", TypeName = "varchar(100)")]
    public required string Name { get; set; }

    public List<PgInvestmentAccount> InvestmentAccounts { get; set; } = [];
}