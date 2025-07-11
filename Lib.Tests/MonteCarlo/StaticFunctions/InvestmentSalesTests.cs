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
    public void SellInvestmentPosition_WithValidPosition_SellsAndUpdatesCash()
    {
        // Arrange
        var position = CreateTestPosition(price: 100m, quantity: 10m);
        var account = CreateTestAccount(McInvestmentAccountType.TAXABLE_BROKERAGE, position);
        var book = CreateTestBookOfAccounts(account);
        var taxLedger = new TaxLedger();
        var expectedSaleAmount = position.CurrentValue;

        // Act
        var result = InvestmentSales.SellInvestmentPosition(
            position, book, _testDate, taxLedger, McInvestmentAccountType.TAXABLE_BROKERAGE);

        // Assert
        Assert.Equal(expectedSaleAmount, result.saleAmount);
        Assert.Equal(0m, position.Quantity);
        Assert.False(position.IsOpen);
        Assert.True(result.newBookOfAccounts.Cash.Positions.Exists(p => p.CurrentValue == expectedSaleAmount));
    }

    [Fact]
    public void SellInvestmentPosition_WithTaxablePosition_SellsAndUpdatesTaxLedgerCapitalGains()
    {
        // Arrange
        var position = CreateTestPosition(price: 100m, quantity: 10m);
        position.InitialCost = 100m;
        position.Entry = _testDate.PlusYears(-2);
        var profit = (100m * 10m) - 100m;
        var account = CreateTestAccount(McInvestmentAccountType.TAXABLE_BROKERAGE, position);
        var book = CreateTestBookOfAccounts(account);
        var taxLedger = new TaxLedger();
        var expectedSaleAmount = position.CurrentValue;

        // Act
        var result = InvestmentSales.SellInvestmentPosition(
            position, book, _testDate, taxLedger, McInvestmentAccountType.TAXABLE_BROKERAGE);

        var recordedIncome = result.newLedger.CapitalGains.Sum(x => x.amount);

        // Assert
        Assert.Equal(profit, recordedIncome);
    }
    
    [Fact]
    public void SellInvestmentPosition_WithTaxDeferredPosition_SellsAndUpdatesTaxLedgerIncome()
    {
        // Arrange
        var position = CreateTestPosition(price: 100m, quantity: 10m);
        position.InitialCost = 100m;
        position.Entry = _testDate.PlusYears(-2);
        var account = CreateTestAccount(McInvestmentAccountType.TRADITIONAL_IRA, position);
        var book = CreateTestBookOfAccounts(account);
        var taxLedger = new TaxLedger();
        var expectedSaleAmount = position.CurrentValue;

        // Act
        var result = InvestmentSales.SellInvestmentPosition(
            position, book, _testDate, taxLedger, McInvestmentAccountType.TRADITIONAL_IRA);

        var recordedIncome = result.newLedger.OrdinaryIncome.Sum(x => x.amount);

        // Assert
        Assert.Equal(1000m, recordedIncome);
    }
    [Fact]
    public void SellInvestmentPosition_WithTaxFreePosition_SellsAndDoesntUpdateTaxLedger()
    {
        // Arrange
        var position = CreateTestPosition(price: 100m, quantity: 10m);
        position.InitialCost = 100m;
        position.Entry = _testDate.PlusYears(-2);
        var account = CreateTestAccount(McInvestmentAccountType.ROTH_401_K, position);
        var book = CreateTestBookOfAccounts(account);
        var taxLedger = new TaxLedger();
        const decimal preSetCapitalGains = 200m;
        const decimal preSetIncome = 300m;
        taxLedger.CapitalGains.Add((_testDate, preSetCapitalGains));
        taxLedger.OrdinaryIncome.Add((_testDate, preSetIncome));

        // Act
        var result = InvestmentSales.SellInvestmentPosition(
            position, book, _testDate, taxLedger, McInvestmentAccountType.ROTH_401_K);

        var recordedGains = result.newLedger.CapitalGains.Sum(x => x.amount);
        var recordedIncome = result.newLedger.OrdinaryIncome.Sum(x => x.amount);

        // Assert
        Assert.Equal(preSetIncome, recordedIncome);
        Assert.Equal(preSetCapitalGains, recordedGains);
    }

    [Fact]
    public void SellInvestmentPosition_WithClosedPosition_ReturnsUnchangedState()
    {
        // Arrange
        var position = CreateTestPosition(isOpen: false);
        var account = CreateTestAccount(McInvestmentAccountType.TAXABLE_BROKERAGE, position);
        var book = CreateTestBookOfAccounts(account);
        var taxLedger = new TaxLedger();

        // Act
        var result = InvestmentSales.SellInvestmentPosition(
            position, book, _testDate, taxLedger, McInvestmentAccountType.TAXABLE_BROKERAGE);

        // Assert
        Assert.Equal(0m, result.saleAmount);
        Assert.Equal(book, result.newBookOfAccounts);
        Assert.Equal(taxLedger, result.newLedger);
    }

    [Fact]
    public void RemovePositionFromBookOfAccounts_RemovesPositionCorrectly()
    {
        // Arrange
        var positionToRemove = CreateTestPosition();
        var positionToKeep = CreateTestPosition();
        var account = CreateTestAccount(
            McInvestmentAccountType.TAXABLE_BROKERAGE, 
            positionToRemove, 
            positionToKeep);
        var book = CreateTestBookOfAccounts(account);

        // Act
        var result = InvestmentSales.RemovePositionFromBookOfAccounts(positionToRemove, book);

        // Assert
        Assert.Single(result.InvestmentAccounts[0].Positions);
        Assert.Equal(positionToKeep.Id, result.InvestmentAccounts[0].Positions[0].Id);
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
        var recordedRmd = result.newLedger.RmdDistributions.Sum(x => x.Value);

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
        Assert.Single(result.newBookOfAccounts.InvestmentAccounts[0].Positions);
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
        var incomeNeededToCreateMinimalRoom = TaxConstants.BaseIncomeTarget - 1500m;
        taxLedger.OrdinaryIncome.Add((_testDate, incomeNeededToCreateMinimalRoom));
        var amountNeeded = 2500m;
        var expectedTaxDeferredSaleAmount = 2000m;
        var expectedBrokerageSaleAmount = 1000m;
        var expectedOrdinaryIncome = incomeNeededToCreateMinimalRoom + expectedTaxDeferredSaleAmount;
        var expectedCapitalGains = expectedBrokerageSaleAmount - brokerageCost;

        // Act
        var result = InvestmentSales.SellInvestmentsToDollarAmountByPositionType(
            amountNeeded,
            McInvestmentPositionType.LONG_TERM,
            book,
            taxLedger,
            _testDate);
        
        var incomeRecorded = result.newLedger.OrdinaryIncome.Sum(x => x.amount);
        var capitalGainsRecorded = result.newLedger.CapitalGains.Sum(x => x.amount);

        // Assert
        Assert.Equal(expectedOrdinaryIncome, incomeRecorded);
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