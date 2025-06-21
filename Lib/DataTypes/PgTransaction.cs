using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lib.DataTypes;
[Table("tran", Schema = "personalfinance")]
[PrimaryKey(nameof(Id))]
public record PgTransaction
{
    [Column("id")]
    public required int Id { get; set; }
    
    [Column("category_id", TypeName = "varchar(100)")]
    public required string CategoryId { get; set; }
    public PgCategory? Category { get; set; }
    
    
    [Column("transactiondate")]
    public required DateTime TransactionDate { get; set; }
    
    [Column("description", TypeName = "varchar(250)")]
    public required string Description { get; set; }
    
    
    [Column("amount", TypeName = "numeric(10,2)")]
    public required decimal Amount { get; set; }
}