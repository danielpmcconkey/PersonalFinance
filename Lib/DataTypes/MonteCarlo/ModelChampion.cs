using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Lib.DataTypes.MonteCarlo;

/// <summary>
/// this class houses prior model champions such that they won't be deleted by model clean-up
/// </summary>
[Table("modelchampion", Schema = "personalfinance")]
[PrimaryKey(nameof(Id))]
public record ModelChampion
{
    [Column("id")]
    public required Guid Id { get; set; }
    
    [Column("modelid")]
    public required Guid ModelId { get; set; }
    public Model? Model { get; set; }
    
    [Column("championdesignateddate")]
    public required LocalDateTime ChampionDesignatedDate { get; set; }
}