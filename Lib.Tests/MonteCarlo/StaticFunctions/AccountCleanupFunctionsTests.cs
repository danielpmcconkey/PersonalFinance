using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using NodaTime;
using Xunit;

namespace Lib.Tests.MonteCarlo.StaticFunctions;


public class AccountCleanupFunctionsTests
{
    private readonly LocalDateTime _testDate = new LocalDateTime(2025, 1, 1, 0, 0);
    private readonly CurrentPrices _testPrices = TestDataManager.CreateTestCurrentPrices(
        0.02M, 2.0M, 1.5M, 1.25M);

    [Fact]
    public void RemoveClosedPositions_RemovesClosedInvestmentPositions()
    {
        // Arrange
        var bookOfAccounts = TestDataManager.CreateEmptyBookOfAccounts();
        
        var positions = new List<McInvestmentPosition>
        {
            TestDataManager.CreateTestInvestmentPosition(1.0m, 1000.0m, McInvestmentPositionType.SHORT_TERM, true),
            TestDataManager.CreateTestInvestmentPosition(1.0m, 500.0m, McInvestmentPositionType.SHORT_TERM, false),
        };

        var account = TestDataManager.CreateTestInvestmentAccount(positions, McInvestmentAccountType.TAXABLE_BROKERAGE);
        bookOfAccounts.InvestmentAccounts = [account];

        // Act
        var cleanedBook = AccountCleanup.RemoveClosedPositions(bookOfAccounts);

        // Assert
        Assert.NotNull(cleanedBook.InvestmentAccounts);
        Assert.Single(cleanedBook.InvestmentAccounts[0].Positions);
        Assert.True(cleanedBook.InvestmentAccounts[0].Positions[0].IsOpen);
    }

    [Fact]
    public void RemoveClosedPositions_RemovesClosedDebtPositions()
    {
        // Arrange
        var bookOfAccounts = TestDataManager.CreateEmptyBookOfAccounts();
        
        var positions = new List<McDebtPosition>
        {
            TestDataManager.CreateTestDebtPosition(true, 0.05m, 100.0m, 1000.0m),
            TestDataManager.CreateTestDebtPosition(false, 0.05m, 100.0m, 1000.0m),
        };

        var account = TestDataManager.CreateTestDebtAccount(positions);
        bookOfAccounts.DebtAccounts = [account];

        // Act
        var cleanedBook = AccountCleanup.RemoveClosedPositions(bookOfAccounts);


        // Assert
        Assert.NotNull(cleanedBook.DebtAccounts);
        Assert.Single(cleanedBook.DebtAccounts[0].Positions);
        Assert.True(cleanedBook.DebtAccounts[0].Positions[0].IsOpen);
    }

    [Fact]
    public void SplitPositionInHalf_SplitsPositionCorrectly()
    {
        // Arrange
        var position =
            TestDataManager.CreateTestInvestmentPosition(15m, 100m, McInvestmentPositionType.LONG_TERM, true);
        position.InitialCost = 1000M;

        // Act
        var splitPositions = AccountCleanup.SplitPositionInHalf(position);

        // Assert
        Assert.Equal(2, splitPositions.Count);
        Assert.Equal(50M, splitPositions[0].Quantity);
        Assert.Equal(50M, splitPositions[1].Quantity);
        Assert.Equal(500M, splitPositions[0].InitialCost);
        Assert.Equal(500M, splitPositions[1].InitialCost);
        Assert.Equal(position.Price, splitPositions[0].Price);
        Assert.Equal(position.Price, splitPositions[1].Price);
        Assert.Equal(position.InvestmentPositionType, splitPositions[0].InvestmentPositionType);
        Assert.Equal(position.InvestmentPositionType, splitPositions[1].InvestmentPositionType);
    }

    [Fact]
    public void SplitLargePositions_SplitsPositionsAboveMaxValue()
    {
        // Arrange
        // this is not thread safe
        var initialMaxPositionValue = StaticConfig.InvestmentConfig.MonteCarloSimMaxPositionValue;
        StaticConfig.InvestmentConfig.MonteCarloSimMaxPositionValue = 1000M;

        var bookOfAccounts = TestDataManager.CreateEmptyBookOfAccounts();
        bookOfAccounts.InvestmentAccounts = [
            TestDataManager.CreateTestInvestmentAccount([
                    TestDataManager.CreateTestInvestmentPosition(20m, 100m, McInvestmentPositionType.LONG_TERM, true), // large position
                    TestDataManager.CreateTestInvestmentPosition(20m, 10m, McInvestmentPositionType.LONG_TERM, true), // small position
                ]
                , McInvestmentAccountType.TAXABLE_BROKERAGE)
        ];

        // Act
        bookOfAccounts = AccountCleanup.SplitLargePositions(bookOfAccounts, _testPrices);

        // Assert
        Assert.NotNull(bookOfAccounts.InvestmentAccounts);
        Assert.Equal(3, bookOfAccounts.InvestmentAccounts[0].Positions.Count);
        var newPositions = bookOfAccounts.InvestmentAccounts[0].Positions
            .OrderBy(p => p.Quantity)
            .ToList();

        // Original small position should remain unchanged
        Assert.Equal(10M, newPositions[0].Quantity);
        
        // Large position should be split into two roughly equal parts
        Assert.Equal(50M, newPositions[1].Quantity);
        Assert.Equal(50M, newPositions[2].Quantity);

        StaticConfig.InvestmentConfig.MonteCarloSimMaxPositionValue = initialMaxPositionValue;
    }

    [Fact]
    public void CleanUpAccounts_PerformsAllCleanupOperations()
    {
        // Arrange
        var initialMaxPositionValue = StaticConfig.InvestmentConfig.MonteCarloSimMaxPositionValue;
        StaticConfig.InvestmentConfig.MonteCarloSimMaxPositionValue = 1000M;

        var bookOfAccounts = TestDataManager.CreateEmptyBookOfAccounts();
        bookOfAccounts.InvestmentAccounts = [
            TestDataManager.CreateTestInvestmentAccount([
                    TestDataManager.CreateTestInvestmentPosition(20m, 100m, McInvestmentPositionType.LONG_TERM, true), // large, open position
                    TestDataManager.CreateTestInvestmentPosition(20m, 100m, McInvestmentPositionType.LONG_TERM, false), // large, closed position
                    TestDataManager.CreateTestInvestmentPosition(20m, 10m, McInvestmentPositionType.LONG_TERM, true), // small position
                ]
                , McInvestmentAccountType.TAXABLE_BROKERAGE)
        ];
        bookOfAccounts.DebtAccounts =
        [
            TestDataManager.CreateTestDebtAccount([
                TestDataManager.CreateTestDebtPosition(true, 0.05m, 100m, 1000m), // open position
                TestDataManager.CreateTestDebtPosition(false, 0.05m, 100m, 4000m), // closed position
            ])
        ];
        

        

        // Act
        bookOfAccounts = AccountCleanup.CleanUpAccounts(_testDate, bookOfAccounts, _testPrices);

        // Assert
        Assert.NotNull(bookOfAccounts.InvestmentAccounts);
        Assert.NotNull(bookOfAccounts.DebtAccounts);
        
        // investment
        // Should have removed closed position, split large investment position, and kept the small position as-is
        var resultingInvestmentPositions = bookOfAccounts.InvestmentAccounts[0].Positions;
        Assert.Equal(3, resultingInvestmentPositions.Count); // large and open split into 2; large and closed removed; small kept
        Assert.All(resultingInvestmentPositions, p => Assert.True(p.IsOpen));
        var splitPositions = resultingInvestmentPositions
            .Cast<McInvestmentPosition>()
            .OrderBy(p => p.Quantity)
            .ToList();
        // Verify split was performed correctly
        Assert.Equal(50M, splitPositions[1].Quantity);
        Assert.Equal(50M, splitPositions[2].Quantity);
        
        // debt
        // should have removed the closed position
        var resultingDebtPositions = bookOfAccounts.DebtAccounts[0].Positions;
        Assert.Single(resultingDebtPositions);
        Assert.True(resultingDebtPositions[0].IsOpen);
        Assert.Equal(1000M, resultingDebtPositions[0].CurrentBalance);
        
        
        StaticConfig.InvestmentConfig.MonteCarloSimMaxPositionValue = initialMaxPositionValue;
    }

    [Fact]
    public void SplitLargePositions_SkipsCashAndPrimaryResidence()
    {
        // Arrange

        var bookOfAccounts = TestDataManager.CreateEmptyBookOfAccounts();
        bookOfAccounts.InvestmentAccounts = [
            TestDataManager.CreateTestInvestmentAccount([
                    TestDataManager.CreateTestInvestmentPosition(200000m, 1m, McInvestmentPositionType.LONG_TERM, true),
                ]
                , McInvestmentAccountType.PRIMARY_RESIDENCE),
            TestDataManager.CreateTestInvestmentAccount([
                    TestDataManager.CreateTestInvestmentPosition(1m, 20000m, McInvestmentPositionType.SHORT_TERM, true),
                ]
                , McInvestmentAccountType.CASH)
        ];

        

        // Act
        bookOfAccounts = AccountCleanup.SplitLargePositions(bookOfAccounts, _testPrices);

        // Assert
        Assert.NotNull(bookOfAccounts.InvestmentAccounts);
        var cashAccount = bookOfAccounts.InvestmentAccounts
            .First(a => a.AccountType == McInvestmentAccountType.CASH);
        var homeAccount = bookOfAccounts.InvestmentAccounts
            .First(a => a.AccountType == McInvestmentAccountType.PRIMARY_RESIDENCE);

        Assert.Single(cashAccount.Positions);
        Assert.Equal(20000M, (cashAccount.Positions[0]).Quantity);

        Assert.Single(homeAccount.Positions);
        Assert.Equal(1M, (homeAccount.Positions[0]).Quantity);
    }
}