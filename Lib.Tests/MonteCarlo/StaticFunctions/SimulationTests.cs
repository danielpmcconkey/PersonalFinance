using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.DataTypes;
using Lib.StaticConfig;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class SimulationTests
{
    private readonly LocalDateTime _testDate = new LocalDateTime(2025, 1, 1, 12, 0);
    private readonly PgPerson _testPerson;
    private readonly McModel _simParams;
    private readonly BookOfAccounts _accounts;
    private readonly TaxLedger _ledger;
    private readonly LifetimeSpend _spend;
    private readonly CurrentPrices _prices;

    public SimulationTests()
    {
        // Initialize test data
        _testPerson = TestDataManager.CreateTestPerson();
        _simParams = CreateTestModel();
        _accounts = new BookOfAccounts();
        _ledger = new TaxLedger();
        _spend = new LifetimeSpend();
        _prices = new CurrentPrices();
    }

    private McModel CreateTestModel()
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
            _simParams, _testPerson, _testDate, recessionStats, _ledger, _spend, accounts);

        // Assert
        Assert.True(result.isSuccessful);
    }
    
    [Fact]
    public void PayForStuff_OnlySpendsRequiredAndFun()
    {
        // Arrange
        var simParams = CreateTestModel();
        simParams.DesiredMonthlySpendPostRetirement = 800m;
        var person = TestDataManager.CreateTestPerson();
        person.RequiredMonthlySpendHealthCare = 500m;
        person.RequiredMonthlySpend = 700m;
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        simParams.RetirementDate = person.BirthDate.PlusYears(60);
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
            + Spend.CalculateMonthlyFunSpend(simParams, person, currentDate);
        var expectedCash = initialCash - expectedSpend;
        // Act
        var (isSuccessful, newAccounts, newLedger, newSpend, messages) =
            Simulation.PayForStuff(simParams, person, currentDate, recessionStats, ledger, spend, accounts);
        var actualCash = AccountCalculation.CalculateCashBalance(newAccounts);
        // Assert
        Assert.True(isSuccessful);
        Assert.Equal(expectedCash, actualCash);
    }
    
    [Fact]
    public void PayForStuff_WhenInRecession_SpendsLess()
    {
        // Arrange
        var simParams = CreateTestModel();
        simParams.DesiredMonthlySpendPostRetirement = 800m;
        simParams.AusterityRatio = 0.9m;
        var person = TestDataManager.CreateTestPerson();
        person.RequiredMonthlySpendHealthCare = 500m;
        person.RequiredMonthlySpend = 700m;
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        simParams.RetirementDate = person.BirthDate.PlusYears(60);
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
            + (Spend.CalculateMonthlyFunSpend(simParams, person, currentDate) * simParams.AusterityRatio);
        var expectedCash = initialCash - expectedSpend;
        // Act
        var (isSuccessful, newAccounts, newLedger, newSpend, messages) =
            Simulation.PayForStuff(simParams, person, currentDate, recessionStats, ledger, spend, accounts);
        var actualCash = AccountCalculation.CalculateCashBalance(newAccounts);
        // Assert
        Assert.True(isSuccessful);
        Assert.Equal(expectedCash, actualCash);
    }
    
    [Fact]
    public void PayForStuff_WhenInExtremeAusterity_SpendsEvenLess()
    {
        // Arrange
        var simParams = CreateTestModel();
        simParams.DesiredMonthlySpendPostRetirement = 800m;
        simParams.AusterityRatio = 0.9m;
        simParams.ExtremeAusterityRatio = 0.7m;
        var person = TestDataManager.CreateTestPerson();
        person.RequiredMonthlySpendHealthCare = 500m;
        person.RequiredMonthlySpend = 700m;
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        simParams.RetirementDate = person.BirthDate.PlusYears(60);
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
            + (Spend.CalculateMonthlyFunSpend(simParams, person, currentDate) * simParams.ExtremeAusterityRatio);
        var expectedCash = initialCash - expectedSpend;
        // Act
        var (isSuccessful, newAccounts, newLedger, newSpend, messages) =
            Simulation.PayForStuff(simParams, person, currentDate, recessionStats, ledger, spend, accounts);
        var actualCash = AccountCalculation.CalculateCashBalance(newAccounts);
        // Assert
        Assert.True(isSuccessful);
        Assert.Equal(expectedCash, actualCash);
    }
    
    [Fact]
    public void PayForStuff_WhenSuccessful_RecordsSpend()
    {
        // Arrange
        var simParams = CreateTestModel();
        simParams.DesiredMonthlySpendPostRetirement = 800m;
        var person = TestDataManager.CreateTestPerson();
        person.RequiredMonthlySpendHealthCare = 500m;
        person.RequiredMonthlySpend = 700m;
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        simParams.RetirementDate = person.BirthDate.PlusYears(60);
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
            + Spend.CalculateMonthlyFunSpend(simParams, person, currentDate);
        var expectedCash = initialCash - expectedSpend;
        // Act
        var (isSuccessful, newAccounts, newLedger, newSpend, messages) =
            Simulation.PayForStuff(simParams, person, currentDate, recessionStats, ledger, spend, accounts);
        var recordedSpend = newSpend.TotalSpendLifetime;
        // Assert
        Assert.True(isSuccessful);
        Assert.Equal(expectedSpend, recordedSpend);
    }
    
    [Fact]
    public void PayForStuff_WhenSuccessful_RecordsFun()
    {
        // Arrange
        var simParams = CreateTestModel();
        simParams.DesiredMonthlySpendPostRetirement = 800m;
        var person = TestDataManager.CreateTestPerson();
        person.RequiredMonthlySpendHealthCare = 500m;
        person.RequiredMonthlySpend = 700m;
        person.BirthDate = new LocalDateTime(1980, 3, 1, 0, 0);
        simParams.RetirementDate = person.BirthDate.PlusYears(60);
        var currentDate = person.BirthDate.PlusYears(64); // post retirement, pre-medicare
        var recessionStats = new RecessionStats();
        recessionStats.AreWeInARecession = false;
        recessionStats.AreWeInExtremeAusterityMeasures = false;
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var spend = TestDataManager.CreateEmptySpend();
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var initialCash = 10000m;
        accounts = AccountCashManagement.DepositCash(accounts, initialCash, currentDate).accounts;
        var funSpend = Spend.CalculateMonthlyFunSpend(simParams, person, currentDate);
        var expectedSpend = // 2000, 
            person.RequiredMonthlySpend
            + person.RequiredMonthlySpendHealthCare
            + funSpend;
        var expectedFun = Spend.CalculateFunPointsForSpend(funSpend, person, currentDate);
        // Act
        var (isSuccessful, newAccounts, newLedger, newSpend, messages) =
            Simulation.PayForStuff(simParams, person, currentDate, recessionStats, ledger, spend, accounts);
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
        var simParams = CreateTestModel();
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
        simParams.RetirementDate = person.BirthDate.PlusYears(65);
        simParams.SocialSecurityStart = person.BirthDate.PlusYears(67); // not drawing ss yet
        var currentDate = simParams.RetirementDate.PlusYears(-10); // not yet retired
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
           person, currentDate, accounts, ledger, spend, simParams, prices);
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
        var simParams = CreateTestModel();
        person.IsRetired = true;
        person.IsBankrupt = false;
        person.BirthDate = new LocalDateTime(1970, 3, 1, 0, 0);
        simParams.RetirementDate = person.BirthDate.PlusYears(65);
        simParams.SocialSecurityStart = person.BirthDate.PlusYears(62);
        person.AnnualSocialSecurityWage = 6500m * 12m;
        var currentDate = simParams.RetirementDate.PlusYears(1); // 1 year after retirement
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
           person, currentDate, accounts, ledger, spend, simParams, prices);
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
        var simParams = CreateTestModel();
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
        simParams.RetirementDate = person.BirthDate.PlusYears(65);
        simParams.SocialSecurityStart = person.BirthDate.PlusYears(62); // already drawing ss
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
           person, currentDate, accounts, ledger, spend, simParams, prices);
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
        var currentDate = _simParams.RetirementDate;

        // Act
        var (isRetired, updatedPerson) = Simulation.SetIsRetiredFlagIfNeeded(
            currentDate, person, _simParams);

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
        var currentDate = _simParams.RetirementDate.PlusYears(1);

        // Act
        var (isRetired, updatedPerson) = Simulation.SetIsRetiredFlagIfNeeded(
            currentDate, person, _simParams);

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
        var currentDate = _simParams.RetirementDate.PlusYears(-1);

        // Act
        var (isRetired, updatedPerson) = Simulation.SetIsRetiredFlagIfNeeded(
            currentDate, person, _simParams);

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

        // Act
        var result = Simulation.SpendCash(
            spendAmount, true, accounts, _testDate, _ledger, _spend, _testPerson);

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

        // Act
        var result = Simulation.SpendCash(
            spendAmount, true, accounts, _testDate, _ledger, _spend, _testPerson);

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

        // Act
        var result = Simulation.SpendCash(
            spendAmount, true, accounts, _testDate, _ledger, _spend, _testPerson);

        // Assert
        Assert.True(result.isSuccessful);
    }

    [Fact]
    internal void PayTaxForYear_PaysBothStateAndFederal()
    {
        // Assert
        Assert.True(false);
    }
    [Fact]
    internal void PayTaxForYear_RecordsThePayment()
    {
        // Assert
        Assert.True(false);
    }
    [Fact]
    internal void PayTaxForYear_WithInsufficientFunds_Fails()
    {
        // Assert
        Assert.True(false);
    }
    [Fact]
    internal void PayTaxForYear_WithSufficientFunds_Succeeds()
    {
        // Assert
        Assert.True(false);
    }
    
    [Fact]
    internal void SetNewPrices_WithInvalidDate_ThrowsInvalidDataException()
    {
        // Assert
        Assert.True(false);
    }
}