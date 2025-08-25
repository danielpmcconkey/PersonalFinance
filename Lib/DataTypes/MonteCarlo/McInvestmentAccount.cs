
namespace Lib.DataTypes.MonteCarlo
{
    /// <summary>
    /// this class is neeeded so that the context doesn't try to save simulated data back to the database
    /// </summary>
    public record McInvestmentAccount
    {
        public required Guid Id { get; set; }
        // public required Guid PersonId { get; set; }
        // public McPerson? Person { get; set; }
        public required string Name { get; set; }
        public required McInvestmentAccountType AccountType { get; set; }
        public required List<McInvestmentPosition> Positions { get; set; } = [];
    }
}
