using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.MonteCarlo.WithdrawalStrategy;
using NodaTime;

namespace Lib.Tests.MonteCarlo.WithdrawalStrategy;

public class BasicBucketsIncomeThresholdTests
{
    private McInvestmentAccount CreateInvestmentAccountWithPositions(
        int count, decimal amountPerPosition, decimal pricePerShare, McInvestmentAccountType accountType,
        McInvestmentPositionType positionType)
    {
        return TestDataManager.CreateTestInvestmentAccount(
            CreateInvestmentPositions(
                count, amountPerPosition, pricePerShare, positionType)
            , accountType);
    }
    private List<McInvestmentPosition> CreateInvestmentPositions(
        int count, decimal amountPerPosition, decimal pricePerShare, McInvestmentPositionType positionType)
    {
        var positions = new List<McInvestmentPosition>();
        for (var i = 0; i < count; i++)
        {
            positions.Add(new McInvestmentPosition()
            {
                Id = Guid.Empty,
                Name = "Test Position",
                Entry = LocalDateTime.FromDateTime(DateTime.Now).PlusYears(-2),
                IsOpen = true,
                InvestmentPositionType = positionType,
                Price = pricePerShare,
                Quantity = amountPerPosition / pricePerShare,
                InitialCost = (amountPerPosition / 2m),
            });
        }
        return positions;
    }
    
    [Theory]
    [InlineData(40, 40, 0,0,140000)]
    [InlineData(60, 40, 0,0,160000)]
    [InlineData(80, 40, 0,0,180000)]
    [InlineData(100, 40, 0,16500,183500)]
    [InlineData(40, 60, 0,0,160000)]
    [InlineData(60, 60, 0,0,180000)]
    [InlineData(80, 60, 0,16500,183500)]
    [InlineData(100, 60, 0,36500,183500)]
    [InlineData(40, 80, 0,0,180000)]
    [InlineData(60, 80, 0,16500,183500)]
    [InlineData(80, 80, 0,36500,183500)]
    [InlineData(100, 80, 0,56500,183500)]
    [InlineData(40, 100, 0,16500,183500)]
    [InlineData(60, 100, 0,36500,183500)]
    [InlineData(80, 100, 0,56500,183500)]
    [InlineData(100, 100, 0,76500,183500)]


    // test values created in the testing spreadsheet attached to this solution in the tab named "basic buckets sales order test"
    public void SellInvestmentsToDollarAmount_WithIncomeRoom_SellsInCorrectOrder(
        int countDeferred, int countTaxable, decimal expectedEndBalDeferred, decimal expectedEndBalTaxable,
        decimal expectedEndBalTaxFree)
    {
        // Arrange
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var amountPerPosition = 1000m;
        var pricePerShare = 10m;
        var amountToSell = 140000m;
        var countTaxFree = 200;

        accounts.InvestmentAccounts.Add(
            CreateInvestmentAccountWithPositions(countDeferred, amountPerPosition,
            pricePerShare, McInvestmentAccountType.TRADITIONAL_401_K, McInvestmentPositionType.LONG_TERM));
        accounts.InvestmentAccounts.Add(
            CreateInvestmentAccountWithPositions(countTaxable, amountPerPosition,
                pricePerShare, McInvestmentAccountType.TAXABLE_BROKERAGE, McInvestmentPositionType.LONG_TERM));
        accounts.InvestmentAccounts.Add(
            CreateInvestmentAccountWithPositions(countTaxFree, amountPerPosition,
                pricePerShare, McInvestmentAccountType.ROTH_IRA, McInvestmentPositionType.LONG_TERM));
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.BasicBucketsIncomeThreshold);
        var ledger = new TaxLedger();
        var currentDate = LocalDateTime.FromDateTime(DateTime.Now);
        
       

        // Act
        var result = model.WithdrawalStrategy.SellInvestmentsToDollarAmount(
            accounts, ledger, currentDate, amountToSell, model);
        var actualEndBalDeferred = AccountCalculation.CalculateTotalBalanceByMultipleFactors(
            result.accounts, [McInvestmentAccountType.TRADITIONAL_401_K]);
        var actualEndBalTaxable = AccountCalculation.CalculateTotalBalanceByMultipleFactors(
            result.accounts, [McInvestmentAccountType.TAXABLE_BROKERAGE]);
        var actualEndBalTaxFree = AccountCalculation.CalculateTotalBalanceByMultipleFactors(
            result.accounts, [McInvestmentAccountType.ROTH_IRA]);
        
        // Assert
        Assert.Equal(expectedEndBalDeferred, actualEndBalDeferred);
        Assert.Equal(expectedEndBalTaxable, actualEndBalTaxable);
        Assert.Equal(expectedEndBalTaxFree, actualEndBalTaxFree);
    }
    
    [Fact]
    public void RebalancePortfolio_YieldsCorrectCashAndMid()
    {
        // Arrange
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = new LocalDateTime(1976, 3, 7, 0, 0);
        person.RequiredMonthlySpend = 1000;
        person.RequiredMonthlySpendHealthCare = 500;
        
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.BasicBucketsIncomeThreshold);
        model.RetirementDate = person.BirthDate.PlusYears(62); // the magic age When you are retired but have no medicare
        model.RebalanceFrequency = RebalanceFrequency.MONTHLY;
        model.NumMonthsCashOnHand = 12;
        model.NumMonthsMidBucketOnHand = 6;
        model.NumMonthsPriorToRetirementToBeginRebalance = 12; 
        model.DesiredMonthlySpendPostRetirement = 800;
        model.DesiredMonthlySpendPreRetirement = 600; 
        
        var currentDate = person.BirthDate.PlusYears(63); // Within rebalance window, post retirement, pre-medicare
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        
        var cashNeededOnHand =
            Spend.CalculateCashNeedForNMonths(model, person, accounts, currentDate, model.NumMonthsCashOnHand);
        var midNeededOnHand =
            Spend.CalculateCashNeedForNMonths(model, person, accounts, currentDate, model.NumMonthsMidBucketOnHand);
        
        var position = TestDataManager.CreateTestInvestmentPosition(
            cashNeededOnHand, 1.5m, McInvestmentPositionType.LONG_TERM);
        accounts.InvestmentAccounts.Add(TestDataManager.CreateTestInvestmentAccount([ position ],
            McInvestmentAccountType.TAXABLE_BROKERAGE));
        var ledger = new TaxLedger();
        var recessionStats = new RecessionStats
        {
            AreWeInARecession = false,
            AreWeInExtremeAusterityMeasures = false,
            AreWeInLivinLargeMode = false
        };
        var prices = new CurrentPrices();


        var expectedCash = cashNeededOnHand;
        var expectedMid = midNeededOnHand;
        var expectedRemainingLong = AccountCalculation.CalculateLongBucketTotalBalance(accounts)
            - cashNeededOnHand
            - midNeededOnHand;
    
        // Act
        var result = model.WithdrawalStrategy.RebalancePortfolio(
            currentDate, accounts, recessionStats, prices, model, ledger, person);
        
        var actualCash = AccountCalculation.CalculateCashBalance(result.accounts);
        var actualMid = AccountCalculation.CalculateMidBucketTotalBalance(result.accounts);
        var actualRemainingLong = AccountCalculation.CalculateLongBucketTotalBalance(result.accounts);
    
        // Assert
        Assert.Equal(expectedCash, actualCash);
        Assert.Equal(expectedMid, actualMid);
        Assert.Equal(expectedRemainingLong, actualRemainingLong);
    }
    
    [Fact]
    public void RebalancePortfolio_WithTaxDeferredLong_MovesToMinWithoutTaxConsequences()
    {
        // Arrange
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = new LocalDateTime(1976, 3, 7, 0, 0);
        person.RequiredMonthlySpend = 1000;
        person.RequiredMonthlySpendHealthCare = 500;
        
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.BasicBucketsIncomeThreshold);
        model.RetirementDate = person.BirthDate.PlusYears(62); // the magic age When you are retired but have no medicare
        model.RebalanceFrequency = RebalanceFrequency.MONTHLY;
        model.NumMonthsCashOnHand = 12;
        model.NumMonthsMidBucketOnHand = 6;
        model.NumMonthsPriorToRetirementToBeginRebalance = 12; 
        model.DesiredMonthlySpendPostRetirement = 800;
        model.DesiredMonthlySpendPreRetirement = 600; 
        
        var currentDate = person.BirthDate.PlusYears(63); // Within rebalance window, post retirement, pre-medicare
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        
        var cashNeededOnHand =
            Spend.CalculateCashNeedForNMonths(model, person, accounts, currentDate, model.NumMonthsCashOnHand);
        var midNeededOnHand =
            Spend.CalculateCashNeedForNMonths(model, person, accounts, currentDate, model.NumMonthsMidBucketOnHand);
        
        // add more than enough cash so we only have to move long to mid
        accounts = AccountCashManagement.DepositCash(accounts, cashNeededOnHand * 2, currentDate).accounts;
        
        // add everything to long positions in a tax deferred account
        var position = TestDataManager.CreateTestInvestmentPosition(
            midNeededOnHand, 1.5m, McInvestmentPositionType.LONG_TERM);
        accounts.InvestmentAccounts.Add(TestDataManager.CreateTestInvestmentAccount([ position ],
            McInvestmentAccountType.TRADITIONAL_IRA));
        var ledger = new TaxLedger();
        var recessionStats = new RecessionStats
        {
            AreWeInARecession = false,
            AreWeInExtremeAusterityMeasures = false,
            AreWeInLivinLargeMode = false
        };
        var prices = new CurrentPrices();
        
        var expectedMid = midNeededOnHand;
        var expectedRemainingLong = AccountCalculation.CalculateLongBucketTotalBalance(accounts)
            - midNeededOnHand;
        var expectedTax = 0m;
    
        // Act
        var result = model.WithdrawalStrategy.RebalancePortfolio(
            currentDate, accounts, recessionStats, prices, model, ledger, person);
        
        var actualMid = AccountCalculation.CalculateMidBucketTotalBalance(result.accounts);
        var actualRemainingLong = AccountCalculation.CalculateLongBucketTotalBalance(result.accounts);
        var actualTax =
            TaxCalculation.CalculateLongTermCapitalGainsForYear(result.ledger, currentDate.Year) +
            TaxCalculation.CalculateShortTermCapitalGainsForYear(result.ledger, currentDate.Year) +
            TaxCalculation.CalculateW2IncomeForYear(result.ledger, currentDate.Year);
        
    
        // Assert
        Assert.Equal(expectedMid, actualMid);
        Assert.Equal(expectedRemainingLong, actualRemainingLong);
        Assert.Equal(expectedTax, actualTax);
    }
    
    [Fact]
    public void RebalancePortfolio_DoesntChangeNetWorth()
    {
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = new LocalDateTime(1976, 3, 7, 0, 0);
        person.RequiredMonthlySpend = 1000;
        person.RequiredMonthlySpendHealthCare = 500;
        
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.BasicBucketsIncomeThreshold);
        model.RetirementDate = person.BirthDate.PlusYears(62); // the magic age When you are retired but have no medicare
        model.RebalanceFrequency = RebalanceFrequency.MONTHLY;
        model.NumMonthsCashOnHand = 12;
        model.NumMonthsMidBucketOnHand = 6;
        model.NumMonthsPriorToRetirementToBeginRebalance = 12; 
        model.DesiredMonthlySpendPostRetirement = 800;
        model.DesiredMonthlySpendPreRetirement = 600; 
        
        var currentDate = person.BirthDate.PlusYears(63); // Within rebalance window, post retirement, pre-medicare
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        
        var cashNeededOnHand =
            Spend.CalculateCashNeedForNMonths(model, person, accounts, currentDate, model.NumMonthsCashOnHand);
        var midNeededOnHand =
            Spend.CalculateCashNeedForNMonths(model, person, accounts, currentDate, model.NumMonthsMidBucketOnHand);
        
       // add everything to long positions in a tax deferred account
        var position = TestDataManager.CreateTestInvestmentPosition(
            cashNeededOnHand + midNeededOnHand, 1.5m, McInvestmentPositionType.LONG_TERM);
        accounts.InvestmentAccounts.Add(TestDataManager.CreateTestInvestmentAccount([ position ],
            McInvestmentAccountType.TRADITIONAL_IRA));
        var ledger = new TaxLedger();
        var recessionStats = new RecessionStats
        {
            AreWeInARecession = false,
            AreWeInExtremeAusterityMeasures = false,
            AreWeInLivinLargeMode = false
        };
        var prices = TestDataManager.CreateTestCurrentPrices(
            1m, 100m, 50m, 0m);
        var expectedNetWorth = AccountCalculation.CalculateNetWorth(accounts);
        
    
        // Act
        var result = model.WithdrawalStrategy.RebalancePortfolio(
            currentDate, accounts, recessionStats, prices, model, new TaxLedger(), person);
        
        var actualNetWorth = AccountCalculation.CalculateNetWorth(result.accounts);
    
        // Assert
        Assert.Equal(Math.Round(expectedNetWorth  ,2),  Math.Round(actualNetWorth, 2));
    }
    
    [Fact]
    public void InvestExcessCash_PreRebalancingTime_InvestsCorrectly()
    {
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = new LocalDateTime(1976, 3, 7, 0, 0);
        person.RequiredMonthlySpend = 1000;
        person.RequiredMonthlySpendHealthCare = 1500;
        var currentDate = person.BirthDate.PlusYears(50); // before rebalance window
        
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.BasicBucketsIncomeThreshold);
        model.RetirementDate = person.BirthDate.PlusYears(67); // the magic age When you are retired but have no medicare
        model.RebalanceFrequency = RebalanceFrequency.MONTHLY;
        model.NumMonthsCashOnHand = 12;
        model.NumMonthsMidBucketOnHand = 6;
        model.NumMonthsPriorToRetirementToBeginRebalance = 12; // well into the future
        model.DesiredMonthlySpendPostRetirement = 800;
        model.DesiredMonthlySpendPreRetirement = 600; 
        model.NumMonthsPriorToRetirementToBeginRebalance = 12;
        
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
                    MonthlyPayment = debtPayment1, Name = "test p", AnnualPercentageRate = 0.23m, Entry = currentDate },
                new McDebtPosition(){ Id = Guid.NewGuid(), IsOpen = false, CurrentBalance = 75000m, 
                    MonthlyPayment = debtPayment2, Name = "test p", AnnualPercentageRate = 0.23m, Entry = currentDate }
            ]
        });
        var currentPrices = new CurrentPrices
        {
            CurrentLongTermInvestmentPrice = 100m
        };
        
        
        
        var expectedCashReserve = 
            person.RequiredMonthlySpend + person.RequiredMonthlySpendHealthCare + debtPayment1;
        
        // start out with $100k in cash
        var initialCashBalance = 100000m;
        accounts = AccountCashManagement.DepositCash(accounts, initialCashBalance, currentDate).accounts;
        
        var expectedInvestment = initialCashBalance - expectedCashReserve;
        
    
        // Act
        var result = model.WithdrawalStrategy.InvestExcessCash(
            currentDate, accounts, currentPrices, model, person).accounts;
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
        var baseDate = new LocalDateTime(2025, 1, 1, 0, 0);
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
                    MonthlyPayment = debtPayment1, Name = "test p", AnnualPercentageRate = 0.23m, Entry = baseDate },
                new McDebtPosition(){ Id = Guid.NewGuid(), IsOpen = false, CurrentBalance = 75000m, 
                    MonthlyPayment = debtPayment2, Name = "test p", AnnualPercentageRate = 0.23m, Entry = baseDate }
            ]
        });
        var currentPrices = new CurrentPrices
        {
            CurrentLongTermInvestmentPrice = 100m
        };
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.BasicBucketsIncomeThreshold);
        model.RetirementDate = baseDate.PlusYears(1);
        model.NumMonthsPriorToRetirementToBeginRebalance = 18; // close enough
        model.NumMonthsCashOnHand = 15;
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = baseDate.PlusYears(-60); // age is 60, so no medicare anytime soon
        person.RequiredMonthlySpend = 1000;
        person.RequiredMonthlySpendHealthCare = 1500;
        model.DesiredMonthlySpendPostRetirement = 700;
        model.DesiredMonthlySpendPreRetirement = 600; // same number to make it easier
        var spanUntilRetirement = (model.RetirementDate - baseDate);
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
        accounts = AccountCashManagement.DepositCash(accounts, initialCashBalance, baseDate).accounts;
        
        var expectedInvestment = initialCashBalance - expectedCashReserve;
        
    
        // Act
        var result = model.WithdrawalStrategy.InvestExcessCash(
            baseDate, accounts, currentPrices, model, person).accounts;
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
        var currentDate = new LocalDateTime(2025, 1, 1, 0, 0);
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
                    MonthlyPayment = debtPayment1, Name = "test p", AnnualPercentageRate = 0.23m, Entry = currentDate },
                new McDebtPosition(){ Id = Guid.NewGuid(), IsOpen = false, CurrentBalance = 75000m, 
                    MonthlyPayment = debtPayment2, Name = "test p", AnnualPercentageRate = 0.23m, Entry = currentDate }
            ]
        });
        var currentPrices = new CurrentPrices
        {
            CurrentLongTermInvestmentPrice = 100m
        };
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.BasicBucketsIncomeThreshold);
        model.RetirementDate = currentDate.PlusYears(27);
        model.NumMonthsPriorToRetirementToBeginRebalance = 12; // well into the future
        var person = TestDataManager.CreateTestPerson();
        person.RequiredMonthlySpend = 1000;
        person.RequiredMonthlySpendHealthCare = 1500;
        var expectedCashReserve = 
            person.RequiredMonthlySpend + person.RequiredMonthlySpendHealthCare + debtPayment1;
        
        // start out with $100k in cash
        var initialCashBalance = 100000m;
        accounts = AccountCashManagement.DepositCash(accounts, initialCashBalance, currentDate).accounts;
        
        var expectedInvestment = initialCashBalance - expectedCashReserve;
        
    
        // Act
        var result = model.WithdrawalStrategy.InvestExcessCash(
            currentDate, accounts, currentPrices, model, person).accounts;
        var actualCash = AccountCalculation.CalculateCashBalance(result);
    
        // Assert
        Assert.Equal(initialCashBalance - expectedInvestment, actualCash);
    }
    
    [Fact]
    public void RebalanceLongToMid_DuringRecession_DoesNotRebalance()
    {
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.BasicBucketsIncomeThreshold);
        model.RebalanceFrequency = RebalanceFrequency.MONTHLY;
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = new LocalDateTime(1976, 3, 7, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(62); // the magic age When you are retired but have no medicare
        model.NumMonthsCashOnHand = 8;
        model.NumMonthsMidBucketOnHand = 6;
        model.DesiredMonthlySpendPostRetirement = 1000;
        person.RequiredMonthlySpend = 1000;
        person.RequiredMonthlySpendHealthCare = 500;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var currentDate = model.RetirementDate.PlusMonths(12); // Within rebalance window, post retirement, pre-medicare
        var recessionStats = new RecessionStats
        {
            AreWeInARecession = true
        };

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
        var result = model.WithdrawalStrategy.RebalancePortfolio(
            currentDate, accounts, recessionStats, new CurrentPrices(), model, new TaxLedger(), person);
    
        var actualTradMidBalance = result.accounts.Traditional401K.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.MID_TERM)
            .Sum(x => x.CurrentValue);
        var actualTradLongBalance = result.accounts.Traditional401K.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.LONG_TERM)
            .Sum(x => x.CurrentValue);
        var actualRothMidBalance = result.accounts.Roth401K.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.MID_TERM)
            .Sum(x => x.CurrentValue);
        var actualRothLongBalance = result.accounts.Roth401K.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.LONG_TERM)
            .Sum(x => x.CurrentValue);
        var actualBrokerageMidBalance = result.accounts.Brokerage.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.MID_TERM)
            .Sum(x => x.CurrentValue);
        var actualBrokerageLongBalance = result.accounts.Brokerage.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.LONG_TERM)
            .Sum(x => x.CurrentValue);
        var actualIraDistributions = result.ledger.TaxableIraDistribution
            .Sum(x => x.amount);
        var actualCapitalGains = result.ledger.LongTermCapitalGains
            .Sum(x => x.amount);
    
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
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.BasicBucketsIncomeThreshold);
        model.RebalanceFrequency = RebalanceFrequency.MONTHLY;
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = new LocalDateTime(1976, 3, 7, 0, 0);
        model.RetirementDate = person.BirthDate.PlusYears(62); // the magic age When you are retired but have no medicare
        model.NumMonthsCashOnHand = 8;
        model.NumMonthsMidBucketOnHand = 6;
        model.DesiredMonthlySpendPostRetirement = 1000;
        person.RequiredMonthlySpend = 1000;
        person.RequiredMonthlySpendHealthCare = 500;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        // load up on cash so we don't first move things and throw off our calcs
        accounts = AccountCashManagement.DepositCash(accounts, 2000000m, model.RetirementDate).accounts;
        
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
        var result = model.WithdrawalStrategy.RebalancePortfolio(
            currentDate, accounts, new RecessionStats(), new CurrentPrices(), model, new TaxLedger(), person);
    
        var actualTradMidBalance = result.accounts.Traditional401K.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.MID_TERM)
            .Sum(x => x.CurrentValue);
        var actualTradLongBalance = result.accounts.Traditional401K.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.LONG_TERM)
            .Sum(x => x.CurrentValue);
        var actualRothMidBalance = result.accounts.Roth401K.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.MID_TERM)
            .Sum(x => x.CurrentValue);
        var actualRothLongBalance = result.accounts.Roth401K.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.LONG_TERM)
            .Sum(x => x.CurrentValue);
        var actualBrokerageMidBalance = result.accounts.Brokerage.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.MID_TERM)
            .Sum(x => x.CurrentValue);
        var actualBrokerageLongBalance = result.accounts.Brokerage.Positions
            .Where(x => x.InvestmentPositionType == McInvestmentPositionType.LONG_TERM)
            .Sum(x => x.CurrentValue);
        var actualIraDistributions = result.ledger.TaxableIraDistribution
            .Sum(x => x.amount);
        var actualCapitalGains = result.ledger.LongTermCapitalGains
            .Sum(x => x.amount);
    
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

        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.BasicBucketsIncomeThreshold);
        model.RebalanceFrequency = RebalanceFrequency.MONTHLY;
        var person = TestDataManager.CreateTestPerson();
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
        var result = model.WithdrawalStrategy.RebalancePortfolio(
            currentDate, accounts, new RecessionStats(), new CurrentPrices(), model, new TaxLedger(), person);
        
        var actualCashBalance = AccountCalculation.CalculateCashBalance(result.accounts);
        var actualMidBalance = AccountCalculation.CalculateMidBucketTotalBalance(result.accounts);
        var actualLongBalance = AccountCalculation.CalculateLongBucketTotalBalance(result.accounts);
        var actualCapitalGains = result.ledger.LongTermCapitalGains
            .Where(x => x.earnedDate.Year == currentDate.Year)
            .Sum(x => x.amount);
    
        // Assert
        Assert.Equal(Math.Round(expectedCashBalance  ,2),  Math.Round(actualCashBalance, 2));
        Assert.Equal(Math.Round(expectedMidBalance   ,2),   Math.Round(actualMidBalance, 2));
        Assert.Equal(Math.Round(expectedLongBalance  ,2),  Math.Round(actualLongBalance, 2));
        Assert.Equal(Math.Round(expectedCapitalGains ,2), Math.Round(actualCapitalGains, 2));
    }
    
    [Fact]
    public void SellInvestmentsToRmdAmount_WithAllMidTermPositions_SellsWhatsNeeded()
    {
        // Arrange
        var currentDate = new LocalDateTime(2050, 1, 1, 0, 0);
        var amountNeeded = 100000m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.TraditionalIra.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            1, amountNeeded * 1.5m, McInvestmentPositionType.MID_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.BasicBucketsIncomeThreshold);
        var expectedAmountSold = amountNeeded;
        // Act
        var results = model.WithdrawalStrategy.SellInvestmentsToRmdAmount(
            amountNeeded, accounts, ledger, currentDate, model);
        var actualAmountSold = results.amountSold;
        // Assert
        Assert.Equal(expectedAmountSold, actualAmountSold);
    }
    
    [Fact]
    public void SellInvestmentsToRmdAmount_WithAllLongTermPositions_SellsWhatsNeeded()
    {
        // Arrange
        var currentDate = new LocalDateTime(2050, 1, 1, 0, 0);
        var amountNeeded = 100000m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.TraditionalIra.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            1, amountNeeded * 1.5m, McInvestmentPositionType.LONG_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.BasicBucketsIncomeThreshold);
        var expectedAmountSold = amountNeeded;
        // Act
        var results = model.WithdrawalStrategy.SellInvestmentsToRmdAmount(
            amountNeeded, accounts, ledger, currentDate, model);
        var actualAmountSold = results.amountSold;
        // Assert
        Assert.Equal(expectedAmountSold, actualAmountSold);
    }
    
    [Fact]
    public void SellInvestmentsToRmdAmount_WithAllRecentPositions_SellsWhatsNeeded()
    {
        // Arrange
        var currentDate = new LocalDateTime(2050, 1, 1, 0, 0);
        var amountNeeded = 100000m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.TraditionalIra.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            1, amountNeeded * 1.5m, McInvestmentPositionType.LONG_TERM, true, 1m,
            currentDate));
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.BasicBucketsIncomeThreshold);
        var expectedAmountSold = amountNeeded;
        // Act
        var results = model.WithdrawalStrategy.SellInvestmentsToRmdAmount(
            amountNeeded, accounts, ledger, currentDate, model);
        var actualAmountSold = results.amountSold;
        // Assert
        Assert.Equal(expectedAmountSold, actualAmountSold);
    }
    
    [Fact]
    public void SellInvestmentsToRmdAmount_WithNoTaxDeferredPositions_ThrowsException()
    {
        // Arrange
        var currentDate = new LocalDateTime(2050, 1, 1, 0, 0);
        var amountNeeded = 100000m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.Brokerage.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            1, amountNeeded * 1.5m, McInvestmentPositionType.LONG_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.BasicBucketsIncomeThreshold);
        
        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => 
            model.WithdrawalStrategy.SellInvestmentsToRmdAmount(
                amountNeeded, accounts, ledger, currentDate, model));
        Assert.Equal("RMD: Nothing left to try. Not sure how we got here", exception.Message);
    }
}