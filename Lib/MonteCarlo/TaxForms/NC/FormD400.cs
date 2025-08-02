using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;

namespace Lib.MonteCarlo.TaxForms.NC;

public class FormD400
{
    private TaxLedger _ledger;
    private int _taxYear;
    private decimal _federalAdjustedGrossIncome;

    public FormD400(TaxLedger ledger, int taxYear, decimal federalAdjustedGrossIncome)
    {
        _ledger = ledger;
        _taxYear = taxYear;
        _federalAdjustedGrossIncome = federalAdjustedGrossIncome;
    }

    public decimal CalculateTaxLiability()
    {
        var line6 = _federalAdjustedGrossIncome;
        var line7 = 0m;
        var line8 = line6 + line7;
        var line9 = 0m;
        var line10 = 0m;
        var line11 = TaxConstants.NcStandardDeduction;
        var line12A = line11 + line9 + line10;
        var line12B = line8 - line12A;
        //var line13 = 0m;
        var line14 = line12B;
        var line15 = Math.Max(0, line14 * TaxConstants.NorthCarolinaFlatTaxRate);
        var line16 = 0m;
        var line17 = line15 - line16;
        var line18 = 0m;
        var line19 = line17 + line18;
        var line20 = TaxCalculation.CalculateStateWithholdingForYear(_ledger, _taxYear);
        decimal whatYouOwe = line15 - line20;
        if (MonteCarloConfig.DebugMode == true && MonteCarloConfig.ShouldReconcileTaxCalcs)
        {
            Reconciliation.AddMessageLine(new(_taxYear,12,31,0,0), 
                whatYouOwe, "Total state liability");
        }
        return whatYouOwe;
    }
}