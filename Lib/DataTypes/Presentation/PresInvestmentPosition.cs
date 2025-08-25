using NodaTime;

namespace Lib.DataTypes.Presentation;

public record PresInvestmentPosition
{
    public required string AccountGroupName { get; init; }
    public required string AccountName { get; init; }
    public required string TaxBucketName { get; init; }
    public required LocalDate MonthEnd { get; init; }
    public required string Symbol { get; init; }
    public string? FundType1 { get; init; }
    public string? FundType2 { get; init; }
    public string? FundType3 { get; init; }
    public string? FundType4 { get; init; }
    public string? FundType5 { get; init; }
    public required decimal Value { get; init; }
    public required bool IsCurrent { get; init; }
}