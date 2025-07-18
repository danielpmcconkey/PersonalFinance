using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;

namespace Lib.MonteCarlo.TaxForms.Federal;

public static class SocialSecurityBenefitsWorksheet
{

    public static decimal CalculateLine1SocialSecurityIncome(TaxLedger ledger, int taxYear)
    {
        return ledger.SocialSecurityIncome
            .Where(x => x.earnedDate.Year == taxYear)
            .Sum(x => x.amount);
    }
   
    public static decimal CalculateTaxableSocialSecurityBenefits(
        TaxLedger ledger, int taxYear, decimal combinedIncomeFrom1040, decimal line2AFrom1040)
    {
        // Social Security Benefits Worksheet
        // https://www.irs.gov/pub/irs-pdf/i1040gi.pdf?os=wtmbzegmu5hwrefapp&ref=app
        // page 32
        
        var line1 = CalculateLine1SocialSecurityIncome(ledger, taxYear);
        var line2 = line1 * 0.5m;
        var line3 = combinedIncomeFrom1040;
        var line4 = line2AFrom1040;
        var line5 = line2 + line3 + line4;
        var line6 = 0m; // not modelling schedule 1 here
        
        // The instructions for line 7 cause me to write confusing looking code. "Is the amount on line 6 less than
        // the amount on line 5? No.STOP None of your social security benefits are taxable. Enter -0- on Form 1040
        // or 1040-SR, line 6b."
        if ((line6 < line5) == false) return 0m; 
        
        var line7 = line5 - line6;
        var line8 = TaxConstants.SocialSecurityWorksheetCreditLine8;
        
        // Is the amount on line 8 less than the amount on line 7?
        // No.STOP None of your social security benefits are taxable. Enter -0- on Form 1040 or
        // 1040-SR, line 6b.
        if ((line8 < line7) == false) return 0m; 
        
        var line9 = line7 - line8;
        var line10 = TaxConstants.SocialSecurityWorksheetCreditLine10;
        var line11 = line9 - line10;
        if (line11 < 0) line11 = 0m;
        var line12 = Math.Min(line9, line10);
        var line13 = line12 * 0.5m;
        var line14 = Math.Min(line2, line13);
        var line15 = line11 * 0.85m;
        var line16 = line14 + line15;
        var line17 = line1 * 0.85m;
        return Math.Min(line16, line17);
    }
}