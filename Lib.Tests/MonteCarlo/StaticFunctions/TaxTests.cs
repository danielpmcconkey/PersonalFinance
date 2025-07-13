using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class TaxTests
{
    private readonly LocalDateTime _baseDate = new(2025, 1, 1, 0, 0);

    private TaxLedger CreateTestLedger(decimal incomeTarget)
    {
        return new TaxLedger
        {
            IncomeTarget = incomeTarget
        };
    }

    [Fact]
    public void CopyTaxLedger_CreatesDeepCopy()
    {
        // Arrange
        var original = CreateTestLedger(50000m);
        original.CapitalGains.Add((_baseDate, 1000m));
        original.OrdinaryIncome.Add((_baseDate, 2000m));
        original.RmdDistributions[2025] = 3000m;
        original.SocialSecurityIncome.Add((_baseDate, 4000m));

        // Act
        var copy = Tax.CopyTaxLedger(original);

        // Assert
#pragma warning disable xUnit2005
        Assert.NotSame(original, copy); // NotEqual doesn't work
#pragma warning restore xUnit2005
        Assert.Equal(original.CapitalGains.Count, copy.CapitalGains.Count);
        Assert.Equal(original.OrdinaryIncome.Count, copy.OrdinaryIncome.Count);
        Assert.Equal(original.RmdDistributions.Count, copy.RmdDistributions.Count);
        Assert.Equal(original.SocialSecurityIncome.Count, copy.SocialSecurityIncome.Count);
        Assert.Equal(original.IncomeTarget, copy.IncomeTarget);
    }

    [Fact]
    public void RecordCapitalGain_AddsGainCorrectly()
    {
        // Arrange
        var ledger = CreateTestLedger(50000m);
        var amount = 1000m;

        // Act
        var result = Tax.RecordCapitalGain(ledger, _baseDate, amount);
        var recordAdded = result.CapitalGains.First(x => x.earnedDate == _baseDate); 

        // Assert
        Assert.Single(result.CapitalGains);
        Assert.Equal(amount, result.CapitalGains[0].amount);
        Assert.Equal(amount, recordAdded.amount);
        Assert.Equal(_baseDate, result.CapitalGains[0].earnedDate);
    }

    [Fact]
    public void RecordIncome_AddsIncomeCorrectly()
    {
        // Arrange
        var ledger = CreateTestLedger(50000m);
        var amount = 2000m;

        // Act
        var result = Tax.RecordIncome(ledger, _baseDate, amount);

        // Assert
        Assert.Single(result.OrdinaryIncome);
        Assert.Equal(amount, result.OrdinaryIncome[0].amount);
        Assert.Equal(_baseDate, result.OrdinaryIncome[0].earnedDate);
    }

    [Theory]
    [InlineData(McInvestmentAccountType.ROTH_401_K)]
    [InlineData(McInvestmentAccountType.ROTH_IRA)]
    [InlineData(McInvestmentAccountType.HSA)]
    public void RecordInvestmentSale_TaxFreeAccounts_NoChanges(McInvestmentAccountType accountType)
    {
        // Arrange
        var ledger = CreateTestLedger(50000m);
        var position = TestDataManager.CreateTestInvestmentPosition(
            150, 10, McInvestmentPositionType.LONG_TERM, true);
        position.InitialCost = 1000m;

        // Act
        var result = Tax.RecordInvestmentSale(ledger, _baseDate, position, accountType);

        // Assert
        Assert.Empty(result.CapitalGains);
        Assert.Empty(result.OrdinaryIncome);
    }

    [Fact]
    public void RecordInvestmentSale_TaxableBrokerage_RecordsCapitalGains()
    {
        // Arrange
        var ledger = CreateTestLedger(50000m);
        var position = TestDataManager.CreateTestInvestmentPosition(
            150, 10, McInvestmentPositionType.LONG_TERM, true);
        position.InitialCost = 1000m;

        // Act
        var result = Tax.RecordInvestmentSale(
            ledger, _baseDate, position, McInvestmentAccountType.TAXABLE_BROKERAGE);

        // Assert
        Assert.Single(result.CapitalGains);
        Assert.Equal(500m, result.CapitalGains[0].amount); // Growth only
    }

    [Theory]
    [InlineData(McInvestmentAccountType.TRADITIONAL_401_K)]
    [InlineData(McInvestmentAccountType.TRADITIONAL_IRA)]
    public void RecordInvestmentSale_TraditionalAccounts_RecordsFullValueAsIncome(McInvestmentAccountType accountType)
    {
        // Arrange
        var ledger = CreateTestLedger(50000m);
        var position = TestDataManager.CreateTestInvestmentPosition(
            150, 10, McInvestmentPositionType.LONG_TERM, true);
        position.InitialCost = 1000m;

        // Act
        var result = Tax.RecordInvestmentSale(ledger, _baseDate, position, accountType);

        // Assert
        Assert.Single(result.OrdinaryIncome);
        Assert.Equal(1500m, result.OrdinaryIncome[0].amount); // Full value
    }

    [Theory]
    [InlineData(McInvestmentAccountType.PRIMARY_RESIDENCE)]
    [InlineData(McInvestmentAccountType.CASH)]
    public void RecordInvestmentSale_InvalidAccounts_ThrowsException(McInvestmentAccountType accountType)
    {
        // Arrange
        var ledger = CreateTestLedger(50000m);
        var position = TestDataManager.CreateTestInvestmentPosition(
            150, 10, McInvestmentPositionType.LONG_TERM, true);
        position.InitialCost = 1000m;

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => 
            Tax.RecordInvestmentSale(ledger, _baseDate, position, accountType));
    }

    [Fact]
    public void RecordRmdDistribution_AddsNewDistribution()
    {
        // Arrange
        var ledger = CreateTestLedger(50000m);
        var amount = 5000m;

        // Act
        var result = Tax.RecordRmdDistribution(ledger, _baseDate, amount);

        // Assert
        Assert.Single(result.RmdDistributions);
        Assert.Equal(amount, result.RmdDistributions[2025]);
    }
    
    [Fact]
    public void RecordRmdDistribution_WithExistingDistribution_UpdatesNotAdds()
    {
        // Arrange
        var ledger = CreateTestLedger(50000m);
        var amount1 = 5000m;
        var amount2 = 2000m;

        // Act
        var result = Tax.RecordRmdDistribution(ledger, _baseDate, amount1);
        result = Tax.RecordRmdDistribution(result, _baseDate, amount2);

        // Assert
        Assert.Single(result.RmdDistributions);
        Assert.Equal(amount1 + amount2, result.RmdDistributions[2025]);
    }
    
    [Fact]
    public void RecordRmdDistribution_WithExistingDistributionFromPriorYear_AddsNotUpdates()
    {
        // Arrange
        var ledger = CreateTestLedger(50000m);
        var amount1 = 5000m;
        var amount2 = 2000m;

        // Act
        var result = Tax.RecordRmdDistribution(ledger, _baseDate.PlusYears(-1), amount1);
        result = Tax.RecordRmdDistribution(result, _baseDate, amount2);

        // Assert
        Assert.True(result.RmdDistributions.Count == 2);
        Assert.Equal(amount1, result.RmdDistributions[2024]);
        Assert.Equal(amount2, result.RmdDistributions[2025]);
    }

    [Fact]
    public void RecordRmdDistribution_UpdatesExistingDistribution()
    {
        // Arrange
        var ledger = CreateTestLedger(50000m);
        ledger.RmdDistributions[2025] = 3000m;
        var additionalAmount = 2000m;

        // Act
        var result = Tax.RecordRmdDistribution(ledger, _baseDate, additionalAmount);

        // Assert
        Assert.Equal(5000m, result.RmdDistributions[2025]);
    }

    [Fact]
    public void RecordSocialSecurityIncome_AddsIncomeCorrectly()
    {
        // Arrange
        var ledger = CreateTestLedger(50000m);
        var amount = 2500m;

        // Act
        Tax.RecordSocialSecurityIncome(ledger, _baseDate, amount);

        // Assert
        Assert.Single(ledger.SocialSecurityIncome);
        Assert.Equal(amount, ledger.SocialSecurityIncome[0].amount);
        Assert.Equal(_baseDate, ledger.SocialSecurityIncome[0].earnedDate);
    }
    
    [Theory]
    [InlineData(2047, 0)]
    [InlineData(2048, 18867.92)]
    [InlineData(2049, 19607.84)]
    [InlineData(2050, 20325.2)]
    [InlineData(2051, 21097.05)]
    [InlineData(2052, 21834.06)]
    [InlineData(2053, 22727.27)]
    [InlineData(2054, 23696.68)]
    [InlineData(2055, 24752.48)]
    [InlineData(2056, 25773.2)]
    [InlineData(2057, 27027.03)]
    [InlineData(2058, 28248.59)]
    [InlineData(2059, 29761.9)]
    [InlineData(2060, 31250)]
    [InlineData(2061, 32894.74)]
    [InlineData(2062, 34722.22)]
    [InlineData(2063, 36496.35)]
    [InlineData(2064, 38759.69)]
    [InlineData(2065, 40983.61)]
    public void CalculateRmdRequirement_CalculatesCorrectly(int year, decimal expectatedRmdAmount)
    {
        // Arrange
        var ledger = CreateTestLedger(50000m);
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        // we want $500k in tax deferred accounts
        accounts.Traditional401K.Positions = [
            TestDataManager.CreateTestInvestmentPosition(
                1000, 100, McInvestmentPositionType.LONG_TERM, true),
            TestDataManager.CreateTestInvestmentPosition(
                1000, 100, McInvestmentPositionType.LONG_TERM, true),
            TestDataManager.CreateTestInvestmentPosition(
                1000, 100, McInvestmentPositionType.LONG_TERM, true)
        ];
        accounts.TraditionalIra.Positions = [
            TestDataManager.CreateTestInvestmentPosition(
                1000, 100, McInvestmentPositionType.LONG_TERM, true),
            TestDataManager.CreateTestInvestmentPosition(
                1000, 100, McInvestmentPositionType.LONG_TERM, true)
        ];
        var currentDate = new LocalDateTime(year, 12, 1, 0, 0);

        // Act
        var result = Tax.CalculateRmdRequirement(ledger, currentDate, accounts);
        result = Math.Round(result, 2); // rounding to avoid floating point errors
        // Assert
        Assert.Equal(expectatedRmdAmount, result);
    }

    [Fact]
    public void CalculateAdditionalRmdSales_WithNoPriorDistributions_ReturnsFullAmount()
    {
        // Arrange
        var ledger = CreateTestLedger(50000m);
        ledger.RmdDistributions = [];
        var year = 2047;
        var currentDate = new LocalDateTime(year, 12, 1, 0, 0);
        var totalRmdRequirement = 100000m;
        
        // Act
        var result = Tax.CalculateAdditionalRmdSales(year, totalRmdRequirement, ledger, currentDate);

        // Assert
        Assert.Equal(totalRmdRequirement, result);
    }
    
    [Fact]
    public void CalculateAdditionalRmdSales_WithPriorDistributions_ReturnsLesserAmount()
    {
        // Arrange
        var priorDistribution = 9000m;
        var year = 2047;
        var ledger = CreateTestLedger(50000m);
        ledger.RmdDistributions = [];
        ledger.RmdDistributions[year] = priorDistribution;
        var currentDate = new LocalDateTime(year, 12, 1, 0, 0);
        var totalRmdRequirement = 100000m;
        
        // Act
        var result = Tax.CalculateAdditionalRmdSales(year, totalRmdRequirement, ledger, currentDate);

        // Assert
        Assert.Equal(totalRmdRequirement - priorDistribution, result);
    }
    
    [Fact]
    public void CalculateAdditionalRmdSales_WithExtraPriorDistributions_ReturnsZero()
    {
        // Arrange
        var priorDistribution = 190000m;
        var year = 2047;
        var ledger = CreateTestLedger(50000m);
        ledger.RmdDistributions = [];
        ledger.RmdDistributions[year] = priorDistribution;
        var currentDate = new LocalDateTime(year, 12, 1, 0, 0);
        var totalRmdRequirement = 100000m;
        
        // Act
        var result = Tax.CalculateAdditionalRmdSales(year, totalRmdRequirement, ledger, currentDate);

        // Assert
        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData(2047, 0)]
    [InlineData(2048, 18867.92)]
    [InlineData(2049, 19607.84)]
    [InlineData(2050, 20325.2)]
    [InlineData(2051, 21097.05)]
    [InlineData(2052, 21834.06)]
    [InlineData(2053, 22727.27)]
    [InlineData(2054, 23696.68)]
    [InlineData(2055, 24752.48)]
    [InlineData(2056, 25773.2)]
    [InlineData(2057, 27027.03)]
    [InlineData(2058, 28248.59)]
    [InlineData(2059, 29761.9)]
    [InlineData(2060, 31250)]
    [InlineData(2061, 32894.74)]
    [InlineData(2062, 34722.22)]
    [InlineData(2063, 36496.35)]
    [InlineData(2064, 38759.69)]
    [InlineData(2065, 40983.61)]
    public void MeetRmdRequirements_CalculatesAndSellsCorrectly(int year, decimal expectatedRmdAmount)
    {
        // Arrange
        var ledger = CreateTestLedger(50000m);
        var accounts = TestDataManager.CreateTestBookOfAccounts();
        // we want $500k in tax deferred accounts
        for (int i = 0; i < 250; i++)
        {
            accounts.Traditional401K.Positions.Add(
                TestDataManager.CreateTestInvestmentPosition(
                    100, 10, McInvestmentPositionType.LONG_TERM, true)
            );
        }
        for (int i = 0; i < 250; i++)
        {
            accounts.TraditionalIra.Positions.Add(
                TestDataManager.CreateTestInvestmentPosition(
                    100, 10, McInvestmentPositionType.LONG_TERM, true)
            );
        }

        var howManyPositionsShouldBeSold = Math.Ceiling(expectatedRmdAmount / 1000m);
        var expectedSale = howManyPositionsShouldBeSold * 1000m;
        
        var prices = new CurrentPrices();
        var currentDate = new LocalDateTime(year, 12, 1, 0, 0);
        var expectedAmountLeft = 500000m - expectedSale;

        // Act
        var result = Tax.MeetRmdRequirements(ledger, currentDate, accounts, prices);
        var amountLeft = AccountCalculation.CalculateLongBucketTotalBalance(result.newBookOfAccounts);
        
        // Assert
        Assert.Equal(expectedSale, result.amountSold);
        Assert.Equal(expectedAmountLeft, amountLeft);
    }

    [Theory]
    [InlineData(24000, 96950)]
    [InlineData(29000, 96950)]
    [InlineData(34000, 96950)]
    [InlineData(39000, 93800)]
    [InlineData(44000, 89550)]
    [InlineData(49000, 85300)]
    [InlineData(54000, 81050)]
    [InlineData(59000, 76800)]
    [InlineData(64000, 72550)]
    [InlineData(69000, 68300)]
    [InlineData(74000, 64050)]
    [InlineData(79000, 59800)]
    [InlineData(84000, 55550)]
    [InlineData(89000, 51300)]
    [InlineData(94000, 47050)]
    [InlineData(99000, 42800)]
    [InlineData(104000, 38550)]
    [InlineData(109000, 34300)]
    [InlineData(114000, 30050)]
    [InlineData(119000, 25800)]
    [InlineData(124000, 21550)]
    [InlineData(129000, 17300)]
    [InlineData(134000, 13050)]
    [InlineData(139000, 8800)]
    [InlineData(144000, 4550)]
    [InlineData(149000, 300)]
    [InlineData(154000, 0)]
    public void UpdateIncomeTarget_CalculatesCorrectTarget(decimal socialSecurityIncome, decimal expectedTarget)
    {
        // todo: dive deeper on these calculations. I'm not sure I got the tax law right here
        
        // Arrange
        var ledger = CreateTestLedger(50000m);
        ledger.SocialSecurityIncome.Add((_baseDate, socialSecurityIncome)); // Annual amount

        // Act
        var result = Tax.UpdateIncomeTarget(ledger, 2025);

        // Assert
        Assert.Equal(Math.Round(expectedTarget * 1m, 2), Math.Round(result.IncomeTarget, 2));
    }
}