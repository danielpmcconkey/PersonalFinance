using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;

namespace Lib.MonteCarlo.TaxForms.Federal;

public class Form1040(TaxLedger ledger, int taxYear)
{
    public decimal AdjustedGrossIncome { get; private set; } = 0m;

    //public decimal Line16TaxLiability => _line16TaxLiability;
    public readonly List<ReconciliationMessage> ReconciliationMessages = [];


    private readonly ScheduleD _scheduleD = new(ledger, taxYear);
    private decimal _line15TaxableIncome = 0m;
    private decimal _line3AQualifiedDividends = 0m;
    private decimal _line16TaxLiability = 0m;


    /// <summary>
    /// This is a combination of Line 38 (Amount you owe) and Line 34 (Refund amount). If you would get a refund, that
    /// returns as a negative here.
    /// </summary>
    public decimal CalculateTaxLiability()
    {
        // https://www.irs.gov/pub/irs-pdf/f1040.pdf
        var line1A = TaxCalculation.CalculateW2IncomeForYear(ledger, taxYear);
        const decimal line1B = 0m; // Household employee wages not reported on Form(s) W-2
        const decimal line1C = 0m; // Tip income not reported on line 1a (see instructions)
        const decimal line1D = 0m; // Medicaid waiver payments not reported on Form(s) W-2 (see instructions)
        const decimal line1E = 0m; // Taxable dependent care benefits from Form 2441, line 26
        const decimal line1F = 0m; // Employer-provided adoption benefits from Form 8839, line 29 
        const decimal line1G = 0m; // Wages from Form 8919, line 6 (Uncollected Social Security and Medicare Tax on Wages)
        const decimal line1H = 0m; // Other earned income (see instructions) 
        const decimal line1I = 0m; // Nontaxable combat pay election (see instructions) 
        var line1Z = line1A + line1B + line1C + line1D + line1E + line1F + line1G + line1H + line1I;
        const decimal line2A = 0m; // Tax-exempt interest received
        var line2B = TaxCalculation.CalculateTaxableInterestReceivedForYear(ledger, taxYear);
        _line3AQualifiedDividends = TaxCalculation.CalculateQualifiedDividendsForYear(ledger, taxYear);
        var line3B = TaxCalculation.CalculateTotalDividendsForYear(ledger, taxYear);
        var line4B = TaxCalculation.CalculateTaxableIraDistributionsForYear(ledger, taxYear);
        const decimal line5B = 0m; // Pensions and annuities
        var line6A = TaxCalculation.CalculateSocialSecurityIncomeForYear(ledger, taxYear);
        // need to skip line 6B for now because it needs line 7 as part of the combined income
        var line6B = 0m;
        // need to complete schedule D here before we can answer line 7
        _scheduleD.Complete();
        var line7 = _scheduleD.Form1040Line7;
        const decimal line8 = 0m; // Additional income from Schedule 1, line 10 
        // revisit line 6B now that you know all the inputs
        var combinedIncome = line1Z + line2B + line3B + line4B + line5B + line7 + line8;
        line6B = SocialSecurityBenefitsWorksheet.CalculateTaxableSocialSecurityBenefits(
            ledger, taxYear, combinedIncome, line2A);
        var line9TotalIncome =  combinedIncome + line6B; // This is your total income
        const decimal line10 = 0m; // Adjustments to income from Schedule 1, line 26 
        AdjustedGrossIncome = line9TotalIncome - line10;
        const decimal line12 = TaxConstants.FederalStandardDeduction;
        const decimal line13 = 0m; // Qualified business income deduction from Form 8995 or Form 8995-A 
        const decimal line14 = line12 + line13;
        _line15TaxableIncome = Math.Max(AdjustedGrossIncome - line14, 0);
        _line16TaxLiability = CalculateTax();
        const decimal line17 = 0m; // we won't model additional taxes and the AMT only kicks in above 1.2MM in income
        var line18 = _line16TaxLiability + line17;
        const decimal line19 = 0m; // kids be growned up
        const decimal line20 = 0m; // we may be able to model some of these credits later 
        const decimal line21 = line19 + line20;
        var line22 = Math.Max(0, line18 - line21);
        const decimal line23 = 0m; // no other taxes
        var line24TotalTax = line22 + line23;
        var line25FederalWithholding = TaxCalculation.CalculateFederalWithholdingForYear(ledger, taxYear);
        const decimal line26 = 0m; // no prior payments
        const decimal line27 = 0m; // no EIC
        const decimal line28 = 0m; // no add'l child tax credit
        const decimal line29 = 0m; 
        // var line30 = 0m; 
        const decimal line31 = 0m; 
        const decimal line32 = line27 + line28 + line29 + line31;
        var line33TotalPayments = line25FederalWithholding + line26 + line32;
        
        var remainingLiability = line24TotalTax - line33TotalPayments;
        if (!MonteCarloConfig.DebugMode) return remainingLiability;
        ReconciliationMessages.Add(new ReconciliationMessage(null, line1A, "W2 income"));
        ReconciliationMessages.Add(new ReconciliationMessage(null, line4B, "IRA distributions"));
        ReconciliationMessages.Add(new ReconciliationMessage(null, line6B, "Taxable SS income"));
        ReconciliationMessages.Add(new ReconciliationMessage(null, line7, "Capital gains"));
        ReconciliationMessages.Add(new ReconciliationMessage(null, 
            line9TotalIncome, "Line 9 total income"));
        ReconciliationMessages.Add(new ReconciliationMessage(null, 
            AdjustedGrossIncome, "Adjusted gross income"));
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