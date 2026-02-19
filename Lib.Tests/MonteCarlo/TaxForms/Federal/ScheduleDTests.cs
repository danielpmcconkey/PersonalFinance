using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.TaxForms.Federal;
using NodaTime;

namespace Lib.Tests.MonteCarlo.TaxForms.Federal;

public class ScheduleDTests
{
    private const int TestYear = 2025;
    private readonly LocalDateTime _testDate = new(TestYear, 6, 1, 0, 0);

    [Fact(DisplayName = "§1.2 — Short-term and long-term gains aggregated correctly into line 15 and line 16")]
    public void ScheduleD_Complete_AggregatesShortAndLongTermGains()
    {
        var ledger = new TaxLedger();
        ledger.ShortTermCapitalGains.Add((_testDate, 5_000m));
        ledger.LongTermCapitalGains.Add((_testDate, 3_000m));

        var scheduleD = new ScheduleD(ledger, TestYear);
        scheduleD.Complete();

        Assert.Equal(3_000m, scheduleD.Line15LongTermCapitalGains);
        Assert.Equal(8_000m, scheduleD.Line16CombinedCapitalGains);
        Assert.Equal(8_000m, scheduleD.Form1040Line7);
    }

    [Fact(DisplayName = "§1.2 — Net gain (line 16 > 0) with long-term portion triggers QD&CG worksheet")]
    public void ScheduleD_Complete_NetGainWithLongTermPortion_TriggersWorksheet()
    {
        var ledger = new TaxLedger();
        ledger.LongTermCapitalGains.Add((_testDate, 10_000m));
        ledger.ShortTermCapitalGains.Add((_testDate, 2_000m));

        var scheduleD = new ScheduleD(ledger, TestYear);
        scheduleD.Complete();

        Assert.True(scheduleD.IsRequiredToCompleteQualifiedDividendsAndCapitalGainsWorksheet);
    }

    [Fact(DisplayName = "§1.2 — Net loss capped at -$3,000 on Form 1040 line 7")]
    public void ScheduleD_Complete_NetLossExceedingCap_CapsAtNegative3000()
    {
        var ledger = new TaxLedger();
        ledger.ShortTermCapitalGains.Add((_testDate, -5_000m));
        ledger.LongTermCapitalGains.Add((_testDate, -4_000m));

        var scheduleD = new ScheduleD(ledger, TestYear);
        scheduleD.Complete();

        // Combined loss = -9,000; Form 1040 line 7 is capped at -3,000
        Assert.Equal(-9_000m, scheduleD.Line16CombinedCapitalGains);
        Assert.Equal(-3_000m, scheduleD.Form1040Line7);
    }

    [Fact(DisplayName = "§1.2 — Zero capital gains: Form 1040 line 7 is zero and no worksheet required")]
    public void ScheduleD_Complete_ZeroGains_WorksheetNotRequired()
    {
        // Empty ledger: no gains, no dividends
        var ledger = new TaxLedger();

        var scheduleD = new ScheduleD(ledger, TestYear);
        scheduleD.Complete();

        Assert.Equal(0m, scheduleD.Line16CombinedCapitalGains);
        Assert.Equal(0m, scheduleD.Form1040Line7);
        Assert.False(scheduleD.IsRequiredToCompleteQualifiedDividendsAndCapitalGainsWorksheet);
    }

    [Fact(DisplayName = "§1.2 — Qualified dividends alone (no capital gains) trigger QD&CG worksheet")]
    public void ScheduleD_Complete_QualifiedDividendsAlone_TriggersWorksheet()
    {
        // No capital gains, but qualified dividends exist
        var ledger = new TaxLedger();
        ledger.QualifiedDividendsReceived.Add((_testDate, 5_000m));
        ledger.DividendsReceived.Add((_testDate, 5_000m));

        var scheduleD = new ScheduleD(ledger, TestYear);
        scheduleD.Complete();

        // Line 22 qualified-dividend check triggers the worksheet even with no capital gains
        Assert.Equal(0m, scheduleD.Line16CombinedCapitalGains);
        Assert.True(scheduleD.IsRequiredToCompleteQualifiedDividendsAndCapitalGainsWorksheet);
    }

    [Fact(DisplayName = "§1.2 — Mixed loss + qualified dividends: loss capped at -$3,000, worksheet required")]
    public void ScheduleD_Complete_MixedLossAndQualifiedDividends_CapsLossAndRequiresWorksheet()
    {
        var ledger = new TaxLedger();
        ledger.ShortTermCapitalGains.Add((_testDate, -6_000m));
        ledger.LongTermCapitalGains.Add((_testDate, -2_000m));
        ledger.QualifiedDividendsReceived.Add((_testDate, 3_000m));
        ledger.DividendsReceived.Add((_testDate, 3_000m));

        var scheduleD = new ScheduleD(ledger, TestYear);
        scheduleD.Complete();

        // -8,000 combined loss capped at -3,000; qualified dividends still trigger worksheet
        Assert.Equal(-3_000m, scheduleD.Form1040Line7);
        Assert.True(scheduleD.IsRequiredToCompleteQualifiedDividendsAndCapitalGainsWorksheet);
    }
}
