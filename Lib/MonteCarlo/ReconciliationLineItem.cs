using NodaTime;

namespace Lib.MonteCarlo;

public record ReconciliationLineItem(
    LocalDateTime Date, 
    decimal Age,
    decimal Amount, 
    string Description, 
    ReconciliationLineItemType Type,
    decimal CurrentMonthGrowthRate,
    decimal TotalNetWorth,
    decimal TotalLongTermInvestment,
    decimal TotalMidTermInvestment,
    decimal TotalShortTermInvestment,
    decimal TotalCash,
    decimal TotalDebt,
    decimal TotalSpendLifetime,
    decimal TotalInvestmentAccrualLifetime,
    decimal TotalDebtAccrualLifetime,
    decimal TotalSocialSecurityWageLifetime,
    decimal TotalDebtPaidLifetime,
    bool IsRetired,
    bool IsBankrupt,
    bool AreWeInARecession,
    bool AreWeInExtremeAusterityMeasures
    );