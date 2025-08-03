using Lib.DataTypes;
using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Payday
{
    // todo: create a UT suite for Payday functions
    public static (BookOfAccounts bookOfAccounts, TaxLedger ledger, LifetimeSpend spend, List<ReconciliationMessage> messages) 
        ProcessSocialSecurityCheck(PgPerson person, LocalDateTime currentDate, BookOfAccounts bookOfAccounts,
            TaxLedger ledger, LifetimeSpend lifetimeSpend, McModel simParams)
    {
        if (currentDate < simParams.SocialSecurityStart)
        {
            return (bookOfAccounts, ledger, lifetimeSpend, []);
        }
        
        // set up return tuple
        (BookOfAccounts bookOfAccounts, TaxLedger ledger, LifetimeSpend spend, List<ReconciliationMessage> messages)
            results = (
                AccountCopy.CopyBookOfAccounts(bookOfAccounts),
                Tax.CopyTaxLedger(ledger),
                Spend.CopyLifetimeSpend(lifetimeSpend),
                []);
        
        // process the social security check
        var amount = person.AnnualSocialSecurityWage / 12m;
        var deposit = AccountCashManagement.DepositCash(results.bookOfAccounts, amount, currentDate);
        results.bookOfAccounts = deposit.accounts;
        
        var recordSsWage = Spend.RecordSocialSecurityWage(results.spend, amount, currentDate);
        results.spend = recordSsWage.spend;
        
        var reccordSsTax = Tax.RecordSocialSecurityIncome(results.ledger, currentDate, amount);
        results.ledger = reccordSsTax.ledger;
        
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.AddRange(deposit.messages);
        results.messages.AddRange(recordSsWage.messages);
        results.messages.AddRange(reccordSsTax.messages);
        results.messages.Add(new ReconciliationMessage(currentDate, amount, "Social Security wage"));
        return results;
    }
    public static (BookOfAccounts bookOfAccounts, TaxLedger ledger, LifetimeSpend spend, List<ReconciliationMessage> messages)
        ProcessPreRetirementPaycheck(PgPerson person, LocalDateTime currentDate, BookOfAccounts bookOfAccounts, 
            TaxLedger ledger, LifetimeSpend lifetimeSpend, McModel simParams, CurrentPrices prices)
    {
        // todo: calculate pre and post tax 401k contributions and assign to the person (I think this is done)

        if (person.IsRetired) return (bookOfAccounts, ledger, lifetimeSpend, []);
        // set up return tuple
        (BookOfAccounts bookOfAccounts, TaxLedger ledger, LifetimeSpend spend, List<ReconciliationMessage> messages) 
            result = (
                AccountCopy.CopyBookOfAccounts(bookOfAccounts),
                Tax.CopyTaxLedger(ledger),
                Spend.CopyLifetimeSpend(lifetimeSpend),
                []);
        
        

        // income
        var grossMonthlyPay = (person.AnnualSalary + person.AnnualBonus) / 12m;
        var netMonthlyPay = grossMonthlyPay;
        var taxableMonthlyPay = grossMonthlyPay;

        // withholdings
        var withholding = WithholdTaxesFromPaycheck(person, currentDate, ledger, grossMonthlyPay);
        result.ledger = withholding.ledger;
        netMonthlyPay -= withholding.amount;

        // pre-tax deductions
        var preTaxDeductions = DeductPreTax(person, lifetimeSpend, currentDate);
        result.spend = preTaxDeductions.spend;
        netMonthlyPay -= preTaxDeductions.amount;
        taxableMonthlyPay -= preTaxDeductions.amount;

        // post-tax deductions
        var postTaxDeductions = DeductPostTax(person, currentDate);
        netMonthlyPay -= postTaxDeductions.amount;

        // record final w2 income (gross, less pre-tax)
        var w2Record = Tax.RecordW2Income(result.ledger, currentDate, taxableMonthlyPay);
        result.ledger = w2Record.ledger;

        // deposit the net pay
        var depositResult= AccountCashManagement.DepositCash(
            result.bookOfAccounts, netMonthlyPay, currentDate);
        result.bookOfAccounts = depositResult.accounts;
        
        // add to savings accounts
        var savingsResult = AddPaycheckRelatedRetirementSavings(
            person, currentDate, result.bookOfAccounts, simParams, prices);
        result.bookOfAccounts = savingsResult.accounts;

        if (!MonteCarloConfig.DebugMode) return result;
        result.messages.Add(new ReconciliationMessage(currentDate, grossMonthlyPay, "Gross monthly paycheck"));
        result.messages.AddRange(withholding.messages);
        result.messages.AddRange(preTaxDeductions.messages);
        result.messages.AddRange(postTaxDeductions.messages);
        result.messages.Add(new ReconciliationMessage(currentDate, netMonthlyPay, "Net monthly paycheck"));
        result.messages.AddRange(w2Record.messages);
        result.messages.AddRange(depositResult.messages);
        result.messages.AddRange(savingsResult.messages);
        return result;
    }

    // todo: need to UT AddRetirementSavings
    /// <summary>
    /// This is for pre-retirement use only. Used for investing pay-check-related stuff like 401K and HSA. It's presumed
    /// that the cash that funds these purchases was already deducted from your paycheck (or is free, like the match)  
    /// </summary>
    public static (BookOfAccounts accounts, List<ReconciliationMessage> messages) AddPaycheckRelatedRetirementSavings(
        PgPerson person, LocalDateTime currentDate, BookOfAccounts bookOfAccounts, McModel simParams, CurrentPrices prices)
    {
        if (person.IsBankrupt || person.IsRetired) return (bookOfAccounts, []);

        (BookOfAccounts accounts, List<ReconciliationMessage> messages) results = (
            AccountCopy.CopyBookOfAccounts(bookOfAccounts), []);
        
        if (MonteCarloConfig.DebugMode) results.messages.Add(new ReconciliationMessage(
                currentDate, null, "Adding pay-check-related retirement savings"));
        
        

        var roth401KAmount =
            person.Annual401KPostTax / 12m;
        var traditional401KAmount =
            person.Annual401KPreTax / 12m;
        var monthly401KMatch = (person.AnnualSalary * person.Annual401KMatchPercent) / 12m;
        var hsaAmount =
            (person.AnnualHsaContribution + person.AnnualHsaEmployerContribution) / 12m;

        var investRothResults = Investment.InvestFunds(results.accounts, currentDate, roth401KAmount,
            McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.ROTH_401_K, prices);
        results.accounts = investRothResults.accounts;

        var invest401KResults = Investment.InvestFunds(
            results.accounts, currentDate, traditional401KAmount,
            McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TRADITIONAL_401_K, prices);
        results.accounts = invest401KResults.accounts;

        var investHsaResults = Investment.InvestFunds(
            results.accounts, currentDate, hsaAmount, McInvestmentPositionType.LONG_TERM,
            McInvestmentAccountType.HSA, prices);
        results.accounts = investHsaResults.accounts;

        var investMatchResults = Investment.InvestFunds(
            results.accounts, currentDate, monthly401KMatch, McInvestmentPositionType.LONG_TERM,
            McInvestmentAccountType.TRADITIONAL_401_K, prices);
        results.accounts = investMatchResults.accounts;
        

        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.AddRange(investRothResults.messages);
        results.messages.Add(new ReconciliationMessage(currentDate, roth401KAmount, "Roth 401k investment from paycheck"));
        results.messages.AddRange(invest401KResults.messages);
        results.messages.Add(new ReconciliationMessage(currentDate, traditional401KAmount, "Traditional 401k investment from paycheck"));
        results.messages.AddRange(investHsaResults.messages);
        results.messages.Add(new ReconciliationMessage(currentDate, hsaAmount, "HSA investment from paycheck"));
        results.messages.AddRange(investMatchResults.messages);
        results.messages.Add(new ReconciliationMessage(currentDate, monthly401KMatch, "Employer 401k match investment"));
        results.messages.Add(new ReconciliationMessage(currentDate, null, "Done adding pay-check-related retirement savings"));
        return results;
    }


    // todo: unit test DeductPostTax
    public static (decimal amount, List<ReconciliationMessage> messages) DeductPostTax(PgPerson person, LocalDateTime currentDate)
    {
        var annual401KPostTax = person.Annual401KPostTax;
        var annualInsuranceDeductions = person.PostTaxInsuranceDeductions;
        var result = (annual401KPostTax + annualInsuranceDeductions) / 12m;
        if (!MonteCarloConfig.DebugMode) return (result, []);
        List<ReconciliationMessage> messages = [];
        messages.Add(new ReconciliationMessage(currentDate, -annual401KPostTax / 12, "Post tax 401k contribution"));
        messages.Add(new ReconciliationMessage(currentDate, -annualInsuranceDeductions / 12, "Post tax insurance deductions"));
        messages.Add(new ReconciliationMessage(currentDate, -result, "Total post tax paycheck deductions"));
        return (result, messages);
    }
    // todo: unit test DeductPreTax
    public static (LifetimeSpend spend, decimal amount, List<ReconciliationMessage> messages) DeductPreTax(
        PgPerson person, LifetimeSpend spend, LocalDateTime currentDate)
    {
        // set up the return tuple
        (LifetimeSpend spend, decimal amount, List<ReconciliationMessage> messages) result = (
            Spend.CopyLifetimeSpend(spend), 0m, []);
        
        // calculate pre-tax deductions
        var annualPreTaxHealthDeductions = person.PreTaxHealthDeductions;
        var annualHsaContribution = person.AnnualHsaContribution;
        var annual401KPreTax = person.Annual401KPreTax;
        var preTaxDeductions =
            (annualPreTaxHealthDeductions + annualHsaContribution + annual401KPreTax) / 12m;
        result.amount = preTaxDeductions;
        
        // record the health spend
        var recordHealthSpend =
            Spend.RecordHealthcareSpend(result.spend, annualPreTaxHealthDeductions, currentDate);
        result.spend = recordHealthSpend.spend;
        
        if (!MonteCarloConfig.DebugMode) return result;
        result.messages.AddRange(recordHealthSpend.messages);
        result.messages.Add(new ReconciliationMessage(currentDate, -annualPreTaxHealthDeductions / 12m, "Pre-tax health deduction"));
        result.messages.Add(new ReconciliationMessage(currentDate, -annualHsaContribution / 12m, "Pre-tax HSA contribution"));
        result.messages.Add(new ReconciliationMessage(currentDate, -annual401KPreTax / 12m, "Pre-tax 401K contribution"));
        result.messages.Add(new ReconciliationMessage(currentDate, -preTaxDeductions, "Total pre tax paycheck deductions"));
        return result;
    }
    public static (TaxLedger ledger, decimal amount, List<ReconciliationMessage> messages) WithholdTaxesFromPaycheck(
        PgPerson person, LocalDateTime currentDate, TaxLedger ledger, decimal grossMonthlyPay)
    {
        // set up return tuple
        (TaxLedger ledger, decimal amount, List<ReconciliationMessage> messages) result = (
            Tax.CopyTaxLedger(ledger), 0m, []);
        // calculate taxes withheld
        var federalWithholding = person.FederalAnnualWithholding / 12m;
        var stateWithholding = person.StateAnnualWithholding / 12m;
        var monthlyOasdi = (Math.Min(
                               TaxConstants.OasdiBasePercent * (grossMonthlyPay * 12m),
                               TaxConstants.OasdiMax))
                           / 12m;
        var annualStandardMedicare = TaxConstants.StandardMedicareTaxRate * grossMonthlyPay;
        var amountOfSalaryOverMedicareThreshold =
            (grossMonthlyPay * 12) - TaxConstants.AdditionalMedicareThreshold;
        var annualAdditionalMedicare =
            TaxConstants.AdditionalMedicareTaxRate * amountOfSalaryOverMedicareThreshold;
        var annualTotalMedicare = annualStandardMedicare + annualAdditionalMedicare;
        var monthlyMedicare = annualTotalMedicare / 12m;
        
        result.amount = (federalWithholding + stateWithholding + monthlyOasdi + monthlyMedicare);
        
        // record the taxes withheld
        var recordWithholding = Tax.RecordWithholdings(
            result.ledger, currentDate, federalWithholding, stateWithholding);
        result.ledger = recordWithholding.ledger;
        
        var recordPaid = Tax.RecordTaxPaid(
            result.ledger, currentDate, monthlyOasdi + monthlyMedicare + federalWithholding + stateWithholding);
        result.ledger = recordPaid.ledger;
        
        if (!MonteCarloConfig.DebugMode) return result;
        result.messages.AddRange(recordWithholding.messages);
        result.messages.AddRange(recordPaid.messages);
        result.messages.Add(new ReconciliationMessage(currentDate, -result.amount, "Total tax withholdings from paycheck"));
        return result;
    }
}