using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NodaTime;

namespace Lib.DataTypes.MonteCarlo
{
    public record SimulationAllLivesResult
    {
        //
        // public required Guid Id { get; set; }
        // public McModel? Model { get; set; }
        // public required Guid ModelId { get; set; }
        // public required LocalDateTime MeasuredDate { get; set; }
        // [Column(TypeName = "decimal(12,2)")] public required decimal NetWorthAt90thPercentile { get; set; }
        // [Column(TypeName = "decimal(12,2)")] public required decimal NetWorthAt75thPercentile { get; set; }
        // [Column(TypeName = "decimal(12,2)")] public required decimal NetWorthAt50thPercentile { get; set; }
        // [Column(TypeName = "decimal(12,2)")] public required decimal NetWorthAt25thPercentile { get; set; }
        // [Column(TypeName = "decimal(12,2)")] public required decimal NetWorthAt10thPercentile { get; set; }
        // [Column(TypeName = "decimal(12,2)")] public required decimal SpendAt90thPercentile { get; set; }
        // [Column(TypeName = "decimal(12,2)")] public required decimal SpendAt75thPercentile { get; set; }
        // [Column(TypeName = "decimal(12,2)")] public required decimal SpendAt50thPercentile { get; set; }
        // [Column(TypeName = "decimal(12,2)")] public required decimal SpendAt25thPercentile { get; set; }
        // [Column(TypeName = "decimal(12,2)")] public required decimal SpendAt10thPercentile { get; set; }
        // [Column(TypeName = "decimal(12,2)")] public required decimal TaxesAt90thPercentile { get; set; }
        // [Column(TypeName = "decimal(12,2)")] public required decimal TaxesAt75thPercentile { get; set; }
        // [Column(TypeName = "decimal(12,2)")] public required decimal TaxesAt50thPercentile { get; set; }
        // [Column(TypeName = "decimal(12,2)")] public required decimal TaxesAt25thPercentile { get; set; }
        // [Column(TypeName = "decimal(12,2)")] public required decimal TaxesAt10thPercentile { get; set; }
        // [Column(TypeName = "decimal(7,6)")] public required decimal BankruptcyRate { get; set; }
    }
}
