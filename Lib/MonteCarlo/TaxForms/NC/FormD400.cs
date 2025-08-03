using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;

namespace Lib.MonteCarlo.TaxForms.NC;

public class FormD400
{
    private TaxLedger _ledger;
    private int _taxYear;
    private decimal _federalAdjustedGrossIncome;
    
    public List<ReconciliationMessage> ReconciliationMessages = [];

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
        
        if (!MonteCarloConfig.DebugMode) return whatYouOwe;
        ReconciliationMessages.Add(new ReconciliationMessage(null, _federalAdjustedGrossIncome, "Federal AGI used in NC tax calc"));
        ReconciliationMessages.Add(new ReconciliationMessage(null, line15, "Total NC tax"));
        ReconciliationMessages.Add(new ReconciliationMessage(null, line20, "State withholding"));
        ReconciliationMessages.Add(new ReconciliationMessage(null, whatYouOwe, "What you owe"));

        return whatYouOwe;
    }
}