using Lib.DataTypes.MonteCarlo;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Spend
{
    public static void RecordDebtPayment(LifetimeSpend lifetimeSpend, long amount)
    {
        lifetimeSpend.TotalDebtPaidLifetime += amount;
    }
}