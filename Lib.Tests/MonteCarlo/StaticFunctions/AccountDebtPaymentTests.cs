using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using System.Collections.Generic;
using Lib.MonteCarlo.StaticFunctions;
using Lib.Tests;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class AccountDebtPaymentTests
{
    private readonly LocalDateTime _testDate = new LocalDateTime(2025, 1, 1, 12, 0);

    private McDebtPosition CreateTestDebtPosition(decimal balance = 1000m, bool isOpen = true)
    {
        
        return new McDebtPosition
        {
            Id = Guid.NewGuid(),
            Name = "Test Loan",
            CurrentBalance = balance,
            IsOpen = isOpen, 
            MonthlyPayment = 100m,
            AnnualPercentageRate = 0.05m,
            Entry = new LocalDateTime(2025, 1, 1, 0, 0)
        };
    }

    private Dictionary<Guid, decimal> CreatePaymentDictionary(McDebtPosition position, decimal amount)
    {
        return new Dictionary<Guid, decimal> { { position.Id, amount } };
    }

    [Fact]
    public void CreditDebtPosition_WithValidPayment_ReducesBalance()
    {
        // Arrange
        var position = CreateTestDebtPosition(1000m);
        var payments = CreatePaymentDictionary(position, 200m);

        // Act
        var (newPosition, totalCredited) = AccountDebtPayment.CreditDebtPosition(
            position, _testDate, payments);

        // Assert
        Assert.Equal(800m, newPosition.CurrentBalance);
        Assert.Equal(200m, totalCredited);
        Assert.True(newPosition.IsOpen);
    }

    [Fact]
    public void CreditDebtPosition_WithFullPayment_ClosesPosition()
    {
        // Arrange
        var position = CreateTestDebtPosition(1000m);
        var payments = CreatePaymentDictionary(position, 1000m);

        // Act
        var (newPosition, totalCredited) = AccountDebtPayment.CreditDebtPosition(
            position, _testDate, payments);

        // Assert
        Assert.Equal(0m, newPosition.CurrentBalance);
        Assert.Equal(1000m, totalCredited);
        Assert.False(newPosition.IsOpen);
    }

    [Fact]
    public void CreditDebtPosition_WithClosedPosition_ReturnsUnchanged()
    {
        // Arrange
        var position = CreateTestDebtPosition(1000m, false);
        var payments = CreatePaymentDictionary(position, 200m);

        // Act
        var (newPosition, totalCredited) = AccountDebtPayment.CreditDebtPosition(
            position, _testDate, payments);

        // Assert
        Assert.Equal(position.CurrentBalance, newPosition.CurrentBalance);
        Assert.Equal(0m, totalCredited);
        Assert.False(newPosition.IsOpen);
    }

    [Fact]
    public void CreditDebtPosition_WithMissingPayment_ThrowsInvalidDataException()
    {
        // Arrange
        var position = CreateTestDebtPosition();
        var payments = new Dictionary<Guid, decimal>();

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => 
            AccountDebtPayment.CreditDebtPosition(position, _testDate, payments));
    }

    [Fact]
    public void CreditDebtAccount_WithMultiplePositions_CreditsCorrectly()
    {
        // Arrange
        var position1 = CreateTestDebtPosition(1000m);
        var position2 = CreateTestDebtPosition(2000m);
        var account = new McDebtAccount
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            Positions = new List<McDebtPosition> { position1, position2 }
        };
        
        var payments = new Dictionary<Guid, decimal>
        {
            { position1.Id, 500m },
            { position2.Id, 1000m }
        };

        // Act
        var (newAccount, totalCredited) = AccountDebtPayment.CreditDebtAccount(
            account, _testDate, payments);

        // Assert
        Assert.Equal(1500m, totalCredited);
        Assert.Equal(2, newAccount.Positions.Count);
        Assert.Equal(500m, newAccount.Positions[0].CurrentBalance);
        Assert.Equal(1000m, newAccount.Positions[1].CurrentBalance);
    }

    [Fact]
    public void CreditDebtAccount_WithNullPositions_ThrowsInvalidDataException()
    {
        // Arrange
        var account = new McDebtAccount
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            Positions = null
        };
        var payments = new Dictionary<Guid, decimal>();

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => 
            AccountDebtPayment.CreditDebtAccount(account, _testDate, payments));
    }

    [Fact]
    public void PayDownLoans_WithSufficientFunds_SuccessfullyPaysLoans()
    {
        // Arrange
        var position = CreateTestDebtPosition(1000m);
        position.MonthlyPayment = 200m;
        var debtAccount = new McDebtAccount
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            Positions = [ position ]
        };

        var accounts = TestDataManager.CreateTestBookOfAccounts();
        accounts.DebtAccounts = [ debtAccount ];
        accounts = AccountCashManagement.DepositCash(accounts, 2000m, _testDate);
         

        var taxLedger = new TaxLedger();
        var lifetimeSpend = new LifetimeSpend();
        
        // Act
        var result = AccountDebtPayment.PayDownLoans(
            accounts, _testDate, taxLedger, lifetimeSpend);
        var newCashBalance = AccountCalculation.CalculateCashBalance(result.newBookOfAccounts);

        // Assert
        Assert.True(result.isSuccessful);
        Assert.Single(result.newBookOfAccounts.DebtAccounts);
        Assert.Equal(800m, result.newBookOfAccounts.DebtAccounts[0].Positions[0].CurrentBalance);
        Assert.True(result.newBookOfAccounts.DebtAccounts[0].Positions[0].IsOpen); 
        Assert.Equal(1800m, newCashBalance);
    }

    [Fact]
    public void PayDownLoans_WithInsufficientFunds_ReturnsFalse()
    {
        // Arrange
        var position = CreateTestDebtPosition(1000m);
        position.MonthlyPayment = 200m;
        var debtAccount = new McDebtAccount
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            Positions = [ position ]
        };

        var accounts = TestDataManager.CreateTestBookOfAccounts();
        accounts.DebtAccounts = [ debtAccount ];
        accounts = AccountCashManagement.DepositCash(accounts, 100m, _testDate);
         

        var taxLedger = new TaxLedger();
        var lifetimeSpend = new LifetimeSpend();

        // Act
        var result = AccountDebtPayment.PayDownLoans(
            accounts, _testDate, taxLedger, lifetimeSpend);

        // Assert
        Assert.False(result.isSuccessful);
    }

    [Fact]
    public void PayDownLoans_WithNullDebtAccounts_ThrowsInvalidDataException()
    {
        // Arrange
        var accounts = new BookOfAccounts { DebtAccounts = null };
        var taxLedger = new TaxLedger();
        var lifetimeSpend = new LifetimeSpend();

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => 
            AccountDebtPayment.PayDownLoans(accounts, _testDate, taxLedger, lifetimeSpend));
    }
}