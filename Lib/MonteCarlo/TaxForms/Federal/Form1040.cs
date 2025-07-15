using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;

namespace Lib.MonteCarlo.TaxForms.Federal;

public class Form1040
{
    public decimal Line15TaxableIncome
    {
        get { return _line15TaxableIncome; }
    }

    private TaxLedger _ledger;
    private int _taxYear;
    
    private decimal _line1A = 0m; // Total amount from Form(s) W-2, box 1 (see instructions)
    private decimal _line1B = 0m; // Household employee wages not reported on Form(s) W-2
    private decimal _line1C = 0m; // Tip income not reported on line 1a (see instructions)
    private decimal _line1D = 0m; // Medicaid waiver payments not reported on Form(s) W-2 (see instructions)
    private decimal _line1E = 0m; // Taxable dependent care benefits from Form 2441, line 26
    private decimal _line1F = 0m; // Employer-provided adoption benefits from Form 8839, line 29 
    private decimal _line1G = 0m; // Wages from Form 8919, line 6 (Uncollected Social Security and Medicare Tax on Wages)
    private decimal _line1H = 0m; // Other earned income (see instructions) 
    private decimal _line1I = 0m; // Nontaxable combat pay election (see instructions) 
    private decimal _line1Z = 0m; // line 1Z: Add lines 1a through 1h 
    private decimal _line2A = 0m; // Tax-exempt interest received
    private decimal _line2B = 0m; // Taxable interest received
    private decimal _line3A = 0m; // Qualified dividends
    private decimal _line3B = 0m; // Ordinary dividends
    private decimal _line4B = 0m; // Taxable IRA Distributions
    private decimal _line5B = 0m; // Pensions and annuities
    private decimal _line7 = 0m; // Capital gain or (loss)
    private decimal _line8 = 0m; // Additional income from Schedule 1, line 10 
    private decimal _line6B = 0m; // Taxable Social Security benefits
    private decimal _line9TotalIncome = 0m;
    private decimal _line10 = 0m; // Adjustments to income from Schedule 1, line 26 
    private decimal _line11AdjustedGrossIncome = 0m;
    private decimal _line12 = 0m; //
    private decimal _line13 = 0m; // Qualified business income deduction from Form 8995 or Form 8995-A 
    private decimal _line14 = 0m; 
    private decimal _line15TaxableIncome = 0m;
    private decimal _line16Tax = 0m;
    private decimal _line17 = 0m; // additional taxes
    private decimal _line18 = 0m;
    private decimal _line19 = 0m; // child tax credit
    private decimal _line20 = 0m; // add'l credits
    private decimal _line21 = 0m;
    private decimal _line22 = 0m;
    private decimal _line23 = 0m;
    private decimal _line24TotalTax = 0m;
    private decimal _line25FederalWithholding = 0m;
    private decimal _line26 = 0m;
    private decimal _line27 = 0m;
    private decimal _line28 = 0m;
    private decimal _line29 = 0m;
    private decimal _line30 = 0m;
    private decimal _line31 = 0m;
    private decimal _line32 = 0m;
    private decimal _line33TotalPayments = 0m;
    

    public Form1040(TaxLedger ledger, int year)
    {
        _ledger = ledger;
        _taxYear = year;
    }
    
    /// <summary>
    /// Line 38: Estimated tax penalty (see instructions)
    /// </summary>
    public decimal CalculateTotalTaxLiability()
    {
        // https://www.irs.gov/pub/irs-pdf/f1040.pdf
        
        _line1A = CalculateTotalW2();
        _line1B = 0m; // Household employee wages not reported on Form(s) W-2
        _line1C = 0m; // Tip income not reported on line 1a (see instructions)
        _line1D = 0m; // Medicaid waiver payments not reported on Form(s) W-2 (see instructions)
        _line1E = 0m; // Taxable dependent care benefits from Form 2441, line 26
        _line1F = 0m; // Employer-provided adoption benefits from Form 8839, line 29 
        _line1G = 0m; // Wages from Form 8919, line 6 (Uncollected Social Security and Medicare Tax on Wages)
        _line1H = 0m; // Other earned income (see instructions) 
        _line1I = 0m; // Nontaxable combat pay election (see instructions) 
        
        // line 1Z: Add lines 1a through 1h 
        _line1Z = _line1A + _line1B + _line1C + _line1D + _line1E + _line1F + _line1G + _line1H + _line1I;

        _line2A = 0m; // Tax-exempt interest received
        _line2B = CalculateTaxableInterestReceived();
        _line3A = CalculateQualifiedDividendsEarned();
        _line3B = CalculateOrdinaryDividendsEarned();
        _line4B = CalculateTaxableIraDistributions();
        _line5B = 0m; // Pensions and annuities
        _line7 = CalculateCapitalGainOrLoss();
        _line8 = 0m; // Additional income from Schedule 1, line 10 
        _line6B = CalculateTaxableSocialSecurityBenefits();
        _line9TotalIncome = _line1Z + _line2B + _line3B + _line4B + _line5B + _line6B + _line7 + _line8;
        _line10 = 0m; // Adjustments to income from Schedule 1, line 26 
        _line11AdjustedGrossIncome = _line9TotalIncome - _line10;
        _line12 = CalculateDeductions();
        _line13 = 0m; // Qualified business income deduction from Form 8995 or Form 8995-A 
        _line14 = _line12 + _line13;
        _line15TaxableIncome = Math.Max(_line11AdjustedGrossIncome - _line14, 0);
        _line16Tax = CalculateTax();
        _line17 = 0m; // we won't model additional taxes and the AMT only kicks in above 1.2MM in income
        _line18 = _line16Tax + _line17;
        _line19 = 0m; // kids be growned up
        _line20 = 0m; // we may be able to model some of these credits later todo: review prior returns
        _line21 = _line19 + _line20;
        _line22 = Math.Min(0, _line18 - _line21);
        _line23 = 0m; // no other taxes
        _line24TotalTax = _line22 + _line23;
        _line25FederalWithholding = CalculateFederalWitholding();
        _line26 = 0m; // no prior payments
        _line27 = 0m; // no EIC
        _line28 = 0m; // no add'l child tax credit
        _line29 = 0m; 
        _line30 = 0m; 
        _line31 = 0m; 
        _line32 = _line27 + _line28 + _line29 + _line31;
        _line33TotalPayments = _line25FederalWithholding + _line26 + _line32;
        return _line24TotalTax - _line33TotalPayments;
    }
    /// <summary>
    /// Line 1A: Total amount from Form(s) W-2, box 1 (see instructions) 
    /// </summary>
    public decimal CalculateTotalW2()
    {
        return _ledger.W2Income
            .Where(x => x.earnedDate.Year == _taxYear)
            .Sum(x => x.amount);
    }
    
    /// <summary>
    /// Line 2B: Taxable interest received
    /// </summary>
    public decimal CalculateTaxableInterestReceived()
    {
        
        return _ledger.TaxableInterestReceived
            .Where(x => x.earnedDate.Year == _taxYear)
            .Sum(x => x.amount);
    }
    
    /// <summary>
    /// Line 3A: Qualified dividends 
    /// </summary>
    public decimal CalculateQualifiedDividendsEarned()
    {
        // we're not gonna model dividend income as it'd be impractical
        return 0m;
    }
    // <summary>
    /// Line 3B: Ordinary dividends 
    /// </summary>
    public decimal CalculateOrdinaryDividendsEarned()
    {
        // we're not gonna model dividend income as it'd be impractical
        return 0m;
    }
    
    /// <summary>
    /// Line 4B: Taxable IRA Distributions 
    /// </summary>
    public decimal CalculateTaxableIraDistributions()
    {
        return _ledger.TaxableIraDistribution
            .Where(x => x.earnedDate.Year == _taxYear)
            .Sum(x => x.amount);
    }
    
    /// <summary>
    /// Line 6B: Taxable Social Security benefits
    /// </summary>
    public decimal CalculateTaxableSocialSecurityBenefits()
    {
        var combinedIncome = 
            _line1Z + _line2B + _line3B + _line4B + _line5B + _line7 + _line8;
        SocialSecurityBenefitsWorksheet worksheet = new SocialSecurityBenefitsWorksheet(
            _ledger, _taxYear, combinedIncome, _line2A);
        return worksheet.CalculateTaxableSocialSecurityBenefits();
    }
    
    /// <summary>
    /// Line 7: Capital gain or (loss) 
    /// </summary>
    public decimal CalculateCapitalGainOrLoss()
    {
        // we're assuming everything is long-term
        return _ledger.LongTermCapitalGains
            .Where(x => x.earnedDate.Year == _taxYear)
            .Sum(x => x.amount);
    }
    
    /// <summary>
    /// Line 12: Standard deduction or itemized deductions
    /// </summary>
    public decimal CalculateDeductions()
    {
        // let's just take the standard deduction to make things easier
        return 29200m;
    }

    public decimal CalculateTax()
    {
        /*
         * https://www.irs.gov/pub/irs-pdf/i1040gi.pdf?os=wtmbzegmu5hwrefapp&ref=app
         * page 33
         * 
         * Tax Table or Tax Computation Worksheet. If your taxable income is less than $100,000, you must use the Tax
         * Table, later in these instructions, to figure your tax. Be sure you use the correct column. If your taxable
         * income is $100,000 or more, use the Tax Computation Worksheet right after the Tax Table.
         *
         * However, don’t use the Tax Table or Tax Computation Worksheet to figure your tax if any of the following
         * applies.
         * 
         * Form 8615. Form 8615 must generally be used to figure the tax on your unearned income over $2,600 if you are
         * under age 18, and in certain situations if you are older.
         *
         * You must file Form 8615 if you meet all of the following conditions.
         *      1. You had more than $2,600 of unearned income (such as taxable interest, ordinary dividends,
         *         or capital gains (including capital gain distributions)).
         *      2. You are required to file a tax return.
         *      3. You were either:
         *          a. Under age 18 at the end of 2024,
         *          b. Age 18 at the end of 2024 and didn't have earned income that was more than half of your
         *             support, or
         *          c. A full-time student at least age 19 but under age 24 at the end of 2024 and didn't have
         *             earned income that was more than half of your support.
         *      4. At least one of your parents was alive at the end of 2024.
         *      5. You don’t file a joint return in 2024.
         *
         * A child born on January 1, 2007, is considered to be age 18 at the end of 2024; a child born on January 1,
         * 2006, is considered to be age 19 at the end of 2024; and a child born on January 1, 2001, is considered to be
         * age 24 at the end of 2024
         */
        
        
        if (_line15TaxableIncome < 100000) 
            return TaxTable.CalculateTaxOwed(_line15TaxableIncome);
        
        return CalculateTaxFromWorksheet();
    }
    
    public decimal CalculateTaxFromWorksheet()
    {
        /*
         * https://www.irs.gov/pub/irs-pdf/i1040gi.pdf?os=wtmbzegmu5hwrefapp&ref=app
         * page 76
         *
         * https://www.irs.gov/pub/irs-pdf/f1040sd.pdf
         * page 2
         *
         * there are 2 worksheets to use, depending on how schedule D works out
         */
        if (_line15TaxableIncome < 100000)
            throw new InvalidDataException("can't use the tax worksheet with income under 100k");
        
        var scheduleD = new ScheduleD(_ledger, _taxYear);
        if (scheduleD.Line20Are18And19BothZero || scheduleD.Line22DoYouHaveQualifiedDividendsOnCapitalGains)
        {
            // gotta use the big boy worksheet
            var worksheet = new QualifiedDividendsAndCapitalGainTaxWorksheet(
                scheduleD, _line3A, _line15TaxableIncome);
            return worksheet.CalculateTaxOwed();
        }

        return TaxComputationWorksheet.CalculateTaxOwed(_line15TaxableIncome);
    }
    /// <summary>
    /// Line 25: Federal income tax withholding
    /// </summary>
    public decimal CalculateFederalWitholding()
    {
        return _ledger.FederalWithholdings
            .Where(x => x.earnedDate.Year == _taxYear)
            .Sum(x => x.amount);
    }
    
}