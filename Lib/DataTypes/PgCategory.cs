using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
namespace Lib.DataTypes;

[Table("category", Schema = "personalfinance")]
[PrimaryKey(nameof(Id))]
public record PgCategory()
{
    [Column("id",  TypeName = "varchar(100)")]
    public string? Id { get; set; }
    
    
    [Column("parent_id", TypeName = "varchar(100)")]
    public string? ParentId { get; set; }
    public PgCategory? Parent { get; set; }
    
    [Column("display_name", TypeName = "varchar(100)")]
    public string? DisplayName { get; set; }
    
    
    [Column("ordinal_within_parent")]
    public Int16 OrdinalWithinParent { get; set; }
    
    [NotMapped]public decimal TransactionTotal { get; set; }
    [NotMapped] public List<PgCategory>? ChildCategories { get; set; } = [];
    [NotMapped] public List<PgTransaction>? Transactions { get; set; } = [];
    
    
    [Column("show_in_report")]
    public bool ShowInReport { get; set; }
}