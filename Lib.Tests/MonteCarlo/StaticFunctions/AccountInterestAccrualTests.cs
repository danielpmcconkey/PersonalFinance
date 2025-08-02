using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using Lib.Tests;
using NodaTime;
using Xunit;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class AccountInterestAccrualTests
{
    private readonly LocalDateTime _testDate = new LocalDateTime(2025, 1, 1, 0, 0);
    private readonly CurrentPrices _testPrices = TestDataManager.CreateTestCurrentPrices(
        0.02M, 2.0M, 1.5M, 1.25M);

    

    [Fact]
    public void AccrueInterest_UpdatesInvestmentAndDebtPositions()
    {
        // Arrange
        var newInvestmentPosition = TestDataManager.CreateTestInvestmentPosition(
            1.0m, 100.0m, McInvestmentPositionType.LONG_TERM, true);
        var investmentPositions = new List<McInvestmentPosition>
        {
            newInvestmentPosition
        };
        var newInvestmentAccount =
            TestDataManager.CreateTestInvestmentAccount(investmentPositions, McInvestmentAccountType.TAXABLE_BROKERAGE);
        var newDebtPosition = TestDataManager.CreateTestDebtPosition(true, 0.12M, 100M, 1000M);
        var debtPositions = new List<McDebtPosition>
        {
            newDebtPosition
        };
        var newDebtAccount = TestDataManager.CreateTestDebtAccount(debtPositions);
        
        var bookOfAccounts = TestDataManager.CreateTestBookOfAccounts();

        bookOfAccounts.InvestmentAccounts = new List<McInvestmentAccount>()
        {
            newInvestmentAccount
        };
        bookOfAccounts.DebtAccounts = new List<McDebtAccount>()
        {
            newDebtAccount
        };
        var investmentAccountId = newInvestmentAccount.Id;
        var investmentPositionId = newInvestmentPosition.Id;
        var debtAccountId = newDebtAccount.Id;
        var debtPositionId = newDebtPosition.Id;

        var lifetimeSpend = new LifetimeSpend();

        // Act
        (BookOfAccounts newAccounts, LifetimeSpend newSpend, List<ReconciliationMessage> messages) result = 
            AccountInterestAccrual.AccrueInterest(_testDate, bookOfAccounts, _testPrices, lifetimeSpend);

        var resultInvestAccount = result.newAccounts.InvestmentAccounts
            .First(x => x.Id == investmentAccountId);
        var resultInvestPosition = resultInvestAccount.Positions
            .First(x => x.Id == investmentPositionId);
        var resultDebtAccount = result.newAccounts.DebtAccounts
            .First(x => x.Id == debtAccountId);
        var resultDebtPosition = resultDebtAccount.Positions
            .First(x => x.Id == debtPositionId);

        // Assert
        Assert.Equal(2.0M, resultInvestPosition.Price); // Long term price from test prices
        Assert.Equal(1010M, resultDebtPosition.CurrentBalance); // 1000 + (1000 * 0.12/12)
    }

    [Fact]
    public void AccrueInterest_SkipsCashAndPrimaryResidence()
    {
        // Arrange

        var startingCashPrice = 1.0m;
        var startingHomePrice = 300000.0m;
        
        var bookOfAccounts = TestDataManager.CreateTestBookOfAccounts();
        var investmentPositionsCash = new List<McInvestmentPosition>
        {
            TestDataManager.CreateTestInvestmentPosition(startingCashPrice, 10000.0m, McInvestmentPositionType.SHORT_TERM, true)
        };
        var investmentPositionsHome = new List<McInvestmentPosition>
        {
            TestDataManager.CreateTestInvestmentPosition(startingHomePrice, 1.0m, McInvestmentPositionType.LONG_TERM, true)
        };

        var debtPositions = new List<McDebtPosition>();

        bookOfAccounts.InvestmentAccounts = new List<McInvestmentAccount>()
        {
            TestDataManager.CreateTestInvestmentAccount(investmentPositionsCash, McInvestmentAccountType.CASH),
            TestDataManager.CreateTestInvestmentAccount(investmentPositionsHome, McInvestmentAccountType.PRIMARY_RESIDENCE)
        };
        bookOfAccounts.DebtAccounts = new List<McDebtAccount>()
        {
            TestDataManager.CreateTestDebtAccount(debtPositions)
        };
        
        var lifetimeSpend = new LifetimeSpend();

        // Act
        (BookOfAccounts newAccounts, LifetimeSpend newSpend, List<ReconciliationMessage>) result = 
            AccountInterestAccrual.AccrueInterest(
            _testDate, bookOfAccounts, _testPrices, lifetimeSpend); 

        // Assert
        Assert.NotNull(result.newAccounts.InvestmentAccounts);
        var cashPosition = result.newAccounts.InvestmentAccounts[0].Positions[0];
        var homePosition = result.newAccounts.InvestmentAccounts[1].Positions[0];
        
        Assert.Equal(startingCashPrice, cashPosition.Price);
        Assert.Equal(startingHomePrice, homePosition.Price);
    }

    [Fact]
    public void AccrueInterest_UpdatesLifetimeSpend()
    {
        // Arrange// this is not be thread safe and may cause interesting results when running many tests at once
        var initialDebugMode = MonteCarloConfig.DebugMode;
        var initialShouldReconcileInterestAccrual = MonteCarloConfig.ShouldReconcileInterestAccrual;
        
        MonteCarloConfig.DebugMode = true;
        MonteCarloConfig.ShouldReconcileInterestAccrual = true;
        
        var bookOfAccounts = TestDataManager.CreateTestBookOfAccounts();
        var investmentPositions = new List<McInvestmentPosition>
        {
            TestDataManager.CreateTestInvestmentPosition(1.0m, 100.0m, McInvestmentPositionType.LONG_TERM, true)
        };

        var debtPositions = new List<McDebtPosition>
        {
            TestDataManager.CreateTestDebtPosition(true, 0.12M, 100M, 1000M)
        };

        bookOfAccounts.InvestmentAccounts = new List<McInvestmentAccount>()
        {
            TestDataManager.CreateTestInvestmentAccount(investmentPositions, McInvestmentAccountType.TAXABLE_BROKERAGE)
        };
        bookOfAccounts.DebtAccounts = new List<McDebtAccount>()
        {
            TestDataManager.CreateTestDebtAccount(debtPositions)
        };

        var lifetimeSpend = new LifetimeSpend();

        

        // Act
        (BookOfAccounts newAccounts, LifetimeSpend newSpend, List<ReconciliationMessage>) result = 
            AccountInterestAccrual.AccrueInterest(_testDate, bookOfAccounts, _testPrices, lifetimeSpend);

        // Assert
        Assert.Equal(10M, result.newSpend.TotalDebtAccrualLifetime); // Debt interest
        Assert.Equal(100M, result.newSpend.TotalInvestmentAccrualLifetime); // Investment value change
        
        MonteCarloConfig.DebugMode = initialDebugMode;
        MonteCarloConfig.ShouldReconcileInterestAccrual = initialShouldReconcileInterestAccrual;
    }
    
    [Fact]
    public void AccrueInterestOnDebtPosition_CalculatesInterestCorrectly()
    {
        // Arrange
        var position = TestDataManager.CreateTestDebtPosition(true, 0.12M, 100M, 1000M);
        
        var lifetimeSpend = new LifetimeSpend();

        // Act
        (McDebtPosition newPosition, LifetimeSpend newSpend, List<ReconciliationMessage>) result = 
            AccountInterestAccrual.AccrueInterestOnDebtPosition(_testDate, position, lifetimeSpend);

        // Assert
        // Monthly interest = 1000 * (0.12 / 12) = 10
        Assert.Equal(1010m, result.newPosition.CurrentBalance);
        Assert.Equal(10m, result.newSpend.TotalDebtAccrualLifetime);
    }

    [Fact]
    public void AccrueInterestOnDebtPosition_DoesNothingForClosedPosition()
    {
        // Arrange
        var position = TestDataManager.CreateTestDebtPosition(false, 0.12M, 100M, 1000M);
        var lifetimeSpend = new LifetimeSpend();

        // Act
        (McDebtPosition newPosition, LifetimeSpend newSpend, List<ReconciliationMessage>) result = 
            AccountInterestAccrual.AccrueInterestOnDebtPosition(_testDate, position, lifetimeSpend);

        // Assert
        Assert.Equal(1000m, result.newPosition.CurrentBalance);
        Assert.Equal(0m, result.newSpend.TotalDebtAccrualLifetime);
    }

    [Fact]
    public void AccrueInterestOnDebtPosition_HandlesZeroBalance()
    {
        // Arrange
        var position = TestDataManager.CreateTestDebtPosition(true, 0.12M, 100M, 0M);
        var lifetimeSpend = new LifetimeSpend();

        // Act
        (McDebtPosition newPosition, LifetimeSpend newSpend, List<ReconciliationMessage>) result = 
            AccountInterestAccrual.AccrueInterestOnDebtPosition(
            _testDate, position, lifetimeSpend);

        // Assert
        Assert.Equal(0m, result.newPosition.CurrentBalance);
        Assert.Equal(0m, result.newSpend.TotalDebtAccrualLifetime);
    }

    [Fact]
    public void AccrueInterestOnDebtPosition_HandlesHighInterestRate()
    {
        // Arrange
        var apr = 1.5M; // 150% -- use a value that can be divided by 12 without repeating decimals so we don't have weird decimal comparison
        var balance = 900;
        
        var position = TestDataManager.CreateTestDebtPosition(true, apr, 100M, balance);
        var lifetimeSpend = new LifetimeSpend();
        
        var expectedAccrual = balance * (apr / 12);

        // Act
        (McDebtPosition newPosition, LifetimeSpend newSpend, List<ReconciliationMessage>) result = 
            AccountInterestAccrual.AccrueInterestOnDebtPosition(_testDate, position, lifetimeSpend);

        // Assert
        Assert.Equal(balance + expectedAccrual, result.newPosition.CurrentBalance);
        Assert.Equal(expectedAccrual, result.newSpend.TotalDebtAccrualLifetime);
    }

    [Fact]
    public void AccrueInterestOnDebtPosition_UpdatesLifetimeSpendCorrectly()
    {
        // Arrange
        // this is not be thread safe and may cause interesting results when running many tests at once
        var initialDebugMode = MonteCarloConfig.DebugMode;
        var initialShouldReconcileInterestAccrual = MonteCarloConfig.ShouldReconcileInterestAccrual;
        
        MonteCarloConfig.DebugMode = true;
        MonteCarloConfig.ShouldReconcileInterestAccrual = true;
        
        var position = TestDataManager.CreateTestDebtPosition(true, 0.12M, 100M, 1000M);

        var lifetimeSpend = new LifetimeSpend();
        var startingAccrual = 13m;
        lifetimeSpend.TotalDebtAccrualLifetime = startingAccrual;
        var expectedAccrual = startingAccrual + (1000m * (0.12m / 12)); // 1000 * (0.12 / 12) = 10

        // Act
        var result = AccountInterestAccrual.AccrueInterestOnDebtPosition(
            _testDate, position, lifetimeSpend);

        // Assert
        Assert.Equal(1010m, result.newPosition.CurrentBalance);
        Assert.Equal(expectedAccrual, result.newSpend.TotalDebtAccrualLifetime);
        
        
        MonteCarloConfig.DebugMode = initialDebugMode;
        MonteCarloConfig.ShouldReconcileInterestAccrual = initialShouldReconcileInterestAccrual;
    }
    
    
    [Fact]
    public void AccrueInterest_LongTermInvestment_UpdatesPriceCorrectly()
    {
        // Arrange

        var position =
            TestDataManager.CreateTestInvestmentPosition(1.0m, 1000.0m, McInvestmentPositionType.LONG_TERM, true);
        var lifetimeSpend = new LifetimeSpend();

        // Act
        var results = AccountInterestAccrual.AccrueInterestOnInvestmentPosition(
            _testDate, position, _testPrices, lifetimeSpend);

        // Assert
        Assert.Equal(2.0M, results.newPosition.Price);
    }

    [Fact]
    public void AccrueInterest_MidTermInvestment_UpdatesPriceCorrectly()
    {
        // Arrange
        var position =
            TestDataManager.CreateTestInvestmentPosition(1.0m, 1000.0m, McInvestmentPositionType.MID_TERM, true);
        var lifetimeSpend = new LifetimeSpend();

        // Act
        var results = AccountInterestAccrual.AccrueInterestOnInvestmentPosition(
            _testDate, position, _testPrices, lifetimeSpend);

        // Assert
        Assert.Equal(1.5M, results.newPosition.Price);
    }

    [Fact]
    public void AccrueInterest_ShortTermInvestment_UpdatesPriceCorrectly()
    {
        // Arrange
        var position =
            TestDataManager.CreateTestInvestmentPosition(1.0m, 1000.0m, McInvestmentPositionType.SHORT_TERM, true);
        var lifetimeSpend = new LifetimeSpend();

        // Act
        var results = AccountInterestAccrual.AccrueInterestOnInvestmentPosition(
            _testDate, position, _testPrices, lifetimeSpend);

        // Assert
        Assert.Equal(1.25M, results.newPosition.Price);
    }

    [Fact]
    public void AccrueInterest_InDebugMode_UpdatesLifetimeSpend()
    {
        // Arrange
        // this is not be thread safe and may cause interesting results when running many tests at once
        var initialDebugMode = MonteCarloConfig.DebugMode;
        var initialShouldReconcileInterestAccrual = MonteCarloConfig.ShouldReconcileInterestAccrual;
        
        MonteCarloConfig.DebugMode = true;
        MonteCarloConfig.ShouldReconcileInterestAccrual = true;

        var position =
            TestDataManager.CreateTestInvestmentPosition(1.0m, 1000.0m, McInvestmentPositionType.LONG_TERM, true);
        var lifetimeSpend = new LifetimeSpend();
        var startingAccrual = 13.0M;
        lifetimeSpend.TotalInvestmentAccrualLifetime = startingAccrual;
        var expectedAccrual =
            startingAccrual + ((2.0M - 1.0M) * 1000M); // startingAccrual + ((new price - old price) * quantity)

        // Act
        var results = AccountInterestAccrual.AccrueInterestOnInvestmentPosition(
            _testDate, position, _testPrices, lifetimeSpend);

        // Assert
        Assert.Equal(expectedAccrual, results.newSpend.TotalInvestmentAccrualLifetime);
        
        // return DebugMode to whatever it was
        MonteCarloConfig.DebugMode = initialDebugMode;
        MonteCarloConfig.ShouldReconcileInterestAccrual = initialShouldReconcileInterestAccrual;
    }
}