using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.MonteCarlo.WithdrawalStrategy;
using NodaTime;

namespace Lib.Tests.MonteCarlo.WithdrawalStrategy;

public class WithdrawalStrategyExtendedTests
{
    private readonly LocalDateTime _testDate = new(2025, 1, 1, 0, 0);

    // ── §7.4 — Account type override bypasses income room logic ──────────────

    [Fact(DisplayName = "§7.4 — Account type override forces sale from specified account regardless of income room")]
    public void IncomeThreasholdSellInvestmentsToDollarAmount_AccountTypeOverride_BypassesIncomeRoomLogic()
    {
        // With an empty ledger, income room = $123,500 (full bracket).
        // Normally, income room > 0 triggers selling from tax-deferred (TRADITIONAL_IRA) first.
        // With accountTypeOverride = ROTH_IRA, the function must sell from ROTH_IRA instead —
        // even though income room is available and TRADITIONAL_IRA is untouched.
        const decimal positionValue = 50_000m;

        var accounts = TestDataManager.CreateEmptyBookOfAccounts();

        // Traditional IRA — would be sold first under normal income-room logic
        accounts.TraditionalIra.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(
                positionValue, 1m, McInvestmentPositionType.LONG_TERM));

        // Roth IRA — override forces sale from here
        accounts.RothIra.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(
                positionValue, 1m, McInvestmentPositionType.LONG_TERM));

        var ledger = new TaxLedger();          // empty → income room = $123,500
        var model  = TestDataManager.CreateTestModel();
        const decimal amountToSell = 10_000m;

        var result = SharedWithdrawalFunctions.IncomeThreasholdSellInvestmentsToDollarAmount(
            accounts, ledger, _testDate, amountToSell, model,
            minDateExclusive:   null,
            maxDateInclusive:   null,
            positionTypeOverride: null,
            accountTypeOverride: McInvestmentAccountType.ROTH_IRA);

        // Sale proceeds must come from ROTH_IRA, not from TRADITIONAL_IRA
        Assert.Equal(amountToSell, Math.Round(result.amountSold, 2));

        var rothBalance = result.accounts.RothIra.Positions.Sum(p => p.CurrentValue);
        var tradBalance = result.accounts.TraditionalIra.Positions.Sum(p => p.CurrentValue);

        Assert.True(rothBalance < positionValue,
            $"Roth IRA should have been partially sold; got {rothBalance:C}");
        Assert.Equal(positionValue, tradBalance);   // traditional untouched
    }
}
