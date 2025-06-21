using NodaTime;

namespace Lib.MonteCarlo;

public class ReconciliationLineItem
{
    public int Ordinal { get; set; }
    public LocalDateTime Date { get; set; }
    public decimal Age { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; }
    public ReconciliationLineItemType Type { get; set; }
    public decimal CurrentMonthGrowthRate { get; set; }
    public decimal TotalNetWorth { get; set; }
    public decimal TotalLongTermInvestment { get; set; }
    public decimal TotalMidTermInvestment { get; set; }
    public decimal TotalShortTermInvestment { get; set; }
    public decimal TotalCash { get; set; }
    public decimal TotalDebt { get; set; }
    public decimal TotalSpendLifetime { get; set; }
    public decimal TotalInvestmentAccrualLifetime { get; set; }
    public decimal TotalDebtAccrualLifetime { get; set; }
    public decimal TotalSocialSecurityWageLifetime { get; set; }
    public decimal TotalDebtPaidLifetime { get; set; }
    public bool IsRetired { get; set; }
    public bool IsBankrupt { get; set; }
    public bool AreWeInARecession { get; set; }
    public bool AreWeInExtremeAusterityMeasures { get; set; }

    
    public ReconciliationLineItem(
        int ordinal,
        LocalDateTime date,
        decimal age,
        decimal amount,
        string description,
        ReconciliationLineItemType type,
        decimal currentMonthGrowthRate,
        decimal totalNetWorth,
        decimal totalLongTermInvestment,
        decimal totalMidTermInvestment,
        decimal totalShortTermInvestment,
        decimal totalCash,
        decimal totalDebt,
        decimal totalSpendLifetime,
        decimal totalInvestmentAccrualLifetime,
        decimal totalDebtAccrualLifetime,
        decimal totalSocialSecurityWageLifetime,
        decimal totalDebtPaidLifetime,
        bool isRetired,
        bool isBankrupt,
        bool areWeInARecession,
        bool areWeInExtremeAusterityMeasures)
    {
        Ordinal = ordinal;
        Date = date;
        Age = age;
        Amount = amount;
        Description = description;
        Type = type;
        CurrentMonthGrowthRate = currentMonthGrowthRate;
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