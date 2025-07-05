using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;

namespace Lib.MonteCarlo.StaticFunctions;

public static class TaxCalculation
{
    public static decimal CalculateCapitalGainsForYear(TaxLedger ledger, int year)
    {
        return ledger.CapitalGains
            .Where(x => x.earnedDate.Year == year)
            .Sum(x => x.amount);
    }
     
    public static decimal CalculateEarnedIncomeForYear(TaxLedger ledger, int year)
    {
        return 
            CalculateOrdinaryIncomeForYear(ledger, year) +
            CalculateTaxableSocialSecurityIncomeForYear(ledger, year) -
            TaxConstants._standardDeduction;
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
    public static decimal CalculateIncomeRoom(TaxLedger ledger, int year)
    {
        var room = ledger.IncomeTarget -
                   CalculateOrdinaryIncomeForYear(ledger, year) -
                   CalculateCapitalGainsForYear(ledger, year)
            ;
        return Math.Max(room, 0);
    }
    public static decimal CalculateOrdinaryIncomeForYear(TaxLedger ledger, int year)
    {
        return ledger.OrdinaryIncome
            .Where(x => x.earnedDate.Year == year)
            .Sum(x => x.amount);
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

    public static decimal CalculateTaxLiabilityForYear(TaxLedger ledger, int taxYear)
    {
        var earnedIncome = CalculateEarnedIncomeForYear(ledger, taxYear);
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(null, earnedIncome, $"Earned income calculated for tax year {taxYear}");
        }

        var totalCapitalGains = CalculateCapitalGainsForYear(ledger, taxYear);
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(null, earnedIncome, $"Total capital gains calculated for tax year {taxYear}");
        }



        decimal totalLiability = 0M;

        // tax on ordinary income
        foreach (var bracket in TaxConstants._incomeTaxBrackets)
        {
            var amountInBracket =
                    earnedIncome
                    - (Math.Max(earnedIncome, bracket.max) - bracket.max) // amount above max
                    - (Math.Min(earnedIncome, bracket.min)) // amount below min
                ;
            totalLiability += (amountInBracket * bracket.rate);
        }

        // tax on capital gains
        if (earnedIncome + totalCapitalGains < TaxConstants._capitalGainsBrackets[0].max)
        {
            // you have 0 capital gains to pay. It stacks on top of earned
            // income but still comes out less than the 0% max
        }
        else if (earnedIncome < TaxConstants._capitalGainsBrackets[0].max)
        {
            // the difference between your earned income and the free
            // bracket max is free. the rest is charged at normal capital
            // gains rates

            var bracket1 = TaxConstants._capitalGainsBrackets[1];
            var bracket2 = TaxConstants._capitalGainsBrackets[2];

            var totalRevenue = earnedIncome + totalCapitalGains;
            // any of totalRevenue above 583,750 is taxed at 20%
            var amountAtBracket2 = Math.Max(0, totalRevenue - bracket2.min);
            totalLiability += (amountAtBracket2 * bracket2.rate);

            // any of totalRevenue above 94,050 but below 583,750 is taxed at 15%
            var amountAtBracket1 = Math.Max(0, totalRevenue - bracket1.min - amountAtBracket2);
            totalLiability += (amountAtBracket1 * bracket1.rate);
        }
        else
        {
            // there is no free bracket. Everything below the bracket 1 max
            // is taxed at the bracket 1 rate
            var bracket1 = TaxConstants._capitalGainsBrackets[1];
            var amountInBracket1 =
                    totalCapitalGains
                    - (Math.Max(totalCapitalGains, bracket1.max) - bracket1.max) // amount above max
                ;
            totalLiability += (amountInBracket1 * bracket1.rate);
            var bracket2 = TaxConstants._capitalGainsBrackets[2];
            var amountInBracket2 =
                    totalCapitalGains
                    - (Math.Max(totalCapitalGains, bracket2.max) - bracket2.max) // amount above max
                    - (Math.Min(totalCapitalGains, bracket2.min)) // amount below min
                ;
            totalLiability += (amountInBracket2 * bracket2.rate);
        }

        // NC state income tax
        totalLiability += earnedIncome * TaxConstants._ncFiatTaxRate;
        totalLiability += totalCapitalGains * TaxConstants._ncFiatTaxRate;
        

        return totalLiability;
    }
}