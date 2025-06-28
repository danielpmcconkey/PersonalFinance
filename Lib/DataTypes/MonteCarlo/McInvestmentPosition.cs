
using NodaTime;

namespace Lib.DataTypes.MonteCarlo
{
    public record McInvestmentPosition
    {
        public Guid Id { get; set; }
        
        public required bool IsOpen { get; set; } = true;
        public required string Name { get; set; }
        public required LocalDateTime Entry { get; set; }
        public required McInvestmentPositionType InvestmentPositionType { get; set; }

        
        public required long InitialCost { get; set; }
        public required long Quantity { get; set; }
        public required long Price { get; set; }
        public long CurrentValue => Price * Quantity;
    }
}
