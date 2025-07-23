using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.TaxForms.Federal;
using Lib.MonteCarlo.TaxForms.NC;
using Lib.StaticConfig;
using NodaTime;

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
    public static decimal CalculateIncomeRoom(TaxLedger ledger, LocalDateTime currentDate)
    {
        // start with the _incomeTaxBrackets 12% max
        var maxAtBracket = TaxConstants.Federal1040TaxTableBrackets[1].max;
        
        // add the standard deduction 
        var standardDeduction= TaxConstants.FederalStandardDeduction;

        var spanUntilSsElectionStart = ledger.SocialSecurityElectionStartDate - currentDate;
        var monthsUntilSsElectionStart = (spanUntilSsElectionStart.Years * 12) + spanUntilSsElectionStart.Months;
        var currentYear = currentDate.Year;
        var benefitsStartYear = ledger.SocialSecurityElectionStartDate.Year;

        // determine total social security wage
        decimal projectedTotalSsIncomeThisYear = 0m;
        if (currentYear < benefitsStartYear) projectedTotalSsIncomeThisYear = 0m;
        else if (currentYear > benefitsStartYear) projectedTotalSsIncomeThisYear = ledger.SocialSecurityWageMonthly * 12m;
        else
        {
            // we're in the election year, so we'll only get wages for part of it
            var nextJan1 = new LocalDateTime(currentYear + 1, 1, 1, 0, 0);
            var benefitMonths = (nextJan1 - ledger.SocialSecurityElectionStartDate).Months;
            projectedTotalSsIncomeThisYear = ledger.SocialSecurityWageMonthly * benefitMonths;
        }
        // determine taxable % of social security wage projection
        var taxableSocialSecurityProjection = projectedTotalSsIncomeThisYear * TaxConstants.MaxSocialSecurityTaxPercent;
        
        
         
        
        
        // remove any other revenue that would be taxed as income
        var w2Income = CalculateW2IncomeForYear(ledger, currentYear);
        var taxableIraDistributions = CalculateTaxableIraDistributionsForYear(ledger, currentYear);
        var taxableInterestReceived = CalculateTaxableInterestReceivedForYear(ledger, currentYear);
        var qualifiedDividends = CalculateQualifiedDividendsForYear(ledger, currentYear);
        var totalDividends = CalculateTotalDividendsForYear(ledger, currentYear);
        var dividendThatCount = totalDividends - qualifiedDividends;
        var shortTermCapitalGains = CalculateShortTermCapitalGainsForYear(ledger, currentYear);
        var headRoom = 0m
                       + maxAtBracket
                       + standardDeduction
                       - taxableSocialSecurityProjection
                       - w2Income
                       - taxableIraDistributions
                       - taxableInterestReceived
                       - dividendThatCount
                       - shortTermCapitalGains;
        
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
            .Sum(x => x.amount)) ;
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
        totalLiability += CalculateNorthCarolinaTaxLiabilityForYear(
            ledger, taxYear, form1040.AdjustedGrossIncome);
        return totalLiability;
    }
    public static decimal CalculateNorthCarolinaTaxLiabilityForYear(
        TaxLedger ledger, int taxYear, decimal adjustedGrossIncomeFrom1040)
    {
        var formD400 = new FormD400(ledger, taxYear, adjustedGrossIncomeFrom1040);
        return formD400.CalculateTaxLiability();
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