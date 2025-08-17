namespace Lib.DataTypes;

public record Position()
{        
    public DateTime PositionDate { get; set; }
    public required string MonthAbbreviation { get; set; }
    public required int AccountId { get; set; }
    public required string AccountName { get; set; }
    public required string AccountGroup { get; set; }
    public required string TaxBucket { get; set; }
    public required string Symbol { get; set; }
    public required decimal Price { get; set; }
    public required decimal TotalQuantity { get; set; }
    public required decimal ValueAtTime { get; set; }
    public decimal? CostBasis { get; set; }
    public decimal? TotalWealthAtTime { get; set; }
    public decimal? InvestmentGain { get; set; }
    public decimal? PercentOfWealth { get; set; }
    public string? FundType1 { get; set; }
    public string? FundType2 {get; set; }
    public string? FundType3 { get; set; }
    public string? FundType4 { get; set; }
    public string? FundType5 { get; set; }
}