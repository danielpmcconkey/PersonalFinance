using Lib.StaticConfig;
using NodaTime;

namespace Lib.DataTypes.MonteCarlo;

public class TaxLedger
{
    public List<(LocalDateTime earnedDate, decimal amount)> SocialSecurityIncome { get; set; } = [];
    public List<(LocalDateTime earnedDate, decimal amount)> OrdinaryIncome { get; set; } = [];
    public List<(LocalDateTime earnedDate, decimal amount)> CapitalGains { get; set; } = [];
    public decimal TotalTaxPaid { get; set; } = 0; // lifetime total

    /// <summary>
    /// list of total distributions by year that qualify against RMD requirements
    /// </summary>
    public Dictionary<int, decimal> RmdDistributions { get; set; } = [];
    
    /// <summary>
    /// this is the amount of income above social security that you want to maximize the 12% tax bracket. every year, we
    /// will reevaluate this based on the prior year's social security income
    /// </summary>
    public decimal IncomeTarget { get; set; } = TaxConstants.BaseIncomeTarget;
}