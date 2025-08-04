using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class TaxTests
{
    private readonly LocalDateTime _baseDate = new(2025, 1, 1, 0, 0);

    private TaxLedger CreateTestLedger()
    {
        return new TaxLedger
        {
            
        };
    }
    
    [Fact]
    public void RecordLongTermCapitalGain_AddsGainCorrectly()
    {
        // Arrange
        var ledger = CreateTestLedger();
        var amount = 1000m;

        // Act
        var result = Tax.RecordLongTermCapitalGain(ledger, _baseDate, amount).ledger;
        var recordAdded = result.LongTermCapitalGains.First(x => x.earnedDate == _baseDate); 

        // Assert
        Assert.Single(result.LongTermCapitalGains);
        Assert.Equal(amount, result.LongTermCapitalGains[0].amount);
        Assert.Equal(amount, recordAdded.amount);
        Assert.Equal(_baseDate, result.LongTermCapitalGains[0].earnedDate);
    }
    
    [Fact]
    public void RecordShortTermCapitalGain_AddsGainCorrectly()
    {
        // Arrange
        var ledger = CreateTestLedger();
        var amount = 1000m;

        // Act
        var result = Tax.RecordShortTermCapitalGain(ledger, _baseDate, amount).ledger;
        var recordAdded = result.ShortTermCapitalGains.First(x => x.earnedDate == _baseDate); 

        // Assert
        Assert.Single(result.ShortTermCapitalGains);
        Assert.Equal(amount, result.ShortTermCapitalGains[0].amount);
        Assert.Equal(amount, recordAdded.amount);
        Assert.Equal(_baseDate, result.ShortTermCapitalGains[0].earnedDate);
    }

    [Fact]
    public void CopyTaxLedger_CopiesAccurately()
    {
        // Arrange
        var socialSecurityIncome = 4000m;
        var w2Income = 2000m;
        var taxableIraDistribution = 20200m;
        var taxableInterestReceived = 2300m;
        var taxFreeInterestPaid = 12m;
        var federalWithholdings = 122m;
        var stateWithholdings = 152m;
        var shortTermCapitalGains = 412m;
        var longTermCapitalGains = 10004m;
        var totalTaxPaid = 19.99m;
        
        
        var original = CreateTestLedger();
        original.SocialSecurityIncome.Add((_baseDate, socialSecurityIncome));
        original.W2Income.Add((_baseDate, w2Income));
        original.TaxableIraDistribution.Add((_baseDate, taxableIraDistribution));
        original.TaxableInterestReceived.Add((_baseDate, taxableInterestReceived));
        original.TaxFreeInterestPaid.Add((_baseDate, taxFreeInterestPaid));
        original.FederalWithholdings.Add((_baseDate, federalWithholdings));
        original.StateWithholdings.Add((_baseDate, stateWithholdings));
        original.ShortTermCapitalGains.Add((_baseDate, shortTermCapitalGains));
        original.LongTermCapitalGains.Add((_baseDate, longTermCapitalGains));
        original.TotalTaxPaidLifetime = totalTaxPaid;

        // Act
        var copy = Tax.CopyTaxLedger(original);
        var socialSecurityIncomeResults = copy.SocialSecurityIncome.Sum(x => x.amount);
        var w2IncomeResults = copy.W2Income.Sum(x => x.amount);
        var taxableIraDistributionResults = copy.TaxableIraDistribution.Sum(x => x.amount);
        var taxableInterestReceivedResults = copy.TaxableInterestReceived.Sum(x => x.amount);
        var taxFreeInterestPaidResults = copy.TaxFreeInterestPaid.Sum(x => x.amount);
        var federalWithholdingsResults = copy.FederalWithholdings.Sum(x => x.amount);
        var stateWithholdingsResults = copy.StateWithholdings.Sum(x => x.amount);
        var shortTermCapitalGainsResults = copy.ShortTermCapitalGains.Sum(x => x.amount);
        var longTermCapitalGainsResults = copy.LongTermCapitalGains.Sum(x => x.amount);

        // Assert
#pragma warning disable xUnit2005
        Assert.NotSame(original, copy); // NotEqual doesn't work
#pragma warning restore xUnit2005
        Assert.Equal(original.SocialSecurityIncome.Count, copy.SocialSecurityIncome.Count);
        Assert.Equal(original.W2Income.Count, copy.W2Income.Count);
        Assert.Equal(original.TaxableIraDistribution.Count, copy.TaxableIraDistribution.Count);
        Assert.Equal(original.TaxableInterestReceived.Count, copy.TaxableInterestReceived.Count);
        Assert.Equal(original.TaxFreeInterestPaid.Count, copy.TaxFreeInterestPaid.Count);
        Assert.Equal(original.FederalWithholdings.Count, copy.FederalWithholdings.Count);
        Assert.Equal(original.StateWithholdings.Count, copy.StateWithholdings.Count);
        Assert.Equal(original.ShortTermCapitalGains.Count, copy.ShortTermCapitalGains.Count);
        Assert.Equal(original.LongTermCapitalGains.Count, copy.LongTermCapitalGains.Count);
        
        Assert.Equal(socialSecurityIncome, socialSecurityIncomeResults);
        Assert.Equal(w2Income, w2IncomeResults);
        Assert.Equal(taxableIraDistribution, taxableIraDistributionResults);
        Assert.Equal(taxableInterestReceived, taxableInterestReceivedResults);
        Assert.Equal(taxFreeInterestPaid, taxFreeInterestPaidResults);
        Assert.Equal(federalWithholdings, federalWithholdingsResults);
        Assert.Equal(stateWithholdings, stateWithholdingsResults);
        Assert.Equal(shortTermCapitalGains, shortTermCapitalGainsResults);
        Assert.Equal(longTermCapitalGains, longTermCapitalGainsResults);
        
        Assert.Equal(original.TotalTaxPaidLifetime, copy.TotalTaxPaidLifetime);
        
    }
    
    [Fact]
    public void RecordW2Income_AddsIncomeCorrectly()
    {
        // Arrange
        var ledger = CreateTestLedger();
        var amount = 2000m;

        // Act
        var result = Tax.RecordW2Income(ledger, _baseDate, amount).ledger;

        // Assert
        Assert.Single(result.W2Income);
        Assert.Equal(amount, result.W2Income[0].amount);
        Assert.Equal(_baseDate, result.W2Income[0].earnedDate);
    }
    
    [Fact]
    public void RecordTaxableIraDistribution_AddsDistributionCorrectly()
    {
        // Arrange
        var ledger = CreateTestLedger();
        var amount = 2000m;

        // Act
        var result = Tax.RecordIraDistribution(ledger, _baseDate, amount).ledger;

        // Assert
        Assert.Single(result.TaxableIraDistribution);
        Assert.Equal(amount, result.TaxableIraDistribution[0].amount);
        Assert.Equal(_baseDate, result.TaxableIraDistribution[0].earnedDate);
    }

    [Theory]
    [InlineData(McInvestmentAccountType.ROTH_401_K)]
    [InlineData(McInvestmentAccountType.ROTH_IRA)]
    [InlineData(McInvestmentAccountType.HSA)]
    public void RecordInvestmentSale_TaxFreeAccounts_NoChanges(McInvestmentAccountType accountType)
    {
        // Arrange
        var ledger = CreateTestLedger();
        var position = TestDataManager.CreateTestInvestmentPosition(
            150, 10, McInvestmentPositionType.LONG_TERM, true);
        position.InitialCost = 1000m;

        // Act
        var result = Tax.RecordInvestmentSale(ledger, _baseDate, position, accountType).ledger;

        // Assert
        Assert.Empty(result.LongTermCapitalGains);
        Assert.Empty(result.ShortTermCapitalGains);
        Assert.Empty(result.W2Income);
        Assert.Empty(result.TaxableIraDistribution);
    }

    [Fact]
    public void RecordInvestmentSale_TaxableBrokerage_RecordsCapitalGains()
    {
        // Arrange
        var ledger = CreateTestLedger();
        var position = TestDataManager.CreateTestInvestmentPosition(
            150, 10, McInvestmentPositionType.LONG_TERM, true);
        position.InitialCost = 1000m;

        // Act
        var result = Tax.RecordInvestmentSale(
            ledger, _baseDate, position, McInvestmentAccountType.TAXABLE_BROKERAGE).ledger;

        // Assert
        Assert.Single(result.LongTermCapitalGains);
        Assert.Equal(500m, result.LongTermCapitalGains[0].amount); // Growth only
    }

    [Theory]
    [InlineData(McInvestmentAccountType.TRADITIONAL_401_K)]
    [InlineData(McInvestmentAccountType.TRADITIONAL_IRA)]
    public void RecordInvestmentSale_TraditionalAccounts_RecordsFullValueAsIraDistribution(McInvestmentAccountType accountType)
    {
        // Arrange
        var ledger = CreateTestLedger();
        var position = TestDataManager.CreateTestInvestmentPosition(
            150, 10, McInvestmentPositionType.LONG_TERM, true);
        position.InitialCost = 1000m;

        // Act
        var result = Tax.RecordInvestmentSale(ledger, _baseDate, position, accountType).ledger;

        // Assert
        Assert.Single(result.TaxableIraDistribution);
        Assert.Equal(1500m, result.TaxableIraDistribution[0].amount); // Full value
    }

    [Theory]
    [InlineData(McInvestmentAccountType.PRIMARY_RESIDENCE)]
    [InlineData(McInvestmentAccountType.CASH)]
    public void RecordInvestmentSale_InvalidAccounts_ThrowsException(McInvestmentAccountType accountType)
    {
        // Arrange
        var ledger = CreateTestLedger();
        var position = TestDataManager.CreateTestInvestmentPosition(
            150, 10, McInvestmentPositionType.LONG_TERM, true);
        position.InitialCost = 1000m;

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => 
            Tax.RecordInvestmentSale(ledger, _baseDate, position, accountType));
    }
    
    [Fact]
    public void RecordSocialSecurityIncome_AddsIncomeCorrectly()
    {
        // Arrange
        var ledger = CreateTestLedger();
        var amount = 2500m;

        // Act
        Tax.RecordSocialSecurityIncome(ledger, _baseDate, amount);

        // Assert
        Assert.Single(ledger.SocialSecurityIncome);
        Assert.Equal(amount, ledger.SocialSecurityIncome[0].amount);
        Assert.Equal(_baseDate, ledger.SocialSecurityIncome[0].earnedDate);
    }
    
    
    [Fact]
    public void CalculateAdditionalRmdSales_WithNoPriorDistributions_ReturnsFullAmount()
    {
        // Arrange
        var ledger = CreateTestLedger();
        ledger.TaxableIraDistribution = [];
        var year = 2047;
        var currentDate = new LocalDateTime(year, 12, 1, 0, 0);
        var totalRmdRequirement = 100000m;
        
        // Act
        var result = TaxCalculation.CalculateAdditionalRmdSales(
            year, totalRmdRequirement, ledger, currentDate).amount;

        // Assert
        Assert.Equal(totalRmdRequirement, result);
    }
    
    [Fact]
    public void CalculateAdditionalRmdSales_WithPriorDistributions_ReturnsLesserAmount()
    {
        // Arrange
        var priorDistribution = 9000m;
        var year = 2047;
        var ledger = CreateTestLedger();
        ledger.TaxableIraDistribution = [];
        var currentDate = new LocalDateTime(year, 12, 1, 0, 0);
        ledger = Tax.RecordIraDistribution(ledger, currentDate, priorDistribution).ledger;
        var totalRmdRequirement = 100000m;
        
        // Act
        var result = TaxCalculation.CalculateAdditionalRmdSales(
            year, totalRmdRequirement, ledger, currentDate).amount;

        // Assert
        Assert.Equal(totalRmdRequirement - priorDistribution, result);
    }
    
    [Fact]
    public void CalculateAdditionalRmdSales_WithExtraPriorDistributions_ReturnsZero()
    {
        // Arrange
        var priorDistribution = 190000m;
        var year = 2047;
        var ledger = CreateTestLedger();
        ledger.TaxableIraDistribution = [];
        var currentDate = new LocalDateTime(year, 12, 1, 0, 0);
        ledger = Tax.RecordIraDistribution(ledger, currentDate, priorDistribution).ledger;
        var totalRmdRequirement = 100000m;
        
        // Act
        var result = TaxCalculation.CalculateAdditionalRmdSales(
            year, totalRmdRequirement, ledger, currentDate).amount;

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
        var ledger = CreateTestLedger();
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
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

        int age = year - 1975;

        // Act
        var result = Tax.MeetRmdRequirements(ledger, currentDate, accounts, age);
        var amountLeft = AccountCalculation.CalculateLongBucketTotalBalance(result.newBookOfAccounts);
        
        // Assert
        Assert.Equal(expectedAmountLeft, amountLeft);
    }
    
    [Fact]
    public void MeetRmdRequirements_DoesntChangeNetWorth()
    {
        // Arrange
        var expectatedRmdAmount = 27027.03m;
        var year = 2057;
        var ledger = CreateTestLedger();
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
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

        int age = year - 1975;
        var expectedNetWorth = AccountCalculation.CalculateNetWorth(accounts);
        
        
        

        // Act
        var result = Tax.MeetRmdRequirements(ledger, currentDate, accounts, age);
        var actualNetWorth = AccountCalculation.CalculateNetWorth(result.newBookOfAccounts);

        // Assert
        Assert.Equal(Math.Round(expectedNetWorth  ,2),  Math.Round(actualNetWorth, 2));
    }
}