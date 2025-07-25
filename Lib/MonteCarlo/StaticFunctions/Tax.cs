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
            SocialSecurityIncome = ledger.SocialSecurityIncome,
            W2Income = ledger.W2Income,
            TaxableIraDistribution = ledger.TaxableIraDistribution,
            TaxableInterestReceived = ledger.TaxableInterestReceived,
            TaxFreeInterestPaid = ledger.TaxFreeInterestPaid,
            FederalWithholdings = ledger.FederalWithholdings,
            StateWithholdings = ledger.StateWithholdings,
            LongTermCapitalGains = ledger.LongTermCapitalGains,
            ShortTermCapitalGains = ledger.ShortTermCapitalGains,
            TotalTaxPaid = ledger.TotalTaxPaid,
        };
    }

    #endregion
    
    #region record functions
    
     
    public static TaxLedger RecordLongTermCapitalGain(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        var result = CopyTaxLedger(ledger);
        result.LongTermCapitalGains.Add((earnedDate, amount));
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(earnedDate, amount, "Long term capital gain logged");
        }
        return result;
    }
    public static TaxLedger RecordShortTermCapitalGain(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        var result = CopyTaxLedger(ledger);
        result.ShortTermCapitalGains.Add((earnedDate, amount));
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(earnedDate, amount, "Short term capital gain logged");
        }
        return result;
    }
    
    public static TaxLedger RecordW2Income(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        var result = CopyTaxLedger(ledger);
        result.W2Income.Add((earnedDate, amount));
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(earnedDate, amount, "Income logged");
        }
        return result;
    }
    public static TaxLedger RecordIraDistribution(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        var result = CopyTaxLedger(ledger);
        result.TaxableIraDistribution.Add((earnedDate, amount));
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(earnedDate, amount, "Taxable distribution logged");
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
                return RecordLongTermCapitalGain(ledger, saleDate, position.CurrentValue - position.InitialCost);
                break;
            case McInvestmentAccountType.TRADITIONAL_401_K:
            case McInvestmentAccountType.TRADITIONAL_IRA:
                // tax deferred. everything is counted as income
                return RecordIraDistribution(ledger, saleDate, position.CurrentValue);
                break;
            case McInvestmentAccountType.PRIMARY_RESIDENCE:
            case McInvestmentAccountType.CASH:
                // these should not be "sold"
                throw new InvalidDataException("Cannot sell cash or primary residence accounts");
        }
        throw new InvalidDataException("Unknown account type");
    }
    
    
    
    public static TaxLedger RecordSocialSecurityIncome(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        var result = CopyTaxLedger(ledger);
        result.SocialSecurityIncome.Add((earnedDate, amount));
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(earnedDate, amount, "Social security income logged");
        }

        return result;
    }

    
    #endregion record functions



    
    
   
    
    public static (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger) MeetRmdRequirements(
        TaxLedger ledger, LocalDateTime currentDate, BookOfAccounts accounts, int age)
    {
        if (accounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        
        var year = currentDate.Year;

        // figure out the RMD requirement
        var totalRmdRequirement = TaxCalculation.CalculateRmdRequirement(ledger, accounts, age);
        if (totalRmdRequirement <= 0) return (0M, accounts, ledger);
        
        // we have a withdrawal requirement. have we already met it?
        var amountLeft = TaxCalculation.CalculateAdditionalRmdSales(year, totalRmdRequirement, ledger, currentDate);
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


}