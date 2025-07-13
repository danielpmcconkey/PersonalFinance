using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Tax
{
    #region copy functions

    public static TaxLedger CopyTaxLedger(TaxLedger ledger)
    {
        return new TaxLedger()
        {
            CapitalGains = ledger.CapitalGains,
            IncomeTarget = ledger.IncomeTarget,
            OrdinaryIncome = ledger.OrdinaryIncome,
            RmdDistributions = ledger.RmdDistributions,
            SocialSecurityIncome = ledger.SocialSecurityIncome,
        };
    }

    #endregion
    
    #region record functions
    
     
    public static TaxLedger RecordCapitalGain(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        var result = CopyTaxLedger(ledger);
        result.CapitalGains.Add((earnedDate, amount));
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(earnedDate, amount, "Capital gain logged");
        }
        return result;
    }
    
    public static TaxLedger RecordIncome(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        var result = CopyTaxLedger(ledger);
        result.OrdinaryIncome.Add((earnedDate, amount));
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(earnedDate, amount, "Income logged");
        }
        return result;
    }
    
    public static TaxLedger RecordInvestmentSale(TaxLedger ledger, LocalDateTime saleDate, McInvestmentPosition position,
        McInvestmentAccountType accountType)
    {
        switch(accountType)
        {
            case McInvestmentAccountType.ROTH_401_K:
            case McInvestmentAccountType.ROTH_IRA:
            case McInvestmentAccountType.HSA:
                // these are completely tax free
                return ledger;
                break; 
            case McInvestmentAccountType.TAXABLE_BROKERAGE:
                // taxed on growth only
                return RecordCapitalGain(ledger, saleDate, position.CurrentValue - position.InitialCost);
                break;
            case McInvestmentAccountType.TRADITIONAL_401_K:
            case McInvestmentAccountType.TRADITIONAL_IRA:
                // tax deferred. everything is counted as income
                return RecordIncome(ledger, saleDate, position.CurrentValue);
                break;
            case McInvestmentAccountType.PRIMARY_RESIDENCE:
            case McInvestmentAccountType.CASH:
                // these should not be "sold"
                throw new InvalidDataException("Cannot sell cash or primary residence accounts");
        }
        throw new InvalidDataException("Unknown account type");
    }
    
    /// <summary>
    /// simply records the RMD distribution. Doesn't update any accounts. Only the ledger
    /// </summary>
    /// <returns>the new, updated ledger</returns>
    public static TaxLedger RecordRmdDistribution(TaxLedger ledger, LocalDateTime currentDate, decimal amount)
    {
        var result = CopyTaxLedger(ledger);
        
        var year = currentDate.Year;
        if (!result.RmdDistributions.TryAdd(year, amount))
        {
            result.RmdDistributions[year] += amount;
        }

        return result;
    }
    
    public static void RecordSocialSecurityIncome(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        ledger.SocialSecurityIncome.Add((earnedDate, amount));
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(earnedDate, amount, "Social security income logged");
        }
    }

    
    #endregion record functions



    // todo: move this to the TaxCalculation class after we build UTs for that class
    public static decimal CalculateAdditionalRmdSales(int year, decimal totalRmdRequirement, TaxLedger ledger, LocalDateTime currentDate)
    {
        // figure out how much we've already withdrawn
        if (!ledger.RmdDistributions.TryGetValue(year, out var totalRmdSoFar))
        {
            ledger.RmdDistributions[year] = 0;
            totalRmdSoFar = 0;
        }

        if (totalRmdSoFar >= totalRmdRequirement) 
        {
            if (MonteCarloConfig.DebugMode == true)
            {
                Reconciliation.AddMessageLine(currentDate, 0, 
                    $"RMD: no additional RMD sales needed ({totalRmdSoFar} previously sold this year)");
            }
            return 0; // no sales needed
        }
        
        if (MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(currentDate, 0, 
                $"RMD: additional RMD sales needed ({totalRmdRequirement - totalRmdSoFar})");
        }
        return totalRmdRequirement - totalRmdSoFar;
    }
// todo: move this to the TaxCalculation class after we build UTs for that class
    public static decimal CalculateRmdRequirement(TaxLedger ledger, LocalDateTime currentDate, BookOfAccounts accounts)
    {
        
        // Logic taken from https://www.irs.gov/retirement-plans/retirement-plan-and-ira-required-minimum-distributions-faqs
        
        if (accounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        
        var year = currentDate.Year;
        var rmdRate = TaxCalculation.CalculateRmdRateByYear(year);
        if (rmdRate is null)
        {
            if (MonteCarloConfig.DebugMode == true)
            {
                Reconciliation.AddMessageLine(currentDate, 0, 
                    $"RMD: no RMD requirement this year");
            }
            return 0M;
        } 
        
        
        
        // calculate the total RMD requirement this year
        decimal totalRmdRequirement = 
            (accounts.InvestmentAccounts
                 .Where(x => x.AccountType is McInvestmentAccountType.TRADITIONAL_401_K
                             || x.AccountType is McInvestmentAccountType.TRADITIONAL_IRA) // all relevant accounts
                 .Sum(AccountCalculation.CalculateInvestmentAccountTotalValue) // the sum of their balances
             / (decimal)rmdRate); // the final amount we gotta withdraw

        return totalRmdRequirement;
    }
    
   
    
    public static (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger) MeetRmdRequirements(
        TaxLedger ledger, LocalDateTime currentDate, BookOfAccounts accounts, CurrentPrices prices)
    {
        if (accounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        
        var year = currentDate.Year;

        // figure out the RMD requirement
        var totalRmdRequirement = CalculateRmdRequirement(ledger, currentDate, accounts);
        if (totalRmdRequirement <= 0) return (0M, accounts, ledger);
        
        // we have a withdrawal requirement. have we already met it?
        var amountLeft = CalculateAdditionalRmdSales(year, totalRmdRequirement, ledger, currentDate);
        if (amountLeft <= 0) return (0M, accounts, ledger);
        
        
        // we gotta go sellin' shit
        // set up the return tuple
        (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger) results = (
            0M, AccountCopy.CopyBookOfAccounts(accounts), Tax.CopyTaxLedger(ledger)
        );

        var localResult = InvestmentSales.SellInvestmentsToRmdAmount(
            amountLeft, results.newBookOfAccounts, results.newLedger, currentDate);
        
        results.amountSold = localResult.amountSold;
        results.newBookOfAccounts = localResult.newBookOfAccounts;
        results.newLedger = localResult.newLedger;
        
        if (MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(currentDate, results.amountSold, 
                $"RMD: Sold investment to meet RMD requirement");
        }
        
        return results;
    }

    /// <summary>
    /// sets the income target for next year based on this year's social security income. the idea is that, below
    /// $96,950 in adjusted gross income, the tax on ordinary income is only 12%. Anything above that amount is taxed at
    /// 22%. Since long-term capital gains is only 15% (below 0.5MM), it's cheaper to receive income (tax deferred
    /// sales) than it is to receive capital gains (brokerage account sales). So we want to take out just enough to hit
    /// that 12% ceiling from our traditional accounts and then move over to capital gains accounts beyond that point.
    /// </summary>
    /// todo: vet out this logic (and all other tax calcs) 
    public static TaxLedger UpdateIncomeTarget(TaxLedger ledger, int year)
    {
        var result = CopyTaxLedger(ledger);

        // update the income target for next year
        var ceiling = TaxConstants._incomeTaxBrackets[1].max;
        
        // add up all income and multiplies by the max percent (85%) that is taxable
        var expectedTaxableIncome =
            TaxCalculation.CalculateTaxableSocialSecurityIncomeForYear(ledger, year); 
        // take out the standard deduction
        expectedTaxableIncome =
            expectedTaxableIncome - TaxConstants._standardDeduction;
        // don't let it go below zero
        expectedTaxableIncome = Math.Max(expectedTaxableIncome, 0); 

        // calculate the amount of actual income room we'll have next year before we'll jump tax brackets
        result.IncomeTarget = Math.Max(ceiling - expectedTaxableIncome, 0); // don't let this go below zero

        return result;
    }

}