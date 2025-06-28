using NodaTime;

namespace Lib.DataTypes.MonteCarlo;

public class TaxLedger
{
    public List<(LocalDateTime earnedDate, long amount)> SocialSecurityIncome { get; set; } = [];
    public List<(LocalDateTime earnedDate, long amount)> OrdinaryIncome { get; set; } = [];
    public List<(LocalDateTime earnedDate, long amount)> CapitalGains { get; set; } = [];
    public long TotalTaxPaid { get; set; } = 0; // lifetime total

    /// <summary>
    /// list of total distributions by year that qualify against RMD requirements
    /// </summary>
    public Dictionary<int, long> RmdDistributions { get; set; } = new();
    
    /// <summary>
    /// this is the amount of income above social security that you want to maximize the 12% tax bracket. every year, we
    /// will reevaluate this based on the prior year's social security income
    /// </summary>
    public long IncomeTarget { get; set; } = 800000000L;
}