namespace Lib.DataTypes.MonteCarlo;

public class LifetimeSpend
{
    public decimal TotalSpendLifetime { get; set; } = 0m;
    public decimal TotalInvestmentAccrualLifetime { get; set; } = 0m;
    public decimal TotalDebtAccrualLifetime { get; set; } = 0m;
    public decimal TotalSocialSecurityWageLifetime { get; set; } = 0m;
    public decimal TotalDebtPaidLifetime { get; set; } = 0m;
}