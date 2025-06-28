namespace Lib.DataTypes.MonteCarlo;

public class LifetimeSpend
{
    public long TotalSpendLifetime { get; set; } = 0;
    public long TotalInvestmentAccrualLifetime { get; set; } = 0;
    public long TotalDebtAccrualLifetime { get; set; } = 0;
    public long TotalSocialSecurityWageLifetime { get; set; } = 0;
    public long TotalDebtPaidLifetime { get; set; } = 0;
    public bool IsBankrupt { get; set; } = false;
}