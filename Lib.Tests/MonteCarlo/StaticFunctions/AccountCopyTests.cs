using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using NodaTime;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class AccountCopyTests
{
    [Fact]
    public void CopyDebtAccounts_ShouldCopyAllPropertiesAccurately()
    {
        // Arrange
        var originalAccounts = new List<McDebtAccount>
        {
            new McDebtAccount
            {
                Id = Guid.NewGuid(),
                Name = "Test Debt Account",
                Positions = new List<McDebtPosition>
                {
                    new McDebtPosition
                    {
                        Id = Guid.NewGuid(),
                        IsOpen = true,
                        Name = "Test Debt Position",
                        Entry = new LocalDateTime(2025, 1, 1, 0, 0),
                        AnnualPercentageRate = 0.05m,
                        MonthlyPayment = 500.0m,
                        CurrentBalance = 10000.0m
                    }
                }
            }
        };

        // Act
        var copiedAccounts = AccountCopy.CopyDebtAccounts(originalAccounts);

        // Assert
        Assert.Single(copiedAccounts);
        var originalAccount = originalAccounts[0];
        var copiedAccount = copiedAccounts[0];

        // Verify account properties
        Assert.Equal(originalAccount.Id, copiedAccount.Id);
        Assert.Equal(originalAccount.Name, copiedAccount.Name);

        // Verify positions
        Assert.Single(copiedAccount.Positions);
        var originalPosition = originalAccount.Positions[0];
        var copiedPosition = copiedAccount.Positions[0];

        // Verify position properties
        Assert.Equal(originalPosition.Id, copiedPosition.Id);
        Assert.Equal(originalPosition.IsOpen, copiedPosition.IsOpen);
        Assert.Equal(originalPosition.Name, copiedPosition.Name);
        Assert.Equal(originalPosition.Entry, copiedPosition.Entry);
        Assert.Equal(originalPosition.AnnualPercentageRate, copiedPosition.AnnualPercentageRate);
        Assert.Equal(originalPosition.MonthlyPayment, copiedPosition.MonthlyPayment);
        Assert.Equal(originalPosition.CurrentBalance, copiedPosition.CurrentBalance);
    }

    [Fact]
    public void CopyInvestmentAccounts_ShouldCopyAllPropertiesAccurately()
    {
        // Arrange
        var originalAccounts = new List<McInvestmentAccount>
        {
            new McInvestmentAccount
            {
                Id = Guid.NewGuid(),
                Name = "Test Account",
                AccountType = McInvestmentAccountType.ROTH_IRA,
                Positions = new List<McInvestmentPosition>
                {
                    new McInvestmentPosition
                    {
                        Id = Guid.NewGuid(),
                        IsOpen = true,
                        Name = "Test Position",
                        Entry = new LocalDateTime(2025, 1, 1, 0, 0),
                        InvestmentPositionType = McInvestmentPositionType.LONG_TERM,
                        InitialCost = 1000.0m,
                        Quantity = 10.0m,
                        Price = 150.0m
                    }
                }
            }
        };

        // Act
        var copiedAccounts = AccountCopy.CopyInvestmentAccounts(originalAccounts);

        // Assert
        Assert.Single(copiedAccounts);
        var originalAccount = originalAccounts[0];
        var copiedAccount = copiedAccounts[0];

        // Verify account properties
        Assert.Equal(originalAccount.Id, copiedAccount.Id);
        Assert.Equal(originalAccount.Name, copiedAccount.Name);
        Assert.Equal(originalAccount.AccountType, copiedAccount.AccountType);

        // Verify positions
        Assert.Single(copiedAccount.Positions);
        var originalPosition = originalAccount.Positions[0];
        var copiedPosition = copiedAccount.Positions[0];

        // Verify position properties
        Assert.Equal(originalPosition.Id, copiedPosition.Id); 
        Assert.Equal(originalPosition.IsOpen, copiedPosition.IsOpen);
        Assert.Equal(originalPosition.Name, copiedPosition.Name);
        Assert.Equal(originalPosition.Entry, copiedPosition.Entry);
        Assert.Equal(originalPosition.InvestmentPositionType, copiedPosition.InvestmentPositionType);
        Assert.Equal(originalPosition.InitialCost, copiedPosition.InitialCost);
        Assert.Equal(originalPosition.Quantity, copiedPosition.Quantity);
        Assert.Equal(originalPosition.Price, copiedPosition.Price);

        // Verify current value calculation is preserved
        Assert.Equal(
            originalPosition.CurrentValue,
            copiedPosition.CurrentValue
        );
    }

    [Fact]
    public void CopyDebtPositions_ShouldCopyAllPropertiesAccurately()
    {
        // Arrange
        var originalPositions = new List<McDebtPosition>
        {
            new McDebtPosition
            {
                Id = Guid.NewGuid(),
                IsOpen = true,
                Name = "Test Debt Position",
                Entry = new LocalDateTime(2025, 1, 1, 0, 0),
                AnnualPercentageRate = 0.05m,
                MonthlyPayment = 500.0m,
                CurrentBalance = 10000.0m
            },
            new McDebtPosition
            {
                Id = Guid.NewGuid(),
                IsOpen = false,
                Name = "Closed Debt Position",
                Entry = new LocalDateTime(2024, 1, 1, 0, 0),
                AnnualPercentageRate = 0.07m,
                MonthlyPayment = 750.0m,
                CurrentBalance = 0.0m
            }
        };

        var newAccountId = Guid.NewGuid();

        // Act
        var copiedPositions = AccountCopy.CopyDebtPositions(originalPositions);

        // Assert
        Assert.Equal(originalPositions.Count, copiedPositions.Count);

        for (int i = 0; i < originalPositions.Count; i++)
        {
            var original = originalPositions[i];
            var copied = copiedPositions[i];

            Assert.Equal(original.Id, copied.Id);
            Assert.Equal(original.IsOpen, copied.IsOpen);
            Assert.Equal(original.Name, copied.Name);
            Assert.Equal(original.Entry, copied.Entry);
            Assert.Equal(original.AnnualPercentageRate, copied.AnnualPercentageRate);
            Assert.Equal(original.MonthlyPayment, copied.MonthlyPayment);
            Assert.Equal(original.CurrentBalance, copied.CurrentBalance);
        }
    }

    [Fact]
    public void CopyInvestmentPositions_ShouldCopyAllPropertiesAccurately()
    {
        // Arrange
        var originalPositions = new List<McInvestmentPosition>
        {
            new McInvestmentPosition
            {
                Id = Guid.NewGuid(),
                IsOpen = true,
                Name = "Test Investment Position",
                Entry = new LocalDateTime(2025, 1, 1, 0, 0),
                InvestmentPositionType = McInvestmentPositionType.LONG_TERM,
                InitialCost = 5000.0m,
                Quantity = 100.0m,
                Price = 75.0m
            },
            new McInvestmentPosition
            {
                Id = Guid.NewGuid(),
                IsOpen = false,
                Name = "Closed Investment Position",
                Entry = new LocalDateTime(2024, 1, 1, 0, 0),
                InvestmentPositionType = McInvestmentPositionType.SHORT_TERM,
                InitialCost = 2000.0m,
                Quantity = 50.0m,
                Price = 45.0m
            }
        };

        var newAccountId = Guid.NewGuid();

        // Act
        var copiedPositions = AccountCopy.CopyInvestmentPositions(originalPositions);

        // Assert
        Assert.Equal(originalPositions.Count, copiedPositions.Count);

        for (int i = 0; i < originalPositions.Count; i++)
        {
            var original = originalPositions[i];
            var copied = copiedPositions[i];

            Assert.Equal(original.Id, copied.Id); 
            Assert.Equal(original.IsOpen, copied.IsOpen);
            Assert.Equal(original.Name, copied.Name);
            Assert.Equal(original.Entry, copied.Entry);
            Assert.Equal(original.InvestmentPositionType, copied.InvestmentPositionType);
            Assert.Equal(original.InitialCost, copied.InitialCost);
            Assert.Equal(original.Quantity, copied.Quantity);
            Assert.Equal(original.Price, copied.Price);

            // Verify calculated values are preserved
            Assert.Equal(original.CurrentValue, copied.CurrentValue);
        }
    }

    [Fact]
    public void CopyPositions_ShouldHandleEmptyLists()
    {
        // Arrange
        var emptyDebtPositions = new List<McDebtPosition>();
        var emptyInvestmentPositions = new List<McInvestmentPosition>();
        var newAccountId = Guid.NewGuid();

        // Act
        var copiedDebtPositions = AccountCopy.CopyDebtPositions(emptyDebtPositions);
        var copiedInvestmentPositions = AccountCopy.CopyInvestmentPositions(emptyInvestmentPositions);

        // Assert
        Assert.Empty(copiedDebtPositions);
        Assert.Empty(copiedInvestmentPositions);
    }

    [Fact]
    public void CopyPositions_ShouldHandleNullPositions()

    {
        // Arrange
        List<McDebtPosition>? nullDebtPositions = null;
        List<McInvestmentPosition>? nullInvestmentPositions = null;
        var newAccountId = Guid.NewGuid();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => AccountCopy.CopyDebtPositions(nullDebtPositions!));
        Assert.Throws<ArgumentNullException>(() => AccountCopy.CopyInvestmentPositions(nullInvestmentPositions!));
    }
}
