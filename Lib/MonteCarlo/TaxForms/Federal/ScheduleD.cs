using Lib.DataTypes.MonteCarlo;

namespace Lib.MonteCarlo.TaxForms.Federal;

/// <summary>
/// Schedule D Capital Gains and Losses
/// </summary>
public class ScheduleD
{
    private TaxLedger _ledger;
    private int _taxYear;
    
    public decimal Line7NetShortTermCapitalGain { get; set; } = 0m;
    public decimal Line15NetLongTermCapitalGain { get; set; } = 0m;
    public decimal Line16CombinedCapitalGains { get; set; } = 0m;
    public decimal Line21SmallerOfTheLoss { get; set; } = 0m;
    public decimal Line18CollectiblesRateGain { get; set; } = 0m; // we're not modelling collectibles
    public decimal Line19UnrecapturedSection1250Gain { get; set; } = 0m; // we're not modelling this, whatever it is
    /// <summary>
    /// this value determines whether we use the Qualified Dividends and Capital Gain Tax Worksheet to calculate tax
    /// </summary>
    public bool Line20Are18And19BothZero => (Line18CollectiblesRateGain == 0 && Line19UnrecapturedSection1250Gain == 0);
    public bool Line22DoYouHaveQualifiedDividendsOnCapitalGains { get; set; } = false; // we're not modelling dividends
    
    public ScheduleD(TaxLedger ledger, int taxYear)
    {
        _ledger = ledger;
        _taxYear = taxYear;
    }

    public decimal PopulateScheduleDAndCalculateFinalValue()
    {
        Line7NetShortTermCapitalGain = CalculateTotalShortTermCapitalGainsAndLosses();
        Line15NetLongTermCapitalGain = CalculateTotalLongTermCapitalGainsAndLosses();
        Line16CombinedCapitalGains = RunSummaryAndCalculateFinalValue();
        return Line16CombinedCapitalGains;
    }
    public decimal CalculateTotalShortTermCapitalGainsAndLosses()
    {
        return _ledger.ShortTermCapitalGains
            .Where(x => x.earnedDate.Year == _taxYear)
            .Sum(x => x.amount);
    }
    public decimal CalculateTotalLongTermCapitalGainsAndLosses()
    {
        return _ledger.LongTermCapitalGains
            .Where(x => x.earnedDate.Year == _taxYear)
            .Sum(x => x.amount);
    }

    public decimal RunSummaryAndCalculateFinalValue()
    {
        var line16 = Line7NetShortTermCapitalGain + Line15NetLongTermCapitalGain;
        if (line16 == 0) return 0;
        if (line16 < 0)
        {
            Line21SmallerOfTheLoss = Math.Min(30000m, Math.Abs(line16));
            return Line21SmallerOfTheLoss;
        }
        // positive gains, bro
        var line17 = (line16 > 0 && Line15NetLongTermCapitalGain > 0);
        if (line17 == false) return line16;
        Line18CollectiblesRateGain = 0;
        Line19UnrecapturedSection1250Gain = 0;
        return line16;
    }
}