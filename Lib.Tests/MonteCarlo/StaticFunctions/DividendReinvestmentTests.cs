using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class DividendReinvestmentTests
{
    private const int TestYear = 2025;

    // Base position: price=100, qty=10 → CurrentValue=1000
    // dividendAmount = 1000 × (0.0334 / 4) = 8.35
    private const decimal TestPrice = 100m;
    private const decimal TestQty   = 10m;
    private static readonly decimal TestDividendAmount =
        TestPrice * TestQty * (TaxConstants.MidTermAnnualDividendYield / 4m); // 8.35

    private static McInvestmentAccount GetResultAccount(
        BookOfAccounts accounts, McInvestmentAccountType type)
        => accounts.InvestmentAccounts.First(a => a.AccountType == type);

    // ── §4.3 — Month filter ──────────────────────────────────────────────────

    [Theory(DisplayName = "§4.3 — Dividends only accrue in months 3, 6, 9, 12; all other months produce no change")]
    [InlineData(1)] [InlineData(2)] [InlineData(4)]  [InlineData(5)]
    [InlineData(7)] [InlineData(8)] [InlineData(10)] [InlineData(11)]
    public void AccrueMidTermDividends_NonQuarterMonth_NoPositionChange(int month)
    {
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.RothIra.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(TestPrice, TestQty, McInvestmentPositionType.MID_TERM));
        var date = new LocalDateTime(TestYear, month, 1, 0, 0);

        var result = AccountInterestAccrual.AccrueMidTermDividends(date, accounts, new TaxLedger());

        var positions = GetResultAccount(result.newAccounts, McInvestmentAccountType.ROTH_IRA).Positions;
        Assert.Single(positions);
        Assert.Equal(TestQty, positions[0].Quantity);
    }

    [Theory(DisplayName = "§4.3 — Dividends DO accrue in quarter-end months 3, 6, 9, 12")]
    [InlineData(3)] [InlineData(6)] [InlineData(9)] [InlineData(12)]
    public void AccrueMidTermDividends_QuarterMonth_DividendAccrues(int month)
    {
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.RothIra.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(TestPrice, TestQty, McInvestmentPositionType.MID_TERM));
        var date = new LocalDateTime(TestYear, month, 1, 0, 0);

        var result = AccountInterestAccrual.AccrueMidTermDividends(date, accounts, new TaxLedger());

        var totalQty = GetResultAccount(result.newAccounts, McInvestmentAccountType.ROTH_IRA)
            .Positions.Sum(p => p.Quantity);
        Assert.True(totalQty > TestQty,
            $"Expected reinvested quantity > {TestQty} in month {month}, but got {totalQty}");
    }

    // ── §4.3 — Dividend amount formula ───────────────────────────────────────

    [Fact(DisplayName = "§4.3 — Dividend amount = CurrentValue × (MidTermAnnualDividendYield / 4)")]
    public void AccrueMidTermDividends_DividendAmount_MatchesFormula()
    {
        // price=100, qty=10 → CurrentValue=1000
        // dividendAmount = 1000 × (0.0334/4) = 8.35
        // code adds shares: newQty = 10 + 8.35/100 = 10.0835
        var expectedTotalQty = TestQty + TestDividendAmount / TestPrice; // 10.0835
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.RothIra.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(TestPrice, TestQty, McInvestmentPositionType.MID_TERM));

        var result = AccountInterestAccrual.AccrueMidTermDividends(
            new LocalDateTime(TestYear, 3, 1, 0, 0), accounts, new TaxLedger());

        var totalQty = GetResultAccount(result.newAccounts, McInvestmentAccountType.ROTH_IRA)
            .Positions.Sum(p => p.Quantity);
        Assert.Equal(expectedTotalQty, totalQty);
    }

    // ── §4.3 — Reinvestment creates a new position [KNOWN CODE BUG] ──────────

    [Fact(DisplayName = "§4.3 — Reinvestment creates a NEW position; original unchanged [KNOWN CODE BUG — EXPECTED FAIL]")]
    public void AccrueMidTermDividends_Reinvestment_CreatesNewPosition()
    {
        // BUG: AccrueMidTermDividendsOnPosition updates Quantity on the existing position
        // instead of creating a new DRIP position.
        // Expected: 2 positions (original + DRIP); original Quantity still 10.
        // Actual:   1 position with Quantity = 10.0835.
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.RothIra.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(TestPrice, TestQty, McInvestmentPositionType.MID_TERM));

        var result = AccountInterestAccrual.AccrueMidTermDividends(
            new LocalDateTime(TestYear, 3, 1, 0, 0), accounts, new TaxLedger());

        var positions = GetResultAccount(result.newAccounts, McInvestmentAccountType.ROTH_IRA).Positions;
        Assert.Equal(2, positions.Count);             // EXPECTED FAIL — bug produces 1 position
        Assert.Equal(TestQty, positions[0].Quantity); // original unchanged
    }

    [Fact(DisplayName = "§4.3 — New DRIP position value equals the dividend amount [KNOWN CODE BUG — EXPECTED FAIL]")]
    public void AccrueMidTermDividends_NewPosition_ValueEqualsDividendAmount()
    {
        // BUG: same root cause — no second position is created.
        // Expected: positions[1].CurrentValue == 8.35.
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.RothIra.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(TestPrice, TestQty, McInvestmentPositionType.MID_TERM));

        var result = AccountInterestAccrual.AccrueMidTermDividends(
            new LocalDateTime(TestYear, 3, 1, 0, 0), accounts, new TaxLedger());

        var positions = GetResultAccount(result.newAccounts, McInvestmentAccountType.ROTH_IRA).Positions;
        Assert.Equal(2, positions.Count);                           // EXPECTED FAIL — same bug
        Assert.Equal(TestDividendAmount, positions[1].CurrentValue);
    }

    // ── §4.3 — Taxable brokerage ledger recording ───────────────────────────

    [Fact(DisplayName = "§4.3 — Taxable brokerage: full dividend recorded in DividendsReceived")]
    public void AccrueMidTermDividends_TaxableBrokerage_RecordsDividendsReceived()
    {
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.Brokerage.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(TestPrice, TestQty, McInvestmentPositionType.MID_TERM));

        var result = AccountInterestAccrual.AccrueMidTermDividends(
            new LocalDateTime(TestYear, 3, 1, 0, 0), accounts, new TaxLedger());

        Assert.Single(result.newLedger.DividendsReceived);
        Assert.Equal(TestDividendAmount, result.newLedger.DividendsReceived[0].amount);
    }

    [Fact(DisplayName = "§4.3 — Taxable brokerage: QualifiedDividendsReceived = 95% of total dividend")]
    public void AccrueMidTermDividends_TaxableBrokerage_RecordsQualifiedDividends()
    {
        var expectedQualified = TestDividendAmount * TaxConstants.DividendPercentQualified; // 7.9325
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.Brokerage.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(TestPrice, TestQty, McInvestmentPositionType.MID_TERM));

        var result = AccountInterestAccrual.AccrueMidTermDividends(
            new LocalDateTime(TestYear, 3, 1, 0, 0), accounts, new TaxLedger());

        Assert.Single(result.newLedger.QualifiedDividendsReceived);
        Assert.Equal(expectedQualified, result.newLedger.QualifiedDividendsReceived[0].amount);
    }

    [Fact(DisplayName = "§4.3 — Taxable brokerage: ordinary dividend = 5% of total (non-qualified portion)")]
    public void AccrueMidTermDividends_TaxableBrokerage_OrdinaryDividendIsFivePercent()
    {
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.Brokerage.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(TestPrice, TestQty, McInvestmentPositionType.MID_TERM));

        var result = AccountInterestAccrual.AccrueMidTermDividends(
            new LocalDateTime(TestYear, 3, 1, 0, 0), accounts, new TaxLedger());

        var total     = result.newLedger.DividendsReceived.Sum(x => x.amount);
        var qualified = result.newLedger.QualifiedDividendsReceived.Sum(x => x.amount);
        var ordinary  = total - qualified;
        Assert.Equal(TestDividendAmount * (1m - TaxConstants.DividendPercentQualified), ordinary);
    }

    // ── §4.3 — Tax-advantaged, non-MID_TERM, skipped accounts ───────────────

    [Fact(DisplayName = "§4.3 — Tax-advantaged accounts: no TaxLedger entries recorded")]
    public void AccrueMidTermDividends_TaxAdvantaged_NoLedgerEntries()
    {
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.RothIra.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(TestPrice, TestQty, McInvestmentPositionType.MID_TERM));

        var result = AccountInterestAccrual.AccrueMidTermDividends(
            new LocalDateTime(TestYear, 3, 1, 0, 0), accounts, new TaxLedger());

        Assert.Empty(result.newLedger.DividendsReceived);
        Assert.Empty(result.newLedger.QualifiedDividendsReceived);
    }

    [Fact(DisplayName = "§4.3 — Non-MID_TERM positions (LONG_TERM) are not affected by dividend accrual")]
    public void AccrueMidTermDividends_LongTermPosition_QuantityUnchanged()
    {
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.RothIra.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(TestPrice, TestQty, McInvestmentPositionType.LONG_TERM));

        var result = AccountInterestAccrual.AccrueMidTermDividends(
            new LocalDateTime(TestYear, 3, 1, 0, 0), accounts, new TaxLedger());

        var positions = GetResultAccount(result.newAccounts, McInvestmentAccountType.ROTH_IRA).Positions;
        Assert.Single(positions);
        Assert.Equal(TestQty, positions[0].Quantity);
    }

    [Fact(DisplayName = "§4.3 — Cash account: MID_TERM positions pass through without receiving dividend")]
    public void AccrueMidTermDividends_CashAccount_SkippedEntirely()
    {
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.Cash.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(TestPrice, TestQty, McInvestmentPositionType.MID_TERM));

        var result = AccountInterestAccrual.AccrueMidTermDividends(
            new LocalDateTime(TestYear, 3, 1, 0, 0), accounts, new TaxLedger());

        var cashPositions = GetResultAccount(result.newAccounts, McInvestmentAccountType.CASH).Positions;
        Assert.Single(cashPositions);
        Assert.Equal(TestQty, cashPositions[0].Quantity);
    }

    // ── §4.3 — Multiple positions ────────────────────────────────────────────

    [Fact(DisplayName = "§4.3 — Multiple MID_TERM positions in same account all receive dividends")]
    public void AccrueMidTermDividends_MultiplePositions_AllReceiveDividends()
    {
        // position 1: price=100, qty=10, CurrentValue=1000, dividend=8.35, addlQty=0.0835 → 10.0835
        // position 2: price=200, qty=5,  CurrentValue=1000, dividend=8.35, addlQty=0.04175 → 5.04175
        const decimal price2 = 200m;
        const decimal qty2   = 5m;
        var expectedQty1 = TestQty + TestDividendAmount / TestPrice; // 10.0835
        var expectedQty2 = qty2    + TestDividendAmount / price2;    //  5.04175

        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.RothIra.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(TestPrice, TestQty,  McInvestmentPositionType.MID_TERM));
        accounts.RothIra.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(price2,   qty2,    McInvestmentPositionType.MID_TERM));

        var result = AccountInterestAccrual.AccrueMidTermDividends(
            new LocalDateTime(TestYear, 3, 1, 0, 0), accounts, new TaxLedger());

        var positions = GetResultAccount(result.newAccounts, McInvestmentAccountType.ROTH_IRA).Positions;
        Assert.Equal(2, positions.Count);
        Assert.Equal(expectedQty1, positions[0].Quantity);
        Assert.Equal(expectedQty2, positions[1].Quantity);
    }
}
