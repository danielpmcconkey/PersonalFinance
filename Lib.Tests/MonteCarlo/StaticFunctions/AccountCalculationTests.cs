using System.Diagnostics;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.Tests;
using NodaTime;
using Xunit;

namespace Lib.Tests.MonteCarlo.StaticFunctions;
public class AccountCalculationTests
{
    [Fact]
    public void CalculateDebtTotal_EmptyAccounts_ReturnsZero()
    {
        // Arrange
        var accounts = TestDataManager.CreateTestBookOfAccounts();

        // Act
        var result = AccountCalculation.CalculateDebtTotal(accounts);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateDebtTotal_NullDebtAccounts_ThrowsInvalidDataException()
    {
        // Arrange
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        accounts.DebtAccounts = null;

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => AccountCalculation.CalculateDebtTotal(accounts));
    }

    [Fact]
    public void CalculateDebtTotal_SingleAccountSinglePosition_ReturnsCorrectTotal()
    {
        // Arrange
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        Assert.NotNull(accounts.DebtAccounts);

        var positions = new List<McDebtPosition>() { 
            TestDataManager.CreateTestDebtPosition(true, 0.05M, 200M, 1000m),
        };
        accounts.DebtAccounts.Add(TestDataManager.CreateTestDebtAccount(positions));

        // Act
        var result = AccountCalculation.CalculateDebtTotal(accounts);

        // Assert
        Assert.Equal(1000m, result);
    }

    [Fact]
    public void CalculateDebtTotal_MultipleAccountsMultiplePositions_ReturnsCorrectTotal()
    {
        // Arrange
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        Assert.NotNull(accounts.DebtAccounts);

        var positions1 = new List<McDebtPosition>() { 
            TestDataManager.CreateTestDebtPosition(true, 0.05M, 200M, 1000m),
            TestDataManager.CreateTestDebtPosition(true, 0.05M, 200M, 2000m),
        };
        var positions2 = new List<McDebtPosition>() { 
            TestDataManager.CreateTestDebtPosition(true, 0.05M, 200M, 3000m),
        };
        accounts.DebtAccounts.Add(TestDataManager.CreateTestDebtAccount(positions1));
        accounts.DebtAccounts.Add(TestDataManager.CreateTestDebtAccount(positions2));

        

        // Act
        var result = AccountCalculation.CalculateDebtTotal(accounts);

        // Assert
        Assert.Equal(6000m, result);
    }

    [Fact]
    public void CalculateDebtTotal_IgnoresClosedPositions()
    {
        // Arrange
        // Arrange
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        Assert.NotNull(accounts.DebtAccounts);

        var positions = new List<McDebtPosition>() { 
            TestDataManager.CreateTestDebtPosition(true, 0.05M, 200M, 1000m),
            TestDataManager.CreateTestDebtPosition(false, 0.05M, 200M, 2000m),
        };
        accounts.DebtAccounts.Add(TestDataManager.CreateTestDebtAccount(positions));

        // Act
        var result = AccountCalculation.CalculateDebtTotal(accounts);

        // Assert
        Assert.Equal(1000m, result);
    }
    
    [Fact]
    public void CalculateCashBalance_WithSinglePosition_ReturnsCorrectBalance()
    {
        // Arrange
        var positions = new List<McInvestmentPosition>
        {
            TestDataManager.CreateTestInvestmentPosition(1.0m, 1000.0m, McInvestmentPositionType.SHORT_TERM, true),
        };
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        Debug.Assert(accounts.Cash != null, "accounts.Cash != null");
        accounts.Cash.Positions = positions;

        // Act
        var balance = AccountCalculation.CalculateCashBalance(accounts);

        // Assert
        Assert.Equal(1000.0m, balance);
    }

    [Fact]
    public void CalculateCashBalance_WithMultiplePositions_ReturnsCorrectTotalBalance()
    {
        // Arrange
        var positions = new List<McInvestmentPosition>
        {
            TestDataManager.CreateTestInvestmentPosition(1.0m, 1000.0m, McInvestmentPositionType.SHORT_TERM, true),
            TestDataManager.CreateTestInvestmentPosition(1.0m, 500.0m, McInvestmentPositionType.SHORT_TERM, true),
        };
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        Debug.Assert(accounts.Cash != null, "accounts.Cash != null");
        accounts.Cash.Positions = positions;

        // Act
        var balance = AccountCalculation.CalculateCashBalance(accounts);

        // Assert
        Assert.Equal(1500.0m, balance);
    }

    [Fact]
    public void CalculateCashBalance_WithClosedPositions_IgnoresClosedPositions()
    {
        // Arrange
        var positions = new List<McInvestmentPosition>
        {
            TestDataManager.CreateTestInvestmentPosition(1.0m, 1000.0m, McInvestmentPositionType.SHORT_TERM, true),
            TestDataManager.CreateTestInvestmentPosition(1.0m, 500.0m, McInvestmentPositionType.SHORT_TERM, false),
            
        };
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        Debug.Assert(accounts.Cash != null, "accounts.Cash != null");
        accounts.Cash.Positions = positions;

        // Act
        var balance = AccountCalculation.CalculateCashBalance(accounts);

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
        var balance = AccountCalculation.CalculateCashBalance(accounts);

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
        var exception = Assert.Throws<InvalidDataException>(() => AccountCalculation.CalculateCashBalance(accounts));
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
        accounts.Cash = cashAccount;
        Debug.Assert(accounts.Cash != null, "accounts.Cash != null");

        

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => AccountCalculation.CalculateCashBalance(accounts));
        Assert.Equal("Cash.Positions is null", exception.Message);
    }
    [Fact]
    public void CalculateInvestmentAccountTotalValue_WithDecimalPrecision_MaintainsAccuracy()
    {
        // Arrange
        var positions = new List<McInvestmentPosition>
        {
            new McInvestmentPosition
            {
                Id = Guid.NewGuid(),
                Name = "Test Position",
                InvestmentPositionType = McInvestmentPositionType.LONG_TERM,
                InitialCost = 500.0m,
                IsOpen = true,
                Price = 10.125m,
                Quantity = 3.75m,
                Entry = new LocalDateTime(2025, 1, 1, 0, 0)
            }
        };
        var account = TestDataManager.CreateTestInvestmentAccount(positions, McInvestmentAccountType.TAXABLE_BROKERAGE);

        // Act
        var totalValue = AccountCalculation.CalculateInvestmentAccountTotalValue(account);

        // Assert
        Assert.Equal(37.96875m, totalValue); // 10.125 * 3.75 = 37.96875
    }
    
    [Fact]
    public void CalculateInvestmentAccountTotalValue_WithSinglePosition_ReturnsCorrectValue()
    {
        // Arrange
        var positions = new List<McInvestmentPosition>
        {
            new McInvestmentPosition
            {
                Id = Guid.NewGuid(),
                Name = "Test Position",
                InvestmentPositionType = McInvestmentPositionType.LONG_TERM,
                InitialCost = 500.0m,
                IsOpen = true,
                Price = 100.0m,
                Quantity = 10.0m,
                Entry = new LocalDateTime(2025, 1, 1, 0, 0),
            }
        };
        var account = TestDataManager.CreateTestInvestmentAccount(positions, McInvestmentAccountType.TAXABLE_BROKERAGE);

        // Act
        var totalValue = AccountCalculation.CalculateInvestmentAccountTotalValue(account);

        // Assert
        Assert.Equal(1000.0m, totalValue); // 100 * 10 = 1000
    }

    [Fact]
    public void CalculateInvestmentAccountTotalValue_WithMultiplePositions_ReturnsCorrectTotal()
    {
        // Arrange
        var positions = new List<McInvestmentPosition>
        {
            new McInvestmentPosition
            {
                Id = Guid.NewGuid(),
                Name = "Test Position",
                InvestmentPositionType = McInvestmentPositionType.LONG_TERM,
                InitialCost = 500.0m,
                IsOpen = true,
                Price = 100.0m,
                Quantity = 10.0m,
                Entry = new LocalDateTime(2025, 1, 1, 0, 0)
            },
            new McInvestmentPosition
            {
                Id = Guid.NewGuid(),
                Name = "Test Position",
                InvestmentPositionType = McInvestmentPositionType.LONG_TERM,
                InitialCost = 500.0m,
                IsOpen = true,
                Price = 50.0m,
                Quantity = 20.0m,
                Entry = new LocalDateTime(2025, 1, 1, 0, 0)
            }
        };
        var account = TestDataManager.CreateTestInvestmentAccount(positions, McInvestmentAccountType.TAXABLE_BROKERAGE);

        // Act
        var totalValue = AccountCalculation.CalculateInvestmentAccountTotalValue(account);

        // Assert
        Assert.Equal(2000.0m, totalValue); // (100 * 10) + (50 * 20) = 1000 + 1000 = 2000
    }

    [Fact]
    public void CalculateInvestmentAccountTotalValue_WithClosedPositions_IgnoresClosedPositions()
    {
        // Arrange
        var positions = new List<McInvestmentPosition>
        {
            new McInvestmentPosition
            {
                Id = Guid.NewGuid(),
                Name = "Test Position",
                InvestmentPositionType = McInvestmentPositionType.LONG_TERM,
                InitialCost = 500.0m,
                IsOpen = true,
                Price = 100.0m,
                Quantity = 10.0m,
                Entry = new LocalDateTime(2025, 1, 1, 0, 0)
            },
            new McInvestmentPosition
            {
                Id = Guid.NewGuid(),
                Name = "Test Position",
                InvestmentPositionType = McInvestmentPositionType.LONG_TERM,
                InitialCost = 500.0m,
                IsOpen = false,
                Price = 50.0m,
                Quantity = 20.0m,
                Entry = new LocalDateTime(2025, 1, 1, 0, 0)
            }
        };
        var account = TestDataManager.CreateTestInvestmentAccount(positions, McInvestmentAccountType.TAXABLE_BROKERAGE);

        // Act
        var totalValue = AccountCalculation.CalculateInvestmentAccountTotalValue(account);

        // Assert
        Assert.Equal(1000.0m, totalValue); // Only counts the open position: 100 * 10 = 1000
    }

    [Fact]
    public void CalculateInvestmentAccountTotalValue_WithEmptyPositions_ReturnsZero()
    {
        // Arrange
        var positions = new List<McInvestmentPosition>();
        var account = TestDataManager.CreateTestInvestmentAccount(positions, McInvestmentAccountType.TAXABLE_BROKERAGE);

        // Act
        var totalValue = AccountCalculation.CalculateInvestmentAccountTotalValue(account);

        // Assert
        Assert.Equal(0m, totalValue);
    }

    [Fact]
    public void CalculateInvestmentAccountTotalValue_WithNullPositions_ThrowsException()
    {
        // Arrange
        var positions = new List<McInvestmentPosition>();
        var account = TestDataManager.CreateTestInvestmentAccount(positions, McInvestmentAccountType.TAXABLE_BROKERAGE);
        account.Positions = null!;

        // Act
        

        // Assert
        var exception = Assert.Throws<InvalidDataException>(() => 
            AccountCalculation.CalculateInvestmentAccountTotalValue(account));
        Assert.Equal("Positions is null", exception.Message);
    }


    [Fact]
    public void CalculateTotalBalanceByBucketType_WithSingleBucketType_ReturnsCorrectBalance()
    {
        
        // Arrange
        var account1 = new McInvestmentAccount()
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
            Positions = [TestDataManager.CreateTestInvestmentPosition(100m, 10m, McInvestmentPositionType.LONG_TERM, true)]
        };
        var bookOfAccounts = TestDataManager.CreateTestBookOfAccounts();
        Assert.NotNull(bookOfAccounts.InvestmentAccounts);
        bookOfAccounts.InvestmentAccounts.Add(account1);

        // Act
        var balance = AccountCalculation.CalculateTotalBalanceByBucketType(bookOfAccounts, McInvestmentPositionType.LONG_TERM);

        // Assert
        Assert.Equal(1000m, balance); 
    }

    [Fact]
    public void CalculateTotalBalanceByBucketType_WithMultipleAccounts_ReturnsCorrectTotal()
    {
        // Arrange
        var account1 = new McInvestmentAccount()
            {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
            Positions = [TestDataManager.CreateTestInvestmentPosition(100m, 10m, McInvestmentPositionType.LONG_TERM, true)]
        };
        var account2 = new McInvestmentAccount()
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
            Positions = [TestDataManager.CreateTestInvestmentPosition(120m, 5m, McInvestmentPositionType.LONG_TERM, true)]
        };
        var bookOfAccounts = TestDataManager.CreateTestBookOfAccounts();
        Assert.NotNull(bookOfAccounts.InvestmentAccounts);
        bookOfAccounts.InvestmentAccounts.Add(account1);
        bookOfAccounts.InvestmentAccounts.Add(account2);

        // Act
        var balance = AccountCalculation.CalculateTotalBalanceByBucketType(bookOfAccounts, McInvestmentPositionType.LONG_TERM);

        // Assert
        Assert.Equal(1600m, balance);
    }

    [Fact]
    public void CalculateTotalBalanceByBucketType_IgnoresOtherBucketTypes()
    {
        // Arrange
        var account1 = new McInvestmentAccount()
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
            Positions = [
                TestDataManager.CreateTestInvestmentPosition(100m, 10m, McInvestmentPositionType.LONG_TERM, true),
                TestDataManager.CreateTestInvestmentPosition(100m, 10m, McInvestmentPositionType.MID_TERM, true)
            ]
        };
        var bookOfAccounts = TestDataManager.CreateTestBookOfAccounts();
        Assert.NotNull(bookOfAccounts.InvestmentAccounts);
        bookOfAccounts.InvestmentAccounts.Add(account1);

        // Act
        var longTermBalance = AccountCalculation.CalculateTotalBalanceByBucketType(bookOfAccounts, McInvestmentPositionType.LONG_TERM);

        // Assert
        Assert.Equal(1000m, longTermBalance); // Only counts long-term: 100 * 10 = 1000
    }

    [Fact]
    public void CalculateTotalBalanceByBucketType_IgnoresClosedPositions()
    {
        // Arrange
        var account1 = new McInvestmentAccount()
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
            Positions = [
                TestDataManager.CreateTestInvestmentPosition(100m, 10m, McInvestmentPositionType.LONG_TERM, true),
                TestDataManager.CreateTestInvestmentPosition(100m, 10m, McInvestmentPositionType.LONG_TERM, false)
            ]
        };
        var bookOfAccounts = TestDataManager.CreateTestBookOfAccounts();
        Assert.NotNull(bookOfAccounts.InvestmentAccounts);
        bookOfAccounts.InvestmentAccounts.Add(account1);

        // Act
        var balance = AccountCalculation.CalculateTotalBalanceByBucketType(bookOfAccounts, McInvestmentPositionType.LONG_TERM);

        // Assert
        Assert.Equal(1000m, balance); // Only counts open position
    }

    [Fact]
    public void CalculateTotalBalanceByBucketType_ExcludesPrimaryResidence()
    {
        // Arrange
        var account1 = new McInvestmentAccount()
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
            Positions = [TestDataManager.CreateTestInvestmentPosition(120m, 5m, McInvestmentPositionType.LONG_TERM, true)]
        };
        var account2 = new McInvestmentAccount()
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            AccountType = McInvestmentAccountType.PRIMARY_RESIDENCE,
            Positions = [TestDataManager.CreateTestInvestmentPosition(120m, 5m, McInvestmentPositionType.LONG_TERM, true)]
        };
        var bookOfAccounts = TestDataManager.CreateTestBookOfAccounts();
        Assert.NotNull(bookOfAccounts.InvestmentAccounts);
        bookOfAccounts.InvestmentAccounts.Add(account1);
        bookOfAccounts.InvestmentAccounts.Add(account2);
        // Act
        var balance = AccountCalculation.CalculateTotalBalanceByBucketType(bookOfAccounts, McInvestmentPositionType.LONG_TERM);

        // Assert
        Assert.Equal(600m, balance); // Primary residence should be excluded
    }

    [Fact]
    public void CalculateTotalBalanceByBucketType_ExcludesCashAccounts()
    {
        // Arrange
        // Arrange
        var account1 = new McInvestmentAccount()
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
            Positions = [TestDataManager.CreateTestInvestmentPosition(120m, 5m, McInvestmentPositionType.SHORT_TERM, true)]
        };
        var account2 = new McInvestmentAccount()
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            AccountType = McInvestmentAccountType.CASH,
            Positions = [TestDataManager.CreateTestInvestmentPosition(120m, 5m, McInvestmentPositionType.SHORT_TERM, true)]
        };
        var bookOfAccounts = TestDataManager.CreateTestBookOfAccounts();
        Assert.NotNull(bookOfAccounts.InvestmentAccounts);
        bookOfAccounts.InvestmentAccounts.Add(account1);
        bookOfAccounts.InvestmentAccounts.Add(account2);

        // Act
        var balance = AccountCalculation.CalculateTotalBalanceByBucketType(bookOfAccounts, McInvestmentPositionType.SHORT_TERM);

        // Assert
        Assert.Equal(600m, balance); // Cash accounts should be excluded
    }

    [Fact]
    public void CalculateTotalBalanceByBucketType_WithNullInvestmentAccounts_ThrowsInvalidDataException()
    {
        // Arrange
        var bookOfAccounts = TestDataManager.CreateTestBookOfAccounts();
        bookOfAccounts.InvestmentAccounts = null;

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => 
            AccountCalculation.CalculateTotalBalanceByBucketType(bookOfAccounts, McInvestmentPositionType.LONG_TERM));
        Assert.Equal("InvestmentAccounts is null", exception.Message);
    }

    
}