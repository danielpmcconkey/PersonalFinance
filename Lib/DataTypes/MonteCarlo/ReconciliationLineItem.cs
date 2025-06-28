using NodaTime;

namespace Lib.DataTypes.MonteCarlo;

public class ReconciliationLineItem
{
    public int? Ordinal { get; set; }
    public LocalDateTime? Date { get; set; }
    public decimal? Age { get; set; }
    public decimal? Amount { get; set; }
    public string? Description { get; set; }
    public decimal? CurrentMonthGrowthRate { get; set; }
    public decimal? CurrentLongRangeInvestmentCost { get; set; }
    public decimal? TotalNetWorth { get; set; }
    public decimal? TotalLongTermInvestment { get; set; }
    public decimal? TotalMidTermInvestment { get; set; }
    public decimal? TotalShortTermInvestment { get; set; }
    public decimal? TotalCash { get; set; }
    public decimal? TotalDebt { get; set; }
    public decimal? TotalSpendLifetime { get; set; }
    public decimal? TotalInvestmentAccrualLifetime { get; set; }
    public decimal? TotalDebtAccrualLifetime { get; set; }
    public decimal? TotalSocialSecurityWageLifetime { get; set; }
    public decimal? TotalDebtPaidLifetime { get; set; }
    public bool? IsRetired { get; set; }
    public bool? IsBankrupt { get; set; }
    public bool? AreWeInARecession { get; set; }
    public bool? AreWeInExtremeAusterityMeasures { get; set; }

    public ReconciliationLineItem(
        int? ordinal = null,
        LocalDateTime? date = null,
        decimal? age = null,
        decimal? amount = null,
        string? description = null,
        decimal? currentMonthGrowthRate = null,
        decimal? currentLongRangeInvestmentCost = null,
        decimal? totalNetWorth = null,
        decimal? totalLongTermInvestment = null,
        decimal? totalMidTermInvestment = null,
        decimal? totalShortTermInvestment = null,
        decimal? totalCash = null,
        decimal? totalDebt = null,
        decimal? totalSpendLifetime = null,
        decimal? totalInvestmentAccrualLifetime = null,
        decimal? totalDebtAccrualLifetime = null,
        decimal? totalSocialSecurityWageLifetime = null,
        decimal? totalDebtPaidLifetime = null,
        bool? isRetired = null,
        bool? isBankrupt = null,
        bool? areWeInARecession = null,
        bool? areWeInExtremeAusterityMeasures = null)
    {
        Ordinal = ordinal;
        Date = date;
        Age = age;
        Amount = amount;
        Description = description;
        CurrentMonthGrowthRate = currentMonthGrowthRate;
        CurrentLongRangeInvestmentCost = currentLongRangeInvestmentCost;
        TotalNetWorth = totalNetWorth;
        TotalLongTermInvestment = totalLongTermInvestment;
        TotalMidTermInvestment = totalMidTermInvestment;
        TotalShortTermInvestment = totalShortTermInvestment;
        TotalCash = totalCash;
        TotalDebt = totalDebt;
        TotalSpendLifetime = totalSpendLifetime;
        TotalInvestmentAccrualLifetime = totalInvestmentAccrualLifetime;
        TotalDebtAccrualLifetime = totalDebtAccrualLifetime;
        TotalSocialSecurityWageLifetime = totalSocialSecurityWageLifetime;
        TotalDebtPaidLifetime = totalDebtPaidLifetime;
        IsRetired = isRetired;
        IsBankrupt = isBankrupt;
        AreWeInARecession = areWeInARecession;
        AreWeInExtremeAusterityMeasures = areWeInExtremeAusterityMeasures;
    }
}