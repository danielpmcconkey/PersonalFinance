using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using System.Collections.Generic;
using Lib.MonteCarlo.StaticFunctions;
using Lib.Tests;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class AccountCleanupTests
{
    private readonly LocalDateTime _testDate = new LocalDateTime(2025, 1, 1, 0, 0);
    private readonly CurrentPrices _testPrices = TestDataManager.CreateTestCurrentPrices(
        0.02M, 2.0M, 1.5M, 1.25M);

    

    private static (LocalDateTime OneYearAgo, BookOfAccounts Accounts) CreateBookForCleanUpTests(
        int roth401KMidCount, int roth401KLongCount, 
        int rothIraMidCount, int rothIraLongCount, 
        int traditional401KMidCount, int traditional401KLongCount, 
        int traditionalIraMidCount, int traditionalIraLongCount,
        int hsaMidCount, int hsaLongCount,
        int brokerageMidLongCount, int brokerageLongLongCount,
        int brokerageMidShortCount, int brokerageLongShortCount)
    {
        return TestDataManager.CreateBookForCleanUpTests(roth401KMidCount, roth401KLongCount, rothIraMidCount,
            rothIraLongCount, traditional401KMidCount, traditional401KLongCount, traditionalIraMidCount,
            traditionalIraLongCount, hsaMidCount, hsaLongCount, brokerageMidLongCount, brokerageLongLongCount,
            brokerageMidShortCount, brokerageLongShortCount);
    }
    
    
    
    [Fact]
    public void RemoveClosedDebtPositions_RemovesClosedDebtPositions()
    {
        // Arrange
        var (oneYearAgo, accounts) = CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var expected = 0;
        
        // Act
        var newAccounts = AccountCleanup.CleanUpAccounts(_testDate, accounts, _testPrices);
        var actual = newAccounts.DebtAccounts
            .SelectMany(x => x.Positions
                .Where(y => !y.IsOpen))
            .Count();
        
        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    internal void RemoveClosedDebtPositions_KeepsOpenDebtPositions()
    {
        // Arrange
        var (oneYearAgo, accounts) = CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var expected = accounts.DebtAccounts
            .SelectMany(x => x.Positions
                .Where(y => y.IsOpen))
            .Count();
        
        // Act
        var newAccounts = AccountCleanup.CleanUpAccounts(_testDate, accounts, _testPrices);
        var actual = newAccounts.DebtAccounts
            .SelectMany(x => x.Positions
                .Where(y => y.IsOpen))
            .Count();
        
        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    internal void CleanUpAccounts_AfterCleanup_ThereIsOnlyOnePositionForEachType()
    {
        // Arrange
        var (oneYearAgo, accounts) = CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        
        // Act
        var newAccounts = AccountCleanup.CleanUpAccounts(_testDate, accounts, _testPrices);
        
        var newBrokerageLongLongPositionsCount =
            newAccounts.InvestmentAccounts.Where(x => x.AccountType == McInvestmentAccountType.TAXABLE_BROKERAGE)
                .SelectMany(x => x.Positions.Where(y =>
                    y.IsOpen &&
                    y.InvestmentPositionType == McInvestmentPositionType.LONG_TERM &&
                    y.Entry < oneYearAgo)).Count();
        var newBrokerageMidLongPositionsCount =
            newAccounts.InvestmentAccounts.Where(x => x.AccountType == McInvestmentAccountType.TAXABLE_BROKERAGE)
                .SelectMany(x => x.Positions.Where(y =>
                    y.IsOpen &&
                    y.InvestmentPositionType == McInvestmentPositionType.MID_TERM &&
                    y.Entry < oneYearAgo)).Count();
        var newRoth401KLongPositionsCount =
            newAccounts.InvestmentAccounts.Where(x => x.AccountType == McInvestmentAccountType.ROTH_401_K)
                .SelectMany(x => x.Positions.Where(y =>
                    y.IsOpen &&
                    y.InvestmentPositionType == McInvestmentPositionType.LONG_TERM))
                .Count();
        var newRoth401KMidPositionsCount =
            newAccounts.InvestmentAccounts.Where(x => x.AccountType == McInvestmentAccountType.ROTH_401_K)
                .SelectMany(x => x.Positions.Where(y =>
                    y.IsOpen &&
                    y.InvestmentPositionType == McInvestmentPositionType.MID_TERM))
                .Count();
        var newRothIraLongPositionsCount =
            newAccounts.InvestmentAccounts.Where(x => x.AccountType == McInvestmentAccountType.ROTH_IRA)
                .SelectMany(x => x.Positions.Where(y =>
                    y.IsOpen &&
                    y.InvestmentPositionType == McInvestmentPositionType.LONG_TERM))
                .Count();
        var newRothIraMidPositionsCount =
            newAccounts.InvestmentAccounts.Where(x => x.AccountType == McInvestmentAccountType.ROTH_IRA)
                .SelectMany(x => x.Positions.Where(y =>
                    y.IsOpen &&
                    y.InvestmentPositionType == McInvestmentPositionType.MID_TERM))
                .Count();
        var newTradIraLongPositionsCount =
            newAccounts.InvestmentAccounts.Where(x => x.AccountType == McInvestmentAccountType.TRADITIONAL_IRA)
                .SelectMany(x => x.Positions.Where(y =>
                    y.IsOpen &&
                    y.InvestmentPositionType == McInvestmentPositionType.LONG_TERM))
                .Count();
        var newTradIraMidPositionsCount =
            newAccounts.InvestmentAccounts.Where(x => x.AccountType == McInvestmentAccountType.TRADITIONAL_IRA)
                .SelectMany(x => x.Positions.Where(y =>
                    y.IsOpen &&
                    y.InvestmentPositionType == McInvestmentPositionType.MID_TERM))
                .Count();
        var newTrad401KLongPositionsCount =
            newAccounts.InvestmentAccounts.Where(x => x.AccountType == McInvestmentAccountType.TRADITIONAL_401_K)
                .SelectMany(x => x.Positions.Where(y =>
                    y.IsOpen &&
                    y.InvestmentPositionType == McInvestmentPositionType.LONG_TERM))
                .Count();
        var newTrad401KMidPositionsCount =
            newAccounts.InvestmentAccounts.Where(x => x.AccountType == McInvestmentAccountType.TRADITIONAL_401_K)
                .SelectMany(x => x.Positions.Where(y =>
                    y.IsOpen &&
                    y.InvestmentPositionType == McInvestmentPositionType.MID_TERM))
                .Count();
        
        var newHsaLongPositionsCount =
            newAccounts.InvestmentAccounts.Where(x => x.AccountType == McInvestmentAccountType.HSA)
                .SelectMany(x => x.Positions.Where(y =>
                    y.IsOpen &&
                    y.InvestmentPositionType == McInvestmentPositionType.LONG_TERM))
                .Count();
        var newHsaMidPositionsCount =
            newAccounts.InvestmentAccounts.Where(x => x.AccountType == McInvestmentAccountType.HSA)
                .SelectMany(x => x.Positions.Where(y =>
                    y.IsOpen &&
                    y.InvestmentPositionType == McInvestmentPositionType.MID_TERM))
                .Count();
        // Assert
        Assert.Equal(1, newBrokerageLongLongPositionsCount);
        Assert.Equal(1, newBrokerageMidLongPositionsCount);
        Assert.Equal(0, newRoth401KLongPositionsCount); // these should all "move" to Roth IRA
        Assert.Equal(0, newRoth401KMidPositionsCount); // these should all "move" to Roth IRA
        Assert.Equal(1, newRothIraLongPositionsCount);
        Assert.Equal(1, newRothIraMidPositionsCount);
        Assert.Equal(1, newTradIraLongPositionsCount); 
        Assert.Equal(1, newTradIraMidPositionsCount);
        Assert.Equal(0, newTrad401KLongPositionsCount); // these should all "move" to Trad IRA
        Assert.Equal(0, newTrad401KMidPositionsCount); // these should all "move" to Trad IRA
        Assert.Equal(0, newHsaLongPositionsCount); // these should all "move" to Roth IRA
        Assert.Equal(0, newHsaMidPositionsCount); // these should all "move" to Roth IRA
    }
    
    [Fact]
    internal void CleanUpAccounts_AfterCleanup_ShortlyHeldBrokeragePositionCountIsTheSame()
    {
        // Arrange
        var (oneYearAgo, accounts) = CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        
        // Act
        var newAccounts = AccountCleanup.CleanUpAccounts(_testDate, accounts, _testPrices);
        var newBrokerageMidShortPositionsCount =
            newAccounts.InvestmentAccounts.Where(x => x.AccountType == McInvestmentAccountType.TAXABLE_BROKERAGE)
                .SelectMany(x => x.Positions.Where(y =>
                    y.IsOpen &&
                    y.InvestmentPositionType == McInvestmentPositionType.MID_TERM &&
                    y.Entry >= oneYearAgo)).Count();
        var newBrokerageLongShortPositionsCount =
            newAccounts.InvestmentAccounts.Where(x => x.AccountType == McInvestmentAccountType.TAXABLE_BROKERAGE)
                .SelectMany(x => x.Positions.Where(y =>
                    y.IsOpen &&
                    y.InvestmentPositionType == McInvestmentPositionType.LONG_TERM &&
                    y.Entry >= oneYearAgo)).Count();
        
        // Assert
        Assert.Equal(13, newBrokerageMidShortPositionsCount);
        Assert.Equal(14, newBrokerageLongShortPositionsCount);
    }
    
    [Fact]
    internal void CleanUpAccounts_AfterCleanup_NetWorthIsTheSameAsBeforeCleanup()
    {
        // Arrange
        var (oneYearAgo, accounts) = CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var expected = Math.Round(AccountCalculation.CalculateNetWorth(accounts), 4);
        
        // Act
        var newAccounts = AccountCleanup.CleanUpAccounts(_testDate, accounts, _testPrices);
        var actual = Math.Round(AccountCalculation.CalculateNetWorth(newAccounts), 4);
        
        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    internal void CleanUpAccounts_AfterCleanup_TotalLongTermIsTheSameAsBeforeCleanup()
    {
        // Arrange
        var (oneYearAgo, accounts) = CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var expected = AccountCalculation.CalculateLongBucketTotalBalance(accounts);
        
        // Act
        var newAccounts = AccountCleanup.CleanUpAccounts(_testDate, accounts, _testPrices);
        var actual = AccountCalculation.CalculateLongBucketTotalBalance(newAccounts);
        
        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    internal void CleanUpAccounts_AfterCleanup_TotalMidTermIsTheSameAsBeforeCleanup()
    {
        // Arrange
        var (oneYearAgo, accounts) = CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var expected = AccountCalculation.CalculateMidBucketTotalBalance(accounts);
        
        // Act
        var newAccounts = AccountCleanup.CleanUpAccounts(_testDate, accounts, _testPrices);
        var actual = AccountCalculation.CalculateMidBucketTotalBalance(newAccounts);
        
        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    internal void CleanUpAccounts_AfterCleanup_TotalCashIsTheSameAsBeforeCleanup()
    {
        // Arrange
        var (oneYearAgo, accounts) = CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var expected = AccountCalculation.CalculateCashBalance(accounts);
        
        // Act
        var newAccounts = AccountCleanup.CleanUpAccounts(_testDate, accounts, _testPrices);
        var actual = AccountCalculation.CalculateCashBalance(newAccounts);
        
        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    internal void CleanUpAccounts_AfterCleanup_TotalDebtIsTheSameAsBeforeCleanup()
    {
        // Arrange
        var (oneYearAgo, accounts) = CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var expected = AccountCalculation.CalculateDebtTotal(accounts);
        
        // Act
        var newAccounts = AccountCleanup.CleanUpAccounts(_testDate, accounts, _testPrices);
        var actual = AccountCalculation.CalculateDebtTotal(newAccounts);
        
        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    internal void CleanUpAccounts_AfterCleanup_TotalPrimaryResidenceIsTheSameAsBeforeCleanup()
    {
        // Arrange
        var (oneYearAgo, accounts) = CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var expected = AccountCalculation.CalculateTotalBalanceByMultipleFactors(accounts, 
            [McInvestmentAccountType.PRIMARY_RESIDENCE]);
        
        // Act
        var newAccounts = AccountCleanup.CleanUpAccounts(_testDate, accounts, _testPrices);
        var actual = AccountCalculation.CalculateTotalBalanceByMultipleFactors(newAccounts,
            [McInvestmentAccountType.PRIMARY_RESIDENCE]);
        
        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    internal void CleanUpAccounts_AfterCleanup_TotalTaxableIsTheSameAsBeforeCleanup()
    {
        // Arrange
        var (oneYearAgo, accounts) = CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var expected = AccountCalculation.CalculateTotalBalanceByMultipleFactors(accounts, 
            [McInvestmentAccountType.TAXABLE_BROKERAGE]);
        
        // Act
        var newAccounts = AccountCleanup.CleanUpAccounts(_testDate, accounts, _testPrices);
        var actual = AccountCalculation.CalculateTotalBalanceByMultipleFactors(newAccounts,
            [McInvestmentAccountType.TAXABLE_BROKERAGE]);
        
        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    internal void CleanUpAccounts_AfterCleanup_TotalTaxFreeIsTheSameAsBeforeCleanup()
    {
        // Arrange
        var (oneYearAgo, accounts) = CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var accountTypes = new McInvestmentAccountType[]
        {
            McInvestmentAccountType.ROTH_401_K,
            McInvestmentAccountType.ROTH_IRA,
            McInvestmentAccountType.HSA,
        };
        var expected = AccountCalculation.CalculateTotalBalanceByMultipleFactors(accounts, accountTypes);
        
        // Act
        var newAccounts = AccountCleanup.CleanUpAccounts(_testDate, accounts, _testPrices);
        var actual = AccountCalculation.CalculateTotalBalanceByMultipleFactors(newAccounts, accountTypes);
        
        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    internal void CleanUpAccounts_AfterCleanup_TotalTaxDeferredIsTheSameAsBeforeCleanup()
    {
        // Arrange
        var (oneYearAgo, accounts) = CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var accountTypes = new McInvestmentAccountType[]
        {
            McInvestmentAccountType.TRADITIONAL_401_K,
            McInvestmentAccountType.TRADITIONAL_IRA,
        };
        var expected = AccountCalculation.CalculateTotalBalanceByMultipleFactors(accounts, accountTypes);
        
        // Act
        var newAccounts = AccountCleanup.CleanUpAccounts(_testDate, accounts, _testPrices);
        var actual = AccountCalculation.CalculateTotalBalanceByMultipleFactors(newAccounts, accountTypes);
        
        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    internal void CleanUpAccounts_AfterCleanup_TotalLongHeldIsTheSameAsBeforeCleanup()
    {
        // Arrange
        var (oneYearAgo, accounts) = CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var expected = AccountCalculation.CalculateTotalBalanceByMultipleFactors(
            accounts, [McInvestmentAccountType.TAXABLE_BROKERAGE], 
            null, null, oneYearAgo);
        
        // Act
        var newAccounts = AccountCleanup.CleanUpAccounts(_testDate, accounts, _testPrices);
        var actual = AccountCalculation.CalculateTotalBalanceByMultipleFactors(
            newAccounts, [McInvestmentAccountType.TAXABLE_BROKERAGE],
            null, null, oneYearAgo);
        
        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    internal void CleanUpAccounts_AfterCleanup_TotalShortHeldIsTheSameAsBeforeCleanup()
    {
        // Arrange
        var (oneYearAgo, accounts) = CreateBookForCleanUpTests(
            10,20,
            13,14,
            15,16,
            17,18,
            19,20,
            11,12,
            13,14
        );
        var expected = AccountCalculation.CalculateTotalBalanceByMultipleFactors(
            accounts, [McInvestmentAccountType.TAXABLE_BROKERAGE], 
            null, oneYearAgo, null);
        
        // Act
        var newAccounts = AccountCleanup.CleanUpAccounts(_testDate, accounts, _testPrices);
        var actual = AccountCalculation.CalculateTotalBalanceByMultipleFactors(
            newAccounts, [McInvestmentAccountType.TAXABLE_BROKERAGE],
            null, oneYearAgo, null);
        
        // Assert
        Assert.Equal(expected, actual);
    }
}