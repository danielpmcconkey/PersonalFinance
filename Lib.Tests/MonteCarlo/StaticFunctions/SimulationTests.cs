using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.DataTypes;
using Lib.DataTypes.Postgres;
using Lib.MonteCarlo.TaxForms.Federal;
using Lib.MonteCarlo.TaxForms.NC;
using Lib.StaticConfig;
using Model = Lib.DataTypes.MonteCarlo.Model;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class SimulationTests
{
    private readonly LocalDateTime _testDate = new LocalDateTime(2025, 1, 1, 12, 0);
    private readonly PgPerson _testPerson;
    private readonly Model _model;
    //private readonly BookOfAccounts _accounts;
    private readonly TaxLedger _ledger;
    private readonly LifetimeSpend _spend;
    private readonly CurrentPrices _prices;

    public SimulationTests()
    {
        // Initialize test data
        _testPerson = TestDataManager.CreateTestPerson();
        _model = CreateTestModel();
        //_accounts = new BookOfAccounts();
        _ledger = new TaxLedger();
        _spend = new LifetimeSpend();
        _prices = new CurrentPrices();
    }

    private Model CreateTestModel()
    {
        var model = TestDataManager.CreateTestModel();
        model.RetirementDate = _testDate.PlusYears(5);
        model.SocialSecurityStart = _testDate.PlusYears(10);
        return model;
    }

    [Fact]
    public void IsReconciliationPeriod_WithinRange_ReturnsTrue()
    {
        // Arrange
        
        var testDate = MonteCarloConfig.ReconciliationSimStartDate.PlusDays(1);
        MonteCarloConfig.DebugMode = true;

        // Act
        var result = Simulation.IsReconciliationPeriod(testDate);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsReconciliationPeriod_OutsideRange_ReturnsFalse()
    {
        // Arrange
        var testDate = MonteCarloConfig.ReconciliationSimEndDate.PlusDays(1);
        MonteCarloConfig.DebugMode = true;

        // Act
        var result = Simulation.IsReconciliationPeriod(testDate);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void PayForStuff_WithSufficientFunds_Succeeds()
    {
        // Arrange
        var recessionStats = new RecessionStats();
        var initialBalance = 10000m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts = AccountCashManagement.DepositCash(accounts, initialBalance, _testDate).accounts;

        // Act
        var result = Simulation.PayForStuff(
            _model, _testPerson, _testDate, recessionStats, _ledger, _spend, accounts);

        // Assert
        Assert.True(result.isSuccessful);
    }
    
    [Fact]
    public void PayForStuff_OnlySpendsRequiredAndFun()
    {
        // Arrange
        var model = CreateTestModel();
        model.DesiredMonthlySpendPostRetirement = 800m;
        var person = TestDataManager.CreateTestPerson();
        person.RequiredMonthlySpendHealthCare = 500m;
        person.RequiredMonthlySpend = 700m;
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(60);
        var currentDate = person.BirthDate.PlusYears(64); // post retirement, pre-medicare
        var recessionStats = new RecessionStats();
        recessionStats.AreWeInARecession = false;
        recessionStats.AreWeInExtremeAusterityMeasures = false;
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var spend = TestDataManager.CreateEmptySpend();
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var initialCash = 10000m;
        accounts = AccountCashManagement.DepositCash(accounts, initialCash, currentDate).accounts;
        var expectedSpend = // 2000, 
            person.RequiredMonthlySpend
            + person.RequiredMonthlySpendHealthCare
            + Spend.CalculateMonthlyFunSpend(model, person, currentDate);
        var expectedCash = initialCash - expectedSpend;
        // Act
        var (isSuccessful, newAccounts, newLedger, newSpend, messages) =
            Simulation.PayForStuff(model, person, currentDate, recessionStats, ledger, spend, accounts);
        var actualCash = AccountCalculation.CalculateCashBalance(newAccounts);
        // Assert
        Assert.True(isSuccessful);
        Assert.Equal(expectedCash, actualCash);
    }
    
    [Fact]
    public void PayForStuff_WhenInRecession_SpendsLess()
    {
        // Arrange
        var model = CreateTestModel();
        model.DesiredMonthlySpendPostRetirement = 800m;
        model.AusterityRatio = 0.9m;
        var person = TestDataManager.CreateTestPerson();
        person.RequiredMonthlySpendHealthCare = 500m;
        person.RequiredMonthlySpend = 700m;
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(60);
        var currentDate = person.BirthDate.PlusYears(64); // post retirement, pre-medicare
        var recessionStats = new RecessionStats();
        recessionStats.AreWeInARecession = true;
        recessionStats.AreWeInExtremeAusterityMeasures = false;
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var spend = TestDataManager.CreateEmptySpend();
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var initialCash = 10000m;
        accounts = AccountCashManagement.DepositCash(accounts, initialCash, currentDate).accounts;
        var expectedSpend =
            person.RequiredMonthlySpend
            + person.RequiredMonthlySpendHealthCare
            + (Spend.CalculateMonthlyFunSpend(model, person, currentDate) * model.AusterityRatio);
        var expectedCash = initialCash - expectedSpend;
        // Act
        var (isSuccessful, newAccounts, newLedger, newSpend, messages) =
            Simulation.PayForStuff(model, person, currentDate, recessionStats, ledger, spend, accounts);
        var actualCash = AccountCalculation.CalculateCashBalance(newAccounts);
        // Assert
        Assert.True(isSuccessful);
        Assert.Equal(expectedCash, actualCash);
    }
    
    [Fact]
    public void PayForStuff_WhenInExtremeAusterity_SpendsEvenLess()
    {
        // Arrange
        var model = CreateTestModel();
        model.DesiredMonthlySpendPostRetirement = 800m;
        model.AusterityRatio = 0.9m;
        model.ExtremeAusterityRatio = 0.7m;
        var person = TestDataManager.CreateTestPerson();
        person.RequiredMonthlySpendHealthCare = 500m;
        person.RequiredMonthlySpend = 700m;
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(60);
        var currentDate = person.BirthDate.PlusYears(64); // post retirement, pre-medicare
        var recessionStats = new RecessionStats();
        recessionStats.AreWeInARecession = true; // make sure that they don't stack
        recessionStats.AreWeInExtremeAusterityMeasures = true;
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var spend = TestDataManager.CreateEmptySpend();
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var initialCash = 10000m;
        accounts = AccountCashManagement.DepositCash(accounts, initialCash, currentDate).accounts;
        var expectedSpend =
            person.RequiredMonthlySpend
            + person.RequiredMonthlySpendHealthCare
            + (Spend.CalculateMonthlyFunSpend(model, person, currentDate) * model.ExtremeAusterityRatio);
        var expectedCash = initialCash - expectedSpend;
        // Act
        var (isSuccessful, newAccounts, newLedger, newSpend, messages) =
            Simulation.PayForStuff(model, person, currentDate, recessionStats, ledger, spend, accounts);
        var actualCash = AccountCalculation.CalculateCashBalance(newAccounts);
        // Assert
        Assert.True(isSuccessful);
        Assert.Equal(expectedCash, actualCash);
    }
    
    [Fact]
    public void PayForStuff_WhenSuccessful_RecordsSpend()
    {
        // Arrange
        var model = CreateTestModel();
        model.DesiredMonthlySpendPostRetirement = 800m;
        var person = TestDataManager.CreateTestPerson();
        person.RequiredMonthlySpendHealthCare = 500m;
        person.RequiredMonthlySpend = 700m;
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(60);
        var currentDate = person.BirthDate.PlusYears(64); // post retirement, pre-medicare
        var recessionStats = new RecessionStats();
        recessionStats.AreWeInARecession = false;
        recessionStats.AreWeInExtremeAusterityMeasures = false;
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var spend = TestDataManager.CreateEmptySpend();
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var initialCash = 10000m;
        accounts = AccountCashManagement.DepositCash(accounts, initialCash, currentDate).accounts;
        var expectedSpend = // 2000, 
            person.RequiredMonthlySpend
            + person.RequiredMonthlySpendHealthCare
            + Spend.CalculateMonthlyFunSpend(model, person, currentDate);
        var expectedCash = initialCash - expectedSpend;
        // Act
        var (isSuccessful, newAccounts, newLedger, newSpend, messages) =
            Simulation.PayForStuff(model, person, currentDate, recessionStats, ledger, spend, accounts);
        var recordedSpend = newSpend.TotalSpendLifetime;
        // Assert
        Assert.True(isSuccessful);
        Assert.Equal(expectedSpend, recordedSpend);
    }
    
    [Fact]
    public void PayForStuff_WhenSuccessful_RecordsFun()
    {
        // Arrange
        var model = CreateTestModel();
        model.DesiredMonthlySpendPostRetirement = 800m;
        var person = TestDataManager.CreateTestPerson();
        person.RequiredMonthlySpendHealthCare = 500m;
        person.RequiredMonthlySpend = 700m;
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(60);
        var currentDate = person.BirthDate.PlusYears(64); // post retirement, pre-medicare
        var recessionStats = new RecessionStats();
        recessionStats.AreWeInARecession = false;
        recessionStats.AreWeInExtremeAusterityMeasures = false;
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var spend = TestDataManager.CreateEmptySpend();
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var initialCash = 10000m;
        accounts = AccountCashManagement.DepositCash(accounts, initialCash, currentDate).accounts;
        var funSpend = Spend.CalculateMonthlyFunSpend(model, person, currentDate);
        var expectedSpend = // 2000, 
            person.RequiredMonthlySpend
            + person.RequiredMonthlySpendHealthCare
            + funSpend;
        var expectedFun = Spend.CalculateFunPointsForSpend(funSpend, person, currentDate);
        // Act
        var (isSuccessful, newAccounts, newLedger, newSpend, messages) =
            Simulation.PayForStuff(model, person, currentDate, recessionStats, ledger, spend, accounts);
        var recordedFun = newSpend.TotalFunPointsLifetime;
        // Assert
        Assert.True(isSuccessful);
        Assert.Equal(expectedFun, recordedFun);
    }
    
    /// <summary>
    /// this is a monster of a UT, but it's better than copy+paste the same scenario over and over again
    /// </summary>
    [Fact]
    public void ProcessPayday_PreRetirement_ProcessesCorrectly()
    {
        // Arrange
        var person = TestDataManager.CreateTestPerson();
        var model = CreateTestModel();
        person.IsRetired = false;
        person.IsBankrupt = false;
        person.Annual401KContribution = 12000m;
        person.AnnualSalary = 120000m; // keep salary + bonus below the OASDI max
        person.AnnualBonus = 12000m;
        person.Annual401KMatchPercent = 0.05m;
        person.Annual401KPostTax = 4000m;
        person.Annual401KPreTax = 5000m;
        person.AnnualHsaContribution = 7500m;
        person.AnnualHsaEmployerContribution = 1000m;
        person.FederalAnnualWithholding = 20000m;
        person.StateAnnualWithholding = 10000m;
        person.PostTaxInsuranceDeductions = 3588.38m;
        person.PreTaxHealthDeductions = 1244.38m;
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(65);
        model.SocialSecurityStart = person.BirthDate.PlusYears(67); // not drawing ss yet
        var currentDate = model.RetirementDate.PlusYears(-10); // not yet retired
        var prices = TestDataManager.CreateTestCurrentPrices(
            1.01m, 100m, 50m, 1m);
        var spend = TestDataManager.CreateEmptySpend();
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        
        var grossMonthlyPay = (person.AnnualSalary + person.AnnualBonus) / 12;
        var fedWithholding = person.FederalAnnualWithholding / 12;
        var stateWithholding = person.StateAnnualWithholding / 12;
        var oasdi = TaxConstants.OasdiBasePercent * grossMonthlyPay;
        var medicare = TaxConstants.StandardMedicareTaxRate * grossMonthlyPay;
        var taxWithholdings = // 3341.5,  
            fedWithholding // 1666.66, 
            + stateWithholding // 833.33, 
            + oasdi // 682, 
            + medicare; // 159.5, 
        var preTaxDeductions = // 1145.365, 
                (person.PreTaxHealthDeductions / 12)
                + (person.AnnualHsaContribution / 12)
                + (person.Annual401KPreTax / 12)
            ;
        var postTaxDeductions = // 632.365, 
                (person.Annual401KPostTax / 12)
                + (person.PostTaxInsuranceDeductions / 12)
            ;
        var expectedNetIncome = grossMonthlyPay - taxWithholdings - preTaxDeductions - postTaxDeductions; // 5880.77, 5880.77
        var expectedW2Income = grossMonthlyPay - preTaxDeductions; // 9854.635, 
        var expectedFederalWithholding = person.FederalAnnualWithholding / 12;
        var expectedStateWithholding = person.StateAnnualWithholding / 12;
        var expectedHealthSpend = person.PreTaxHealthDeductions / 12;
        var expectedTaxesPaid = taxWithholdings;
        var expectedHsaBalance = Math.Round((person.AnnualHsaEmployerContribution / 12) 
                                 + (person.AnnualHsaContribution / 12)
                                 , 2);
        var expected401KTraditional = Math.Round(
            (person.Annual401KPreTax / 12)
            + (person.AnnualSalary * person.Annual401KMatchPercent / 12)
            , 2);
        
        var expected401KRoth = Math.Round(
            (person.Annual401KPostTax / 12)
            , 2);
        
            
        // Act
        var result = Simulation.ProcessPayday(
           person, currentDate, accounts, ledger, spend, model, prices);
        var actualCash = AccountCalculation.CalculateCashBalance(result.accounts);
        var reportedW2 = result.ledger.W2Income.Sum(x => x.amount);
        var reportedFederalWithholding = result.ledger.FederalWithholdings.Sum(x => x.amount);
        var reportedStateWithholding = result.ledger.StateWithholdings.Sum(x => x.amount);
        var reportedHealthSpend = result.spend.TotalLifetimeHealthCareSpend;
        var reportedTaxesPaid = result.ledger.TotalTaxPaidLifetime;
        var actualHsa = result.accounts.Hsa.Positions.Sum(x => x.CurrentValue);
        var actual401KTraditional = result.accounts.Traditional401K.Positions.Sum(x => x.CurrentValue);
        var actual401KRoth = result.accounts.Roth401K.Positions.Sum(x => x.CurrentValue);

        // Assert
        Assert.Equal(expectedNetIncome, actualCash);
        Assert.Equal(expectedW2Income, reportedW2);
        Assert.Equal(expectedFederalWithholding, reportedFederalWithholding);
        Assert.Equal(expectedStateWithholding, reportedStateWithholding);
        Assert.Equal(expectedHealthSpend, reportedHealthSpend);
        Assert.Equal(expectedTaxesPaid, reportedTaxesPaid);
        Assert.Equal(expectedHsaBalance, actualHsa);
        Assert.Equal(expected401KTraditional, actual401KTraditional);
        Assert.Equal(expected401KRoth, actual401KRoth);
    }
    
    [Fact]
    public void ProcessPayday_PostRetirementWithSocialSecurity_ProcessesCorrectly()
    {
        // Arrange
        var person = TestDataManager.CreateTestPerson();
        var model = CreateTestModel();
        person.IsRetired = true;
        person.IsBankrupt = false;
        person.BirthDate = new LocalDateTime(1970, 3, 1, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(65);
        model.SocialSecurityStart = person.BirthDate.PlusYears(62);
        person.AnnualSocialSecurityWage = 6500m * 12m;
        var currentDate = model.RetirementDate.PlusYears(1); // 1 year after retirement
        var prices = TestDataManager.CreateTestCurrentPrices(
            1.01m, 100m, 50m, 1m);
        var spend = TestDataManager.CreateEmptySpend();
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        
        
        var expectedNetIncome = person.AnnualSocialSecurityWage / 12;
        var expectedW2Income = 0;
        var expectedSsIncome = person.AnnualSocialSecurityWage / 12; 
        
            
        // Act
        var result = Simulation.ProcessPayday(
           person, currentDate, accounts, ledger, spend, model, prices);
        var actualCash = AccountCalculation.CalculateCashBalance(result.accounts);
        var reportedW2 = result.ledger.W2Income.Sum(x => x.amount);
        var reportedSsIncome = result.ledger.SocialSecurityIncome.Sum(x => x.amount);
        
        // Assert
        Assert.Equal(expectedNetIncome, actualCash);
        Assert.Equal(expectedW2Income, reportedW2);
        Assert.Equal(expectedSsIncome, reportedSsIncome);
    }

    [Fact]
    public void ProcessPayday_PreRetirementWithSocialSecurity_ProcessesBoth()
    {
        // Arrange
        var person = TestDataManager.CreateTestPerson();
        var model = CreateTestModel();
        person.IsRetired = false;
        person.IsBankrupt = false;
        person.Annual401KContribution = 0;
        person.AnnualSalary = 100000; 
        person.AnnualSocialSecurityWage = 6500m * 12m;
        person.AnnualBonus = 0;
        person.Annual401KMatchPercent = 0;
        person.Annual401KPostTax = 0;
        person.Annual401KPreTax = 0;
        person.AnnualHsaContribution = 0;
        person.AnnualHsaEmployerContribution = 0;
        person.FederalAnnualWithholding = 20000m;
        person.StateAnnualWithholding = 10000m;
        person.PostTaxInsuranceDeductions = 0;
        person.PreTaxHealthDeductions = 0;
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(65);
        model.SocialSecurityStart = person.BirthDate.PlusYears(62); // already drawing ss
        var currentDate = person.BirthDate.PlusYears(63); // already drawing ss
        var prices = TestDataManager.CreateTestCurrentPrices(
            1.01m, 100m, 50m, 1m);
        var spend = TestDataManager.CreateEmptySpend();
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var grossMonthlyPay = (person.AnnualSalary + person.AnnualBonus) / 12;
        var expectedW2Income = grossMonthlyPay; // no pre-tax deductions in this scenario 
        var expectedSsIncome = person.AnnualSocialSecurityWage / 12;
        
            
        // Act
        var result = Simulation.ProcessPayday(
           person, currentDate, accounts, ledger, spend, model, prices);
        var reportedW2 = result.ledger.W2Income.Sum(x => x.amount);
        var reportedSsIncome = result.ledger.SocialSecurityIncome.Sum(x => x.amount);
        
        // Assert
        Assert.Equal(expectedW2Income, reportedW2);
        Assert.Equal(expectedSsIncome, reportedSsIncome);
    }

    [Fact]
    public void SetIsRetiredFlagIfNeeded_AtRetirementDate_SetsFlag()
    {
        // Arrange
        var person = TestDataManager.CreateTestPerson();
        person.IsRetired = false;
        var currentDate = _model.RetirementDate;

        // Act
        var (isRetired, updatedPerson) = Simulation.SetIsRetiredFlagIfNeeded(
            currentDate, person, _model);

        // Assert
        Assert.True(isRetired);
        Assert.True(updatedPerson.IsRetired);
    }
    
    [Fact]
    public void SetIsRetiredFlagIfNeeded_AfterRetirementDate_SetsFlag()
    {
        // Arrange
        var person = TestDataManager.CreateTestPerson();
        person.IsRetired = false;
        var currentDate = _model.RetirementDate.PlusYears(1);

        // Act
        var (isRetired, updatedPerson) = Simulation.SetIsRetiredFlagIfNeeded(
            currentDate, person, _model);

        // Assert
        Assert.True(isRetired);
        Assert.True(updatedPerson.IsRetired);
    }
    
    [Fact]
    public void SetIsRetiredFlagIfNeeded_BeforeRetirementDate_DoesntSetFlag()
    {
        // Arrange
        var person = TestDataManager.CreateTestPerson();
        person.IsRetired = false;
        var currentDate = _model.RetirementDate.PlusYears(-1);

        // Act
        var (isRetired, updatedPerson) = Simulation.SetIsRetiredFlagIfNeeded(
            currentDate, person, _model);

        // Assert
        Assert.False(isRetired);
        Assert.False(updatedPerson.IsRetired);
    }

    [Fact]
    public void SpendCash_WithSufficientFunds_Succeeds()
    {
        // Arrange
        var initialBalance = 1000m;
        var spendAmount = 500m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts = AccountCashManagement.DepositCash(accounts, initialBalance, _testDate).accounts;
        var model = TestDataManager.CreateTestModel();

        // Act
        var result = Simulation.SpendCash(
            spendAmount, true, accounts, _testDate, _ledger, _spend, _testPerson, model);

        // Assert
        Assert.True(result.isSuccessful);
    }

    [Fact]
    public void SpendCash_WithInSufficientFunds_Fails()
    {
        // Arrange
        var initialBalance = 1000m;
        var spendAmount = 5000m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts = AccountCashManagement.DepositCash(accounts, initialBalance, _testDate).accounts;
        var model = TestDataManager.CreateTestModel();

        // Act
        var result = Simulation.SpendCash(
            spendAmount, true, accounts, _testDate, _ledger, _spend, _testPerson, model);

        // Assert
        Assert.False(result.isSuccessful);
    }
    
    [Fact]
    public void SpendCash_WithInSufficientCashButEnoughInvestment_Succeeds()
    {
        // Arrange
        var initialBalance = 1000m;
        var spendAmount = 5000m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts = AccountCashManagement.DepositCash(accounts, initialBalance, _testDate).accounts;
        var position = TestDataManager.CreateTestInvestmentPosition(
            100m, 50m, McInvestmentPositionType.LONG_TERM, true);
        accounts.Brokerage.Positions.Add(position);
        var model = TestDataManager.CreateTestModel();

        // Act
        var result = Simulation.SpendCash(
            spendAmount, true, accounts, _testDate, _ledger, _spend, _testPerson, model);

        // Assert
        Assert.True(result.isSuccessful);
    }

    [Fact]
    internal void PayTaxForYear_PaysBothStateAndFederal()
    {
        var person = TestDataManager.CreateTestPerson();
        var currentDate = new LocalDateTime(2026, 1, 1, 0, 0);
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var totalIncome = 100000m;
        ledger = Tax.RecordW2Income(ledger, currentDate.PlusMonths(-1), totalIncome).ledger;
        var spend = TestDataManager.CreateEmptySpend();
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts = AccountCashManagement.DepositCash(accounts, totalIncome, currentDate).accounts;
        var model = TestDataManager.CreateTestModel();
        var taxYear = currentDate.Year - 1;
        var form1040 = new Form1040(ledger, taxYear);
        var expectedFederalLiability = form1040.CalculateTaxLiability();
        var formD400 = new FormD400(ledger, taxYear, form1040.AdjustedGrossIncome);
        var expectedStateLiability = formD400.CalculateTaxLiability();
        var expectedTotalLiability = expectedFederalLiability + expectedStateLiability;
        var expectedCash = totalIncome - expectedTotalLiability;
        
        // Act
        var (isSuccessful, newAccounts, newLedger, newSpend, messages) =
            Simulation.PayTaxForYear(person, currentDate, ledger, spend, accounts, taxYear, model);
        var actualCash = AccountCalculation.CalculateCashBalance(newAccounts);
            
        // Assert
        Assert.Equal(expectedCash, actualCash);
    }
    [Fact]
    internal void PayTaxForYear_RecordsThePayment()
    {
        var person = TestDataManager.CreateTestPerson();
        var currentDate = new LocalDateTime(2026, 1, 1, 0, 0);
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var totalIncome = 100000m;
        ledger = Tax.RecordW2Income(ledger, currentDate.PlusMonths(-1), totalIncome).ledger;
        var spend = TestDataManager.CreateEmptySpend();
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts = AccountCashManagement.DepositCash(accounts, totalIncome, currentDate).accounts;
        var model = TestDataManager.CreateTestModel();
        var taxYear = currentDate.Year - 1;
        var form1040 = new Form1040(ledger, taxYear);
        var expectedFederalLiability = form1040.CalculateTaxLiability();
        var formD400 = new FormD400(ledger, taxYear, form1040.AdjustedGrossIncome);
        var expectedStateLiability = formD400.CalculateTaxLiability();
        var expectedTotalLiability = expectedFederalLiability + expectedStateLiability;
        var expectedCash = totalIncome - expectedTotalLiability;
        
        // Act
        var (isSuccessful, newAccounts, newLedger, newSpend, messages) =
            Simulation.PayTaxForYear(person, currentDate, ledger, spend, accounts, taxYear, model);
        var recordedPayment = newLedger.TotalTaxPaidLifetime;
            
        // Assert
        Assert.Equal(expectedTotalLiability, recordedPayment);
    }
    [Fact]
    internal void PayTaxForYear_WithInsufficientFunds_Fails()
    {
        var person = TestDataManager.CreateTestPerson();
        var currentDate = new LocalDateTime(2026, 1, 1, 0, 0);
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var totalIncome = 100000m;
        ledger = Tax.RecordW2Income(ledger, currentDate.PlusMonths(-1), totalIncome).ledger;
        var spend = TestDataManager.CreateEmptySpend();
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts = AccountCashManagement.DepositCash(accounts, 4.99m, currentDate).accounts;
        var model = TestDataManager.CreateTestModel();
        var taxYear = currentDate.Year - 1;
        
        // Act
        var (isSuccessful, newAccounts, newLedger, newSpend, messages) =
            Simulation.PayTaxForYear(person, currentDate, ledger, spend, accounts, taxYear, model);
            
        // Assert
        Assert.False(isSuccessful);
    }
    [Fact]
    internal void PayTaxForYear_WithSufficientFunds_Succeeds()
    {
        var person = TestDataManager.CreateTestPerson();
        var currentDate = new LocalDateTime(2026, 1, 1, 0, 0);
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var totalIncome = 100000m;
        ledger = Tax.RecordW2Income(ledger, currentDate.PlusMonths(-1), totalIncome).ledger;
        var spend = TestDataManager.CreateEmptySpend();
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts = AccountCashManagement.DepositCash(accounts, totalIncome, currentDate).accounts;
        var model = TestDataManager.CreateTestModel();
        var taxYear = currentDate.Year - 1;
        
        // Act
        var (isSuccessful, newAccounts, newLedger, newSpend, messages) =
            Simulation.PayTaxForYear(person, currentDate, ledger, spend, accounts, taxYear, model);
            
        // Assert
        Assert.True(isSuccessful);
    }
    
    [Fact]
    internal void SetNewPrices_WithInvalidDate_ThrowsInvalidDataException()
    {
        // Assemble
        var prices = new CurrentPrices();
        var hypotheticalPrices = TestDataManager.CreateOrFetchHypotheticalPricingForRuns();
        var invalidDate = new LocalDateTime(2024, 1, 1, 0, 0);
        
        // Act
       
        
        // Assert
        Assert.Throws<InvalidDataException>(() => 
            Simulation.SetNewPrices(prices, hypotheticalPrices[0], invalidDate));
        
    }
    
    [Fact]
    internal void RecordFunAndAnxiety_PreRetirement_Punishes()
    {
        // Assemble
        var model = TestDataManager.CreateTestModel();
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(65);
        person.IsRetired = false;
        person.RequiredMonthlySpend = 1300m;
        person.RequiredMonthlySpendHealthCare = 900m;
        var currentDate = new LocalDateTime(2036, 1, 1, 0, 0);
        var spend = TestDataManager.CreateEmptySpend();
        var recessionStats = new RecessionStats
        {
            AreWeInARecession = false,
            AreWeInExtremeAusterityMeasures = false,
        };
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var requiredSpend = Spend.CalculateMonthlyRequiredSpendWithoutDebt(
            model, person, currentDate).TotalSpend;
        var expectedFun = requiredSpend * ModelConstants.FunPenaltyNotRetiredPercentOfRequiredSpend * -1;

        // Act
        var (newSpend, messages) = Simulation.RecordFunAndAnxiety(
            model, person, currentDate, recessionStats, spend, accounts);
        var recordedFun = newSpend.TotalFunPointsLifetime;
        
        // Assert
        Assert.Equal(expectedFun, recordedFun);
        
    }
    
    [Fact]
    internal void RecordFunAndAnxiety_PostRetirement_Rewards()
    {
        // Assemble
        var model = TestDataManager.CreateTestModel();
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(65);
        person.IsRetired = true;
        person.RequiredMonthlySpend = 1300m;
        person.RequiredMonthlySpendHealthCare = 900m;
        var currentDate = person.BirthDate.PlusYears(66);
        var spend = TestDataManager.CreateEmptySpend();
        var recessionStats = new RecessionStats
        {
            AreWeInARecession = false,
            AreWeInExtremeAusterityMeasures = false,
        };
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var requiredSpend = Spend.CalculateMonthlyRequiredSpendWithoutDebt(model, person, currentDate);
        var expectedFun = ModelConstants.FunBonusRetirement;

        // Act
        var (newSpend, messages) = Simulation.RecordFunAndAnxiety(
            model, person, currentDate, recessionStats, spend, accounts);
        var recordedFun = newSpend.TotalFunPointsLifetime;
        
        // Assert
        Assert.Equal(expectedFun, recordedFun);
    }
    
    [Fact]
    internal void RecordFunAndAnxiety_PreRetirement_RecessionDoesntMatter()
    {
        // Assemble
        var model = TestDataManager.CreateTestModel();
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(65);
        person.IsRetired = false;
        person.RequiredMonthlySpend = 1300m;
        person.RequiredMonthlySpendHealthCare = 900m;
        var currentDate = person.BirthDate.PlusYears(56);
        var spend = TestDataManager.CreateEmptySpend();
        var recessionStats = new RecessionStats
        {
            AreWeInARecession = true,
            AreWeInExtremeAusterityMeasures = false,
        };
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var requiredSpend = Spend.CalculateMonthlyRequiredSpendWithoutDebt(
            model, person, currentDate).TotalSpend;
        var expectedFun = requiredSpend * ModelConstants.FunPenaltyNotRetiredPercentOfRequiredSpend * -1;

        // Act
        var (newSpend, messages) = Simulation.RecordFunAndAnxiety(
            model, person, currentDate, recessionStats, spend, accounts);
        var recordedFun = newSpend.TotalFunPointsLifetime;
        
        // Assert
        Assert.Equal(expectedFun, recordedFun);
        
    }
    
    
    [Fact]
    internal void RecordFunAndAnxiety_PreRetirement_ExtremeAusterityDoesntMatter()
    {
        // Assemble
        var model = TestDataManager.CreateTestModel();
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(65);
        person.IsRetired = false;
        person.RequiredMonthlySpend = 1300m;
        person.RequiredMonthlySpendHealthCare = 900m;
        var currentDate = person.BirthDate.PlusYears(56);
        var spend = TestDataManager.CreateEmptySpend();
        var recessionStats = new RecessionStats
        {
            AreWeInARecession = false,
            AreWeInExtremeAusterityMeasures = true,
        };
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var requiredSpend = Spend.CalculateMonthlyRequiredSpendWithoutDebt(
            model, person, currentDate).TotalSpend;
        var expectedFun = requiredSpend * ModelConstants.FunPenaltyNotRetiredPercentOfRequiredSpend * -1;

        // Act
        var (newSpend, messages) = Simulation.RecordFunAndAnxiety(
            model, person, currentDate, recessionStats, spend, accounts);
        var recordedFun = newSpend.TotalFunPointsLifetime;
        
        // Assert
        Assert.Equal(expectedFun, recordedFun);
        
    }
    
    [Fact]
    internal void RecordFunAndAnxiety_PostRetirementInRecession_RecordsAnxiety()
    {
        // Assemble
        var model = TestDataManager.CreateTestModel();
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(65);
        person.IsRetired = true;
        person.RequiredMonthlySpend = 1300m;
        person.RequiredMonthlySpendHealthCare = 900m;
        var currentDate = person.BirthDate.PlusYears(66);
        var spend = TestDataManager.CreateEmptySpend();
        var recessionStats = new RecessionStats
        {
            AreWeInARecession = true,
            AreWeInExtremeAusterityMeasures = false,
        };
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var requiredSpend = Spend.CalculateMonthlyRequiredSpendWithoutDebt(
            model, person, currentDate).TotalSpend;
        var expectedFun = 
            ModelConstants.FunBonusRetirement + // you still get retirement freedom
            requiredSpend * ModelConstants.FunPenaltyRetiredInRecessionPercentOfRequiredSpend * -1 // but each dollar spent makes you nervous
            ;

        // Act
        var (newSpend, messages) = Simulation.RecordFunAndAnxiety(
            model, person, currentDate, recessionStats, spend, accounts);
        var recordedFun = newSpend.TotalFunPointsLifetime;
        
        // Assert
        Assert.Equal(expectedFun, recordedFun);
        
    }
    
    [Fact]
    internal void RecordFunAndAnxiety_PostRetirementInExtremeAnxiety_RecordsAnxiety()
    {
        // Assemble
        var model = TestDataManager.CreateTestModel();
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(65);
        person.IsRetired = true;
        person.RequiredMonthlySpend = 1300m;
        person.RequiredMonthlySpendHealthCare = 900m;
        var currentDate = person.BirthDate.PlusYears(66);
        var spend = TestDataManager.CreateEmptySpend();
        var recessionStats = new RecessionStats
        {
            AreWeInARecession = false,
            AreWeInExtremeAusterityMeasures = true,
        };
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var requiredSpend = Spend.CalculateMonthlyRequiredSpendWithoutDebt(
            model, person, currentDate).TotalSpend;
        var expectedFun = 
                ModelConstants.FunBonusRetirement + // you still get retirement freedom
                requiredSpend * ModelConstants.FunPenaltyRetiredInExtremeAusterityPercentOfRequiredSpend * -1 // but each dollar spent makes you nervous
            ;

        // Act
        var (newSpend, messages) = Simulation.RecordFunAndAnxiety(
            model, person, currentDate, recessionStats, spend, accounts);
        var recordedFun = newSpend.TotalFunPointsLifetime;
        
        // Assert
        Assert.Equal(expectedFun, recordedFun);
        
    }
    
    [Fact]
    internal void RecordFunAndAnxiety_InRecessionAndInExtremeAnxiety_AnxietyStacks()
    {
        // Assemble
        var model = TestDataManager.CreateTestModel();
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(65);
        person.IsRetired = true;
        person.RequiredMonthlySpend = 1300m;
        person.RequiredMonthlySpendHealthCare = 900m;
        var currentDate = person.BirthDate.PlusYears(66);
        var spend = TestDataManager.CreateEmptySpend();
        var recessionStats = new RecessionStats
        {
            AreWeInARecession = true,
            AreWeInExtremeAusterityMeasures = true,
        };
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var requiredSpend = Spend.CalculateMonthlyRequiredSpendWithoutDebt(
            model, person, currentDate).TotalSpend;
        var expectedFun = 
                ModelConstants.FunBonusRetirement + // you still get retirement freedom
                requiredSpend * ModelConstants.FunPenaltyRetiredInRecessionPercentOfRequiredSpend * -1 + // but each dollar spent makes you nervous
                requiredSpend * ModelConstants.FunPenaltyRetiredInExtremeAusterityPercentOfRequiredSpend * -1 // and now each dollar spent makes you way more nervous
            ;

        // Act
        var (newSpend, messages) = Simulation.RecordFunAndAnxiety(
            model, person, currentDate, recessionStats, spend, accounts);
        var recordedFun = newSpend.TotalFunPointsLifetime;
        
        // Assert
        Assert.Equal(expectedFun, recordedFun);
        
    }
    
    [Fact]
    internal void RecordFunAndAnxiety_InBankruptcy_PunishesHard()
    {
        // Assemble
        var model = TestDataManager.CreateTestModel();
        var person = TestDataManager.CreateTestPerson();
        person.IsBankrupt = true;
        person.IsRetired = true;
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        var currentDate = person.BirthDate.PlusYears(66);
        var spend = TestDataManager.CreateEmptySpend();
        var recessionStats = new RecessionStats
        {
            AreWeInARecession = true,
            AreWeInExtremeAusterityMeasures = true,
        };
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var expectedFun = ModelConstants.FunPenaltyBankruptcy;

        // Act
        var (newSpend, messages) = Simulation.RecordFunAndAnxiety(
            model, person, currentDate, recessionStats, spend, accounts);
        var recordedFun = newSpend.TotalFunPointsLifetime;
        
        // Assert
        Assert.Equal(expectedFun, recordedFun);
        
    }
    
    [Fact]
    internal void RecordFunAndAnxiety_WithDebtDuringRecession_PunishesHarderThanWithoutDebt()
    {
        // Assemble
        var model = TestDataManager.CreateTestModel();
        var person = TestDataManager.CreateTestPerson();
        person.IsBankrupt = false;
        person.IsRetired = true;
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        person.RequiredMonthlySpend = 1300m;
        person.RequiredMonthlySpendHealthCare = 900m;
        model.RetirementDate = person.BirthDate.PlusYears(62);
        var currentDate = person.BirthDate.PlusYears(63);
        var spend = TestDataManager.CreateEmptySpend();
        var recessionStats = new RecessionStats
        {
            AreWeInARecession = true,
            AreWeInExtremeAusterityMeasures = false,
        };
        var accountsWithoutDebt = TestDataManager.CreateEmptyBookOfAccounts();
        var accountsWithDebt = TestDataManager.CreateEmptyBookOfAccounts();
        var monthlyDebtPayment = 500m;
        var debtPosition = TestDataManager.CreateTestDebtPosition(
            true, 0.03m, monthlyDebtPayment, 10001m);
        accountsWithDebt.DebtAccounts.Add(TestDataManager.CreateTestDebtAccount([debtPosition]));
        var requiredSpendWithoutDebt = person.RequiredMonthlySpend + person.RequiredMonthlySpendHealthCare;
        var requiredSpendWithDebt = requiredSpendWithoutDebt + monthlyDebtPayment;
        var expectedFunWithoutDebt = ModelConstants.FunBonusRetirement
            - (requiredSpendWithoutDebt * ModelConstants.FunPenaltyRetiredInRecessionPercentOfRequiredSpend);
        var expectedFunWithDebt = ModelConstants.FunBonusRetirement 
            - (requiredSpendWithDebt * ModelConstants.FunPenaltyRetiredInRecessionPercentOfRequiredSpend);

        // Act
        var (newSpendWithoutDebt, messagesWithoutDebt) = Simulation.RecordFunAndAnxiety(
            model, person, currentDate, recessionStats, spend, accountsWithoutDebt);
        var recordedFunWithoutDebt = newSpendWithoutDebt.TotalFunPointsLifetime;
        var (newSpendWithDebt, messagesWithDebt) = Simulation.RecordFunAndAnxiety(
            model, person, currentDate, recessionStats, spend, accountsWithDebt);
        var recordedFunWithDebt = newSpendWithDebt.TotalFunPointsLifetime;
        
        // Assert
        Assert.Equal(expectedFunWithoutDebt, recordedFunWithoutDebt);
        Assert.Equal(expectedFunWithDebt, recordedFunWithDebt);
    }

    [Theory]
    [InlineData(0.05, 615.23)]
    [InlineData(0.1, 1127.06)]
    [InlineData(0.15, 1638.89)]
    [InlineData(0.2, 2150.72)]
    [InlineData(0.25, 2662.55)]
    [InlineData(0.3, 3174.38)]
    [InlineData(0.35, 3686.21)]
    [InlineData(0.4, 4198.04)]
    [InlineData(0.45, 4709.87)]
    [InlineData(0.5, 5221.7)]
    [InlineData(0.55, 5733.53)]
    [InlineData(0.6, 6245.36)]
    [InlineData(0.65, 6757.19)]
    [InlineData(0.7, 7269.02)]
    [InlineData(0.75, 7780.85)]
    [InlineData(0.8, 8292.68)]
    [InlineData(0.85, 8804.51)]
    [InlineData(0.9, 9316.34)]
    [InlineData(0.95, 9828.17)]
    internal void CalculatePercentileValue_CalculatesCorrectly(decimal percentile, decimal expectedValue)
    {
        // Arrange
        List<decimal> values = [];
        
        // create an out-of-order array. first do all the odd numbers, then all the even
        for (var i = 1; i <= 100m; i += 2)
        {
            values.Add(Math.Round(i * 103.4m, 2));
        }
        for (var i = 100; i > 0m; i -= 2)
        {
            values.Add(Math.Round(i * 103.4m, 2));
        }

        var sequence = values.ToArray();
        
        // Act
        var result = Simulation.CalculatePercentileValue(sequence, percentile);
        
        // Assert
        Assert.Equal(expectedValue, result);
    }
    
    [Fact]
    internal void InterpretSimulationResults_WithBankruptcies_HasANonZeroBankruptcyRate()
    {
        // Assemble
        var model = TestDataManager.CreateTestModel();
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = new LocalDateTime(1975, 3, 1, 0, 0);
        // set model params to guarantee bankruptcy
        model.RetirementDate = person.BirthDate.PlusYears(55);
        model.DesiredMonthlySpendPostRetirement = 10000m;
        model.DesiredMonthlySpendPreRetirement = 10000m;
        model.SocialSecurityStart = person.BirthDate.PlusYears(63);
        model.RecessionCheckLookBackMonths = 10;
        model.NumMonthsCashOnHand = 1;
        model.NumMonthsMidBucketOnHand = 1;
        model.AusterityRatio = 1m;
        model.ExtremeAusterityNetWorthTrigger = 100000000m;
        model.ExtremeAusterityRatio = 1m;
        model.NumMonthsPriorToRetirementToBeginRebalance = 1;
        model.Percent401KTraditional = 0.05m;
        model.RebalanceFrequency = RebalanceFrequency.YEARLY;
        model.RecessionRecoveryPointModifier = 1m;
        person.RequiredMonthlySpend = 10000m;
        person.RequiredMonthlySpendHealthCare = 1000m;
        person.Annual401KContribution = 0m;
        person.AnnualSalary = 100000m;
        person.AnnualBonus = 0;
        person.AnnualHsaContribution = 0;
        person.AnnualHsaEmployerContribution = 0;
        person.Annual401KMatchPercent = 0;
        person.MonthlyFullSocialSecurityBenefit = 2500m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts = AccountCashManagement.DepositCash(accounts, 100000m, _testDate).accounts;
        
        
        var hypotheticalPrices = TestDataManager.CreateOrFetchHypotheticalPricingForRuns();
        
        // gotta set up the logger for this
        string logDir = ConfigManager.ReadStringSetting("LogDir");
        string timeSuffix = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
        string logFilePath = $"{logDir}MonteCarloLog{timeSuffix}.txt";
        var logger = new Logger(
            Lib.StaticConfig.MonteCarloConfig.LogLevel,
            logFilePath
        );
        
        // Act
        var allLivesRuns = SimulationTrigger.ExecuteSingleModelAllLives(
            logger, model, person, accounts.InvestmentAccounts, accounts.DebtAccounts, hypotheticalPrices);
        
        var results = Simulation.InterpretSimulationResults(model, allLivesRuns, -1, person);
        var bankruptcyRate = results.BankruptcyRateAtEndOfSim;
        
        // Assert
        Assert.True(bankruptcyRate > 0);
    }
    
    
    [Fact]
    public void FetchModelsForTrainingByVersion_ReturnsCorrectNumberOfModels()
    {
        // Arrange
        var person = TestDataManager.CreateTestPerson();
        var expectedCount = MonteCarloConfig.NumberOfModelsToPull;
    
        // Act
        var models = SimulationTrigger.FetchModelsForTrainingByVersion(
            person, 
            ModelConstants.MajorVersion, 
            ModelConstants.MinorVersion, 
            -1);

        // Assert
        Assert.NotNull(models);
        Assert.True(models.Count <= expectedCount, 
            $"Expected no more than {expectedCount} models, but got {models.Count}");
    }
}