using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.TaxForms.Federal;
using Lib.MonteCarlo.TaxForms.NC;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class TaxCalculation
{
    /// <summary>
    /// calculates the amount left that we still have to take out. Once we know our total RMD requirement this year,
    /// this function looks at how much we've already taken out and tells us how much we STILL have to sell / withdraw
    /// </summary>
    public static (decimal amount, List<ReconciliationMessage> messages) CalculateAdditionalRmdSales(
        int year, decimal totalRmdRequirement, TaxLedger ledger, LocalDateTime currentDate)
    {
        // set up the return tuple
        (decimal amount, List<ReconciliationMessage> messages) results = (0m, []);
        
        // figure out how much we've already withdrawn
        var totalRmdSoFar = ledger.TaxableIraDistribution
            .Where(x => x.earnedDate.Year == year)
            .Sum(x => x.amount);
        
        if (totalRmdSoFar >= totalRmdRequirement) 
        {
            if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcileTaxCalcs) return results;
            results.messages.Add(new ReconciliationMessage(currentDate, null, 
                    $"RMD: no additional RMD sales needed ({totalRmdSoFar} previously sold this year)"));
            return results;
        }
        
        results.amount = totalRmdRequirement - totalRmdSoFar;

        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcileTaxCalcs) return results;
        results.messages.Add(new ReconciliationMessage(currentDate, 0, 
                $"RMD: additional RMD sales needed ({totalRmdRequirement - totalRmdSoFar})"));
        return results;
    }
    
    /// <summary>
    /// sets the income target for next year based on this year's social security income. the idea is that, below
    /// $96,950 in adjusted gross income, the tax on ordinary income is only 12%. Anything above that amount is taxed at
    /// 22%. Since long-term capital gains is only 15% (below 0.5MM), it's cheaper to receive income (tax deferred
    /// sales) than it is to receive capital gains (brokerage account sales). So we want to take out just enough to hit
    /// that 12% ceiling from our traditional accounts and then move over to capital gains accounts beyond that point.
    /// </summary>
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
    
    
    public static decimal CalculateRmdRequirement(TaxLedger ledger, BookOfAccounts accounts, int age)
    {
        
        // Logic taken from https://www.irs.gov/retirement-plans/retirement-plan-and-ira-required-minimum-distributions-faqs
        
        if (accounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        
        var rmdRate = TaxCalculation.CalculateRmdRateByAge(age);
        
        if (rmdRate <= 0) return 0;
        
        // calculate the total RMD requirement this year
        decimal totalRmdRequirement = 
            (accounts.InvestmentAccounts
                 .Where(x => x.AccountType is McInvestmentAccountType.TRADITIONAL_401_K
                             || x.AccountType is McInvestmentAccountType.TRADITIONAL_IRA) // all relevant accounts
                 .Sum(AccountCalculation.CalculateInvestmentAccountTotalValue) // the sum of their balances
             / (decimal)rmdRate); // the final amount we gotta withdraw

        return totalRmdRequirement;
    }

    public static decimal CalculateW2IncomeForYear(TaxLedger ledger, int year)
    {
        return ledger.W2Income
            .Where(x => x.earnedDate.Year == year)
            .Sum(x => x.amount);
    }

    private static int _minRmdAgeProvided = -1; // set it to -1 to notate that we haven't yet looked it up
    private static int _maxRmdAgeProvided = -1; // set it to -1 to notate that we haven't yet looked it up
    public static decimal CalculateRmdRateByAge(int age)
    {
        if (_minRmdAgeProvided < 0) _minRmdAgeProvided = TaxConstants.RmdTable.Min(x => x.age); 
        if (_maxRmdAgeProvided < 0) _maxRmdAgeProvided = TaxConstants.RmdTable.Max(x => x.age);
        
        if (age < _minRmdAgeProvided) return 0;
        if (age >= _maxRmdAgeProvided) 
            return TaxConstants.RmdTable.FirstOrDefault(x => x.age == _maxRmdAgeProvided).rate;

        return TaxConstants.RmdTable.FirstOrDefault(x => x.age == age).rate;
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
    public static (decimal amount, List<ReconciliationMessage> messages) CalculateTaxLiabilityForYear(
        TaxLedger ledger, int taxYear)
    {
        // set up return tuple
        (decimal amount, List<ReconciliationMessage> messages) result = (0m, []);
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcileTaxCalcs)
        {
            
        }

        decimal totalLiability = 0M;
        var form1040 = new Form1040(ledger, taxYear);
        totalLiability = form1040.CalculateTaxLiability();
        var stateLiability = CalculateNorthCarolinaTaxLiabilityForYear(
            ledger, taxYear, form1040.AdjustedGrossIncome);
        totalLiability += stateLiability;
        
        if (!MonteCarloConfig.DebugMode || MonteCarloConfig.ShouldReconcileTaxCalcs) return result;
        result.messages.Add(new ReconciliationMessage(
            null, null, "Calculating tax liability for year " + taxYear));
        result.messages.AddRange(form1040.ReconciliationMessages);
        result.messages.Add(new ReconciliationMessage(null, stateLiability, "State tax liability"));
        result.messages.Add(new ReconciliationMessage(null, totalLiability, "Total tax liability"));
        return result;
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