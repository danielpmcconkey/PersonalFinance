using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using System.Collections.Generic;
using Lib.MonteCarlo.StaticFunctions;
using Microsoft.EntityFrameworkCore;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class InvestmentTests
{
    private readonly LocalDateTime _testDate = new LocalDateTime(2025, 1, 1, 12, 0);
    private readonly CurrentPrices _testPrices = new()
    {
        CurrentLongTermInvestmentPrice = 100m,
        CurrentMidTermInvestmentPrice = 50m,
        CurrentShortTermInvestmentPrice = 25m
    };

    private McInvestmentAccount CreateTestAccount(
        McInvestmentAccountType accountType = McInvestmentAccountType.TAXABLE_BROKERAGE)
    {
        return new McInvestmentAccount
        {
            Id = Guid.NewGuid(),
            Name = $"Test {accountType} Account",
            AccountType = accountType,
            Positions = []
        };
    }

    private McInvestmentPosition CreateTestPosition(
        bool isOpen = true,
        LocalDateTime? entry = null,
        McInvestmentPositionType positionType = McInvestmentPositionType.LONG_TERM,
        decimal price = 100m,
        decimal quantity = 10m)
    {
        return new McInvestmentPosition
        {
            Id = Guid.NewGuid(),
            Name = "Test Position",
            IsOpen = isOpen,
            Entry = entry ?? _testDate,
            InvestmentPositionType = positionType,
            Price = price,
            Quantity = quantity,
            InitialCost = price * quantity
        };
    }

    private BookOfAccounts CreateTestBookOfAccounts()
    {
        return Account.CreateBookOfAccounts([], []);
    }

    [Theory]
    [InlineData("SCHD", McInvestmentPositionType.MID_TERM)]
    [InlineData("FXAIX", McInvestmentPositionType.LONG_TERM)]
    public void GetInvestmentPositionType_ReturnsCorrectType(string symbol, McInvestmentPositionType expectedType)
    {
        // Arrange
        using var context = new PgContext();
        var position = context.PgPositions
            .Where(x => x.Symbol == symbol)
            .Include(x => x.Fund)
            .ThenInclude(f => f.Objective)
            .FirstOrDefault();
        Assert.NotNull(position);
        
        // Act
        var result = Investment.GetInvestmentPositionType(position.Fund.Objective);

        // Assert
        Assert.Equal(expectedType, result);
    }

    [Fact]
    public void GetInvestmentPositionsToSellByAccountTypeAndPositionType_ReturnsMatchingPositions()
    {
        // Arrange
        var accounts = new List<McInvestmentAccount>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Test Brokerage Account",
                AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
                Positions = new List<McInvestmentPosition>
                {
                    CreateTestPosition(
                        isOpen: true,
                        entry: _testDate.PlusYears(-2),
                        positionType: McInvestmentPositionType.LONG_TERM),
                    CreateTestPosition(
                        isOpen: true,
                        entry: _testDate.PlusMonths(-1),
                        positionType: McInvestmentPositionType.LONG_TERM),
                    CreateTestPosition(
                        isOpen: false,
                        entry: _testDate.PlusYears(-2),
                        positionType: McInvestmentPositionType.LONG_TERM)
                }
            }
        };

        // Act
        var result = Investment.GetInvestmentPositionsToSellByAccountTypeAndPositionType(
            accounts,
            McInvestmentAccountType.TAXABLE_BROKERAGE,
            McInvestmentPositionType.LONG_TERM,
            _testDate);

        // Assert
        Assert.Single(result);
        Assert.True(result[0].IsOpen);
        Assert.Equal(_testDate.PlusYears(-2), result[0].Entry);
    }

    [Fact]
    public void InvestFunds_CreatesCorrectPosition()
    {
        // Arrange
        var accounts = CreateTestBookOfAccounts();
        var investmentAmount = 1000m;

        // Act
        var result = Investment.InvestFunds(
            accounts,
            _testDate,
            investmentAmount,
            McInvestmentPositionType.LONG_TERM,
            McInvestmentAccountType.TAXABLE_BROKERAGE,
            _testPrices).accounts;

        // Assert
        Assert.Single(result.Brokerage.Positions);
        var position = result.Brokerage.Positions[0];
        Assert.Equal(investmentAmount, position.InitialCost);
        Assert.Equal(_testPrices.CurrentLongTermInvestmentPrice, position.Price);
        Assert.Equal(
            Math.Round(investmentAmount / _testPrices.CurrentLongTermInvestmentPrice, 4), 
            position.Quantity);
        Assert.True(position.IsOpen);
        Assert.Equal(_testDate, position.Entry);
        Assert.NotEqual(Guid.Empty, position.Id);
        Assert.Equal("automated investment", position.Name);
    }

    [Fact]
    public void InvestFunds_WithZeroAmount_ReturnsUnchangedAccounts()
    {
        // Arrange
        var accounts = CreateTestBookOfAccounts();

        // Act
        var result = Investment.InvestFunds(
            accounts,
            _testDate,
            0m,
            McInvestmentPositionType.LONG_TERM,
            McInvestmentAccountType.TAXABLE_BROKERAGE,
            _testPrices).accounts;

        // Assert
        Assert.Empty(result.Brokerage.Positions);
    }

    [Fact]
    public void NormalizeInvestmentPositions_NormalizesLongTermPositionsCorrectly()
    {
        // Arrange
        var account = CreateTestAccount();
        account.Positions.Add(CreateTestPosition(
            price: 75m,
            quantity: 10m,
            positionType: McInvestmentPositionType.LONG_TERM));

        var accounts = new BookOfAccounts
        {
            InvestmentAccounts = new List<McInvestmentAccount> { account },
            DebtAccounts = new List<McDebtAccount>()
        };

        // Act
        var result = Investment.NormalizeInvestmentPositions(accounts, _testPrices);

        // Assert
        var normalizedPosition = result.InvestmentAccounts[0].Positions[0];
        Assert.Equal(_testPrices.CurrentLongTermInvestmentPrice, normalizedPosition.Price);
        
        // Check that the total value remains the same
        var originalValue = 75m * 10m;
        var newValue = normalizedPosition.Price * normalizedPosition.Quantity;
        Assert.Equal(originalValue, newValue);
    }
    [Fact]
    public void NormalizeInvestmentPositions_NormalizesMidTermPositionsCorrectly()
    {
        // Arrange
        var account = CreateTestAccount();
        account.Positions.Add(CreateTestPosition(
            price: 315m,
            quantity: 10m,
            positionType: McInvestmentPositionType.MID_TERM));

        var accounts = new BookOfAccounts
        {
            InvestmentAccounts = new List<McInvestmentAccount> { account },
            DebtAccounts = new List<McDebtAccount>()
        };

        // Act
        var result = Investment.NormalizeInvestmentPositions(accounts, _testPrices);

        // Assert
        var normalizedPosition = result.InvestmentAccounts[0].Positions[0];
        Assert.Equal(_testPrices.CurrentMidTermInvestmentPrice, normalizedPosition.Price);
        
        // Check that the total value remains the same
        var originalValue = 315m * 10m;
        var newValue = normalizedPosition.Price * normalizedPosition.Quantity;
        Assert.Equal(originalValue, newValue);
    }

    [Theory]
    [InlineData(McInvestmentAccountType.PRIMARY_RESIDENCE)]
    [InlineData(McInvestmentAccountType.CASH)]
    public void NormalizeInvestmentPositions_SkipsSpecialAccounts(McInvestmentAccountType accountType)
    {
        // Arrange
        var account = CreateTestAccount(accountType);
        account.Positions.Add(CreateTestPosition(
            price: 75m,
            quantity: 10m,
            positionType: McInvestmentPositionType.LONG_TERM));

        var accounts = new BookOfAccounts
        {
            InvestmentAccounts = new List<McInvestmentAccount> { account },
            DebtAccounts = new List<McDebtAccount>()
        };

        // Act
        var result = Investment.NormalizeInvestmentPositions(accounts, _testPrices);

        // Assert
        var position = result.InvestmentAccounts[0].Positions[0];
        Assert.Equal(75m, position.Price);
        Assert.Equal(10m, position.Quantity);
    }
}