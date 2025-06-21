using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lib.DataTypes;
[Table("investmentaccountgroup", Schema = "personalfinance")]
[PrimaryKey(nameof(Id))]
public record PgInvestmentAccountGroup
{
    [Column("id")]
    public required int Id { get; set; }
    
    [Column("name", TypeName = "varchar(200)")]
    public required string Name { get; set; }
    
    public List<PgInvestmentAccount> InvestmentAccounts { get; set; } = [];
}