using Lib.DataTypes;
using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Payday
{
    // todo: create a UT suite for Payday functions
    public static (BookOfAccounts bookOfAccounts, TaxLedger ledger, LifetimeSpend spend) ProcessSocialSecurityCheck(
        PgPerson person, LocalDateTime currentDate, BookOfAccounts bookOfAccounts, TaxLedger ledger, 
        LifetimeSpend lifetimeSpend, McModel simParams)
    {
        if (currentDate < simParams.SocialSecurityStart)
        {
            return (bookOfAccounts, ledger, lifetimeSpend);
        }
        
        // set up return tuple
        (BookOfAccounts bookOfAccounts, TaxLedger ledger, LifetimeSpend spend) results = (
            AccountCopy.CopyBookOfAccounts(bookOfAccounts),
            Tax.CopyTaxLedger(ledger),
            Spend.CopyLifetimeSpend(lifetimeSpend));
        
        // process the social security check
        var amount = person.AnnualSocialSecurityWage / 12m;
        results.bookOfAccounts = AccountCashManagement.DepositCash(results.bookOfAccounts, amount, currentDate);
        results.spend = Spend.RecordSocialSecurityWage(results.spend, amount, currentDate);
        results.ledger = Tax.RecordSocialSecurityIncome(results.ledger, currentDate, amount);
        
        if (!MonteCarloConfig.DebugMode) return results;
        Reconciliation.AddMessageLine(currentDate, amount, "Social Security check processed");
        return results;
    }
    public static (BookOfAccounts bookOfAccounts, TaxLedger ledger, LifetimeSpend spend) ProcessPreRetirementPaycheck(
        PgPerson person, LocalDateTime currentDate, BookOfAccounts bookOfAccounts, TaxLedger ledger, 
        LifetimeSpend lifetimeSpend, McModel simParams, CurrentPrices prices)
    {
        // todo: calculate pre and post tax 401k contributions and assign to the person (I think this is done)

        if (person.IsRetired) return (bookOfAccounts, ledger, lifetimeSpend);
        if (MonteCarloConfig.DebugMode)
        {
            Reconciliation.AddMessageLine(currentDate, 0, "Collecting paycheck");
        }
        
        // set up return tuple
        (BookOfAccounts bookOfAccounts, TaxLedger ledger, LifetimeSpend spend) result = (
            AccountCopy.CopyBookOfAccounts(bookOfAccounts),
            Tax.CopyTaxLedger(ledger),
            Simulation.CopyLifetimeSpend(lifetimeSpend));

        // income
        var grossMonthlyPay = (person.AnnualSalary + person.AnnualBonus) / 12m;
        if (MonteCarloConfig.DebugMode)
        {
            Reconciliation.AddMessageLine(currentDate, grossMonthlyPay, "gross income");
        }
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
        netMonthlyPay -= postTaxDeductions;

        // record final w2 income (gross, less pre-tax)
        result.ledger = Tax.RecordW2Income(result.ledger, currentDate, taxableMonthlyPay);

        // deposit the net pay
        result.bookOfAccounts = AccountCashManagement.DepositCash(result.bookOfAccounts, netMonthlyPay, currentDate);
        
        // add to savings accounts
        result.bookOfAccounts = AddRetirementSavings(person, currentDate, result.bookOfAccounts, simParams, prices);
        
        if (MonteCarloConfig.DebugMode)
        {
            Reconciliation.AddMessageLine(currentDate, 0, "processed paycheck");
        }
        
        return result;
    }

    public static BookOfAccounts AddRetirementSavings(
        PgPerson person, LocalDateTime currentDate, BookOfAccounts bookOfAccounts, McModel simParams, CurrentPrices prices)
    {
        // todo: UT AddRetirementSavings
        if (person.IsBankrupt || person.IsRetired) return bookOfAccounts;

        if (MonteCarloConfig.DebugMode)
        {
            Reconciliation.AddMessageLine(currentDate, 0, "Adding retirement savings");
        }
        
        var results = AccountCopy.CopyBookOfAccounts(bookOfAccounts);

        var roth401KAmount =
            person.Annual401KContribution * (1 - simParams.Percent401KTraditional) / 12m;
        var traditional401KAmount =
            person.Annual401KContribution * (simParams.Percent401KTraditional) / 12m;
        var monthly401KMatch = (person.AnnualSalary * person.Annual401KMatchPercent) / 12m;
        var taxDefferedAmount = traditional401KAmount + monthly401KMatch;
        var hsaAmount =
            (person.AnnualHsaContribution + person.AnnualHsaEmployerContribution) / 12m;

        results = Investment.InvestFunds(results, currentDate, roth401KAmount,
            McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.ROTH_401_K, prices);

        results = Investment.InvestFunds(
            results, currentDate, taxDefferedAmount,
            McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TRADITIONAL_401_K, prices);

        results = Investment.InvestFunds(
            results, currentDate, hsaAmount,
            McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.HSA, prices);

        results = Investment.InvestFunds(results, currentDate, monthly401KMatch,
            McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TRADITIONAL_401_K, prices);

        if (!StaticConfig.MonteCarloConfig.DebugMode) return results;
        
        Reconciliation.AddMessageLine(currentDate, roth401KAmount, "Roth 401k investment from paycheck");
        Reconciliation.AddMessageLine(currentDate, traditional401KAmount, "Traditional 401k investment from paycheck");
        Reconciliation.AddMessageLine(currentDate, monthly401KMatch, "Employer 401k match investment");
        Reconciliation.AddMessageLine(currentDate, hsaAmount, "HSA investment from paycheck");
        return results;
    }



    public static decimal DeductPostTax(PgPerson person, LocalDateTime currentDate)
    {
        var annual401KPostTax = person.Annual401KPostTax;
        var annualInsuranceDeductions = person.PostTaxInsuranceDeductions;
        var result = (annual401KPostTax + annualInsuranceDeductions) / 12m;
        if (MonteCarloConfig.DebugMode)
        {
            Reconciliation.AddMessageLine(currentDate, -result, "post tax paycheck deductions");
        }
        return result;
    }
    public static (LifetimeSpend spend, decimal amount) DeductPreTax(
        PgPerson person, LifetimeSpend spend, LocalDateTime currentDate)
    {
        // set up the return tuple
        (LifetimeSpend spend, decimal amount) result = (Simulation.CopyLifetimeSpend(spend), 0m);
        
        // calculate pre-tax deductions
        var annualPreTaxHealthDeductions = person.PreTaxHealthDeductions;
        var annualHsaContribution = person.AnnualHsaContribution;
        var annual401KPreTax = person.Annual401KPreTax;
        var preTaxDeductions =
            (annualPreTaxHealthDeductions + annualHsaContribution + annual401KPreTax) / 12m;
        
        result.spend =
            Spend.RecordHealthcareSpend(result.spend, annualPreTaxHealthDeductions, currentDate);
        
        if (MonteCarloConfig.DebugMode)
        {
            Reconciliation.AddMessageLine(currentDate, -result.amount, "pre tax paycheck deductions");
        }
        return result;
    }
    public static (TaxLedger ledger, decimal amount) WithholdTaxesFromPaycheck(
        PgPerson person, LocalDateTime currentDate, TaxLedger ledger, decimal grossMonthlyPay)
    {
        // set up return tuple
        (TaxLedger ledger, decimal amount) result = (Tax.CopyTaxLedger(ledger), 0m);
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
        result.ledger = Tax.RecordWithholdings(
            result.ledger, currentDate, federalWithholding, stateWithholding);
        result.ledger = Tax.RecordTaxPaid(
            result.ledger, currentDate, monthlyOasdi + monthlyMedicare);
        
        if (MonteCarloConfig.DebugMode)
        {
            Reconciliation.AddMessageLine(currentDate, -result.amount, "tax withholdings from paycheck");
        }
        return result;
    }
}