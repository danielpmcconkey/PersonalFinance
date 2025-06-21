using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NodaTime;

namespace Lib.DataTypes.MonteCarlo
{
    public record NetWorthMeasurement
    {
        public required LocalDateTime MeasuredDate {  get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public required decimal  TotalAssets { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public required decimal TotalLiabilities { get; set; }

        [NotMapped]
        public decimal NetWorth { get {  return TotalAssets - TotalLiabilities; } }

        [Column(TypeName = "decimal(12,2)")]
        public required decimal TotalCash { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public required decimal TotalMidTermInvestments { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public required decimal TotalLongTermInvestments { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public required decimal TotalSpend { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public required decimal TotalTax { get; set; }
    }
}
