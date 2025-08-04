using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using System.Collections.Generic;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class InvestmentSalesTests
{
    private readonly LocalDateTime _testDate = new LocalDateTime(2025, 1, 1, 12, 0);

    private McInvestmentPosition CreateTestPosition(
        decimal price = 100m,
        decimal quantity = 10m,
        McInvestmentPositionType positionType = McInvestmentPositionType.LONG_TERM,
        bool isOpen = true,
        LocalDateTime? entry = null)
    {
        return new McInvestmentPosition
        {
            Id = Guid.NewGuid(),
            Name = "Test Position",
            Price = price,
            Quantity = quantity,
            InitialCost = price * quantity,
            InvestmentPositionType = positionType,
            IsOpen = isOpen,
            Entry = entry ?? _testDate.PlusYears(-2)
        };
    }

    private McInvestmentAccount CreateTestAccount(
        McInvestmentAccountType accountType,
        params McInvestmentPosition[] positions)
    {
        return new McInvestmentAccount
        {
            Id = Guid.NewGuid(),
            Name = $"Test {accountType} Account",
            AccountType = accountType,
            Positions = new List<McInvestmentPosition>(positions)
        };
    }

    private BookOfAccounts CreateTestBookOfAccounts(params McInvestmentAccount[] accounts)
    {
        return Account.CreateBookOfAccounts(new List<McInvestmentAccount>(accounts), []);
    }

    [Fact]
    public void SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType_WithValidPosition_SellsAndUpdatesCash()
    {
        // Arrange
        var amountToSell = 1000m;
        var individualPositionValue = 300m;
        var totalPositionValue = 0m;
        var numSales = 0;
        var positionType = McInvestmentPositionType.LONG_TERM;
        McInvestmentAccount account = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
            Positions = []
        };
        while (totalPositionValue < amountToSell)
        {
            account.Positions.Add(new McInvestmentPosition()
            {
                Id = Guid.NewGuid(),
                Name = "Test Position",
                Price = individualPositionValue,
                Quantity = 1m,
                InitialCost = individualPositionValue * 0.5m,
                InvestmentPositionType = positionType,
                IsOpen = true,
                Entry = _testDate.PlusYears(-2)
            });
            totalPositionValue += individualPositionValue;
            numSales++;
        }
        var book = CreateTestBookOfAccounts(account);
        var taxLedger = new TaxLedger();
        var expectedSaleAmount = totalPositionValue;

        // Act
        var result = InvestmentSales.SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType(
            amountToSell, positionType,  InvestmentConfig.SalesOrderWithRoom, book, taxLedger, _testDate);
        var cashBalance = AccountCalculation.CalculateCashBalance(result.newBookOfAccounts);
        var numClosedPositions = result.newBookOfAccounts.InvestmentAccounts[0].Positions.Count(x => !x.IsOpen);

        // Assert
        Assert.Equal(expectedSaleAmount, result.amountSold);
        Assert.Equal(expectedSaleAmount, cashBalance);
        Assert.Equal(numSales, numClosedPositions);
    }

    [Fact]
    public void SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType_WithTaxablePosition_SellsAndUpdatesTaxLedgerCapitalGains()
    {
        // Arrange
        var amountToSell = 1000m;
        var individualPositionValue = 300m;
        var totalPositionValue = 0m;
        var numSales = 0;
        var positionType = McInvestmentPositionType.LONG_TERM;
        var expectedCapitalGains = 0m;
        McInvestmentAccount account = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
            Positions = []
        };
        while (totalPositionValue < amountToSell)
        {
            account.Positions.Add(new McInvestmentPosition()
            {
                Id = Guid.NewGuid(),
                Name = "Test Position",
                Price = individualPositionValue,
                Quantity = 1m,
                InitialCost = individualPositionValue * 0.5m,
                InvestmentPositionType = positionType,
                IsOpen = true,
                Entry = _testDate.PlusYears(-2)
            });
            totalPositionValue += individualPositionValue;
            numSales++;
            expectedCapitalGains += individualPositionValue * 0.5m;
        }
        var book = CreateTestBookOfAccounts(account);
        var taxLedger = new TaxLedger();
        var expectedSaleAmount = totalPositionValue;

        // Act
        var result = InvestmentSales.SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType(
            amountToSell, positionType,  InvestmentConfig.SalesOrderWithRoom, book, taxLedger, _testDate);
        var capitalGains = result.newLedger.LongTermCapitalGains
            .Where(x => x.earnedDate == _testDate)
            .Sum(x => x.amount);

        // Assert
        Assert.Equal(expectedSaleAmount, result.amountSold);
        Assert.Equal(expectedCapitalGains, capitalGains);
    }
    
    [Fact]
    public void SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType_WithTaxDeferredPosition_SellsAndUpdatesTaxLedgerDistribution()
    {
        // Arrange
        var amountToSell = 1000m;
        var individualPositionValue = 300m;
        var totalPositionValue = 0m;
        var numSales = 0;
        var positionType = McInvestmentPositionType.LONG_TERM;
        McInvestmentAccount account = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            AccountType = McInvestmentAccountType.TRADITIONAL_IRA,
            Positions = []
        };
        while (totalPositionValue < amountToSell)
        {
            account.Positions.Add(new McInvestmentPosition()
            {
                Id = Guid.NewGuid(),
                Name = "Test Position",
                Price = individualPositionValue,
                Quantity = 1m,
                InitialCost = individualPositionValue * 0.5m,
                InvestmentPositionType = positionType,
                IsOpen = true,
                Entry = _testDate.PlusYears(-2)
            });
            totalPositionValue += individualPositionValue;
            numSales++;
        }
        var book = CreateTestBookOfAccounts(account);
        var taxLedger = new TaxLedger();
        var expectedSaleAmount = totalPositionValue;

        // Act
        var result = InvestmentSales.SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType(
            amountToSell, positionType,  InvestmentConfig.SalesOrderWithRoom, book, taxLedger, _testDate);
        var income = result.newLedger.TaxableIraDistribution
            .Where(x => x.earnedDate == _testDate)
            .Sum(x => x.amount);

        // Assert
        Assert.Equal(expectedSaleAmount, result.amountSold);
        Assert.Equal(expectedSaleAmount, income);
    }
    [Fact]
    public void SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType_WithTaxFreePosition_SellsAndDoesntUpdateTaxLedger()
    {
        // Arrange
        var amountToSell = 1000m;
        var individualPositionValue = 300m;
        var totalPositionValue = 0m;
        var numSales = 0;
        var positionType = McInvestmentPositionType.LONG_TERM;
        McInvestmentAccount account = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            AccountType = McInvestmentAccountType.HSA,
            Positions = []
        };
        while (totalPositionValue < amountToSell)
        {
            account.Positions.Add(new McInvestmentPosition()
            {
                Id = Guid.NewGuid(),
                Name = "Test Position",
                Price = individualPositionValue,
                Quantity = 1m,
                InitialCost = individualPositionValue * 0.5m,
                InvestmentPositionType = positionType,
                IsOpen = true,
                Entry = _testDate.PlusYears(-2)
            });
            totalPositionValue += individualPositionValue;
            numSales++;
        }
        var book = CreateTestBookOfAccounts(account);
        var taxLedger = new TaxLedger();
        var expectedSaleAmount = totalPositionValue;

        // Act
        var result = InvestmentSales.SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType(
            amountToSell, positionType,  InvestmentConfig.SalesOrderWithRoom, book, taxLedger, _testDate);
        var distributions = result.newLedger.TaxableIraDistribution
            .Where(x => x.earnedDate == _testDate)
            .Sum(x => x.amount);
        var longTermCapitalGains = result.newLedger.LongTermCapitalGains
            .Where(x => x.earnedDate == _testDate)
            .Sum(x => x.amount);
        var shortTermCapitalGains = result.newLedger.ShortTermCapitalGains
            .Where(x => x.earnedDate == _testDate)
            .Sum(x => x.amount);
        var w2Income = result.newLedger.W2Income
            .Where(x => x.earnedDate == _testDate)
            .Sum(x => x.amount);

        // Assert
        Assert.Equal(expectedSaleAmount, result.amountSold);
        Assert.Equal(0, distributions);
        Assert.Equal(0, longTermCapitalGains);
        Assert.Equal(0, shortTermCapitalGains);
        Assert.Equal(0, w2Income);
    }

    [Fact]
    public void SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType_WithClosedPosition_ReturnsUnchangedState()
    {
        // Arrange
        var amountToSell = 1000m;
        var individualPositionValue = 300m;
        var totalPositionValue = 0m;
        var numSales = 0;
        var positionType = McInvestmentPositionType.LONG_TERM;
        McInvestmentAccount account = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
            Positions = []
        };
        while (totalPositionValue < amountToSell)
        {
            account.Positions.Add(new McInvestmentPosition()
            {
                Id = Guid.NewGuid(),
                Name = "Test Position",
                Price = individualPositionValue,
                Quantity = 1m,
                InitialCost = individualPositionValue * 0.5m,
                InvestmentPositionType = positionType,
                IsOpen = false,
                Entry = _testDate.PlusYears(-2)
            });
            totalPositionValue += individualPositionValue;
            numSales++;
        }
        var book = CreateTestBookOfAccounts(account);
        var taxLedger = new TaxLedger();
        var expectedSaleAmount = totalPositionValue;

        // Act
        var result = InvestmentSales.SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType(
            amountToSell, positionType,  InvestmentConfig.SalesOrderWithRoom, book, taxLedger, _testDate);
        var cashBalance = AccountCalculation.CalculateCashBalance(result.newBookOfAccounts);
        var numClosedPositions = result.newBookOfAccounts.InvestmentAccounts[0].Positions.Count(x => !x.IsOpen);

        // Assert
        Assert.Equal(0, result.amountSold);
        Assert.Equal(0, cashBalance);
        Assert.Equal(numSales, numClosedPositions);
    }

    

    [Fact]
    public void SellInvestmentsToRmdAmount_WithSufficientLongTermPositions_SellsCorrectAmount()
    {
        
        // Arrange
        var position = CreateTestPosition(
            price: 100m, 
            quantity: 10m, 
            positionType: McInvestmentPositionType.LONG_TERM);
        var account = CreateTestAccount(McInvestmentAccountType.TRADITIONAL_IRA, position);
        var book = CreateTestBookOfAccounts(account);
        var taxLedger = new TaxLedger();
        var amountNeeded = 500m;

        var expectedSaleAmount = 100m * 10m; // this sells whole positions
        
        // Act
        var result = InvestmentSales.SellInvestmentsToRmdAmount(
            amountNeeded, book, taxLedger, _testDate);
        
        var newCash = AccountCalculation.CalculateCashBalance(result.newBookOfAccounts);

        // Assert
        Assert.Equal(expectedSaleAmount, result.amountSold);
        Assert.Equal(expectedSaleAmount, newCash);
    }
    
    [Fact]
    public void SellInvestmentsToRmdAmount_WithSufficientLongTermPositions_UpdatesTaxLedger()
    {
        
        // Arrange
        var position = CreateTestPosition(
            price: 100m, 
            quantity: 10m, 
            positionType: McInvestmentPositionType.LONG_TERM);
        var account = CreateTestAccount(McInvestmentAccountType.TRADITIONAL_IRA, position);
        var book = CreateTestBookOfAccounts(account);
        var taxLedger = new TaxLedger();
        var amountNeeded = 500m;

        var expectedSaleAmount = 100m * 10m; // this sells whole positions
        
        // Act
        var result = InvestmentSales.SellInvestmentsToRmdAmount(
            amountNeeded, book, taxLedger, _testDate);
        
        var newCash = AccountCalculation.CalculateCashBalance(result.newBookOfAccounts);
        var recordedRmd = result.newLedger.TaxableIraDistribution
            .Where(x => x.earnedDate == _testDate)
            .Sum(x => x.amount);

        // Assert
        Assert.Equal(expectedSaleAmount, recordedRmd);
    }

    /// <summary>
    /// if you have previously calculated an RMD amount, but you don't have that much in any of your traditional
    /// accounts, then that should be a problem
    /// </summary>
    [Fact]
    public void SellInvestmentsToRmdAmount_WithInsufficientFunds_ThrowsInvalidDataException()
    {
        // Arrange
        var position = CreateTestPosition(price: 10m, quantity: 1m);
        var account = CreateTestAccount(McInvestmentAccountType.TRADITIONAL_IRA, position);
        var book = CreateTestBookOfAccounts(account);
        var taxLedger = new TaxLedger();
        var amountNeeded = 1000m;

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => 
            InvestmentSales.SellInvestmentsToRmdAmount(amountNeeded, book, taxLedger, _testDate));
    }

    [Fact]
    public void SellInvestmentsToRmdAmount_WithNullInvestmentAccounts_ThrowsInvalidDataException()
    {
        // Arrange
        var book = new BookOfAccounts { InvestmentAccounts = null };
        var taxLedger = new TaxLedger();

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => 
            InvestmentSales.SellInvestmentsToRmdAmount(100m, book, taxLedger, _testDate));
    }

    [Fact]
    public void SellInvestmentsToDollarAmountByPositionType_WithSufficientFundsInOnePosition_SellsCorrectAmount()
    {
        // Arrange
        var position = CreateTestPosition(
            price: 100m,
            quantity: 10m,
            positionType: McInvestmentPositionType.LONG_TERM);
        var account = CreateTestAccount(McInvestmentAccountType.TAXABLE_BROKERAGE, position);
        var book = CreateTestBookOfAccounts(account);
        var taxLedger = new TaxLedger();
        var amountNeeded = 500m;
        var expectedSaleAmount = 100m * 10m; // this sells whole positions

        // Act
        var result = InvestmentSales.SellInvestmentsToDollarAmountByPositionType(
            amountNeeded,
            McInvestmentPositionType.LONG_TERM,
            book,
            taxLedger,
            _testDate);
        
        var cashBalance = AccountCalculation.CalculateCashBalance(result.newBookOfAccounts);

        // Assert
        Assert.Equal(expectedSaleAmount, result.amountSold);
        Assert.Equal(cashBalance, expectedSaleAmount);
    }
    [Fact]
    public void SellInvestmentsToDollarAmountByPositionType_WithSufficientFundsInManyPosition_SellsCorrectAmount()
    {
        // Arrange
        var position1 = CreateTestPosition(
            price: 100m,
            quantity: 10m,
            positionType: McInvestmentPositionType.LONG_TERM);
        var position2 = CreateTestPosition(
            price: 100m,
            quantity: 10m,
            positionType: McInvestmentPositionType.LONG_TERM);
        var position3 = CreateTestPosition(
            price: 100m,
            quantity: 10m,
            positionType: McInvestmentPositionType.LONG_TERM);
        var account = CreateTestAccount(McInvestmentAccountType.TAXABLE_BROKERAGE, position1);
        account.Positions.Add(position2);
        account.Positions.Add(position3);
        var book = CreateTestBookOfAccounts(account);
        var taxLedger = new TaxLedger();
        var amountNeeded = 1500m;
        var expectedSaleAmount = 2000m;

        // Act
        var result = InvestmentSales.SellInvestmentsToDollarAmountByPositionType(
            amountNeeded,
            McInvestmentPositionType.LONG_TERM,
            book,
            taxLedger,
            _testDate);
        
        var cashBalance = AccountCalculation.CalculateCashBalance(result.newBookOfAccounts);

        // Assert
        Assert.Equal(expectedSaleAmount, result.amountSold);
        Assert.Equal(cashBalance, expectedSaleAmount);
        Assert.Single(result.newBookOfAccounts.InvestmentAccounts[0].Positions.Where(x => x.IsOpen));
    }
    
    /// <summary>
    /// each year, you want to sell tax deferred positions until you reach teh income room, then sell tax free or
    /// brokerage. Test to make sure that happens
    /// </summary>
    [Fact]
    public void SellInvestmentsToDollarAmountByPositionType_WithVarryingAccountTypes_SellsInCorrectOrder()
    {
        // Arrange
        var taxDeferredPositions = new List<McInvestmentPosition>
        {
            TestDataManager.CreateTestInvestmentPosition(100m, 10m, McInvestmentPositionType.LONG_TERM),
            TestDataManager.CreateTestInvestmentPosition(100m, 10m, McInvestmentPositionType.LONG_TERM),
        };
        var taxDeferredAccount = TestDataManager.CreateTestInvestmentAccount(
            taxDeferredPositions, McInvestmentAccountType.TRADITIONAL_IRA);
        
        var brokeragePositions = new List<McInvestmentPosition>
        {
            TestDataManager.CreateTestInvestmentPosition(100m, 10m, McInvestmentPositionType.LONG_TERM),
            TestDataManager.CreateTestInvestmentPosition(100m, 10m, McInvestmentPositionType.LONG_TERM),
        };
        const decimal brokerageCost = 100m;
        brokeragePositions[0].InitialCost = brokerageCost;
        brokeragePositions[1].InitialCost = brokerageCost;
        
        var brokerageAccount = TestDataManager.CreateTestInvestmentAccount(
            brokeragePositions, McInvestmentAccountType.TAXABLE_BROKERAGE);
        
        var book = Account.CreateBookOfAccounts([taxDeferredAccount, brokerageAccount], []);
        var taxLedger = new TaxLedger();
        var incomeNeededToCreateMinimalRoom =
            TaxCalculation.CalculateIncomeRoom(taxLedger, _testDate)
              - 1500m; 
        taxLedger.W2Income.Add((_testDate, incomeNeededToCreateMinimalRoom));
        var actualRoom = TaxCalculation.CalculateIncomeRoom(taxLedger, _testDate);
        var amountNeeded = 2500m;
        var expectedTaxDeferredSaleAmount = 2000m;
        var expectedBrokerageSaleAmount = 1000m;
        var expectedCapitalGains = expectedBrokerageSaleAmount - brokerageCost;

        // Act
        var result = InvestmentSales.SellInvestmentsToDollarAmountByPositionType(
            amountNeeded,
            McInvestmentPositionType.LONG_TERM,
            book,
            taxLedger,
            _testDate);
        
        
        var taxDistributionRecorded = result.newLedger.TaxableIraDistribution.Sum(x => x.amount);
        var capitalGainsRecorded = result.newLedger.LongTermCapitalGains.Sum(x => x.amount);

        // Assert
        Assert.Equal(expectedTaxDeferredSaleAmount, taxDistributionRecorded);
        Assert.Equal(expectedCapitalGains, capitalGainsRecorded);
    }

    [Fact]
    public void SellInvestmentsToDollarAmountByPositionType_WithNullInvestmentAccounts_ThrowsInvalidDataException()
    {
        // Arrange
        var book = new BookOfAccounts { InvestmentAccounts = null };
        var taxLedger = new TaxLedger();

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => 
            InvestmentSales.SellInvestmentsToDollarAmountByPositionType(
                100m,
                McInvestmentPositionType.LONG_TERM,
                book,
                taxLedger,
                _testDate));
    }
}