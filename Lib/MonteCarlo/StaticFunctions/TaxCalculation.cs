using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.TaxForms.Federal;
using Lib.StaticConfig;

namespace Lib.MonteCarlo.StaticFunctions;

public static class TaxCalculation
{
    /// <summary>
    /// sets the income target for next year based on this year's social security income. the idea is that, below
    /// $96,950 in adjusted gross income, the tax on ordinary income is only 12%. Anything above that amount is taxed at
    /// 22%. Since long-term capital gains is only 15% (below 0.5MM), it's cheaper to receive income (tax deferred
    /// sales) than it is to receive capital gains (brokerage account sales). So we want to take out just enough to hit
    /// that 12% ceiling from our traditional accounts and then move over to capital gains accounts beyond that point.
    /// </summary>
    /// todo: vet out this logic (and all other tax calcs) 
    public static decimal CalculateIncomeRoom(TaxLedger ledger, int year)
    {
        // start with the _incomeTaxBrackets 12% max
        var headRoom = TaxConstants.Federal1040TaxTableBrackets[1].max;
        
        // add the standard deduction 
        headRoom += TaxConstants.FederalStandardDeduction;
        
        // remove last year's social security, but only 85% of it
        var taxableSocialSecurity = CalculateSocialSecurityIncomeForYear(ledger, year - 1);
        // in case this is the first year we need to do this, but haven't logged any social security income, use a min
        // value placeholder so we don't wildly overestimate our head room
        if (taxableSocialSecurity <= 0) taxableSocialSecurity = 
            TaxConstants.PlaceholderLastYearsSocialSecurityIncome * 
            TaxConstants.MaxSocialSecurityTaxPercent;
        headRoom -= taxableSocialSecurity;
        
        // remove any other income, capital gains, or taxable distribution
        headRoom -= ledger.W2Income
            .Where(x => x.earnedDate.Year == year)
            .Sum(x => x.amount);
        headRoom -= ledger.LongTermCapitalGains
            .Where(x => x.earnedDate.Year == year)
            .Sum(x => x.amount);
        headRoom -= ledger.ShortTermCapitalGains
            .Where(x => x.earnedDate.Year == year)
            .Sum(x => x.amount);
        headRoom -= ledger.TaxableIraDistribution
            .Where(x => x.earnedDate.Year == year)
            .Sum(x => x.amount);
        headRoom -= ledger.TaxableInterestReceived
            .Where(x => x.earnedDate.Year == year)
            .Sum(x => x.amount);
        
        // finally, don't let it go below 0
        return Math.Max(headRoom, 0);
    }
    
    public static decimal CalculateW2IncomeForYear(TaxLedger ledger, int year)
    {
        return ledger.W2Income
            .Where(x => x.earnedDate.Year == year)
            .Sum(x => x.amount);
    }
    
    public static decimal? CalculateRmdRateByYear(int year)
    {
        if(!TaxConstants.RmdTable.TryGetValue(year, out var rmd))
            return null;
        return rmd;

    }
    /// <summary>
    /// Assumes that all of my social security and income benifit will add
    /// up to enough to be maximally taxable, which is 85% of the total
    /// benefit
    /// </summary>
    public static decimal CalculateSocialSecurityIncomeForYear(TaxLedger ledger, int year)
    {
        return (ledger.SocialSecurityIncome
            .Where(x => x.earnedDate.Year == year)
            .Sum(x => x.amount)) * TaxConstants.MaxSocialSecurityTaxPercent;
    }
    public static decimal CalculateTaxableInterestReceivedForYear(TaxLedger ledger, int taxYear)
    {
        return ledger.TaxableInterestReceived
            .Where(x => x.earnedDate.Year == taxYear)
            .Sum(x => x.amount);
    }
    public static decimal CalculateQualifiedDividendsForYear(TaxLedger ledger, int taxYear)
    {
        return ledger.QualifiedDividendsReceived
            .Where(x => x.earnedDate.Year == taxYear)
            .Sum(x => x.amount);
    }
    public static decimal CalculateTotalDividendsForYear(TaxLedger ledger, int taxYear)
    {
        return ledger.DividendsReceived
            .Where(x => x.earnedDate.Year == taxYear)
            .Sum(x => x.amount);
    }
    public static decimal CalculateTaxLiabilityForYear(TaxLedger ledger, int taxYear)
    {
        decimal totalLiability = 0M;
        var form1040 = new Form1040(ledger, taxYear);
        totalLiability += form1040.CalculateTaxLiability();
        totalLiability += CalculateNorthCarolinaTaxLiabilityForYear(form1040.AdjustedGrossIncome);
        return totalLiability;
    }
    public static decimal CalculateNorthCarolinaTaxLiabilityForYear(decimal adjustedGrossIncomeFrom1040)
    {
        // todo: actually work through NC tax rules
        decimal totalLiability = 0M;
        // NC state income tax
        totalLiability += adjustedGrossIncomeFrom1040 * TaxConstants.NorthCarolinaFlatTaxRate;
        return totalLiability;
    }
    public static decimal CalculateTaxableIraDistributionsForYear(TaxLedger ledger, int taxYear)
    {
        return ledger.TaxableIraDistribution
            .Where(x => x.earnedDate.Year == taxYear)
            .Sum(x => x.amount);
    }
    public static decimal CalculateFederalWithholdingForYear(TaxLedger ledger, int taxYear)
    {
        return ledger.FederalWithholdings
            .Where(x => x.earnedDate.Year == taxYear)
            .Sum(x => x.amount);
    }
    public static decimal CalculateStateWithholdingForYear(TaxLedger ledger, int taxYear)
    {
        return ledger.StateWithholdings
            .Where(x => x.earnedDate.Year == taxYear)
            .Sum(x => x.amount);
    }
    public static decimal CalculateLongTermCapitalGainsForYear(TaxLedger ledger, int taxYear)
    {
        return ledger.LongTermCapitalGains
            .Where(x => x.earnedDate.Year == taxYear)
            .Sum(x => x.amount);
    }
    public static decimal CalculateShortTermCapitalGainsForYear(TaxLedger ledger, int taxYear)
    {
        return ledger.ShortTermCapitalGains
            .Where(x => x.earnedDate.Year == taxYear)
            .Sum(x => x.amount);
    }
}