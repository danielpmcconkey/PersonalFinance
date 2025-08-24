using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using NodaTime;

namespace Lib.DataTypes.Postgres;
[Table("cashposition", Schema = "personalfinance")]
[PrimaryKey(nameof(Id))]
public record PgCashPosition
{
    [Column("id")]
    public required int Id { get; set; }
    
    [Column("cashaccount")]
    public required int CashAccountId { get; set; }
    public PgCashAccount? CashAccount { get; set; }
    
    [Column("position_date")]
    public required LocalDateTime PositionDate { get; set; }
    
    [Column("current_balance", TypeName = "numeric(12,4)")]
    public required decimal CurrentBalance { get; set; }
}