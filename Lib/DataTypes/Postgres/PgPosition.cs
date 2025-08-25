using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using NodaTime;

namespace Lib.DataTypes.Postgres;

[Table("position", Schema = "personalfinance")]
[PrimaryKey(nameof(Id))]
public record PgPosition()
{
    [Column("id")]
    public required int Id { get; init; }
    
    [Column("investmentaccount")]
    public required int InvestmentAccountId { get; init; }
    public required PgInvestmentAccount InvestmentAccount { get; init; }
    
    [Column("symbol", TypeName = "varchar(100)")]
    public required string Symbol { get; init; }
    public required PgFund Fund { get; init; }
    
    [Column("position_date")]
    public required LocalDateTime PositionDate { get; init; }
    
    [Column("price", TypeName = "numeric(12,4)")]
    public required decimal Price { get; init; }
    
    [Column("total_quantity", TypeName = "numeric(12,4)")]
    public required decimal TotalQuantity { get; init; }
    
    [Column("current_value", TypeName = "numeric(12,4)")]
    public required decimal CurrentValue { get; init; }
    
    [Column("cost_basis", TypeName = "numeric(12,4)")]
    public required decimal CostBasis { get; init; }
}