using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;

namespace Lib.MonteCarlo.TaxForms.Federal;

/// <summary>
/// Schedule D Capital Gains and Losses
/// https://www.irs.gov/pub/irs-pdf/f1040sd.pdf
/// https://www.irs.gov/pub/irs-pdf/i1040sd.pdf
/// </summary>
public class ScheduleD
{
    private TaxLedger _ledger;
    private int _taxYear;
    private decimal _form1040Line7 = 0m;
    private bool _isRequiredToCompleteQualifiedDividendsAndCapitalGainsWorksheet = false;
    private decimal _line16CombinedCapitalGains = 0m;
    private decimal _line15LongTermCapitalGains = 0m;
    public decimal Form1040Line7 => _form1040Line7;
    public bool IsRequiredToCompleteQualifiedDividendsAndCapitalGainsWorksheet =>
        _isRequiredToCompleteQualifiedDividendsAndCapitalGainsWorksheet;
    public decimal Line15LongTermCapitalGains => _line15LongTermCapitalGains;
    public decimal Line16CombinedCapitalGains => _line16CombinedCapitalGains;
    
    public ScheduleD(TaxLedger ledger, int taxYear)
    {
        _ledger = ledger;
        _taxYear = taxYear;
    }

    public void Complete()
    {
        var line7 = TaxCalculation.CalculateLongTermCapitalGainsForYear(_ledger, _taxYear);
        _line15LongTermCapitalGains = TaxCalculation.CalculateLongTermCapitalGainsForYear(_ledger, _taxYear);
        _line16CombinedCapitalGains = line7 + _line15LongTermCapitalGains;
        if (_line16CombinedCapitalGains > 0)
        {
            CompleteBothGainsPath();
            return;
        }

        if (_line16CombinedCapitalGains < 0)
        {
            var reportedLoss = Math.Max(TaxConstants.ScheduleDMaximumCapitalLoss, _line16CombinedCapitalGains);
            _form1040Line7 = reportedLoss;
            CompleteLine22();
            return;
        }
        // line16 == 0;
        _form1040Line7 = 0m;
        CompleteLine22();
        return;
    }

    private void CompleteBothGainsPath()
    {
        _form1040Line7 = _line16CombinedCapitalGains;
        var line17 = (_line15LongTermCapitalGains > 0 && _line16CombinedCapitalGains > 0);
        if (line17 == false)
        {
            CompleteLine22();
            return;
        }

        const decimal line18 = 0m; // 28% rate gain worksheet
        const decimal line19 = 0m; // unrecaptured something
        const bool line20 = true;
        _isRequiredToCompleteQualifiedDividendsAndCapitalGainsWorksheet = true;
        return;
    }

    private void CompleteLine22()
    {
        var dividends = TaxCalculation.CalculateQualifiedDividendsForYear(_ledger, _taxYear);
        if (dividends > 0)
        {
            _isRequiredToCompleteQualifiedDividendsAndCapitalGainsWorksheet = true;
        }
    }
}