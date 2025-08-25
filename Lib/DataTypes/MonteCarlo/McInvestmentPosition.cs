using NodaTime;

namespace Lib.DataTypes.MonteCarlo
{
    /// <summary>
    /// this class is neeeded so that the context doesn't try to save simulated data back to the database
    /// </summary>
    public record McInvestmentPosition
    {
        public Guid Id { get; set; }
        
        public required bool IsOpen { get; set; } = true;
        public required string Name { get; set; }
        public required LocalDateTime Entry { get; set; }
        public required McInvestmentPositionType InvestmentPositionType { get; set; }

        
        public required decimal InitialCost { get; set; }
        public required decimal Quantity { get; set; }
        public required decimal Price { get; set; }
        public decimal CurrentValue => Price * Quantity;
    }
}