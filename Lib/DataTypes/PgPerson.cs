using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using NodaTime;

namespace Lib.DataTypes
{
    [Table("person", Schema = "personalfinance")]
    [PrimaryKey(nameof(Id))]
    public record PgPerson
    {
        [Column("id")]
        public required Guid Id { get; set; }
        
        [Column("name", TypeName = "varchar(100)")]
        public required string Name { get; set; }
        
        [Column("birthdate")]
        public required LocalDateTime BirthDate {  get; set; }

        
        
        [Column("annualsalary",TypeName = "numeric(12,2)")]
        public required decimal AnnualSalary { get; set; }

        [Column("annualbonus", TypeName = "numeric(12,2)")]
        public required decimal AnnualBonus { get; set; }

        [Column("annual401kmatchpercent",TypeName = "numeric(5,4)")]
        public required decimal Annual401kMatchPercent { get; set; }

        
        [Column("monthlyfullsocialsecuritybenefit",TypeName = "numeric(12,2)")]
        public required decimal MonthlyFullSocialSecurityBenefit { get; set; }
    }
}