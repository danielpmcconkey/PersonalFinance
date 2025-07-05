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
    /// <summary>
    /// simply records the RMD distribution. Doesn't update any accounts. Only the ledger
    /// </summary>
    /// <returns>the new, updated ledger</returns>
    public static TaxLedger AddRmdDistribution(TaxLedger ledger, LocalDateTime currentDate, decimal amount)
    {
        var result = CopyTaxLedger(ledger);
        
        var year = currentDate.Year;
        if (!result.RmdDistributions.TryAdd(year, amount))
        {
            result.RmdDistributions[year] += amount;
        }

        return result;
    }
    
   
    
    
    public static decimal? GetRmdRateByYear(int year)
    {
        if(!TaxConstants._rmdTable.TryGetValue(year, out var rmd))
            return null;
        return rmd;

    }
    public static TaxLedger LogCapitalGain(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        var result = CopyTaxLedger(ledger);
        result.CapitalGains.Add((earnedDate, amount));
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(earnedDate, amount, "Capital gain logged");
        }
        return result;
    }
    public static TaxLedger LogIncome(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        var result = CopyTaxLedger(ledger);
        result.OrdinaryIncome.Add((earnedDate, amount));
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(earnedDate, amount, "Income logged");
        }
        return result;
    }
    public static TaxLedger LogInvestmentSale(TaxLedger ledger, LocalDateTime saleDate, McInvestmentPosition position,
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
                return LogCapitalGain(ledger, saleDate, position.CurrentValue - position.InitialCost);
                break;
            case McInvestmentAccountType.TRADITIONAL_401_K:
            case McInvestmentAccountType.TRADITIONAL_IRA:
                // tax deferred. everything is counted as income
                return LogIncome(ledger, saleDate, position.CurrentValue);
                break;
            case McInvestmentAccountType.PRIMARY_RESIDENCE:
            case McInvestmentAccountType.CASH:
                // these should not be "sold"
                throw new InvalidDataException("Cannot sell cash or primary residence accounts");
        }
        throw new InvalidDataException("Unknown account type");
    }
    public static void LogSocialSecurityIncome(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        ledger.SocialSecurityIncome.Add((earnedDate, amount));
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(earnedDate, amount, "Social security income logged");
        }
    }

    public static (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger) MeetRmdRequirements(
        TaxLedger ledger, LocalDateTime currentDate, BookOfAccounts accounts, CurrentPrices prices)
    {
        if (accounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        
        var year = currentDate.Year;
        var rmdRate = GetRmdRateByYear(year);
        if (rmdRate is null)
        {
            if (MonteCarloConfig.DebugMode == true)
            {
                Reconciliation.AddMessageLine(currentDate, 0, 
                    $"RMD: no RMD requirement this year");
            }
            return (0M, accounts, ledger);
        } 
        
        // set up the return tuple
        (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger) results = (
            0M, AccountCopy.CopyBookOfAccounts(accounts), Tax.CopyTaxLedger(ledger)
            );
        
        // calculate the total RMD requirement this year
        decimal totalRmdRequirement = 
            (accounts.InvestmentAccounts
                .Where(x => x.AccountType is McInvestmentAccountType.TRADITIONAL_401_K
                        || x.AccountType is McInvestmentAccountType.TRADITIONAL_IRA) // all relevant accounts
                .Sum(AccountCalculation.CalculateInvestmentAccountTotalValue) // the sum of their balances
                / (decimal)rmdRate); // the final amount we gotta withdraw

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
            return results; // no sales needed
        }

        var amountLeft = totalRmdRequirement - totalRmdSoFar;
        var localResult = InvestmentSales.SellInvestmentsToRmdAmount(
            amountLeft, results.newBookOfAccounts, results.newLedger, currentDate);
        
        results.amountSold = localResult.amountSold;
        results.newBookOfAccounts = localResult.newBookOfAccounts;
        results.newLedger = localResult.newLedger;
        
        if (MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(currentDate, results.amountSold, 
                $"RMD: Sold investment ({totalRmdSoFar} previously sold this year)");
        }
        
        return results;
    }

    /// <summary>
    /// sets the income target for next year based on this year's social
    /// security income
    /// </summary>
    public static TaxLedger UpdateIncomeTarget(TaxLedger ledger, int year)
    {
        var result = CopyTaxLedger(ledger);
        
        // update the income target for next year
        var ceiling = TaxConstants._incomeTaxBrackets[1].max;
        var expectedSocialSecurityIncome =
            TaxCalculation.CalculateTaxableSocialSecurityIncomeForYear(ledger, year);
        var expectedTaxableIncome = 
            expectedSocialSecurityIncome - TaxConstants._standardDeduction;
         result.IncomeTarget = ceiling - expectedTaxableIncome;
         
         return result;
    }
    
}