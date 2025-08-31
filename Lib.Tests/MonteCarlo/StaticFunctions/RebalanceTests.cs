using Lib.DataTypes.Postgres;
using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.Tests;
using Model = Lib.DataTypes.MonteCarlo.Model;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class RebalanceTests
{
    private readonly LocalDateTime _baseDate = new(2025, 1, 1, 0, 0);
    private readonly LocalDateTime _retirementDate = new(2030, 1, 1, 0, 0);

    private Model CreateTestModel(RebalanceFrequency frequency = RebalanceFrequency.MONTHLY)
    {
        var model = TestDataManager.CreateTestModel();
        model.RetirementDate = _retirementDate;
        model.RebalanceFrequency = frequency;
        return model;
    }
    private PgPerson CreateTestPerson()
    {
        return new PgPerson
        {
            Id = Guid.NewGuid(),
            Name = "Test Person",
            BirthDate = LocalDateTime.FromDateTime(DateTime.Now.AddYears(-30)),
            AnnualSalary = 50000M,
            AnnualBonus = 5000M,
            MonthlyFullSocialSecurityBenefit = 2000M,
            Annual401KMatchPercent = 0.05M,
            IsRetired = true,
            IsBankrupt = true,
            AnnualSocialSecurityWage = 1500M * 12m,
            Annual401KContribution = 1,
            AnnualHsaContribution = 2,
            AnnualHsaEmployerContribution = 3,
            FederalAnnualWithholding = 4,
            StateAnnualWithholding = 5,
            PreTaxHealthDeductions = 12,
            PostTaxInsuranceDeductions = 13,
            RequiredMonthlySpend = 14,
            RequiredMonthlySpendHealthCare = 15,
            ThisYearsIncomePreSimStart = 0,
            ThisYearsFederalTaxWithholdingPreSimStart = 0,
            ThisYearsStateTaxWithholdingPreSimStart = 0,
        };
    }

    

    [Theory]
    [InlineData(RebalanceFrequency.MONTHLY, 1, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 2, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 3, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 4, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 5, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 6, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 7, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 8, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 9, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 10, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 11, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 12, true)]
    [InlineData(RebalanceFrequency.QUARTERLY, 1, true)]
    [InlineData(RebalanceFrequency.QUARTERLY, 2, false)]
    [InlineData(RebalanceFrequency.QUARTERLY, 3, false)]
    [InlineData(RebalanceFrequency.QUARTERLY, 4, true)]
    [InlineData(RebalanceFrequency.QUARTERLY, 5, false)]
    [InlineData(RebalanceFrequency.QUARTERLY, 6, false)]
    [InlineData(RebalanceFrequency.QUARTERLY, 7, true)]
    [InlineData(RebalanceFrequency.QUARTERLY, 8, false)]
    [InlineData(RebalanceFrequency.QUARTERLY, 9, false)]
    [InlineData(RebalanceFrequency.QUARTERLY, 10, true)]
    [InlineData(RebalanceFrequency.QUARTERLY, 11, false)]
    [InlineData(RebalanceFrequency.QUARTERLY, 12, false)]
    [InlineData(RebalanceFrequency.YEARLY, 1, true)]
    [InlineData(RebalanceFrequency.YEARLY, 2, false)]
    [InlineData(RebalanceFrequency.YEARLY, 3, false)]
    [InlineData(RebalanceFrequency.YEARLY, 4, false)]
    [InlineData(RebalanceFrequency.YEARLY, 5, false)]
    [InlineData(RebalanceFrequency.YEARLY, 6, false)]
    [InlineData(RebalanceFrequency.YEARLY, 7, false)]
    [InlineData(RebalanceFrequency.YEARLY, 8, false)]
    [InlineData(RebalanceFrequency.YEARLY, 9, false)]
    [InlineData(RebalanceFrequency.YEARLY, 10, false)]
    [InlineData(RebalanceFrequency.YEARLY, 11, false)]
    [InlineData(RebalanceFrequency.YEARLY, 12, false)]
    public void CalculateWhetherItsBucketRebalanceTime_ChecksFrequencyCorrectly(
        RebalanceFrequency frequency, int month, bool expectedResult)
    {
        // Arrange
        var model = CreateTestModel(frequency);
        var currentDate = new LocalDateTime(2029, month, 1, 0, 0); // Within rebalance window

        // Act
        var result = Rebalance.CalculateWhetherItsBucketRebalanceTime(currentDate, model);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void MoveFromInvestmentToCash_SellsCorrectAmount()
    {
        // Arrange
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var position = TestDataManager.CreateTestInvestmentPosition(100m, 1m, McInvestmentPositionType.LONG_TERM);
        accounts.InvestmentAccounts.Add(TestDataManager.CreateTestInvestmentAccount(
            new List<McInvestmentPosition> { position },
            McInvestmentAccountType.TAXABLE_BROKERAGE));
        var taxLedger = new TaxLedger();

        var expectedSale = 50m;

        // Act
        var result = Rebalance.MoveFromInvestmentToCash(
            accounts, 50m, McInvestmentPositionType.LONG_TERM, _baseDate, taxLedger);
        var newCashBalance = AccountCalculation.CalculateCashBalance(result.newBookOfAccounts);

        // Assert
        Assert.Equal(expectedSale, result.amountMoved);
        Assert.Equal(expectedSale, newCashBalance);
    }

    [Fact]
    public void InvestExcessCash_PreRebalancingTime_InvestsCorrectly()
    {
        // Arrange
        var debtPayment1 = 399.99m;
        var debtPayment2 = 400.01m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.DebtAccounts.Add(new McDebtAccount()
        {
            Id = Guid.NewGuid(),
            Name = "test debt account",
            Positions = [
                // one open, one closed
                new McDebtPosition(){ Id = Guid.NewGuid(), IsOpen = true, CurrentBalance = 75000m, 
                    MonthlyPayment = debtPayment1, Name = "test p", AnnualPercentageRate = 0.23m, Entry = _baseDate },
                new McDebtPosition(){ Id = Guid.NewGuid(), IsOpen = false, CurrentBalance = 75000m, 
                    MonthlyPayment = debtPayment2, Name = "test p", AnnualPercentageRate = 0.23m, Entry = _baseDate }
            ]
        });
        var currentPrices = new CurrentPrices
        {
            CurrentLongTermInvestmentPrice = 100m
        };
        var model = CreateTestModel();
        model.RetirementDate = _baseDate.PlusYears(27);
        model.NumMonthsPriorToRetirementToBeginRebalance = 12; // well into the future
        var person = CreateTestPerson();
        person.RequiredMonthlySpend = 1000;
        person.RequiredMonthlySpendHealthCare = 1500;
        var expectedCashReserve = 
            person.RequiredMonthlySpend + person.RequiredMonthlySpendHealthCare + debtPayment1;
        
        // start out with $100k in cash
        var initialCashBalance = 100000m;
        accounts = AccountCashManagement.DepositCash(accounts, initialCashBalance, _baseDate).accounts;
        
        var expectedInvestment = initialCashBalance - expectedCashReserve;
        

        // Act
        var result = Rebalance.InvestExcessCash(
            _baseDate, accounts, currentPrices, model, person).newBookOfAccounts;
        var actualLongTermInvestmentBalance = AccountCalculation.CalculateLongBucketTotalBalance(result);
        var actualBrokerageBalance = AccountCalculation.CalculateInvestmentAccountTotalValue(result.Brokerage);

        // Assert
        Assert.Equal(expectedInvestment, actualLongTermInvestmentBalance); // Should invest it all in long-term
        Assert.Equal(expectedInvestment, actualBrokerageBalance); // Should invest it all in the brokerage account
    }
    [Fact]
    public void InvestExcessCash_PostRebalancingTime_InvestsCorrectly()
    {
        // Arrange
        var debtPayment1 = 399.07m;
        var debtPayment2 = 400.11m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.DebtAccounts.Add(new McDebtAccount()
        {
            Id = Guid.NewGuid(),
            Name = "test debt account",
            Positions = [
                // one open, one closed
                new McDebtPosition(){ Id = Guid.NewGuid(), IsOpen = true, CurrentBalance = 75000m, 
                    MonthlyPayment = debtPayment1, Name = "test p", AnnualPercentageRate = 0.23m, Entry = _baseDate },
                new McDebtPosition(){ Id = Guid.NewGuid(), IsOpen = false, CurrentBalance = 75000m, 
                    MonthlyPayment = debtPayment2, Name = "test p", AnnualPercentageRate = 0.23m, Entry = _baseDate }
            ]
        });
        var currentPrices = new CurrentPrices
        {
            CurrentLongTermInvestmentPrice = 100m
        };
        var model = CreateTestModel();
        model.RetirementDate = _baseDate.PlusYears(1);
        model.NumMonthsPriorToRetirementToBeginRebalance = 18; // close enough
        model.NumMonthsCashOnHand = 15;
        var person = CreateTestPerson();
        person.BirthDate = _baseDate.PlusYears(-60); // age is 60, so no medicare anytime soon
        person.RequiredMonthlySpend = 1000;
        person.RequiredMonthlySpendHealthCare = 1500;
        model.DesiredMonthlySpendPostRetirement = 700;
        model.DesiredMonthlySpendPreRetirement = 600; // same number to make it easier
        var spanUntilRetirement = (model.RetirementDate - _baseDate);
        var numMonthsUntilRetirement = (spanUntilRetirement.Years * 12) + spanUntilRetirement.Months;
        var requiredSpend = (person.RequiredMonthlySpend * model.NumMonthsCashOnHand);
        var requiredSpendHealthCare =  (person.RequiredMonthlySpendHealthCare *
                                       (model.NumMonthsCashOnHand - numMonthsUntilRetirement));
        var desiredSpendPre = (model.DesiredMonthlySpendPreRetirement * numMonthsUntilRetirement);
        var desiredSpendPost = (model.DesiredMonthlySpendPostRetirement *
                                (model.NumMonthsCashOnHand - numMonthsUntilRetirement));
        var expectedCashReserve = 0m
            + requiredSpend
            + requiredSpendHealthCare
            + (debtPayment1 * model.NumMonthsCashOnHand)
            + desiredSpendPre
            + desiredSpendPost
            ;
        
        // start out more money than you need
        var initialCashBalance = expectedCashReserve * 3m;
        accounts = AccountCashManagement.DepositCash(accounts, initialCashBalance, _baseDate).accounts;
        
        var expectedInvestment = initialCashBalance - expectedCashReserve;
        

        // Act
        var result = Rebalance.InvestExcessCash(
            _baseDate, accounts, currentPrices, model, person).newBookOfAccounts;
        var actualLongTermInvestmentBalance = AccountCalculation.CalculateLongBucketTotalBalance(result);
        var actualBrokerageBalance = AccountCalculation.CalculateInvestmentAccountTotalValue(result.Brokerage);

        // Assert
        Assert.Equal(expectedInvestment, actualLongTermInvestmentBalance); // Should invest it all in long-term
        Assert.Equal(expectedInvestment, actualBrokerageBalance); // Should invest it all in the brokerage account
    }
    
    [Fact]
    public void InvestExcessCash_WithExcessCash_ReducesTheCashBalanceCorrectly()
    {
        // Arrange
        var debtPayment1 = 399.99m;
        var debtPayment2 = 400.01m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.DebtAccounts.Add(new McDebtAccount()
        {
            Id = Guid.NewGuid(),
            Name = "test debt account",
            Positions = [
                // one open, one closed
                new McDebtPosition(){ Id = Guid.NewGuid(), IsOpen = true, CurrentBalance = 75000m, 
                    MonthlyPayment = debtPayment1, Name = "test p", AnnualPercentageRate = 0.23m, Entry = _baseDate },
                new McDebtPosition(){ Id = Guid.NewGuid(), IsOpen = false, CurrentBalance = 75000m, 
                    MonthlyPayment = debtPayment2, Name = "test p", AnnualPercentageRate = 0.23m, Entry = _baseDate }
            ]
        });
        var currentPrices = new CurrentPrices
        {
            CurrentLongTermInvestmentPrice = 100m
        };
        var model = CreateTestModel();
        model.RetirementDate = _baseDate.PlusYears(27);
        model.NumMonthsPriorToRetirementToBeginRebalance = 12; // well into the future
        var person = CreateTestPerson();
        person.RequiredMonthlySpend = 1000;
        person.RequiredMonthlySpendHealthCare = 1500;
        var expectedCashReserve = 
            person.RequiredMonthlySpend + person.RequiredMonthlySpendHealthCare + debtPayment1;
        
        // start out with $100k in cash
        var initialCashBalance = 100000m;
        accounts = AccountCashManagement.DepositCash(accounts, initialCashBalance, _baseDate).accounts;
        
        var expectedInvestment = initialCashBalance - expectedCashReserve;
        

        // Act
        var result = Rebalance.InvestExcessCash(
            _baseDate, accounts, currentPrices, model, person).newBookOfAccounts;
        var actualCash = AccountCalculation.CalculateCashBalance(result);

        // Assert
        Assert.Equal(initialCashBalance - expectedInvestment, actualCash);
    }

    [Fact]
    public void RebalanceLongToMid_DuringRecession_DoesNotRebalance()
    {
        var model = CreateTestModel(RebalanceFrequency.MONTHLY);
        var person = CreateTestPerson();
        person.BirthDate = new LocalDateTime(1976, 3, 7, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(62); // the magic age When you are retired but have no medicare
        model.NumMonthsCashOnHand = 8;
        model.NumMonthsMidBucketOnHand = 6;
        model.DesiredMonthlySpendPostRetirement = 1000;
        person.RequiredMonthlySpend = 1000;
        person.RequiredMonthlySpendHealthCare = 500;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var currentDate = model.RetirementDate.PlusMonths(12); // Within rebalance window, post retirement, pre-medicare
        var recessionStats = new RecessionStats();
        recessionStats.AreWeInARecession = true;
        
        var totalMidAmountNeeded = Spend.CalculateCashNeedForNMonths(model, person, accounts,
            currentDate, model.NumMonthsMidBucketOnHand);
        
        var desiredTraditional = 5000m; // well under our total mid needed line; trad should move first
        var desiredRoth = 2000m; // still under our total mid needed line; roth shouldn't get touched
        var desiredBrokerage = 25000m; // well over our total mid needed line; brokerage should move second 
        var totalTraditionalSoFar = 0m;
        var totalRothSoFar = 0m;
        var totalBrokerageSoFar = 0m;
        var priceEach = 10m;
        var quantityEach = 100;
        while (totalTraditionalSoFar < desiredTraditional)
        {
            var position1 = TestDataManager.CreateTestInvestmentPosition(
                priceEach, quantityEach, McInvestmentPositionType.LONG_TERM);
            position1.InitialCost = priceEach * quantityEach;
            position1.Entry = new LocalDateTime(2020, 1, 1, 0, 0);
            accounts.Traditional401K.Positions.Add(position1);
            totalTraditionalSoFar += priceEach * quantityEach;
        }
        while (totalRothSoFar < desiredRoth)
        {
            var position1 = TestDataManager.CreateTestInvestmentPosition(
                priceEach, quantityEach, McInvestmentPositionType.LONG_TERM);
            position1.InitialCost = priceEach * quantityEach;
            position1.Entry = new LocalDateTime(2020, 1, 1, 0, 0);
            accounts.Roth401K.Positions.Add(position1);
            totalRothSoFar += priceEach * quantityEach;
        }
        while (totalBrokerageSoFar < desiredBrokerage)
        {
            var position1 = TestDataManager.CreateTestInvestmentPosition(
                priceEach, quantityEach, McInvestmentPositionType.LONG_TERM);
            position1.InitialCost = priceEach * quantityEach * 0.5m; // $500 profit
            position1.Entry = new LocalDateTime(2020, 1, 1, 0, 0);
            accounts.Brokerage.Positions.Add(position1);
            totalBrokerageSoFar += priceEach * quantityEach;
        }
        
        var expectedTradMidBalance = 0m;
        var expectedTradLongBalance = desiredTraditional;
        var expectedRothMidBalance = 0m;
        var expectedRothLongBalance = desiredRoth;
        var expectedBrokerageMidBalance = 0m;
        var expectedBrokerageLongBalance = desiredBrokerage;
        var expectedIraDistributions = 0m;
        var expectedCapitalGains = 0m;
        

        // Act
        var result = Rebalance.RebalanceLongToMid(
            currentDate, accounts, recessionStats, new CurrentPrices(), model, new TaxLedger(), person);

        var actualTradMidBalance = result.newBookOfAccounts.Traditional401K.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.MID_TERM)
            .Sum(x => x.CurrentValue);
        var actualTradLongBalance = result.newBookOfAccounts.Traditional401K.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.LONG_TERM)
            .Sum(x => x.CurrentValue);
        var actualRothMidBalance = result.newBookOfAccounts.Roth401K.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.MID_TERM)
            .Sum(x => x.CurrentValue);
        var actualRothLongBalance = result.newBookOfAccounts.Roth401K.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.LONG_TERM)
            .Sum(x => x.CurrentValue);
        var actualBrokerageMidBalance = result.newBookOfAccounts.Brokerage.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.MID_TERM)
            .Sum(x => x.CurrentValue);
        var actualBrokerageLongBalance = result.newBookOfAccounts.Brokerage.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.LONG_TERM)
            .Sum(x => x.CurrentValue);
        var actualIraDistributions = result.newLedger.TaxableIraDistribution
            .Sum(x => x.amount);;
        var actualCapitalGains = result.newLedger.LongTermCapitalGains
            .Sum(x => x.amount);;

        // Assert
        Assert.Equal(Math.Round(expectedTradMidBalance, 2),  Math.Round(actualTradMidBalance, 2));
        Assert.Equal(Math.Round(expectedTradLongBalance, 2),  Math.Round(actualTradLongBalance, 2));
        Assert.Equal(Math.Round(expectedRothMidBalance, 2),  Math.Round(actualRothMidBalance, 2));
        Assert.Equal(Math.Round(expectedRothLongBalance, 2),  Math.Round(actualRothLongBalance, 2));
        Assert.Equal(Math.Round(expectedBrokerageMidBalance, 2),  Math.Round(actualBrokerageMidBalance, 2));
        Assert.Equal(Math.Round(expectedBrokerageLongBalance, 2),  Math.Round(actualBrokerageLongBalance, 2));
        Assert.Equal(Math.Round(expectedIraDistributions, 2),  Math.Round(actualIraDistributions, 2));
        Assert.Equal(Math.Round(expectedCapitalGains, 2),  Math.Round(actualCapitalGains, 2));
    }

    [Fact]
    public void RebalanceLongToMid_MovesFromTaxDeferredAccountsFirst()
    {
        var model = CreateTestModel(RebalanceFrequency.MONTHLY);
        var person = CreateTestPerson();
        person.BirthDate = new LocalDateTime(1976, 3, 7, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(62); // the magic age When you are retired but have no medicare
        model.NumMonthsCashOnHand = 8;
        model.NumMonthsMidBucketOnHand = 6;
        model.DesiredMonthlySpendPostRetirement = 1000;
        person.RequiredMonthlySpend = 1000;
        person.RequiredMonthlySpendHealthCare = 500;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var currentDate = model.RetirementDate.PlusMonths(12); // Within rebalance window, post retirement, pre-medicare
        
        var totalMidAmountNeeded = Spend.CalculateCashNeedForNMonths(model, person, accounts,
            currentDate, model.NumMonthsMidBucketOnHand);
        
        var desiredTraditional = 5000m; // well under our total mid needed line; trad should move first
        var desiredRoth = 2000m; // still under our total mid needed line; roth shouldn't get touched
        var desiredBrokerage = 25000m; // well over our total mid needed line; brokerage should move second 
        var totalTraditionalSoFar = 0m;
        var totalRothSoFar = 0m;
        var totalBrokerageSoFar = 0m;
        var priceEach = 10m;
        var quantityEach = 100;
        while (totalTraditionalSoFar < desiredTraditional)
        {
            var position1 = TestDataManager.CreateTestInvestmentPosition(
                priceEach, quantityEach, McInvestmentPositionType.LONG_TERM);
            position1.InitialCost = priceEach * quantityEach;
            position1.Entry = new LocalDateTime(2020, 1, 1, 0, 0);
            accounts.Traditional401K.Positions.Add(position1);
            totalTraditionalSoFar += priceEach * quantityEach;
        }
        while (totalRothSoFar < desiredRoth)
        {
            var position1 = TestDataManager.CreateTestInvestmentPosition(
                priceEach, quantityEach, McInvestmentPositionType.LONG_TERM);
            position1.InitialCost = priceEach * quantityEach;
            position1.Entry = new LocalDateTime(2020, 1, 1, 0, 0);
            accounts.Roth401K.Positions.Add(position1);
            totalRothSoFar += priceEach * quantityEach;
        }
        while (totalBrokerageSoFar < desiredBrokerage)
        {
            var position1 = TestDataManager.CreateTestInvestmentPosition(
                priceEach, quantityEach, McInvestmentPositionType.LONG_TERM);
            position1.InitialCost = priceEach * quantityEach * 0.5m; // $500 profit
            position1.Entry = new LocalDateTime(2020, 1, 1, 0, 0);
            accounts.Brokerage.Positions.Add(position1);
            totalBrokerageSoFar += priceEach * quantityEach;
        }
        
        var expectedTradMidBalance = desiredTraditional;
        var expectedTradLongBalance = 0m;
        var expectedRothMidBalance = 0m;
        var expectedRothLongBalance = desiredRoth;
        var expectedBrokerageMidBalance = totalMidAmountNeeded - expectedTradMidBalance;
        var expectedBrokerageLongBalance = desiredBrokerage - expectedBrokerageMidBalance;
        var expectedIraDistributions = 0m;
        var expectedCapitalGains = expectedBrokerageMidBalance * .5m;
        

        // Act
        var result = Rebalance.RebalanceLongToMid(
            currentDate, accounts, new RecessionStats(), new CurrentPrices(), model, new TaxLedger(), person);

        var actualTradMidBalance = result.newBookOfAccounts.Traditional401K.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.MID_TERM)
            .Sum(x => x.CurrentValue);
        var actualTradLongBalance = result.newBookOfAccounts.Traditional401K.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.LONG_TERM)
            .Sum(x => x.CurrentValue);
        var actualRothMidBalance = result.newBookOfAccounts.Roth401K.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.MID_TERM)
            .Sum(x => x.CurrentValue);
        var actualRothLongBalance = result.newBookOfAccounts.Roth401K.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.LONG_TERM)
            .Sum(x => x.CurrentValue);
        var actualBrokerageMidBalance = result.newBookOfAccounts.Brokerage.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.MID_TERM)
            .Sum(x => x.CurrentValue);
        var actualBrokerageLongBalance = result.newBookOfAccounts.Brokerage.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.LONG_TERM)
            .Sum(x => x.CurrentValue);
        var actualIraDistributions = result.newLedger.TaxableIraDistribution
            .Sum(x => x.amount);;
        var actualCapitalGains = result.newLedger.LongTermCapitalGains
            .Sum(x => x.amount);;

        // Assert
        Assert.Equal(Math.Round(expectedTradMidBalance, 2),  Math.Round(actualTradMidBalance, 2));
        Assert.Equal(Math.Round(expectedTradLongBalance, 2),  Math.Round(actualTradLongBalance, 2));
        Assert.Equal(Math.Round(expectedRothMidBalance, 2),  Math.Round(actualRothMidBalance, 2));
        Assert.Equal(Math.Round(expectedRothLongBalance, 2),  Math.Round(actualRothLongBalance, 2));
        Assert.Equal(Math.Round(expectedBrokerageMidBalance, 2),  Math.Round(actualBrokerageMidBalance, 2));
        Assert.Equal(Math.Round(expectedBrokerageLongBalance, 2),  Math.Round(actualBrokerageLongBalance, 2));
        Assert.Equal(Math.Round(expectedIraDistributions, 2),  Math.Round(actualIraDistributions, 2));
        Assert.Equal(Math.Round(expectedCapitalGains, 2),  Math.Round(actualCapitalGains, 2));
    }
    
    [Fact]
    public void RebalancePortfolio_AtRebalanceTime_ExecutesRebalancingCorrectly()
    {
        /*
         * right, this is a bit of a mess, as rebalance is so very complicated. basically, as you age, your expected
         * spend will change, and, therefore, your amount of cash needed isn't linear. So you need to use the same
         * method to calculate that here as you do in the rebalance. Don't worry, we'll UT that on its own separately.
         *
         * Once you know your cash and mid amounts that you want on-hand, you have to sell enough [strike]whole positions until
         * you at or over that amount[/strike]. Each sale will log a capital gain and each sale will update your cash balance.
         * When rebalancing to top up your mid bucket, you'll need to sell, then withdraw cash, then buy.
         *
         * This checks whether all that works.
         */
        // Arrange
        
        var model = CreateTestModel(RebalanceFrequency.MONTHLY);
        var person = CreateTestPerson();
        person.BirthDate = new LocalDateTime(1976, 3, 7, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(62); // the magic age When you are retired but have no medicare
        var currentDate = model.RetirementDate.PlusMonths(12); // Within rebalance window, post retirement
        var fiveYearsAgo = currentDate.PlusYears(-5);
        model.NumMonthsCashOnHand = 18;
        model.NumMonthsMidBucketOnHand = 24;
        model.DesiredMonthlySpendPostRetirement = 1000;
        person.RequiredMonthlySpend = 1000;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        // we'll need 36k in cash and 48k in mid. start out with 100k in long and see where that takes you
        var longtermTotalWanted = 100000m;
        var position = TestDataManager.CreateTestInvestmentPosition(
            1m, longtermTotalWanted, McInvestmentPositionType.LONG_TERM, 
            true, 0.5m, fiveYearsAgo );
        accounts.Brokerage.Positions.Add(position);
        
        var cashNeededTotal = Spend.CalculateCashNeedForNMonths(
            model, person, accounts, currentDate, model.NumMonthsCashOnHand);
        var midNeededTotal = Spend.CalculateCashNeedForNMonths(
            model, person, accounts, currentDate, model.NumMonthsMidBucketOnHand);
        
        
        var expectedCashBalance = cashNeededTotal;
        var expectedMidBalance = midNeededTotal;
        
        var amountSoldFromLong = cashNeededTotal + midNeededTotal;
        var expectedLongBalance = longtermTotalWanted - amountSoldFromLong;
        var expectedCapitalGains = amountSoldFromLong * .5m;
        
        

        // Act
        var result = Rebalance.RebalancePortfolio(
            currentDate, accounts, new RecessionStats(), new CurrentPrices(), model, new TaxLedger(), person);
        
        var actualCashBalance = AccountCalculation.CalculateCashBalance(result.newBookOfAccounts);
        var actualMidBalance = AccountCalculation.CalculateMidBucketTotalBalance(result.newBookOfAccounts);
        var actualLongBalance = AccountCalculation.CalculateLongBucketTotalBalance(result.newBookOfAccounts);
        var actualCapitalGains = result.newLedger.LongTermCapitalGains
            .Where(x => x.earnedDate.Year == currentDate.Year)
            .Sum(x => x.amount);

        // Assert
        Assert.Equal(Math.Round(expectedCashBalance  ,2),  Math.Round(actualCashBalance, 2));
        Assert.Equal(Math.Round(expectedMidBalance   ,2),   Math.Round(actualMidBalance, 2));
        Assert.Equal(Math.Round(expectedLongBalance  ,2),  Math.Round(actualLongBalance, 2));
        Assert.Equal(Math.Round(expectedCapitalGains ,2), Math.Round(actualCapitalGains, 2));
    }

    [Fact]
    public void RebalancePortfolio_DoesntChangeNetWorth()
    {
        // just set up enough to make sure that we have crap in long, nothing in mid or cash so that we'll move both
        var model = CreateTestModel(RebalanceFrequency.MONTHLY);
        var person = CreateTestPerson();
        model.NumMonthsCashOnHand = 18;
        model.NumMonthsMidBucketOnHand = 24;
        model.DesiredMonthlySpendPostRetirement = 1000;
        person.RequiredMonthlySpend = 1000;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        // we'll need 36k in cash and 48k in mid
        var numPositionsWanted = 100;
        var priceEach = 10m;
        var quantityEach = 100;
        for (int i = 0; i < numPositionsWanted; i++)
        {
            var position = TestDataManager.CreateTestInvestmentPosition(
                priceEach, quantityEach, McInvestmentPositionType.LONG_TERM);
            position.InitialCost = priceEach * quantityEach * 0.5m; // 100% growth
            position.Entry = new LocalDateTime(2020, 1, 1, 0, 0);
            accounts.Brokerage.Positions.Add(position);
        }
        var currentDate = _retirementDate.PlusMonths(12); // Within rebalance window, post retirement
        person.BirthDate = new LocalDateTime(1976, 3, 7, 0, 0);
        var expectedNetWorth = AccountCalculation.CalculateNetWorth(accounts);
        
        
        

        // Act
        var result = Rebalance.RebalancePortfolio(
            currentDate, accounts, new RecessionStats(), new CurrentPrices(), model, new TaxLedger(), person);
        
        var actualNetWorth = AccountCalculation.CalculateNetWorth(result.newBookOfAccounts);

        // Assert
        Assert.Equal(Math.Round(expectedNetWorth  ,2),  Math.Round(actualNetWorth, 2));
    }

    [Fact]
    public void SellInOrder_SellsFromProvidedOrder()
    {
        // Arrange
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var position1 = TestDataManager.CreateTestInvestmentPosition(
            100m, 1m, McInvestmentPositionType.LONG_TERM);
        var position2 = TestDataManager.CreateTestInvestmentPosition(
            100m, 1m, McInvestmentPositionType.LONG_TERM);
        var position3 = TestDataManager.CreateTestInvestmentPosition(
            100m, 1m, McInvestmentPositionType.MID_TERM);
        var position4 = TestDataManager.CreateTestInvestmentPosition(
            100m, 1m, McInvestmentPositionType.MID_TERM);
        
        accounts.InvestmentAccounts.Add(TestDataManager.CreateTestInvestmentAccount(
            new List<McInvestmentPosition> { position1, position2, position3, position4 },
            McInvestmentAccountType.TAXABLE_BROKERAGE));
        var pullOrder = new[] { McInvestmentPositionType.LONG_TERM, McInvestmentPositionType.MID_TERM };

        // Act
        var result = Rebalance.SellInOrder(300m, pullOrder, accounts, new TaxLedger(), _baseDate);
        var actualMidBucketBalance = AccountCalculation.CalculateMidBucketTotalBalance(result.newAccounts);
        var actualLongBucketBalance = AccountCalculation.CalculateLongBucketTotalBalance(result.newAccounts);
        
        // Assert
        Assert.Equal(300m, result.amountSold);
        Assert.Equal(100m, actualMidBucketBalance);
        Assert.Equal(0m, actualLongBucketBalance);
    }

    
}