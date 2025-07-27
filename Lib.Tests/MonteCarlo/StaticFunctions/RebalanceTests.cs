using Lib.DataTypes;
using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.Tests;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class RebalanceTests
{
    private readonly LocalDateTime _baseDate = new(2025, 1, 1, 0, 0);
    private readonly LocalDateTime _retirementDate = new(2030, 1, 1, 0, 0);

    private McModel CreateTestModel(RebalanceFrequency frequency = RebalanceFrequency.MONTHLY)
    {
        return new McModel
        {
            RetirementDate = _retirementDate,
            NumMonthsCashOnHand = 12,
            NumMonthsMidBucketOnHand = 24,
            NumMonthsPriorToRetirementToBeginRebalance = 60,
            RebalanceFrequency = frequency,
            Id = Guid.Empty,
            PersonId = Guid.Empty,
            ParentAId = Guid.Empty,
            ParentBId = Guid.Empty, AusterityRatio = 0m,
            DesiredMonthlySpendPostRetirement = 0,
            DesiredMonthlySpendPreRetirement = 0,
            ExtremeAusterityNetWorthTrigger = 0,
            ExtremeAusterityRatio = 0,
            ModelCreatedDate = new LocalDateTime(2025, 1, 1, 0, 0),
            Percent401KTraditional = 0,
            RecessionCheckLookBackMonths = 0,
            RecessionRecoveryPointModifier = 0,
            SimEndDate = new LocalDateTime(2025, 1, 1, 0, 0),
            SimStartDate = new LocalDateTime(2025, 1, 1, 0, 0),
            SocialSecurityStart = new LocalDateTime(2025, 1, 1, 0, 0),
        };
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
        var simParams = CreateTestModel(frequency);
        var currentDate = new LocalDateTime(2029, month, 1, 0, 0); // Within rebalance window

        // Act
        var result = Rebalance.CalculateWhetherItsBucketRebalanceTime(currentDate, simParams);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void MoveFromInvestmentToCash_SellsCorrectAmount()
    {
        // Arrange
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        var position = TestDataManager.CreateTestInvestmentPosition(100m, 1m, McInvestmentPositionType.LONG_TERM);
        accounts.InvestmentAccounts.Add(TestDataManager.CreateTestInvestmentAccount(
            new List<McInvestmentPosition> { position },
            McInvestmentAccountType.TAXABLE_BROKERAGE));
        var taxLedger = new TaxLedger();

        var expectedSale = 100m; // we sell whole positions

        // Act
        var result = Rebalance.MoveFromInvestmentToCash(
            accounts, 50m, McInvestmentPositionType.LONG_TERM, _baseDate, taxLedger);
        var newCashBalance = AccountCalculation.CalculateCashBalance(result.newBookOfAccounts);

        // Assert
        Assert.Equal(expectedSale, result.amountMoved);
        Assert.Equal(expectedSale, newCashBalance);
    }

    [Fact]
    public void InvestExcessCash_WithExcessCash_InvestsCorrectly()
    {
        // Arrange
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        var currentPrices = new CurrentPrices
        {
            CurrentLongTermInvestmentPrice = 100m
        };
        
        // start out with $10k in cash
        var initialCashBalance = 10000m;
        accounts = AccountCashManagement.DepositCash(accounts, initialCashBalance, _baseDate);
        
        // pretend we need to hold back $4500 and can invest the rest
        var reserveAmount = 4500m;
        
        var expectedInvestment = initialCashBalance - reserveAmount;
        

        // Act
        var result = Rebalance.InvestExcessCash(_baseDate, accounts, currentPrices, reserveAmount);
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
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        var currentPrices = new CurrentPrices
        {
            CurrentLongTermInvestmentPrice = 100m
        };
        
        // start out with $10k in cash
        var initialCashBalance = 10000m;
        accounts = AccountCashManagement.DepositCash(accounts, initialCashBalance, _baseDate);
        
        // pretend we need to hold back $4500 and can invest the rest
        var reserveAmount = 4500m;
        
        var expectedInvestment = initialCashBalance - reserveAmount;
        

        // Act
        var result = Rebalance.InvestExcessCash(_baseDate, accounts, currentPrices, reserveAmount);

        // Assert
        var newCashBalance = AccountCalculation.CalculateCashBalance(result);
        Assert.Equal(reserveAmount, newCashBalance); // Should keep reserve amount
    }

    [Fact]
    public void RebalanceLongToMid_DuringRecession_DoesNotRebalance()
    {
        // Arrange
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        var simParams = CreateTestModel();
        var recessionStats = new RecessionStats { AreWeInARecession = true };
        var ledger = new TaxLedger();

        // Act
        var result = Rebalance.RebalanceLongToMid(
            _baseDate, accounts, recessionStats, new CurrentPrices(), simParams, ledger, CreateTestPerson());

        // Assert
        Assert.Equal(accounts, result.newBookOfAccounts); // Should not modify accounts during recession
        Assert.Equal(ledger, result.newLedger); // Should not modify ledger during recession
    }

    [Fact]
    public void RebalancePortfolio_AtRebalanceTime_ExecutesRebalancingCorrectly()
    {
        /*
         * right, this is a bit of a mess, as rebalance is so very complicated. basically, as you age, your expected
         * spend will change, and, therefore, your amount of cash needed isn't linear. So you need to use the same
         * method to calculate that here as you do in the reballance. Don't worry, we'll UT that on its own separately.
         *
         * Once you know your cash and mid amounts that you want on-hand, you have to sell enough whole positions until
         * you at or over that amount. Each sale will log a capital gain and each sale will update your cash balance.
         * When reballancing to top up your mid bucket, you'll need to sell, then withdraw cash, then buy.
         *
         * Finally, when you're done reballancing, it'll check if you have excess cash on hand and re-invest that back
         * into long-term. 
         * 
         * This checks whether all that works.
         */
        // Arrange
        var simParams = CreateTestModel(RebalanceFrequency.MONTHLY);
        var person = CreateTestPerson();
        simParams.NumMonthsCashOnHand = 18;
        simParams.NumMonthsMidBucketOnHand = 24;
        simParams.DesiredMonthlySpendPostRetirement = 1000;
        person.RequiredMonthlySpend = 1000;
        var accounts = TestDataManager.CreateTestBookOfAccounts();
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
        
        
        var cashNeededTotal = Spend.CalculateCashNeedForNMonths(
            simParams, person, currentDate, simParams.NumMonthsCashOnHand);
        var midNeededTotal = Spend.CalculateCashNeedForNMonths(
            simParams, person, currentDate, simParams.NumMonthsMidBucketOnHand);
        
        // we'll be selling in blocks of priceEach * quantityEach
        var valuePerPosition = priceEach * quantityEach;
        var numPositionsSoldForCash = Math.Ceiling(cashNeededTotal / valuePerPosition);
        var expectedCashBalance = numPositionsSoldForCash * valuePerPosition;
        
        var numPositionsSoldForMid = Math.Ceiling(midNeededTotal / valuePerPosition);
        var expectedMidBalance = numPositionsSoldForMid * valuePerPosition;
        
        var numPositionsSoldFromLong = numPositionsSoldForCash + numPositionsSoldForMid;
        var expectedLongBalance = (numPositionsWanted - numPositionsSoldFromLong) * valuePerPosition;
        var expectedCapitalGains = numPositionsSoldFromLong * valuePerPosition * .5m;
        
        // but now we'll re-invest any extra cash at the end of reballancing
        var excessCash = expectedCashBalance - cashNeededTotal;
        expectedCashBalance -= excessCash;
        expectedLongBalance += excessCash;

        // Act
        var result = Rebalance.RebalancePortfolio(
            currentDate, accounts, new RecessionStats(), new CurrentPrices(), simParams, new TaxLedger(), person);
        
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
    public void SellInOrder_SellsFromProvidedOrder()
    {
        // Arrange
        var accounts = TestDataManager.CreateTestBookOfAccounts();
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