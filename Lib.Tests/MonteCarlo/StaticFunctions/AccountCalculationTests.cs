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
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();

        // Act
        var result = AccountCalculation.CalculateDebtTotal(accounts);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateDebtTotal_NullDebtAccounts_ThrowsInvalidDataException()
    {
        // Arrange
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.DebtAccounts = null;

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => AccountCalculation.CalculateDebtTotal(accounts));
    }

    [Fact]
    public void CalculateDebtTotal_SingleAccountSinglePosition_ReturnsCorrectTotal()
    {
        // Arrange
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
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
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
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
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
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
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
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
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
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
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
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
        
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
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
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
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
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
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
        var bookOfAccounts = TestDataManager.CreateEmptyBookOfAccounts();
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
        var bookOfAccounts = TestDataManager.CreateEmptyBookOfAccounts();
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
        var bookOfAccounts = TestDataManager.CreateEmptyBookOfAccounts();
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
        var bookOfAccounts = TestDataManager.CreateEmptyBookOfAccounts();
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
        var bookOfAccounts = TestDataManager.CreateEmptyBookOfAccounts();
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
        var bookOfAccounts = TestDataManager.CreateEmptyBookOfAccounts();
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
        var bookOfAccounts = TestDataManager.CreateEmptyBookOfAccounts();
        bookOfAccounts.InvestmentAccounts = null;

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => 
            AccountCalculation.CalculateTotalBalanceByBucketType(bookOfAccounts, McInvestmentPositionType.LONG_TERM));
        Assert.Equal("InvestmentAccounts is null", exception.Message);
    }
    
    [Theory]
    [InlineData(100, 10, 1000)] // Regular case
    [InlineData(0, 10, 0)]      // Zero price
    [InlineData(100, 0, 0)]     // Zero quantity
    public void CalculateInvestmentAccountTotalValue_WithValidPositions_ReturnsTotalValue(
        decimal price, decimal quantity, decimal expectedValue)
    {
        // Arrange
        var position = TestDataManager.CreateTestInvestmentPosition(
            price, quantity, McInvestmentPositionType.LONG_TERM);
        var account = TestDataManager.CreateTestInvestmentAccount(
            new List<McInvestmentPosition> { position }, 
            McInvestmentAccountType.TAXABLE_BROKERAGE);

        // Act
        var result = AccountCalculation.CalculateInvestmentAccountTotalValue(account);

        // Assert
        Assert.Equal(expectedValue, result);
    }
    
    [Fact]
    public void CalculateInvestmentAccountTotalValue_WithMultiplePositions_AddsAllValuesCorrectly()
    {
        var price1 = 100m;
        var quantity1 = 10m;
        var price2 = 50m;
        var quantity2 = 20m;
        var expectedValue = (price1 * quantity1) + (price2 * quantity2);
        // Arrange
        var positions = new List<McInvestmentPosition> {
                TestDataManager.CreateTestInvestmentPosition(price1, quantity1, McInvestmentPositionType.LONG_TERM),
                TestDataManager.CreateTestInvestmentPosition(price2, quantity2, McInvestmentPositionType.LONG_TERM),
            };
        
        var account = TestDataManager.CreateTestInvestmentAccount(positions, McInvestmentAccountType.TAXABLE_BROKERAGE);

        // Act
        var result = AccountCalculation.CalculateInvestmentAccountTotalValue(account);

        // Assert
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void CalculateInvestmentAccountTotalValue_WithNullPositions_ThrowsInvalidDataException()
    {
        // Arrange
        var account = TestDataManager.CreateTestInvestmentAccount([], McInvestmentAccountType.TAXABLE_BROKERAGE);
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        // we disable the warning because that's the point of the test
        account.Positions = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => 
            AccountCalculation.CalculateInvestmentAccountTotalValue(account));
    }

    [Fact]
    public void CalculateInvestmentAccountTotalValue_WithClosedPositions_ExcludesFromTotal()
    {
        // Arrange
        var positions = new List<McInvestmentPosition>
        {
            TestDataManager.CreateTestInvestmentPosition(
                100m, 1m, McInvestmentPositionType.LONG_TERM, true),
            TestDataManager.CreateTestInvestmentPosition(
                100m, 1m, McInvestmentPositionType.LONG_TERM, false)
        };
        var account = TestDataManager.CreateTestInvestmentAccount(
            positions, McInvestmentAccountType.TAXABLE_BROKERAGE);

        // Act
        var result = AccountCalculation.CalculateInvestmentAccountTotalValue(account);

        // Assert
        Assert.Equal(100m, result); // Only counts the open position
    }

    [Fact]
    public void CalculateLongBucketTotalBalance_WithMixedPositions_OnlyCountsLongTerm()
    {
        // Arrange
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var positions = new List<McInvestmentPosition>
        {
            TestDataManager.CreateTestInvestmentPosition(100m, 1m, McInvestmentPositionType.LONG_TERM),
            TestDataManager.CreateTestInvestmentPosition(200m, 1m, McInvestmentPositionType.MID_TERM),
            TestDataManager.CreateTestInvestmentPosition(300m, 1m, McInvestmentPositionType.SHORT_TERM)
        };
        accounts.InvestmentAccounts.Add(TestDataManager.CreateTestInvestmentAccount(
            positions, McInvestmentAccountType.TAXABLE_BROKERAGE));

        // Act
        var result = AccountCalculation.CalculateLongBucketTotalBalance(accounts);

        // Assert
        Assert.Equal(100m, result); // Only the long-term position value
    }

    [Fact]
    public void CalculateMidBucketTotalBalance_WithMixedPositions_OnlyCountsMidTerm()
    {
        // Arrange
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var positions = new List<McInvestmentPosition>
        {
            TestDataManager.CreateTestInvestmentPosition(100m, 1m, McInvestmentPositionType.LONG_TERM),
            TestDataManager.CreateTestInvestmentPosition(200m, 1m, McInvestmentPositionType.MID_TERM),
            TestDataManager.CreateTestInvestmentPosition(300m, 1m, McInvestmentPositionType.SHORT_TERM)
        };
        accounts.InvestmentAccounts.Add(TestDataManager.CreateTestInvestmentAccount(
            positions, McInvestmentAccountType.TAXABLE_BROKERAGE));

        // Act
        var result = AccountCalculation.CalculateMidBucketTotalBalance(accounts);

        // Assert
        Assert.Equal(200m, result); // Only the mid-term position value
    }

    [Fact]
    public void CalculateShortBucketTotalBalance_WithMixedPositions_OnlyCountsShortTerm()
    {
        // Arrange
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var positions = new List<McInvestmentPosition>
        {
            TestDataManager.CreateTestInvestmentPosition(100m, 1m, McInvestmentPositionType.LONG_TERM),
            TestDataManager.CreateTestInvestmentPosition(200m, 1m, McInvestmentPositionType.MID_TERM),
            TestDataManager.CreateTestInvestmentPosition(300m, 1m, McInvestmentPositionType.SHORT_TERM)
        };
        accounts.InvestmentAccounts.Add(TestDataManager.CreateTestInvestmentAccount(
            positions, McInvestmentAccountType.TAXABLE_BROKERAGE));

        // Act
        var result = AccountCalculation.CalculateShortBucketTotalBalance(accounts);

        // Assert
        Assert.Equal(300m, result); // Only the short-term position value
    }

    [Fact]
    public void CalculateNetWorth_WithValidAccounts_ReturnsCorrectBalance()
    {
        // Arrange
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        
        // Add investment position
        var investmentPositions = new List<McInvestmentPosition>
        {
            TestDataManager.CreateTestInvestmentPosition(100m, 1m, McInvestmentPositionType.LONG_TERM)
        };
        accounts.InvestmentAccounts.Add(TestDataManager.CreateTestInvestmentAccount(
            investmentPositions, McInvestmentAccountType.TAXABLE_BROKERAGE));

        // Add debt position
        var debtPosition = TestDataManager.CreateTestDebtPosition(true, 0.05m, 10m, 50m);
        var debtAccount = TestDataManager.CreateTestDebtAccount(
            new List<McDebtPosition> { debtPosition });
        accounts.DebtAccounts = new List<McDebtAccount> { debtAccount };

        // Act
        var result = AccountCalculation.CalculateNetWorth(accounts);

        // Assert
        Assert.Equal(50m, result); // 100 (assets) - 50 (liabilities)
    }

    [Fact]
    public void CalculateNetWorth_ExcludesPrimaryResidence()
    {
        // Arrange
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var positions = new List<McInvestmentPosition>
        {
            TestDataManager.CreateTestInvestmentPosition(1000m, 1m, McInvestmentPositionType.LONG_TERM)
        };
        accounts.InvestmentAccounts.Add(TestDataManager.CreateTestInvestmentAccount(
            positions, McInvestmentAccountType.PRIMARY_RESIDENCE));

        // Act
        var result = AccountCalculation.CalculateNetWorth(accounts);

        // Assert
        Assert.Equal(0m, result); // Primary residence should be excluded
    }

    [Fact]
    public void CalculateNetWorth_WithNullAccounts_ThrowsInvalidDataException()
    {
        // Arrange
        var accounts = new BookOfAccounts
        {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            InvestmentAccounts = null,
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
            DebtAccounts = []
        };

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => 
            AccountCalculation.CalculateNetWorth(accounts));
    }

    [Fact]
    public void CalculateNetWorth_WithClosedPositions_ExcludesFromCalculation()
    {
        // Arrange
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var positions = new List<McInvestmentPosition>
        {
            TestDataManager.CreateTestInvestmentPosition(
                200m, 1m, McInvestmentPositionType.LONG_TERM, true),
            TestDataManager.CreateTestInvestmentPosition(
                100m, 1m, McInvestmentPositionType.LONG_TERM, false)
        };
        accounts.InvestmentAccounts.Add(TestDataManager.CreateTestInvestmentAccount(
            positions, McInvestmentAccountType.TAXABLE_BROKERAGE));

        // Act
        var result = AccountCalculation.CalculateNetWorth(accounts);

        // Assert
        Assert.Equal(200m, result); // Only the open position
    }
    
    [Fact]
    internal void CalculateTotalBalanceByMultipleFactors_WithMinDateOnly_OnlyAddsRecentPositions()
    {
        // Arrange
        var (oneYearAgo, accounts) = TestDataManager.CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var expected = 27000m;
        
        // Act
        var actual = AccountCalculation.CalculateTotalBalanceByMultipleFactors(accounts,
            [McInvestmentAccountType.TAXABLE_BROKERAGE], null, oneYearAgo);
        
        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    internal void CalculateTotalBalanceByMultipleFactors_WithMaxDateOnly_OnlyAddsOlderPositions()
    {
        // Arrange
        var (oneYearAgo, accounts) = TestDataManager.CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var expected = 23000m;
        
        // Act
        var actual = AccountCalculation.CalculateTotalBalanceByMultipleFactors(
            accounts, [McInvestmentAccountType.TAXABLE_BROKERAGE], null,
            null, oneYearAgo);
        
        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    internal void CalculateTotalBalanceByMultipleFactors_WithMindAndMaxDate_OnlyAddsPositionsInRange()
    {
        // Arrange
        var (oneYearAgo, accounts) = TestDataManager.CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        accounts.Brokerage.Positions = [];
        for (var i = 0; i < 10; i++)
        {
            accounts.Brokerage.Positions.Add(
                TestDataManager.CreateTestInvestmentPosition(
                    1000m, 1m, McInvestmentPositionType.LONG_TERM, true, .5m,
                    oneYearAgo.PlusYears(-i)));
        }

        var expected = 5000m;
        
        // Act
        var actual = AccountCalculation.CalculateTotalBalanceByMultipleFactors(
            accounts, [McInvestmentAccountType.TAXABLE_BROKERAGE], null,
            oneYearAgo.PlusYears(-9), oneYearAgo.PlusYears(-4));
        
        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    internal void CalculateTotalBalanceByMultipleFactors_WithAccountType_OnlyAddsThosePositions()
    {
        // Arrange
        var (oneYearAgo, accounts) = TestDataManager.CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var expected = 70000m;
        
        // Act
        var actual = AccountCalculation.CalculateTotalBalanceByMultipleFactors(
            accounts, [McInvestmentAccountType.TRADITIONAL_401_K, McInvestmentAccountType.HSA]);
        
        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    internal void CalculateTotalBalanceByMultipleFactors_WithPositionType_OnlyAddsThosePositions()
    {
        // Arrange
        var (oneYearAgo, accounts) = TestDataManager.CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var expected = 17000m;
        
        // Act
        var actual = AccountCalculation.CalculateTotalBalanceByMultipleFactors(
            accounts, [McInvestmentAccountType.TRADITIONAL_IRA],
            [McInvestmentPositionType.MID_TERM]);
        
        // Assert
        Assert.Equal(expected, actual);
    }
    
    
    [Fact]
    internal void CalculateAverageCostsOfBrokeragePositionsByMultipleFactors_WithMinDateOnly_OnlyAddsRecentPositions()
    {
        // Arrange
        var (oneYearAgo, accounts) = TestDataManager.CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var expected = 27000m * 0.5m;
        
        // Act
        var actual = AccountCalculation.CalculateAverageCostsOfBrokeragePositionsByMultipleFactors(accounts,
            null, oneYearAgo);
        
        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    internal void CalculateAverageCostsOfBrokeragePositionsByMultipleFactors_WithMaxDateOnly_OnlyAddsOlderPositions()
    {
        // Arrange
        var (oneYearAgo, accounts) = TestDataManager.CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var expected = 23000m * 0.5m;
        
        // Act
        var actual = AccountCalculation.CalculateAverageCostsOfBrokeragePositionsByMultipleFactors(accounts,
            null, null, oneYearAgo);
        
        // Assert
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    internal void CalculateAverageCostsOfBrokeragePositionsByMultipleFactors_WithMindAndMaxDate_OnlyAddsPositionsInRange()
    {
        // Arrange
        var (oneYearAgo, accounts) = TestDataManager.CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var costModifier = 0.2m;
        accounts.Brokerage.Positions = [];
        for (var i = 0; i < 10; i++)
        {
            accounts.Brokerage.Positions.Add(
                TestDataManager.CreateTestInvestmentPosition(
                    1000m, 1m, McInvestmentPositionType.LONG_TERM, true, costModifier,
                    oneYearAgo.PlusYears(-i)));
        }

        var expected = 5000m * costModifier;
        
        // Act
        var actual = AccountCalculation.CalculateAverageCostsOfBrokeragePositionsByMultipleFactors(
            accounts, null,
            oneYearAgo.PlusYears(-9), oneYearAgo.PlusYears(-4));
        
        // Assert
        Assert.Equal(expected, actual);
    }
    
    
    
    [Fact]
    internal void CalculateAverageCostsOfBrokeragePositionsByMultipleFactors_WithPositionType_OnlyAddsThosePositions()
    {
        // Arrange
        var (oneYearAgo, accounts) = TestDataManager.CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var expected = 24000m * 0.5m;
        
        // Act
        var actual = AccountCalculation.CalculateAverageCostsOfBrokeragePositionsByMultipleFactors(
            accounts, [McInvestmentPositionType.MID_TERM]);
        
        // Assert
        Assert.Equal(expected, actual);
    }

    
}