
using NodaTime;

namespace Lib.DataTypes.MonteCarlo
{
    public record McInvestmentPosition
    {
        public Guid Id { get; set; }
        
        public required bool IsOpen { get; set; } = true;
        public required string Name { get; set; }
        public required LocalDateTime Entry { get; set; }
        public required McInvestmentPositionType InvenstmentPositionType { get; set; }

        
        public required decimal InitialCost { get; set; }
        public required decimal Quantity { get; set; }
        public required decimal Price { get; set; }
        public decimal CurrentValue { get { return Math.Round(Price * Quantity, 4); } }
    }
}
