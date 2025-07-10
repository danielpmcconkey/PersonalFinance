using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using System.Collections.Generic;
using Lib.Tests;

namespace Lib.MonteCarlo.StaticFunctions.Tests;

public class AccountCleanupTests
{
    private readonly LocalDateTime _testDate = new LocalDateTime(2025, 1, 1, 12, 0);
    private readonly CurrentPrices _testPrices = new();

    private BookOfAccounts CreateTestAccounts()
    {
        return new BookOfAccounts
        {
            InvestmentAccounts = new List<McInvestmentAccount>(),
            DebtAccounts = new List<McDebtAccount>()
        };
    }

    [Fact]
    public void RemoveClosedPositions_WithMixedPositions_RemovesClosedOnes()
    {
        // Arrange
        var accounts = CreateTestAccounts();
        var investmentAccount = new McInvestmentAccount
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
            Positions = new List<McInvestmentPosition>
            {
                TestDataManager.CreateTestInvestmentPosition(
                    10, 100, McInvestmentPositionType.LONG_TERM, true),
                TestDataManager.CreateTestInvestmentPosition(
                    10, 200, McInvestmentPositionType.LONG_TERM, false),
                TestDataManager.CreateTestInvestmentPosition(
                    10, 300, McInvestmentPositionType.LONG_TERM, true),
            }
        };
        accounts.InvestmentAccounts.Add(investmentAccount);

        // Act
        var result = AccountCleanup.RemoveClosedPositions(accounts);

        // Assert
        Assert.Single(result.InvestmentAccounts);
        Assert.Equal(2, result.InvestmentAccounts[0].Positions.Count);
        Assert.All(result.InvestmentAccounts[0].Positions, position => Assert.True(position.IsOpen));
    }

    [Fact]
    public void RemoveClosedPositions_WithNullInvestmentAccounts_ThrowsInvalidDataException()
    {
        // Arrange
        var accounts = new BookOfAccounts { InvestmentAccounts = null };

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => AccountCleanup.RemoveClosedPositions(accounts));
    }

    [Fact]
    public void RemoveClosedPositions_WithNullDebtAccounts_ThrowsInvalidDataException()
    {
        // Arrange
        var accounts = new BookOfAccounts 
        { 
            InvestmentAccounts = new List<McInvestmentAccount>(),
            DebtAccounts = null
        };

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => AccountCleanup.RemoveClosedPositions(accounts));
    }

    [Fact]
    public void SplitLargePositions_WithOversizedPosition_SplitsIntoSmallerOnes()
    {
        // Arrange
        var accounts = CreateTestAccounts();
        var maxValue = StaticConfig.InvestmentConfig.MonteCarloSimMaxPositionValue;
        var largePosition = TestDataManager.CreateTestInvestmentPosition(
            1m,
            maxValue * 2, // Ensure position is larger than max,
            McInvestmentPositionType.LONG_TERM,
            true);
        
        var investmentAccount = new McInvestmentAccount
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
            Positions = new List<McInvestmentPosition> { largePosition }
        };
        accounts.InvestmentAccounts.Add(investmentAccount);

        // Act
        var result = AccountCleanup.SplitLargePositions(accounts, _testPrices);

        // Assert
        Assert.Single(result.InvestmentAccounts);
        Assert.Equal(2, result.InvestmentAccounts[0].Positions.Count);
        Assert.All(result.InvestmentAccounts[0].Positions, 
            position => Assert.True(position.CurrentValue <= maxValue));
    }

    [Fact]
    public void SplitLargePositions_WithPrimaryResidence_DoesNotSplit()
    {
        // Arrange
        var accounts = CreateTestAccounts();
        var maxValue = StaticConfig.InvestmentConfig.MonteCarloSimMaxPositionValue;
        var largePosition = TestDataManager.CreateTestInvestmentPosition(
            1m,
            maxValue * 2, // Ensure position is larger than max,
            McInvestmentPositionType.LONG_TERM,
            true);
        
        var investmentAccount = new McInvestmentAccount
        {
            Id = Guid.NewGuid(),
            Name = "Home",
            AccountType = McInvestmentAccountType.PRIMARY_RESIDENCE,
            Positions = new List<McInvestmentPosition> { largePosition }
        };
        accounts.InvestmentAccounts.Add(investmentAccount);

        // Act
        var result = AccountCleanup.SplitLargePositions(accounts, _testPrices);

        // Assert
        Assert.Single(result.InvestmentAccounts);
        Assert.Single(result.InvestmentAccounts[0].Positions);
    }

    [Fact]
    public void SplitPositionInHalf_SplitsPositionEvenly()
    {
        // Arrange
        var originalPosition = new McInvestmentPosition
        {
            Id = Guid.NewGuid(),
            Entry = _testDate,
            InitialCost = 1000m,
            InvestmentPositionType = McInvestmentPositionType.LONG_TERM,
            IsOpen = true,
            Name = "Test Position",
            Price = 10m,
            Quantity = 100m
        };

        // Act
        var result = AccountCleanup.SplitPositionInHalf(originalPosition);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(50m, result[0].Quantity);
        Assert.Equal(50m, result[1].Quantity);
        Assert.Equal(500m, result[0].InitialCost);
        Assert.Equal(500m, result[1].InitialCost);
        Assert.NotEqual(result[0].Id, result[1].Id);
        Assert.All(result, position =>
        {
            Assert.Equal(originalPosition.Entry, position.Entry);
            Assert.Equal(originalPosition.Price, position.Price);
            Assert.Equal(originalPosition.InvestmentPositionType, position.InvestmentPositionType);
            Assert.Equal(originalPosition.IsOpen, position.IsOpen);
        });
    }

    [Fact]
    public void CleanUpAccounts_PerformsCompleteCleanup()
    {
        // Arrange
        var accounts = CreateTestAccounts();
        var maxValue = StaticConfig.InvestmentConfig.MonteCarloSimMaxPositionValue;
        var positions = new List<McInvestmentPosition>
        {
            TestDataManager.CreateTestInvestmentPosition(
                1m, maxValue * 2, McInvestmentPositionType.LONG_TERM, true),
            TestDataManager.CreateTestInvestmentPosition(
                1m, 100m, McInvestmentPositionType.LONG_TERM, false),
        };
        
        var investmentAccount = new McInvestmentAccount
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
            Positions = positions
        };
        accounts.InvestmentAccounts.Add(investmentAccount);

        // Act
        var result = AccountCleanup.CleanUpAccounts(_testDate, accounts, _testPrices);

        // Assert
        Assert.Single(result.InvestmentAccounts);
        Assert.Equal(2, result.InvestmentAccounts[0].Positions.Count);
        Assert.All(result.InvestmentAccounts[0].Positions, position => 
        {
            Assert.True(position.IsOpen);
            Assert.True(position.CurrentValue <= maxValue);
        });
    }
}