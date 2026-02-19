using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using NodaTime;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class AccountDebtPaymentExtendedTests
{
    private readonly LocalDateTime _testDate = new(2025, 1, 1, 0, 0);

    // ── §5 — Insufficient cash: investments liquidated before failure ─────────

    [Fact(DisplayName = "§5 — Insufficient cash: MID_TERM investments liquidated to cover debt payment")]
    public void PayDownLoans_InsufficientCash_LiquidatesInvestmentsBeforeFailure()
    {
        // Debt payment needed = $500; starting cash = $0.
        // A MID_TERM position (value $2,000, held > 1 year) is sold to cover the shortfall.
        // Expected: isSuccessful = true; investment value falls below $2,000.
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();

        // $2,000 MID_TERM investment, entry 2023-01-01 (> 1 year before test date)
        accounts.RothIra.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(
                100m, 20m, McInvestmentPositionType.MID_TERM));

        // Debt: balance=$500, payment=$500
        var debtPos = TestDataManager.CreateTestDebtPosition(true, 0.05m, 500m, 500m);
        accounts.DebtAccounts = [TestDataManager.CreateTestDebtAccount([debtPos])];

        var model  = TestDataManager.CreateTestModel();
        var ledger = new TaxLedger();
        var spend  = new LifetimeSpend();

        var result = AccountDebtPayment.PayDownLoans(accounts, _testDate, ledger, spend, model);

        Assert.True(result.isSuccessful, "PayDownLoans should succeed via investment liquidation");

        // Investment value must be strictly less than $2,000 (liquidation occurred)
        var postInvestmentValue = result.newBookOfAccounts.InvestmentAccounts
            .SelectMany(a => a.Positions)
            .Where(p => p.IsOpen)
            .Sum(p => p.CurrentValue);
        Assert.True(postInvestmentValue < 2_000m,
            $"Expected investments < $2,000 after liquidation; got {postInvestmentValue:C}");

        // Debt balance must be zero
        var postDebtBalance = AccountCalculation.CalculateDebtTotal(result.newBookOfAccounts);
        Assert.Equal(0m, postDebtBalance);
    }

    // ── §5 — Internal accounting check: debited = credited within $1 ──────────

    [Fact(DisplayName = "§5 — Internal accounting: cash withdrawn equals debt balance reduced (within $1)")]
    public void PayDownLoans_WithSufficientCash_DebitedEqualsCreditedWithinOneDollar()
    {
        // Cash = $500; debt = $200 balance with $200 monthly payment.
        // Expected: cash withdrawn = $200 = debt balance reduced.
        const decimal debtBalance  = 200m;
        const decimal monthlyPayment = 200m;

        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts = AccountCashManagement.DepositCash(accounts, 500m, _testDate).accounts;

        var debtPos = TestDataManager.CreateTestDebtPosition(true, 0.05m, monthlyPayment, debtBalance);
        accounts.DebtAccounts = [TestDataManager.CreateTestDebtAccount([debtPos])];

        var cashBefore = AccountCalculation.CalculateCashBalance(accounts);
        var debtBefore = AccountCalculation.CalculateDebtTotal(accounts);

        var model  = TestDataManager.CreateTestModel();
        var result = AccountDebtPayment.PayDownLoans(accounts, _testDate, new TaxLedger(), new LifetimeSpend(), model);

        Assert.True(result.isSuccessful);

        var cashAfter = AccountCalculation.CalculateCashBalance(result.newBookOfAccounts);
        var debtAfter = AccountCalculation.CalculateDebtTotal(result.newBookOfAccounts);

        var debited  = cashBefore - cashAfter;   // cash withdrawn
        var credited = debtBefore - debtAfter;   // debt balance reduced

        Assert.True(Math.Abs(debited - credited) <= 1m,
            $"Internal accounting mismatch: debited={debited:C}, credited={credited:C}, diff={Math.Abs(debited - credited):C}");
    }

    // ── §5 — Net worth conserved by debt paydown ─────────────────────────────

    [Fact(DisplayName = "§5 — Net worth is conserved when paying down debt with cash")]
    public void PayDownLoans_WithSufficientCash_NetWorthIsConserved()
    {
        // Cash = $1,000; debt = $300 balance with $300 monthly payment.
        // Net worth before = $1,000 − $300 = $700.
        // After paying: cash = $700, debt = $0 → net worth = $700 − $0 = $700.
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts = AccountCashManagement.DepositCash(accounts, 1_000m, _testDate).accounts;

        var debtPos = TestDataManager.CreateTestDebtPosition(true, 0.05m, 300m, 300m);
        accounts.DebtAccounts = [TestDataManager.CreateTestDebtAccount([debtPos])];

        var netWorthBefore = AccountCalculation.CalculateNetWorth(accounts);

        var model  = TestDataManager.CreateTestModel();
        var result = AccountDebtPayment.PayDownLoans(accounts, _testDate, new TaxLedger(), new LifetimeSpend(), model);

        Assert.True(result.isSuccessful);

        var netWorthAfter = AccountCalculation.CalculateNetWorth(result.newBookOfAccounts);

        Assert.Equal(netWorthBefore, netWorthAfter);
    }
}
