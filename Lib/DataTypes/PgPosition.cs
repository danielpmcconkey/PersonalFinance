using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using NodaTime;

namespace Lib.DataTypes;

[Table("position", Schema = "personalfinance")]
[PrimaryKey(nameof(Id))]
public record PgPosition()
{
    [Column("id")]
    public required int Id { get; set; }
    
    [Column("investmentaccount")]
    public required int InvestmentAccountId { get; set; }
    public PgInvestmentAccount? InvestmentAccount { get; set; }
    
    [Column("symbol", TypeName = "varchar(100)")]
    public required string Symbol { get; set; }
    //public PgFund? Fund { get; set; }
    
    [Column("position_date")]
    public required LocalDateTime PositionDate { get; set; }
    
    [Column("price", TypeName = "numeric(12,4)")]
    public required decimal Price { get; set; }
    
    [Column("total_quantity", TypeName = "numeric(12,4)")]
    public required decimal TotalQuantity { get; set; }
    
    [Column("current_value", TypeName = "numeric(12,4)")]
    public required decimal CurrentValue { get; set; }
    
    [Column("cost_basis", TypeName = "numeric(12,4)")]
    public required decimal CostBasis { get; set; }
}