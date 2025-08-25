using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lib.DataTypes.MonteCarlo
{
    /// <summary>
    /// this class is neeeded so that the context doesn't try to save simulated data back to the database
    /// </summary>
    [Table("DebtAccount")]
    [PrimaryKey(nameof(Id))]
    public record McDebtAccount
    {
        public required Guid Id { get; set; }
        public required string Name { get; set; }
        public required List<McDebtPosition> Positions { get; set; } = [];

    }
}
