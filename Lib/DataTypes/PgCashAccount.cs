using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lib.DataTypes;
[Table("cashaccount", Schema = "personalfinance")]
[PrimaryKey(nameof(Id))]
public record PgCashAccount
{
    [Column("id")]
    public required int Id { get; set; }
    
    [Column("name", TypeName = "varchar(200)")]
    public required string Name { get; set; }
    
    [Column("type", TypeName = "varchar(100)")]
    public required string Type { get; set; }

    public List<PgCashPosition> Positions { get; set; } = [];
}