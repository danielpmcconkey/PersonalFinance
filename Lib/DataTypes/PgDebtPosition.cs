using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using NodaTime;

namespace Lib.DataTypes;
[Table("debtposition", Schema = "personalfinance")]
[PrimaryKey(nameof(Id))]
public record PgDebtPosition
{
    [Column("id")]
    public required int Id { get; set; }
    
    [Column("debtaccount")]
    public required int DebtAccountId { get; set; }
    public PgDebtAccount? DebtAccount { get; set; }
    
    [Column("position_date")]
    public required LocalDateTime PositionDate { get; set; }
    
    [Column("current_balance", TypeName = "numeric(12,4)")]
    public required decimal CurrentBalance { get; set; }
}