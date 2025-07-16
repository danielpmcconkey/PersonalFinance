using Lib.DataTypes.MonteCarlo;

namespace Lib.MonteCarlo.TaxForms.Federal;

/// <summary>
/// Schedule D Capital Gains and Losses
/// </summary>
public static class ScheduleD
{
    public static (decimal scheduleDLine15NetLongTermCapitalGain, decimal scheduleDLine16CombinedCapitalGains,
        bool scheduleDLine20Are18And19BothZero, bool scheduleDLine22DoYouHaveQualifiedDividendsOnCapitalGains)
        PopulateScheduleDAndCalculateFinalValue(TaxLedger ledger, int taxYear)
    {
        var line7NetShortTermCapitalGain = CalculateTotalShortTermCapitalGainsAndLosses(ledger, taxYear);
        var line15NetLongTermCapitalGain = CalculateTotalLongTermCapitalGainsAndLosses(ledger, taxYear);
        var summaryResults = RunSummaryAndCalculateFinalValue(ledger, line7NetShortTermCapitalGain, line15NetLongTermCapitalGain);;
        return (line15NetLongTermCapitalGain, summaryResults.line16CombinedCapitalGains,
            summaryResults.line20Are18And19BothZero, summaryResults.line22DoYouHaveQualifiedDividendsOnCapitalGains);
    }
    public static decimal CalculateTotalShortTermCapitalGainsAndLosses(TaxLedger ledger, int taxYear)
    {
        return ledger.ShortTermCapitalGains
            .Where(x => x.earnedDate.Year == taxYear)
            .Sum(x => x.amount);
    }
    public static decimal CalculateTotalLongTermCapitalGainsAndLosses(TaxLedger ledger, int taxYear)
    {
        return ledger.LongTermCapitalGains
            .Where(x => x.earnedDate.Year == taxYear)
            .Sum(x => x.amount);
    }

    public static (decimal line16CombinedCapitalGains, bool line20Are18And19BothZero,
        bool line22DoYouHaveQualifiedDividendsOnCapitalGains)
        RunSummaryAndCalculateFinalValue(
            TaxLedger ledger, decimal line7NetShortTermCapitalGain, decimal line15NetLongTermCapitalGain)
    {
        (decimal line16, bool Line20Are18And19BothZero, bool Line22DoYouHaveQualifiedDividendsOnCapitalGains) results = 
            (0m, false, false);
        results.line16 = line7NetShortTermCapitalGain + line15NetLongTermCapitalGain;
        if (results.line16 == 0) return results;
        if (results.line16 < 0)
        {
            var line21SmallerOfTheLoss = Math.Min(30000m, Math.Abs(results.line16));
            results.line16 = -1 * line21SmallerOfTheLoss;
            return results;
        }
        // positive gains, bro
        var line17 = (results.line16 > 0 && line15NetLongTermCapitalGain > 0);
        if (line17 == false) return results;
        var line18CollectiblesRateGain = 0;
        var line19UnrecapturedSection1250Gain = 0;
       
        results.Line20Are18And19BothZero = (line18CollectiblesRateGain == 0 && line19UnrecapturedSection1250Gain == 0);
        results.Line22DoYouHaveQualifiedDividendsOnCapitalGains = false; // we're not modelling dividends

        return results;
    }
}