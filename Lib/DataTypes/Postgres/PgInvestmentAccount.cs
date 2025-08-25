using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lib.DataTypes.Postgres;


[Table("investmentaccount", Schema = "personalfinance")]
[PrimaryKey(nameof(Id))]
public record PgInvestmentAccount
{
    [Column("id")]
    public required int Id { get; set; }
    
    [Column("name", TypeName = "varchar(200)")]
    public required string Name { get; set; }
    
    [Column("taxbucket")]
    public required int TaxBucketId { get; set; }
    public PgTaxBucket? TaxBucket { get; set; }
    
    [Column("investmentaccountgroup")]
    public required int InvestmentAccountGroupId { get; set; }
    public PgInvestmentAccountGroup? InvestmentAccountGroup { get; set; }

    public required List<PgPosition> Positions { get; set; } = [];
}