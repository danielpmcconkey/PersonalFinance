using NodaTime;

namespace Lib.DataTypes.Presentation;

public record PresInvestmentPosition
{
    public required string AccountGroupName { get; init; }
    public required string AccountName { get; init; }
    public required string TaxBucketName { get; init; }
    public required LocalDate MonthEnd { get; init; }
    public required string Symbol { get; init; }
    public required string InvestmentType { get; init; }
    public required string Size { get; init; }
    public required string IndexOrIndividual { get; init; }
    public required string Sector { get; init; }
    public required string Region { get; init; }
    
    public required string Objective { get; init; }
    public required decimal Value { get; init; }
    public required bool IsCurrent { get; init; }
}