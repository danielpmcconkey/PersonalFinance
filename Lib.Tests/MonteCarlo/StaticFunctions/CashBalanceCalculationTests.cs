using System.Diagnostics;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using NodaTime;
using Xunit;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class CashBalanceCalculationTests
{
    

    [Fact]
    public void CalculateCashBalance_WithSinglePosition_ReturnsCorrectBalance()
    {
        // Arrange
        var positions = new List<McInvestmentPosition>
        {
            new()
            {
                Name = "Test Position",
                InitialCost = 1000.0m,
                Id = Guid.NewGuid(),
                IsOpen = true,
                Price = 1.0m,
                Quantity = 1000.0m,
                Entry = new LocalDateTime(2025, 1, 1, 0, 0),
                InvestmentPositionType = McInvestmentPositionType.SHORT_TERM
            }
        };
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        Debug.Assert(accounts.Cash != null, "accounts.Cash != null");
        accounts.Cash.Positions = positions;

        // Act
        var balance = Account.CalculateCashBalance(accounts);

        // Assert
        Assert.Equal(1000.0m, balance);
    }

    [Fact]
    public void CalculateCashBalance_WithMultiplePositions_ReturnsCorrectTotalBalance()
    {
        // Arrange
        var positions = new List<McInvestmentPosition>
        {
            new()
            {
                Name = "Test Position",
                InitialCost = 1000.0m,
                Id = Guid.NewGuid(),
                IsOpen = true,
                Price = 1.0m,
                Quantity = 1000.0m,
                Entry = new LocalDateTime(2025, 1, 1, 0, 0),
                InvestmentPositionType = McInvestmentPositionType.SHORT_TERM
            },
            new()
            {
                Name = "Test Position",
                InitialCost = 1000.0m,
                Id = Guid.NewGuid(),
                IsOpen = true,
                Price = 1.0m,
                Quantity = 500.0m,
                Entry = new LocalDateTime(2025, 1, 1, 0, 0),
                InvestmentPositionType = McInvestmentPositionType.SHORT_TERM
            }
        };
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        Debug.Assert(accounts.Cash != null, "accounts.Cash != null");
        accounts.Cash.Positions = positions;

        // Act
        var balance = Account.CalculateCashBalance(accounts);

        // Assert
        Assert.Equal(1500.0m, balance);
    }

    [Fact]
    public void CalculateCashBalance_WithClosedPositions_IgnoresClosedPositions()
    {
        // Arrange
        var positions = new List<McInvestmentPosition>
        {
            new()
            {
                Name = "Test Position",
                InitialCost = 1000.0m,
                Id = Guid.NewGuid(),
                IsOpen = true,
                Price = 1.0m,
                Quantity = 1000.0m,
                Entry = new LocalDateTime(2025, 1, 1, 0, 0),
                InvestmentPositionType = McInvestmentPositionType.SHORT_TERM
            },
            new()
            {
                Name = "Test Position",
                InitialCost = 1000.0m,
                Id = Guid.NewGuid(),
                IsOpen = false,
                Price = 1.0m,
                Quantity = 500.0m,
                Entry = new LocalDateTime(2025, 1, 1, 0, 0),
                InvestmentPositionType = McInvestmentPositionType.SHORT_TERM
            }
        };
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        Debug.Assert(accounts.Cash != null, "accounts.Cash != null");
        accounts.Cash.Positions = positions;

        // Act
        var balance = Account.CalculateCashBalance(accounts);

        // Assert
        Assert.Equal(1000.0m, balance);
    }

    [Fact]
    public void CalculateCashBalance_WithNoPositions_ReturnsZero()
    {
        // Arrange
        
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        Debug.Assert(accounts.Cash != null, "accounts.Cash != null");

        // Act
        var balance = Account.CalculateCashBalance(accounts);

        // Assert
        Assert.Equal(0m, balance);
    }

    [Fact]
    public void CalculateCashBalance_WithNullCash_ThrowsInvalidDataException()
    {
        // Arrange
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        Debug.Assert(accounts.Cash != null, "accounts.Cash != null");
        
        accounts.Cash = null!;

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => Account.CalculateCashBalance(accounts));
        Assert.Equal("Cash is null", exception.Message);
    }

    [Fact]
    public void CalculateCashBalance_WithNullPositions_ThrowsInvalidDataException()
    {
        // Arrange
        var cashAccount = new McInvestmentAccount
        {
            Id = Guid.NewGuid(),
            Name = "Cash Account",
            AccountType = McInvestmentAccountType.CASH,
            Positions = null!
        };
        // Arrange
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        Debug.Assert(accounts.Cash != null, "accounts.Cash != null");

        

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => Account.CalculateCashBalance(accounts));
        Assert.Equal("Cash.Positions is null", exception.Message);
    }
}