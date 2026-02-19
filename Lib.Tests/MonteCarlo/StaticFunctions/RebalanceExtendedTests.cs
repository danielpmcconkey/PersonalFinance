using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.MonteCarlo.WithdrawalStrategy;
using NodaTime;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class RebalanceExtendedTests
{
    private readonly LocalDateTime _testDate = new(2026, 1, 1, 0, 0);

    // ── §7.3 — Partial conversion creates two positions ────────────────────────

    [Fact(DisplayName = "§7.3 — Partial long-to-mid conversion creates two positions with correct values")]
    public void MoveLongToMidWithoutTaxConsequences_PartialConversion_CreatesTwoPositions()
    {
        // Initial: Traditional401K holds ONE LONG_TERM position (price=100, qty=100, value=$10,000).
        // amountToMove = $4,000 (partial).
        // With equity price = mid price = 100:
        //   Converted portion:   MID_TERM,  price=100, qty=40,  value=$4,000
        //   Remaining "keep":    LONG_TERM,  price=100, qty=60,  value=$6,000
        const decimal price = 100m;
        const decimal qty   = 100m;
        const decimal amountToMove = 4_000m;

        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.Traditional401K.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(price, qty, McInvestmentPositionType.LONG_TERM));

        var prices = TestDataManager.CreateTestCurrentPrices(0.02m, price, price, 50m);
        var ledger = new TaxLedger();

        var result = Rebalance.MoveLongToMidWithoutTaxConsequences(
            amountToMove, accounts, ledger, _testDate, prices);

        Assert.Equal(amountToMove, result.amountMoved);

        var positions = result.accounts.Traditional401K.Positions;
        Assert.Equal(2, positions.Count);

        var midPos = positions.Single(p => p.InvestmentPositionType == McInvestmentPositionType.MID_TERM);
        var longPos = positions.Single(p => p.InvestmentPositionType == McInvestmentPositionType.LONG_TERM);

        Assert.Equal(4_000m, Math.Round(midPos.CurrentValue, 4));
        Assert.Equal(6_000m, Math.Round(longPos.CurrentValue, 4));
    }

    // ── §7.3 — Mid-term target already met: no movement ────────────────────────

    [Fact(DisplayName = "§7.3 — Mid-term bucket already at target: no long-to-mid movement occurs")]
    public void RebalancePortfolio_MidTermTargetAlreadyMet_NoLongToMidMovement()
    {
        // Set up a model where mid target ≈ 1 month × $500 = $500.
        // The mid bucket already holds $1,000,000 >> $500, so amountNeededToMove ≤ 0.
        // The LONG_TERM position in Traditional401K should remain completely untouched.
        var person = TestDataManager.CreateTestPerson();
        person.RequiredMonthlySpend = 500;
        person.RequiredMonthlySpendHealthCare = 0;

        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.BasicBucketsIncomeThreshold);
        model.RetirementDate = new LocalDateTime(2025, 1, 1, 0, 0);  // in the past
        model.RebalanceFrequency = RebalanceFrequency.MONTHLY;
        model.NumMonthsCashOnHand  = 1;
        model.NumMonthsMidBucketOnHand = 1;
        model.DesiredMonthlySpendPostRetirement = 500;

        var accounts = TestDataManager.CreateEmptyBookOfAccounts();

        // Enough cash to exceed cash target
        accounts = AccountCashManagement.DepositCash(accounts, 100_000m, _testDate).accounts;

        // Enormous MID_TERM balance: far exceeds the mid target ($500)
        accounts.TraditionalIra.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(
                1_000m, 1_000m, McInvestmentPositionType.MID_TERM));

        // LONG_TERM balance in Traditional401K that would be moved IF rebalancing needed it
        accounts.Traditional401K.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(
                100m, 500m, McInvestmentPositionType.LONG_TERM));

        var longBalanceBefore = AccountCalculation.CalculateLongBucketTotalBalance(accounts);
        var midBalanceBefore  = AccountCalculation.CalculateMidBucketTotalBalance(accounts);

        var result = model.WithdrawalStrategy.RebalancePortfolio(
            _testDate, accounts, new RecessionStats(), new CurrentPrices(),
            model, new TaxLedger(), person);

        var longBalanceAfter = AccountCalculation.CalculateLongBucketTotalBalance(result.accounts);
        var midBalanceAfter  = AccountCalculation.CalculateMidBucketTotalBalance(result.accounts);

        // No long-to-mid movement should have occurred
        Assert.Equal(Math.Round(longBalanceBefore, 2), Math.Round(longBalanceAfter, 2));
        Assert.Equal(Math.Round(midBalanceBefore, 2),  Math.Round(midBalanceAfter, 2));
    }
}
