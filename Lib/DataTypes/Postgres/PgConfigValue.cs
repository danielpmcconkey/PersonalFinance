using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Lib.DataTypes.Postgres;

[Table("configvalue", Schema = "personalfinance")]
[PrimaryKey(nameof(Id))]
public class PgConfigValue
{
    [Column("id",  TypeName = "int")]
    //[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public required int Id { get; init; }
    
    [Column("configkey", TypeName = "varchar(100)")]
    public required String ConfigKey { get; init; }
    
    [Column("configvalue", TypeName = "varchar(250)")]
    public required String ConfigValue { get; init; }
}