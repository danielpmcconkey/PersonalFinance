using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
namespace Lib.DataTypes;

[Table("debtaccount", Schema = "personalfinance")]
[PrimaryKey(nameof(Id))]
public record PgDebtAccount
{
    [Column("id")]
    public required int Id { get; set; }
    
    [Column("name", TypeName = "varchar(200)")]
    public required string Name { get; set; }
    
    [Column("type", TypeName = "varchar(100)")]
    public required string Type { get; set; }

    public List<PgDebtPosition> Positions { get; set; } = [];
    
    [Column("annualpercentagerate", TypeName = "numeric(5,4)")]
    public required decimal AnnualPercentageRate { get; set; }
    
    [Column("monthlypayment", TypeName = "numeric(10,2)")]
    public required decimal MonthlyPayment { get; set; }
}