using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;

namespace Lib.MonteCarlo.TaxForms.Federal;

public static class Form1040
{
    /// <summary>
    /// Line 38: AmountYouOwe
    /// </summary>
    public static (decimal liability, decimal taxableIncome) CalculateLine38AmountYouOwe(TaxLedger ledger, int taxYear)
    {
        // https://www.irs.gov/pub/irs-pdf/f1040.pdf

        var totalTaxResults = CalculateLine24TotalTax(ledger, taxYear);
        var line15TaxableIncome = totalTaxResults.line15TaxableIncome;
        var line24TotalTax = totalTaxResults.line24TotalTax;
        var line33TotalPayments = CalculateLine33TotalPayments(ledger, taxYear);
        return (line24TotalTax - line33TotalPayments, line15TaxableIncome);
    }

    public static decimal CalculateLine33TotalPayments(TaxLedger ledger, int taxYear)
    {
        var line25FederalWithholding = CalculateFederalWitholding(ledger, taxYear);
        var line26 = 0m; // no prior payments
        var line27 = 0m; // no EIC
        var line28 = 0m; // no add'l child tax credit
        var line29 = 0m; 
        var line30 = 0m; 
        var line31 = 0m; 
        var line32 = line27 + line28 + line29 + line31;
        var line33TotalPayments = line25FederalWithholding + line26 + line32;
        return line33TotalPayments;
    }
    public static (decimal line15TaxableIncome, decimal line24TotalTax) CalculateLine24TotalTax(
        TaxLedger ledger, int taxYear)
    {
        var line3A = CalculateQualifiedDividendsEarned();
        var line15TaxableIncome = CalculateLine15TaxableIncome(ledger, taxYear);
        var line16Tax = CalculateTax(ledger, taxYear, line15TaxableIncome, line3A);
        var line17 = 0m; // we won't model additional taxes and the AMT only kicks in above 1.2MM in income
        var line18 = line16Tax + line17;
        var line19 = 0m; // kids be growned up
        var line20 = 0m; // we may be able to model some of these credits later todo: review prior returns
        var line21 = line19 + line20;
        var line22 = Math.Min(0, line18 - line21);
        var line23 = 0m; // no other taxes
        var line24TotalTax = line22 + line23;
        return (line15TaxableIncome, line24TotalTax);
    }
    /// <summary>
    /// Line 1A: Total amount from Form(s) W-2, box 1 (see instructions) 
    /// </summary>
    public static decimal CalculateTotalW2(TaxLedger ledger, int taxYear)
    {
        return ledger.W2Income
            .Where(x => x.earnedDate.Year == taxYear)
            .Sum(x => x.amount);
    }
    /// <summary>
    /// Line 1Z: Total of all line 1 sub items 
    /// </summary>
    public static decimal CalculateLine1Z(TaxLedger ledger, int taxYear)
    {
        var line1A = CalculateTotalW2(ledger, taxYear);
        var line1B = 0m; // Household employee wages not reported on Form(s) W-2
        var line1C = 0m; // Tip income not reported on line 1a (see instructions)
        var line1D = 0m; // Medicaid waiver payments not reported on Form(s) W-2 (see instructions)
        var line1E = 0m; // Taxable dependent care benefits from Form 2441, line 26
        var line1F = 0m; // Employer-provided adoption benefits from Form 8839, line 29 
        var line1G = 0m; // Wages from Form 8919, line 6 (Uncollected Social Security and Medicare Tax on Wages)
        var line1H = 0m; // Other earned income (see instructions) 
        var line1I = 0m; // Nontaxable combat pay election (see instructions) 
        return line1A + line1B + line1C + line1D + line1E + line1F + line1G + line1H + line1I;
    }

    /// <summary>
    /// Line 9: Total of all income
    /// </summary>
    public static decimal CalculateLine9TotalIncome(TaxLedger ledger, int taxYear)
    {
        var line1Z = CalculateLine1Z(ledger, taxYear);
        var line2A = 0m; // Tax-exempt interest received
        var line2B = CalculateTaxableInterestReceived(ledger, taxYear);
        var line3B = CalculateOrdinaryDividendsEarned();
        var line4B = CalculateTaxableIraDistributions(ledger, taxYear);
        var line5B = 0m; // Pensions and annuities
        var line7 = CalculateCapitalGainOrLoss(ledger, taxYear);
        var line8 = 0m; // Additional income from Schedule 1, line 10 
        var line6B = CalculateTaxableSocialSecurityBenefits(ledger, taxYear, line1Z, line2A, line2B, line3B, line4B, line5B, line7, line8);
        return line1Z + line2B + line3B + line4B + line5B + line6B + line7 + line8;
    }

    /// <summary>
    /// Line 2B: Taxable interest received
    /// </summary>
    public static decimal CalculateTaxableInterestReceived(TaxLedger ledger, int taxYear)
    {
        
        return ledger.TaxableInterestReceived
            .Where(x => x.earnedDate.Year == taxYear)
            .Sum(x => x.amount);
    }
    
    /// <summary>
    /// Line 3A: Qualified dividends 
    /// </summary>
    public static decimal CalculateQualifiedDividendsEarned()
    {
        // we're not gonna model dividend income as it'd be impractical
        return 0m;
    }
    // <summary>
    /// Line 3B: Ordinary dividends 
    /// </summary>
    public static decimal CalculateOrdinaryDividendsEarned()
    {
        // we're not gonna model dividend income as it'd be impractical
        return 0m;
    }
    
    /// <summary>
    /// Line 4B: Taxable IRA Distributions 
    /// </summary>
    public static decimal CalculateTaxableIraDistributions(TaxLedger ledger, int taxYear)
    {
        return ledger.TaxableIraDistribution
            .Where(x => x.earnedDate.Year == taxYear)
            .Sum(x => x.amount);
    }
    
    /// <summary>
    /// Line 6B: Taxable Social Security benefits
    /// </summary>
    public static decimal CalculateTaxableSocialSecurityBenefits(
        TaxLedger ledger, int taxYear, decimal line1Z, decimal line2A, decimal line2B, decimal line3B, decimal line4B,
        decimal line5B, decimal line7, decimal line8)
    {
        var combinedIncome = 
            line1Z + line2B + line3B + line4B + line5B + line7 + line8;
        // SocialSecurityBenefitsWorksheet worksheet = new SocialSecurityBenefitsWorksheet(
        //     ledger, taxYear, combinedIncome, line2A);
        return SocialSecurityBenefitsWorksheet.CalculateTaxableSocialSecurityBenefits(
            ledger, taxYear, combinedIncome, line2A);
    }
    
    /// <summary>
    /// Line 7: Capital gain or (loss) 
    /// </summary>
    public static decimal CalculateCapitalGainOrLoss(TaxLedger ledger, int taxYear)
    {
        // we're assuming everything is long-term
        return ledger.LongTermCapitalGains
            .Where(x => x.earnedDate.Year == taxYear)
            .Sum(x => x.amount);
    }
    
    /// <summary>
    /// Line 12: Standard deduction or itemized deductions
    /// </summary>
    public static decimal CalculateDeductions()
    {
        // let's just take the standard deduction to make things easier
        return TaxConstants.FederalStandardDeduction;
    }
    /// <summary>
    /// Line 15: Taxable income
    /// </summary>
    public static decimal CalculateLine15TaxableIncome(TaxLedger ledger, int taxYear)
    {
        var line9TotalIncome = CalculateLine9TotalIncome(ledger, taxYear);
        var line10 = 0m; // Adjustments to income from Schedule 1, line 26 
        var line11AdjustedGrossIncome = line9TotalIncome - line10;
        var line12 = CalculateDeductions();
        var line13 = 0m; // Qualified business income deduction from Form 8995 or Form 8995-A 
        var line14 = line12 + line13;
        var line15TaxableIncome = Math.Max(line11AdjustedGrossIncome - line14, 0);
        return line15TaxableIncome;
    }


    public static decimal CalculateTax(TaxLedger ledger, int taxYear, decimal line15TaxableIncome, decimal line3A)
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
        
        
        if (line15TaxableIncome < 100000) 
            return TaxTable.CalculateTaxOwed(line15TaxableIncome);
        
        return CalculateTaxFromWorksheet(ledger, taxYear, line15TaxableIncome, line3A);
    }
    
    public static decimal CalculateTaxFromWorksheet(TaxLedger ledger, int taxYear, decimal line15TaxableIncome,
        decimal line3A)
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
        if (line15TaxableIncome < 100000)
            throw new InvalidDataException("can't use the tax worksheet with income under 100k");
        
        var scheduleD = ScheduleD.PopulateScheduleDAndCalculateFinalValue(ledger, taxYear);
        if (scheduleD.scheduleDLine20Are18And19BothZero || scheduleD.scheduleDLine22DoYouHaveQualifiedDividendsOnCapitalGains)
        {
            // gotta use the big boy worksheet
            
            return QualifiedDividendsAndCapitalGainTaxWorksheet.CalculateTaxOwed(
                scheduleD.scheduleDLine15NetLongTermCapitalGain, scheduleD.scheduleDLine16CombinedCapitalGains, line3A,
                line15TaxableIncome);
        }

        return TaxComputationWorksheet.CalculateTaxOwed(line15TaxableIncome);
    }
    /// <summary>
    /// Line 25: Federal income tax withholding
    /// </summary>
    public static decimal CalculateFederalWitholding(TaxLedger ledger, int taxYear)
    {
        // todo: model witholding
        return ledger.FederalWithholdings
            .Where(x => x.earnedDate.Year == taxYear)
            .Sum(x => x.amount);
    }
    
}