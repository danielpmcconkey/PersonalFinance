using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Tax
{
    public static void AddRmdDistribution(TaxLedger ledger, LocalDateTime currentDate, decimal amount)
    {
        int year = currentDate.Year;
        if (ledger.RmdDistributions.ContainsKey(year))
        {
            ledger.RmdDistributions[year] += amount;
        }
        else ledger.RmdDistributions[year] = amount;
    }
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

        // todo: don't update the income target in the calc function
        UpdateIncomeTarget(ledger, taxYear);


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


        //Logger.Info($"Tax bill of {totalLiability.ToString("C")}");

        return totalLiability;
    }
    
    public static decimal? GetRmdRateByYear(int year)
    {
        if(!TaxConstants._rmdTable.TryGetValue(year, out var rmd))
            return null;
        return rmd;

    }
    public static void LogCapitalGain(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        ledger.CapitalGains.Add((earnedDate, amount));
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(earnedDate, amount, "Capital gain logged");
        }
    }
    public static void LogIncome(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        ledger.OrdinaryIncome.Add((earnedDate, amount));
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(earnedDate, amount, "Income logged");
        }
    }
    public static void LogInvestmentSale(TaxLedger ledger, LocalDateTime saleDate, McInvestmentPosition position,
        McInvestmentAccountType accountType)
    {
        switch(accountType)
        {
            case McInvestmentAccountType.ROTH_401_K:
            case McInvestmentAccountType.ROTH_IRA:
            case McInvestmentAccountType.HSA:
                // these are completely tax free
                break; 
            case McInvestmentAccountType.TAXABLE_BROKERAGE:
                // taxed on growth only
                LogCapitalGain(ledger, saleDate, position.CurrentValue - position.InitialCost);
                break;
            case McInvestmentAccountType.TRADITIONAL_401_K:
            case McInvestmentAccountType.TRADITIONAL_IRA:
                // tax deferred. everything is counted as income
                LogIncome(ledger, saleDate, position.CurrentValue);
                break;
            case McInvestmentAccountType.PRIMARY_RESIDENCE:
            case McInvestmentAccountType.CASH:
                // these should not be "sold"
                throw new InvalidDataException();
        }
    }
    public static void LogSocialSecurityIncome(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        ledger.SocialSecurityIncome.Add((earnedDate, amount));
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(earnedDate, amount, "Social security income logged");
        }
    }

    public static decimal MeetRmdRequirements(
        TaxLedger ledger, LocalDateTime currentDate, BookOfAccounts accounts, CurrentPrices prices)
    {
        if (accounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        
        var year = currentDate.Year;
        var rmdRate = GetRmdRateByYear(year);
        if (rmdRate is null)
        {
            // no requirement this year
            return 0M;
        } 

        var rate = (decimal)rmdRate;

        // get total balance in rmd-relevant accounts
        var relevantAccounts = accounts.InvestmentAccounts
            .Where(x => x.AccountType is McInvestmentAccountType.TRADITIONAL_401_K
                        || x.AccountType is McInvestmentAccountType.TRADITIONAL_IRA);
        var balance = 0M;
        foreach (var account in relevantAccounts)
            balance += Account.CalculateInvestmentAccountTotalValue(account);

        var totalRmdRequirement = balance / rate;
        if (!ledger.RmdDistributions.TryGetValue(year, out var totalRmdSoFar))
        {
            ledger.RmdDistributions[year] = 0;
            totalRmdSoFar = 0;
        }

        if (totalRmdSoFar >= totalRmdRequirement) return totalRmdSoFar;

        var amountLeft = totalRmdRequirement - totalRmdSoFar;

        // start with long-term investments as you're most likely to have them there
        var cashSold = Investment.SellInvestment(accounts, amountLeft,
            McInvestmentPositionType.LONG_TERM, currentDate, ledger, true);
        totalRmdSoFar += cashSold;
        if (MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(currentDate, cashSold, "RMD: Sold long-term investment");
        }

        // and invest it back into mid-term
        Investment.InvestFunds(accounts, currentDate, cashSold, McInvestmentPositionType.MID_TERM,
            McInvestmentAccountType.TAXABLE_BROKERAGE, prices);
        if (MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(currentDate, cashSold, "RMD: Bought mid-term investment");
        }

        amountLeft -= cashSold;
        if (amountLeft <= 0) return totalRmdSoFar;

        // try mid-term investments for remainder
        cashSold = Investment.SellInvestment(accounts, amountLeft,
            McInvestmentPositionType.MID_TERM, currentDate, ledger, true);
        totalRmdSoFar += cashSold;
        if (MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(currentDate, cashSold, "RMD: Sold mid-term investment");
        }

        // and invest it back into mid-term
        Investment.InvestFunds(accounts, currentDate, cashSold, McInvestmentPositionType.MID_TERM,
            McInvestmentAccountType.TAXABLE_BROKERAGE, prices);
        if (MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(currentDate, cashSold, "RMD: Bought mid-term investment");
        }

        amountLeft -= cashSold;
        if (amountLeft <= 0) return totalRmdSoFar;

        // nothing's left to try. not sure how we got here
        throw new InvalidDataException("RMD: Nothing left to try. Not sure how we got here");
    }

    /// <summary>
    /// sets the income target for next year based on this year's social
    /// security income
    /// </summary>
    public static void UpdateIncomeTarget(TaxLedger ledger, int year)
    {
        // update the income target for next year
        var ceiling = TaxConstants._incomeTaxBrackets[1].max;
        var expectedSocialSecurityIncome =
            CalculateTaxableSocialSecurityIncomeForYear(ledger, year);
        var expectedTaxableIncome = 
            expectedSocialSecurityIncome - TaxConstants._standardDeduction;
         ledger.IncomeTarget = ceiling - expectedTaxableIncome;
    }
    
}