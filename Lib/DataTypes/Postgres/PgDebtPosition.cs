using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using NodaTime;

namespace Lib.DataTypes.Postgres;
[Table("debtposition", Schema = "personalfinance")]
[PrimaryKey(nameof(Id))]
public record PgDebtPosition
{
    [Column("id")]
    public required int Id { get; init; }
    
    [Column("debtaccount")]
    public required int DebtAccountId { get; init; }
    public PgDebtAccount? DebtAccount { get; init; }
    
    [Column("position_date")]
    public required LocalDateTime PositionDate { get; init; }
    
    [Column("current_balance", TypeName = "numeric(12,4)")]
    public required decimal CurrentBalance { get; init; }
}