using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;

namespace Lib.MonteCarlo.TaxForms.Federal;

public class Form1040
{
    public decimal AdjustedGrossIncome => _adjustedGrossIncome;
    public decimal Line16TaxLiability => _line16TaxLiability;
    public List<ReconciliationMessage> ReconciliationMessages = [];
    
    
    private TaxLedger _ledger;
    private int _taxYear;
    private ScheduleD _scheduleD;
    private decimal _line15TaxableIncome = 0m;
    private decimal _line3AQualifiedDividends = 0m;
    private decimal _adjustedGrossIncome = 0m;
    private decimal _line16TaxLiability = 0m;
    
    
    public Form1040(TaxLedger ledger, int taxYear)
    {
        _ledger = ledger;
        _taxYear = taxYear;
        _scheduleD = new ScheduleD(ledger, taxYear);
    }
    
    
    
    /// <summary>
    /// This is a combination of Line 38 (Amount you owe) and Line 34 (Refund amount). If you would get a refund, that
    /// returns as a negative here.
    /// </summary>
    public decimal CalculateTaxLiability()
    {
        // https://www.irs.gov/pub/irs-pdf/f1040.pdf
        var line1A = TaxCalculation.CalculateW2IncomeForYear(_ledger, _taxYear);
        var line1B = 0m; // Household employee wages not reported on Form(s) W-2
        var line1C = 0m; // Tip income not reported on line 1a (see instructions)
        var line1D = 0m; // Medicaid waiver payments not reported on Form(s) W-2 (see instructions)
        var line1E = 0m; // Taxable dependent care benefits from Form 2441, line 26
        var line1F = 0m; // Employer-provided adoption benefits from Form 8839, line 29 
        var line1G = 0m; // Wages from Form 8919, line 6 (Uncollected Social Security and Medicare Tax on Wages)
        var line1H = 0m; // Other earned income (see instructions) 
        var line1I = 0m; // Nontaxable combat pay election (see instructions) 
        var line1Z = line1A + line1B + line1C + line1D + line1E + line1F + line1G + line1H + line1I;
        var line2A = 0m; // Tax-exempt interest received
        var line2B = TaxCalculation.CalculateTaxableInterestReceivedForYear(_ledger, _taxYear);
        _line3AQualifiedDividends = TaxCalculation.CalculateQualifiedDividendsForYear(_ledger, _taxYear);
        var line3B = TaxCalculation.CalculateTotalDividendsForYear(_ledger, _taxYear);
        var line4B = TaxCalculation.CalculateTaxableIraDistributionsForYear(_ledger, _taxYear);
        var line5B = 0m; // Pensions and annuities
        var line6A = TaxCalculation.CalculateSocialSecurityIncomeForYear(_ledger, _taxYear);
        // need to skip line 6B for now because it needs line 7 as part of the combined income
        var line6B = 0m;
        // need to complete schedule D here before we can answer line 7
        _scheduleD.Complete();
        var line7 = _scheduleD.Form1040Line7;
        var line8 = 0m; // Additional income from Schedule 1, line 10 
        // revisit line 6B now that you know all the inputs
        var combinedIncome = line1Z + line2B + line3B + line4B + line5B + line7 + line8;
        line6B = SocialSecurityBenefitsWorksheet.CalculateTaxableSocialSecurityBenefits(
            _ledger, _taxYear, combinedIncome, line2A);
        var line9TotalIncome =  combinedIncome + line6B; // This is your total income
        var line10 = 0m; // Adjustments to income from Schedule 1, line 26 
        _adjustedGrossIncome = line9TotalIncome - line10;
        var line12 = TaxConstants.FederalStandardDeduction;
        var line13 = 0m; // Qualified business income deduction from Form 8995 or Form 8995-A 
        var line14 = line12 + line13;
        _line15TaxableIncome = Math.Max(_adjustedGrossIncome - line14, 0);
        _line16TaxLiability = CalculateTax();
        var line17 = 0m; // we won't model additional taxes and the AMT only kicks in above 1.2MM in income
        var line18 = _line16TaxLiability + line17;
        var line19 = 0m; // kids be growned up
        var line20 = 0m; // we may be able to model some of these credits later 
        var line21 = line19 + line20;
        var line22 = Math.Max(0, line18 - line21);
        var line23 = 0m; // no other taxes
        var line24TotalTax = line22 + line23;
        var line25FederalWithholding = TaxCalculation.CalculateFederalWithholdingForYear(_ledger, _taxYear);
        var line26 = 0m; // no prior payments
        var line27 = 0m; // no EIC
        var line28 = 0m; // no add'l child tax credit
        var line29 = 0m; 
        var line30 = 0m; 
        var line31 = 0m; 
        var line32 = line27 + line28 + line29 + line31;
        var line33TotalPayments = line25FederalWithholding + line26 + line32;
        
        var remainingLiability = line24TotalTax - line33TotalPayments;
        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcileTaxCalcs) return remainingLiability;
        ReconciliationMessages.Add(new ReconciliationMessage(null, line1A, "W2 income"));
        ReconciliationMessages.Add(new ReconciliationMessage(null, line6B, "Taxable SS income"));
        ReconciliationMessages.Add(new ReconciliationMessage(null, line7, "Capital gains"));
        ReconciliationMessages.Add(new ReconciliationMessage(null, 
            line9TotalIncome, "Line 9 total income"));
        ReconciliationMessages.Add(new ReconciliationMessage(null, 
            _adjustedGrossIncome, "Adjusted gross income"));
        ReconciliationMessages.Add(new ReconciliationMessage(null, 
            _line15TaxableIncome, "Line 15 taxable income"));
        ReconciliationMessages.Add(new ReconciliationMessage(null, 
            _line16TaxLiability, "Line 16 tax"));
        ReconciliationMessages.Add(new ReconciliationMessage(null, 
            line25FederalWithholding, "Line 25 federal withholding"));
        
        ReconciliationMessages.Add(new ReconciliationMessage(null, 
            remainingLiability, "Total federal liability"));
        
        return remainingLiability;
    }

    private decimal CalculateTax()
    {
        if (_scheduleD.IsRequiredToCompleteQualifiedDividendsAndCapitalGainsWorksheet)
        {
            // big boy form
            return QualifiedDividendsAndCapitalGainTaxWorksheet.CalculateTaxOwed(
                _scheduleD.Line15LongTermCapitalGains, _scheduleD.Line16CombinedCapitalGains, 
                _line3AQualifiedDividends, _line15TaxableIncome);
        }

        if (_line15TaxableIncome >= TaxConstants.FederalWorksheetVsTableThreshold)
        {
            // basic tax worksheet
            return TaxComputationWorksheet.CalculateTaxOwed(_line15TaxableIncome);
        }
        // basic table
        return TaxTable.CalculateTaxOwed(_line15TaxableIncome);
    }
}