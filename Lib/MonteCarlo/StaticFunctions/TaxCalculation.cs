using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.TaxForms.Federal;
using Lib.StaticConfig;

namespace Lib.MonteCarlo.StaticFunctions;

public static class TaxCalculation
{
    // public static decimal CalculateCapitalGainsForYear(TaxLedger ledger, int year)
    // {
    //     var longTerm = ledger.LongTermCapitalGains
    //         .Where(x => x.earnedDate.Year == year)
    //         .Sum(x => x.amount);
    //     var shortTerm = ledger.ShortTermCapitalGains
    //         .Where(x => x.earnedDate.Year == year)
    //         .Sum(x => x.amount);
    //     return longTerm + shortTerm;
    // }
    //  
    // public static decimal CalculateEarnedIncomeForYear(TaxLedger ledger, int year)
    // {
    //     return 
    //         CalculateOrdinaryIncomeForYear(ledger, year) +
    //         CalculateTaxableSocialSecurityIncomeForYear(ledger, year) -
    //         TaxConstants._standardDeduction;
    // }
    
    
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
        var taxableSocialSecurity = CalculateTaxableSocialSecurityIncomeForYear(ledger, year - 1);
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
    
    /// <summary>
    /// The best income scenario is that all taxable social security and
    /// all taxable capital gains add up to $96,950 and are taxed at 12%,
    /// with all other income coming from Roth or HSA accounts. So this
    /// takes our income target (which is already $96,950 minus last year's
    /// taxable social security minus the standard deduction) and subtracts
    /// any income or taxable capital gains accrued thus far in the year
    /// </summary>
    /// <param name="year"></param>
    /// <returns></returns>
    
    public static decimal CalculateOrdinaryIncomeForYear(TaxLedger ledger, int year)
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
    public static decimal CalculateTaxableSocialSecurityIncomeForYear(TaxLedger ledger, int year)
    {
        return (ledger.SocialSecurityIncome
            .Where(x => x.earnedDate.Year == year)
            .Sum(x => x.amount)) * TaxConstants.MaxSocialSecurityTaxPercent;
    }

    /// <summary>
    /// This assumes all capital gains are long-term. The investment sales functions should only be pulling positions
    /// held for longer than a year
    /// </summary>
    // public static decimal CalculateTaxOnCapitalGainsForYear(decimal totalCapitalGains, decimal earnedIncome, TaxLedger ledger, int year)
    // {
    //     // https://www.irs.gov/taxtopics/tc409
    //     
    //     var combined = earnedIncome + totalCapitalGains;
    //     
    //     // if your combined income comes in under the tier-1 threshold, you have no capital gains to pay 
    //     if (combined < TaxConstants._capitalGainsBrackets[0].max) return 0;
    //     
    //     // if your combined income is greater than the teir-3 min, pay all capital gains at 20%
    //     if (combined > TaxConstants._capitalGainsBrackets[2].min) return totalCapitalGains * TaxConstants._capitalGainsBrackets[2].rate;
    //     
    //     // if your earned income is greater than the teir 2 min, pall all capital gains at 15%
    //     if (earnedIncome > TaxConstants._capitalGainsBrackets[1].min) return totalCapitalGains * TaxConstants._capitalGainsBrackets[1].rate;
    //     
    //     // if your earned income is lower than the tier-1 threshold, but adding your capital gains would put it over
    //     // that threshold, then the portion of capital gains it took to reach that threshold is taxed at 0% and the rest
    //     // is taxed at 15% 
    //     var amountUnderBracket1 = TaxConstants._capitalGainsBrackets[0].max - earnedIncome;
    //     if (amountUnderBracket1 > 0) return (totalCapitalGains - amountUnderBracket1) * TaxConstants._capitalGainsBrackets[1].rate;
    //     
    //     // if you got here, then the logic above is not correct
    //     throw new InvalidDataException("Capital gains logic is incorrect");
    // }

    // public static decimal CalculateTaxOnOrdinaryIncomeForYear(decimal earnedIncome, TaxLedger ledger, int year)
    // {
    //     var totalLiability = 0m;
    //     // tax on ordinary income
    //     foreach (var bracket in TaxConstants._incomeTaxBrackets)
    //     {
    //         var amountInBracket =
    //                 earnedIncome
    //                 - (Math.Max(earnedIncome, bracket.max) - bracket.max) // amount above max
    //                 - (Math.Min(earnedIncome, bracket.min)) // amount below min
    //             ;
    //         totalLiability += (amountInBracket * bracket.rate);
    //     }
    //     return totalLiability;
    // }

    public static decimal CalculateTaxLiabilityForYear(TaxLedger ledger, int taxYear)
    {
        decimal totalLiability = 0M;
        var federalResults = Form1040.CalculateLine38AmountYouOwe(ledger, taxYear);
        totalLiability += federalResults.liability;
        totalLiability += CalculateNorthCarolinaTaxLiabilityForYear(federalResults.taxableIncome);
        return totalLiability;
    }

    public static decimal CalculateNorthCarolinaTaxLiabilityForYear(decimal taxableIncomeFrom1040)
    {
        // todo: actually work through NC tax rules
        decimal totalLiability = 0M;
        // NC state income tax
        totalLiability += taxableIncomeFrom1040 * TaxConstants.NorthCarolinaFlatTaxRate;
        return totalLiability;
    }
}