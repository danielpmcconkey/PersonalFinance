using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class TaxCalculationExtendedTests2
{
    // ── §15 — Baseline income room with empty ledger ──────────────────────────

    [Fact(DisplayName = "§15 — CalculateIncomeRoom with empty ledger returns $123,500 (12%-bracket ceiling + standard deduction)")]
    public void CalculateIncomeRoom_EmptyLedger_ReturnsBaselineAmount()
    {
        // Baseline headroom = Federal1040TaxTableBrackets[1].max + FederalStandardDeduction
        //                   = $94,300 + $29,200 = $123,500
        //
        // With a default TaxLedger:
        //   • SocialSecurityElectionStartDate defaults to 2999-01-01 → currentYear < 2999
        //     → projectedTotalSsIncomeThisYear = 0
        //   • All ledger income buckets (W2, IRA distributions, interest, dividends, STCG) = 0
        //
        // So every subtraction in the headroom formula is zero → full $123,500 is returned.
        var ledger   = new TaxLedger();
        var testDate = new LocalDateTime(2025, 1, 1, 0, 0);

        var incomeRoom = TaxCalculation.CalculateIncomeRoom(ledger, testDate);

        var expectedBaseline = TaxConstants.Federal1040TaxTableBrackets[1].max
                             + TaxConstants.FederalStandardDeduction;
        Assert.Equal(expectedBaseline, incomeRoom);
    }
}
