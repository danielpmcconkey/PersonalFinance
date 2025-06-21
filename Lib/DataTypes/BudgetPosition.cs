namespace Lib.DataTypes;

public record BudgetPosition()
{
    public DateTime PositionDate { get; set; }
    public string? MonthAbbreviation { get; set; }
    public string? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? ParentCategoryId { get; set; }
    public int? Ordinal { get; set; }
    public decimal SumTotal { get; set; }
    public bool ShowInReport { get; set; }
}