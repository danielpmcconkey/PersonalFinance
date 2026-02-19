using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class AccountCalculationExtendedTests
{
    // ── §9 — CalculateDebtPaydownAmounts: min(MonthlyPayment, balance) ────────

    [Fact(DisplayName = "§9 — CalculateDebtPaydownAmounts clips payment to CurrentBalance when MonthlyPayment > CurrentBalance")]
    public void CalculateDebtPaydownAmounts_MonthlyPaymentExceedsBalance_ReturnsCurrentBalance()
    {
        // MonthlyPayment = $500, CurrentBalance = $200 → min($500, $200) = $200.
        // Exercises the Math.Min() clip that prevents overpaying a nearly-paid-off loan.
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var debtPos  = TestDataManager.CreateTestDebtPosition(true, 0.05m, 500m, 200m);
        accounts.DebtAccounts = [TestDataManager.CreateTestDebtAccount([debtPos])];

        var paydownAmounts = AccountCalculation.CalculateDebtPaydownAmounts(accounts.DebtAccounts);

        Assert.Single(paydownAmounts);
        Assert.Equal(200m, paydownAmounts.Values.Single());  // capped at balance, not at payment amount
    }
}
