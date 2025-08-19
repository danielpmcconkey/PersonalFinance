//#define PERFORMANCEPROFILING
using System.Diagnostics;
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
            TotalTaxPaidLifetime = ledger.TotalTaxPaidLifetime,
            TaxFreeWithrawals = ledger.TaxFreeWithrawals,
        };
    }

    #endregion
    
    #region record functions
    
     
    public static (TaxLedger ledger, List<ReconciliationMessage> messages) RecordLongTermCapitalGain(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        (TaxLedger ledger, List<ReconciliationMessage> messages) result = (CopyTaxLedger(ledger), []);
        result.ledger.LongTermCapitalGains.Add((earnedDate, amount));
        if (!MonteCarloConfig.DebugMode) return result;
        result.messages.Add(new ReconciliationMessage(earnedDate, amount, "Long term capital gain logged"));
        result.messages.Add(new ReconciliationMessage(earnedDate, amount, "Long term capital gain logged"));
        return result;
    }
    public static (TaxLedger ledger, List<ReconciliationMessage> messages) RecordShortTermCapitalGain(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        (TaxLedger ledger, List<ReconciliationMessage> messages) result = (CopyTaxLedger(ledger), []);
        result.ledger.ShortTermCapitalGains.Add((earnedDate, amount));
        if (!MonteCarloConfig.DebugMode) return result;
        result.messages.Add(new ReconciliationMessage(earnedDate, amount, "Short term capital gain logged"));
        return result;
    }
    
    public static (TaxLedger ledger, List<ReconciliationMessage> messages) RecordW2Income(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        (TaxLedger ledger, List<ReconciliationMessage> messages) result = (CopyTaxLedger(ledger), []);
        result.ledger.W2Income.Add((earnedDate, amount));
        if (!MonteCarloConfig.DebugMode) return result;
        result.messages.Add(new ReconciliationMessage(earnedDate, amount, "Income logged"));
        return result;
    }
    public static (TaxLedger ledger, List<ReconciliationMessage> messages) RecordIraDistribution(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        (TaxLedger ledger, List<ReconciliationMessage> messages) result = (CopyTaxLedger(ledger), []);
        result.ledger.TaxableIraDistribution.Add((earnedDate, amount));
        if (!MonteCarloConfig.DebugMode) return result;
        result.messages.Add(new ReconciliationMessage(earnedDate, amount, "Taxable distribution logged"));
        return result;
    }
    
    public static (TaxLedger ledger, List<ReconciliationMessage> messages) RecordInvestmentSale(TaxLedger ledger, LocalDateTime saleDate, McInvestmentPosition position,
        McInvestmentAccountType accountType)
    {
        // todo: add a UT to RecordInvestmentSale for tax free withdrawals
        return accountType switch
        {
            McInvestmentAccountType.ROTH_401_K or McInvestmentAccountType.ROTH_IRA or McInvestmentAccountType.HSA =>
                // these are completely tax free
                RecordTaxFreeWithdrawal(ledger, saleDate, position.CurrentValue),
            McInvestmentAccountType.TAXABLE_BROKERAGE =>
                // taxed on growth only
                RecordLongTermCapitalGain(ledger, saleDate, position.CurrentValue - position.InitialCost),
            McInvestmentAccountType.TRADITIONAL_401_K or McInvestmentAccountType.TRADITIONAL_IRA =>
                // tax deferred. everything is counted as income
                RecordIraDistribution(ledger, saleDate, position.CurrentValue),
            McInvestmentAccountType.PRIMARY_RESIDENCE or McInvestmentAccountType.CASH =>
                // these should not be "sold"
                throw new InvalidDataException("Cannot sell cash or primary residence accounts"),
            _ => throw new InvalidDataException("Unknown account type")
        };
    }
    
    public static (TaxLedger ledger, List<ReconciliationMessage> messages) RecordTaxFreeWithdrawal(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        // todo: UT RecordTaxFreeWithdrawal
        if (amount <= 0) return (ledger, []);
        (TaxLedger ledger, List<ReconciliationMessage> messages) result = (CopyTaxLedger(ledger), []);
        result.ledger.TaxFreeWithrawals.Add((earnedDate, amount));
        if (!MonteCarloConfig.DebugMode) return result;
        result.messages.Add(new ReconciliationMessage(earnedDate, amount, "Tax free withdrawal logged"));
        return result;
    }
    
    public static (TaxLedger ledger, List<ReconciliationMessage> messages) RecordTaxPaid(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        (TaxLedger ledger, List<ReconciliationMessage> messages) result = (CopyTaxLedger(ledger), []);
        result.ledger.TotalTaxPaidLifetime += amount;
        if (!MonteCarloConfig.DebugMode) return result;
        result.messages.Add(new ReconciliationMessage(earnedDate, amount, "Tax payment logged"));
        return result;
    }
    
    public static (TaxLedger ledger, List<ReconciliationMessage> messages) RecordWithholdings(
        TaxLedger ledger, LocalDateTime earnedDate, decimal amountFed, decimal amountState)
    {
        (TaxLedger ledger, List<ReconciliationMessage> messages) result = (CopyTaxLedger(ledger), []);
        
        result.ledger.FederalWithholdings.Add((earnedDate, amountFed));
        result.ledger.StateWithholdings.Add((earnedDate, amountState));
        if (!MonteCarloConfig.DebugMode) return result;
        result.messages.Add(new ReconciliationMessage(earnedDate, -amountFed, "Federal withholding logged"));
        result.messages.Add(new ReconciliationMessage(earnedDate, -amountState, "State withholding logged"));
        return result;
    }
    
    public static (TaxLedger ledger, List<ReconciliationMessage> messages) RecordSocialSecurityIncome(TaxLedger ledger, LocalDateTime earnedDate, decimal amount)
    {
        (TaxLedger ledger, List<ReconciliationMessage> messages) result = (CopyTaxLedger(ledger), []);
        result.ledger.SocialSecurityIncome.Add((earnedDate, amount));
        if (!MonteCarloConfig.DebugMode) return result;
        result.messages.Add(new ReconciliationMessage(earnedDate, amount, "Social security income logged"));
        return result;
    }

    
    #endregion record functions



    
    
   
    
    public static (BookOfAccounts newBookOfAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages) 
        MeetRmdRequirements(TaxLedger ledger, LocalDateTime currentDate, BookOfAccounts accounts, int age)
    {
        if (accounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        
        // set up return tuple
        (BookOfAccounts newBookOfAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages) results = 
            (accounts, ledger, []);
            
        var year = currentDate.Year;

        // figure out the RMD requirement
        var totalRmdRequirement = TaxCalculation.CalculateRmdRequirement(ledger, accounts, age);
        if (totalRmdRequirement <= 0) return (accounts, ledger, []);
        
        // we have a withdrawal requirement. have we already met it?
        var amountLeftCalcResult = TaxCalculation.CalculateAdditionalRmdSales(year, totalRmdRequirement, ledger, currentDate);
        var amountLeft = amountLeftCalcResult.amount;
        results.messages.AddRange(amountLeftCalcResult.messages);
        if (amountLeft <= 0) return results;
    
        // we gotta go sellin' shit
        // copy the accounts and ledger before we modify them
        results.newBookOfAccounts = AccountCopy.CopyBookOfAccounts(accounts);
        results.newLedger = Tax.CopyTaxLedger(ledger);

        var localResult = InvestmentSales.SellInvestmentsToRmdAmount(
            amountLeft, results.newBookOfAccounts, results.newLedger, currentDate);
        
        results.newBookOfAccounts = localResult.newBookOfAccounts;
        results.newLedger = localResult.newLedger;
     
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.AddRange(localResult.messages);
        results.messages.Add(new ReconciliationMessage(currentDate, localResult.amountSold, 
            "RMD: Total investment sold to meet RMD requirement"));
        
        return results;
    }


}