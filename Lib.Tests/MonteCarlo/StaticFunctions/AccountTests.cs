using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using System.Collections.Generic;
using Lib.MonteCarlo.StaticFunctions;
using Lib.Tests;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class AccountTests
{
    private readonly LocalDateTime _testDate = new LocalDateTime(2025, 1, 1, 12, 0);

    [Fact]
    public void CreateBookOfAccounts_WithValidAccounts_CreatesAllDefaultAccounts()
    {
        // Arrange
        var investmentAccounts = new List<McInvestmentAccount>();
        var debtAccounts = new List<McDebtAccount>();

        // Act
        var result = Account.CreateBookOfAccounts(investmentAccounts, debtAccounts);

        // Assert
        Assert.NotNull(result.Roth401K);
        Assert.NotNull(result.RothIra);
        Assert.NotNull(result.Traditional401K);
        Assert.NotNull(result.TraditionalIra);
        Assert.NotNull(result.Brokerage);
        Assert.NotNull(result.Hsa);
        Assert.NotNull(result.Cash);
        Assert.Equal(7, result.InvestmentAccounts.Count); // All default accounts
        Assert.Same(debtAccounts, result.DebtAccounts);
    }

    [Fact]
    public void CreateBookOfAccounts_WithExistingAccounts_UsesExistingAccounts()
    {
        // Arrange
        var existingAccount = new McInvestmentAccount
        {
            Id = Guid.NewGuid(),
            AccountType = McInvestmentAccountType.ROTH_401_K,
            Name = "Existing Roth 401K",
            Positions = new List<McInvestmentPosition>()
        };
        var investmentAccounts = new List<McInvestmentAccount> { existingAccount };
        var debtAccounts = new List<McDebtAccount>();

        // Act
        var result = Account.CreateBookOfAccounts(investmentAccounts, debtAccounts);

        // Assert
        Assert.Same(existingAccount, result.Roth401K);
        /*
         * disabling the warning because Assert.Contains() doesn't actually find the account. Not sure why
         */
#pragma warning disable xUnit2012
        Assert.True(result.InvestmentAccounts.Any(x => x.Id == existingAccount.Id));
#pragma warning restore xUnit2012
        
    }

    
    /// <summary>
    /// AI wrote this UT. It's not really necessary
    /// </summary>
    [Theory]
    [InlineData(1, 1, McInvestmentAccountType.TRADITIONAL_401_K)] // Tax deferred, 401k group
    [InlineData(2, 6, McInvestmentAccountType.HSA)] // Tax free HSA
    [InlineData(3, 1, McInvestmentAccountType.ROTH_401_K)] // Tax free Roth, 401k group
    [InlineData(4, 4, McInvestmentAccountType.TAXABLE_BROKERAGE)] // Tax on capital gains, brokerage
    [InlineData(4, 5, McInvestmentAccountType.PRIMARY_RESIDENCE)] // Tax on capital gains, home equity
    public void GetAccountType_WithValidInputs_ReturnsCorrectType(
        int taxBucket, int accountGroup, McInvestmentAccountType expectedType)
    {
        // Act
        var result = Account.GetAccountType(taxBucket, accountGroup);

        // Assert
        Assert.Equal(expectedType, result);
    }

    /// <summary>
    /// AI wrote this UT. It's not really necessary
    /// </summary>
    [Fact]
    public void GetAccountType_WithInvalidCombination_ReturnsCash()
    {
        // Act
        var result = Account.GetAccountType(99, 99);

        // Assert
        Assert.Equal(McInvestmentAccountType.CASH, result);
    }

    [Fact]
    public void GetOrCreateDefaultAccount_WithExistingAccount_ReturnsExisting()
    {
        // Arrange
        var existingAccount = new McInvestmentAccount
        {
            Id = Guid.NewGuid(),
            AccountType = McInvestmentAccountType.ROTH_401_K,
            Name = "Existing Account",
            Positions = new List<McInvestmentPosition>()
        };
        var accounts = new List<McInvestmentAccount> { existingAccount };

        // Act
        var (defaultAccount, newAccounts) = Account.GetOrCreateDefaultAccount(
            McInvestmentAccountType.ROTH_401_K, accounts);

        // Assert
        Assert.Same(existingAccount, defaultAccount);
        Assert.Same(accounts, newAccounts);
    }

    [Fact]
    public void GetOrCreateDefaultAccount_WithNoExistingAccount_CreatesNew()
    {
        // Arrange
        var accounts = new List<McInvestmentAccount>();
        var accountType = McInvestmentAccountType.ROTH_401_K;

        // Act
        var (defaultAccount, newAccounts) = Account.GetOrCreateDefaultAccount(accountType, accounts);

        // Assert
        Assert.NotNull(defaultAccount);
        Assert.Equal(accountType, defaultAccount.AccountType);
        Assert.Single(newAccounts);
        Assert.Contains(defaultAccount, newAccounts);
        Assert.Empty(defaultAccount.Positions);
    }
}