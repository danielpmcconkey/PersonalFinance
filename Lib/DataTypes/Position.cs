namespace Lib.DataTypes;

public record Position()
{        
    public DateTime PositionDate { get; set; }
    public string? MonthAbbreviation { get; set; }
    public int AccountId { get; set; }
    public string? AccountName { get; set; }
    public string? AccountGroup { get; set; }
    public string? TaxBucket { get; set; }
    public string? Symbol { get; set; }
    public decimal Price { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal ValueAtTime { get; set; }
    public decimal CostBasis { get; set; }
    public decimal TotalWealthAtTime { get; set; }
    public decimal InvestmentGain { get; set; }
    public decimal PercentOfWealth { get; set; }
    public string? FundType1 { get; set; }
    public string? FundType2 {get; set; }
    public string? FundType3 { get; set; }
    public string? FundType4 { get; set; }
    public string? FundType5 { get; set; }
}