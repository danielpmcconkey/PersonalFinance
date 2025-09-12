using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
using Lib.MonteCarlo.StaticFunctions;
using Lib.MonteCarlo.WithdrawalStrategy;
using NodaTime;

namespace Lib.Tests.MonteCarlo.WithdrawalStrategy;

internal record RebalanceTestAssetRow
{
    internal required McInvestmentAccountType AccountType;
    internal required McInvestmentPositionType PositionType;
    internal required LocalDateTime EntryDate;
    internal required decimal StartingBalance;
    internal required decimal ActualEndingBalance;
    internal required decimal ExpectedEndingBalance;
    internal required decimal CostModifier;
}
public class SixtyFortyTests
{
    private Model _rebalanceModel;
    private PgPerson _rebalancePerson;
    private LocalDateTime _rebalanceCurrentDate;
    private decimal _rebalanceCashNeededOnHand;
    public SixtyFortyTests()
    {
        _rebalancePerson = TestDataManager.CreateTestPerson();
        _rebalancePerson.BirthDate = new LocalDateTime(1976, 3, 7, 0, 0);
        _rebalancePerson.RequiredMonthlySpend = 1000;
        _rebalancePerson.RequiredMonthlySpendHealthCare = 500;
        _rebalanceModel = TestDataManager.CreateTestModel(WithdrawalStrategyType.SixtyForty);
        _rebalanceModel.RetirementDate = _rebalancePerson.BirthDate.PlusYears(62); // the magic age When you are retired but have no medicare
        _rebalanceModel.RebalanceFrequency = RebalanceFrequency.MONTHLY;
        _rebalanceModel.NumMonthsCashOnHand = 12;
        _rebalanceModel.NumMonthsMidBucketOnHand = 6;
        _rebalanceModel.NumMonthsPriorToRetirementToBeginRebalance = 12; 
        _rebalanceModel.DesiredMonthlySpendPostRetirement = 800;
        _rebalanceModel.DesiredMonthlySpendPreRetirement = 600; 
        _rebalanceCurrentDate = _rebalancePerson.BirthDate.PlusYears(63); // Within rebalance window, post retirement, pre-medicare
        _rebalanceCashNeededOnHand = Spend.CalculateCashNeedForNMonths(_rebalanceModel, _rebalancePerson, 
            TestDataManager.CreateEmptyBookOfAccounts(), _rebalanceCurrentDate, _rebalanceModel.NumMonthsCashOnHand);
    }
    private (RebalanceTestAssetRow[] assetRowsAfter, TaxLedger ledger)
        RebalancePrep(RebalanceTestAssetRow[] assetRowsBefore)
    {
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        foreach (var assetRow in assetRowsBefore)
        {
            var entryDate = assetRow.EntryDate;
            var position = TestDataManager.CreateTestInvestmentPosition(
                1.0m, assetRow.StartingBalance, assetRow.PositionType, true,
                assetRow.CostModifier, entryDate);
            switch (assetRow.AccountType)
            {
                case McInvestmentAccountType.CASH :
                    accounts = AccountCashManagement.DepositCash(accounts, assetRow.StartingBalance,
                            _rebalanceCurrentDate).accounts;
                    break;
                case McInvestmentAccountType.HSA:
                    accounts.Hsa.Positions.Add(position);
                    break;
                case McInvestmentAccountType.TAXABLE_BROKERAGE:
                    accounts.Brokerage.Positions.Add(position);
                    break;
                case McInvestmentAccountType.TRADITIONAL_401_K:
                    accounts.Traditional401K.Positions.Add(position);
                    break;
                case McInvestmentAccountType.ROTH_401_K:
                    accounts.Roth401K.Positions.Add(position);
                    break;
                case McInvestmentAccountType.ROTH_IRA:
                    accounts.RothIra.Positions.Add(position);
                    break;
                case McInvestmentAccountType.TRADITIONAL_IRA:
                    accounts.TraditionalIra.Positions.Add(position);
                    break;
                case McInvestmentAccountType.PRIMARY_RESIDENCE:
                    throw new InvalidDataException();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        var ledger = new TaxLedger();
        var recessionStats = new RecessionStats
        {
            AreWeInARecession = false,
            AreWeInExtremeAusterityMeasures = false,
            AreWeInLivinLargeMode = false
        };
        var prices = TestDataManager.CreateTestCurrentPrices(
            1m, 100m, 50m, 0m);
    
        var result = _rebalanceModel.WithdrawalStrategy.RebalancePortfolio(
            _rebalanceCurrentDate, accounts, recessionStats, prices, _rebalanceModel, ledger, _rebalancePerson);

        var assetRowsAfter = new RebalanceTestAssetRow[assetRowsBefore.Length];
        for (var i = 0; i < assetRowsBefore.Length; i++)
        {
            var assetRowStart = assetRowsBefore[i];
            var endingBalance = 0m;
            if (assetRowStart.AccountType == McInvestmentAccountType.CASH)
            {
                endingBalance = AccountCalculation.CalculateCashBalance(result.accounts);
            }
            else
            {
                endingBalance = AccountCalculation.CalculateTotalBalanceByMultipleFactors(result.accounts,
                    [assetRowStart.AccountType], [assetRowStart.PositionType]);
            }

            assetRowsAfter[i] = new RebalanceTestAssetRow()
            {
                AccountType = assetRowStart.AccountType,
                ActualEndingBalance = endingBalance,
                StartingBalance = assetRowStart.StartingBalance,
                CostModifier = assetRowStart.CostModifier,
                EntryDate = assetRowStart.EntryDate,
                ExpectedEndingBalance = assetRowStart.ExpectedEndingBalance,
                PositionType = assetRowStart.PositionType
            };
        }

        return (assetRowsAfter, result.ledger);
    }
    [Theory]
    // these values are in the testing spreadsheet in the 60-40 invest excess cash tab
    [InlineData(1, 2039, 100000, 525000, 180800, 525000)]
    [InlineData(2, 2039, 96000, 273230.77, 175100, 273230.77)]
    [InlineData(3, 2039, 92000, 163555.56, 169400, 163555.56)]
    [InlineData(4, 2039, 88000, 103304.35, 163700, 103304.35)]
    [InlineData(5, 2039, 84000, 66000, 158000, 66000)]
    [InlineData(6, 2039, 80000, 41212.12, 152300, 41212.12)]
    [InlineData(7, 2039, 76000, 24000, 136480, 34120)]
    [InlineData(8, 2039, 72000, 11720.93, 117009.38, 35611.55)]
    [InlineData(9, 2039, 68000, 2833.33, 101224.44, 36808.89)]
    [InlineData(10, 2039, 64000, 336000, 129500, 336000)]
    [InlineData(11, 2039, 60000, 170769.23, 123800, 170769.23)]
    [InlineData(12, 2039, 56000, 99555.56, 118100, 99555.56)]
    [InlineData(1, 2040, 52000, 61043.48, 104066.09, 69377.39)]
    [InlineData(1, 2041, 48000, 37714.29, 92238.17, 61492.12)]
    [InlineData(1, 2042, 44000, 22666.67, 80959.48, 53972.98)]
    [InlineData(1, 2043, 40000, 12631.58, 72688.3, 48458.86)]
    [InlineData(1, 2044, 36000, 5860.47, 66375.5, 44250.34)]
    [InlineData(1, 2043, 32000, 1333.33, 61109.35, 40739.56)]
    [InlineData(1, 2042, 28000, 147000, 96265.79, 147000)]
    [InlineData(1, 2041, 24000, 68307.69, 92016, 68307.69)]
    [InlineData(1, 2040, 20000, 35555.56, 69573.34, 46382.22)]
    [InlineData(1, 2039, 16000, 18782.61, 96800, 18782.61)]
    [InlineData(1, 2038, 12000, 9428.57, 109500, 9428.57)]
    [InlineData(1, 2037, 8000, 4121.21, 105500, 4121.21)]
    [InlineData(1, 2036, 4000, 1263.16, 101500, 1263.16)]

    public void InvestExcessCash_UnderDifferentScenarios_CalculatesCorrectly(
        int currentMonth, int currentYear, decimal currentLong, decimal currentMid, decimal expectedLong,
        decimal expectedMid)
    {
        // Arrange
        var currentDate = new LocalDateTime(currentYear, currentMonth, 1, 0, 0);
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts = AccountCashManagement.DepositCash(accounts, 100000m, currentDate).accounts;
        accounts.Brokerage.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            currentLong, 1, McInvestmentPositionType.LONG_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        accounts.Brokerage.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            currentMid, 1, McInvestmentPositionType.MID_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.SixtyForty);
        model.NumMonthsCashOnHand = 12;
        model.NumMonthsMidBucketOnHand = 6;
        model.DesiredMonthlySpendPreRetirement = 600m;
        model.DesiredMonthlySpendPostRetirement = 800m;
        model.RetirementDate = new LocalDateTime(2040, 1, 1, 0, 0);
        model.NumMonthsPriorToRetirementToBeginRebalance = 12;
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = new LocalDateTime(1976, 3, 7, 0, 0);
        person.RequiredMonthlySpend = 1000m;
        person.RequiredMonthlySpendHealthCare = 1500m;
        var prices = TestDataManager.CreateTestCurrentPrices(
            1.0m, 100m, 50m, 0m);
        
        // Act
        var results = model.WithdrawalStrategy.InvestExcessCash(
            currentDate, accounts, prices, model, person).accounts;
        var actualLong = AccountCalculation.CalculateLongBucketTotalBalance(results);
        var actualMid = AccountCalculation.CalculateMidBucketTotalBalance(results);
        // Assert
        Assert.Equal(Math.Round(expectedLong,1, MidpointRounding.AwayFromZero), 
            Math.Round(actualLong,1, MidpointRounding.AwayFromZero));
        Assert.Equal(Math.Round(expectedMid,1, MidpointRounding.AwayFromZero),
            Math.Round(actualMid,1, MidpointRounding.AwayFromZero));
        
    }
    
    [Theory]
    // these values are in the testing spreadsheet in the 60-40 invest excess cash tab
    // testing common scenarios at 60% target ratio
    [InlineData(1, 2040, 5000, 5000, -2500, 4500, 3000)]  // straightforward sale
    [InlineData(1, 2040, 5000, 15000, -2500, 5000, 12500)]  // sale where you already have a bad imbalance favoring mid and can afford both
    [InlineData(1, 2040, 15000, 5000, -2500, 12500, 5000)]  // sale where you already have a bad imbalance favoring long and can afford both
    [InlineData(1, 2040, 5000, 5000, -12000, 0, 0)]  // sale where you can’t afford it altogether
    [InlineData(1, 2040, 2000, 2000, -4000, 0, 0)]  // sale where the movement amount is the same as total balance
    // testing various balance and target scenarios
    [InlineData(1, 2041, 2000, 2000, -2000, 1200, 800)]  // low balances, low sales amount, after retirement
    [InlineData(1, 2038, 2000, 2000, -2000, 2000, 0)]  // low balances, low sales amount, before rebalance time
    [InlineData(6, 2039, 2000, 2000, -2000, 1666.66666666667, 333.333333333333)]  // low balances, low sales amount, between rebalance start and retirement
    [InlineData(1, 2041, 2000, 2000, -3500, 300, 200)]  // low balances, mid sales amount, after retirement
    [InlineData(1, 2038, 2000, 2000, -3500, 500, 0)]  // low balances, mid sales amount, before rebalance time
    [InlineData(6, 2039, 2000, 2000, -3500, 416.666666666667, 83.3333333333333)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(1, 2041, 2000, 2000, -10000, 0, 0)]  // low balances, high sales amount, after retirement
    [InlineData(1, 2038, 2000, 2000, -10000, 0, 0)]  // low balances, high sales amount, before rebalance time
    [InlineData(6, 2039, 2000, 2000, -10000, 0, 0)]  // low balances, high sales amount, between rebalance start and retirement
    [InlineData(1, 2039, 2000, 2000, -3500, 500, 0)]  // low balances, mid sales amount, at rebalance start
    [InlineData(2, 2039, 2000, 2000, -3500, 483.333333333333, 16.6666666666667)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(3, 2039, 2000, 2000, -3500, 466.666666666667, 33.3333333333333)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(4, 2039, 2000, 2000, -3500, 450, 50)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(5, 2039, 2000, 2000, -3500, 433.333333333334, 66.6666666666667)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(7, 2039, 2000, 2000, -3500, 400, 100)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(8, 2039, 2000, 2000, -3500, 383.333333333333, 116.666666666667)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(9, 2039, 2000, 2000, -3500, 366.666666666667, 133.333333333333)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(10, 2039, 2000, 2000, -3500, 350, 150)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(11, 2039, 2000, 2000, -3500, 333.333333333334, 166.666666666667)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(12, 2039, 2000, 2000, -3500, 316.666666666667, 183.333333333333)]  // low balances, mid sales amount, between rebalance start and retirement
    [InlineData(1, 2040, 2000, 2000, -3500, 300, 200)]  // low balances, mid sales amount, at retirement start
    public void SellInvestmentsToDollarAmount_UnderDifferentScenarios_SellsCorrectly(
        int currentMonth, int currentYear, decimal currentLong, decimal currentMid, decimal movementAmount,
        decimal expectedLong, decimal expectedMid)
    {
        // Arrange
        var currentDate = new LocalDateTime(currentYear, currentMonth, 1, 0, 0);
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts = AccountCashManagement.DepositCash(accounts, 100000m, currentDate).accounts;
        accounts.Brokerage.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            currentLong, 1, McInvestmentPositionType.LONG_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        accounts.Brokerage.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            currentMid, 1, McInvestmentPositionType.MID_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.SixtyForty);
        model.NumMonthsCashOnHand = 12;
        model.NumMonthsMidBucketOnHand = 6;
        model.DesiredMonthlySpendPreRetirement = 600m;
        model.DesiredMonthlySpendPostRetirement = 800m;
        model.RetirementDate = new LocalDateTime(2040, 1, 1, 0, 0);
        model.NumMonthsPriorToRetirementToBeginRebalance = 12;
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = new LocalDateTime(1976, 3, 7, 0, 0);
        person.RequiredMonthlySpend = 1000m;
        person.RequiredMonthlySpendHealthCare = 1500m;
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        
        // Act
        var results = model.WithdrawalStrategy.SellInvestmentsToDollarAmount(
            accounts, ledger, currentDate, -movementAmount, model).accounts;
        var actualLong = AccountCalculation.CalculateLongBucketTotalBalance(results);
        var actualMid = AccountCalculation.CalculateMidBucketTotalBalance(results);
        // Assert
        Assert.Equal(Math.Round(expectedLong,1, MidpointRounding.AwayFromZero), 
            Math.Round(actualLong,1, MidpointRounding.AwayFromZero));
        Assert.Equal(Math.Round(expectedMid,1, MidpointRounding.AwayFromZero),
            Math.Round(actualMid,1, MidpointRounding.AwayFromZero));
        
    }

    [Theory]
    // these values are in the testing spreadsheet in the 60-40 movement calc tab
    
    // testing common scenarios at 60% target ratio
    [InlineData(1, 2040, 5000, 5000, -2500, -500, -2000)]  // straightforward sale
    [InlineData(1, 2040, 5000, 15000, -2500, 0, -2500)]  // sale where you already have a bad imbalance favoring mid and can afford both
    [InlineData(1, 2040, 15000, 5000, -2500, -2500, 0)]  // sale where you already have a bad imbalance favoring long and can afford both
    [InlineData(1, 2040, 5000, 5000, 2500, 2500, 0)]  // buy where you wouln’t need to sell anything to reach ideal
    [InlineData(1, 2040, 5000, 50000, 2500, 2500, 0)]  // buy where you obviously have too much mid
    [InlineData(1, 2040, 50000, 5000, 2500, 0, 2500)]  // buy where you obviously have too much long
    [InlineData(1, 2040, 5000, 7000, 2500, 2500, 0)]  // buy where you have a little too much mid
    [InlineData(1, 2040, 12000, 5000, 2500, 0, 2500)]  // buy where you have a little too much long
    [InlineData(1, 2040, 5000, 5000, -12000, -5000, -5000)]  // sale where you can’t afford it altogether
    [InlineData(1, 2040, 2000, 2000, -4000, -2000, -2000)]  // sale where the movement amount is the same as total balance
    // testing common scenarios at 100% target ratio
    [InlineData(1, 2039, 5000, 5000, -2500, 0, -2500)]  // straightforward sale
    [InlineData(1, 2039, 5000, 15000, -2500, 0, -2500)]  // sale where you already have a bad imbalance favoring mid and can afford both
    [InlineData(1, 2039, 15000, 5000, -2500, 0, -2500)]  // sale where you already have a bad imbalance favoring long and can afford both
    [InlineData(1, 2039, 5000, 5000, 2500, 2500, 0)]  // buy where you wouln’t need to sell anything to reach ideal
    [InlineData(1, 2039, 5000, 50000, 2500, 2500, 0)]  // buy where you obviously have too much mid
    [InlineData(1, 2039, 50000, 5000, 2500, 2500, 0)]  // buy where you obviously have too much long
    [InlineData(1, 2039, 5000, 7000, 2500, 2500, 0)]  // buy where you have a little too much mid
    [InlineData(1, 2039, 12000, 5000, 2500, 2500, 0)]  // buy where you have a little too much long
    [InlineData(1, 2039, 5000, 5000, -12000, -5000, -5000)]  // sale where you can’t afford it altogether
    [InlineData(1, 2039, 2000, 2000, -4000, -2000, -2000)]  // sale where the movement amount is the same as total balance
    // testing all scenarios at 60% target ratio
    [InlineData(1, 2040, 2000, 2000, -2000, -800, -1200)] 
    [InlineData(1, 2040, 4000, 2000, -2000, -1600, -400)] 
    [InlineData(1, 2040, 6000, 2000, -2000, -2000, 0)] 
    [InlineData(1, 2040, 8000, 2000, -2000, -2000, 0)] 
    [InlineData(1, 2040, 10000, 2000, -2000, -2000, 0)] 
    [InlineData(1, 2040, 2000, 4000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 4000, 4000, -2000, -400, -1600)] 
    [InlineData(1, 2040, 6000, 4000, -2000, -1200, -800)] 
    [InlineData(1, 2040, 8000, 4000, -2000, -2000, 0)] 
    [InlineData(1, 2040, 10000, 4000, -2000, -2000, 0)] 
    [InlineData(1, 2040, 2000, 6000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 4000, 6000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 6000, 6000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 8000, 6000, -2000, -800, -1200)] 
    [InlineData(1, 2040, 10000, 6000, -2000, -1600, -400)] 
    [InlineData(1, 2040, 2000, 8000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 4000, 8000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 6000, 8000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 8000, 8000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 10000, 8000, -2000, -400, -1600)] 
    [InlineData(1, 2040, 2000, 10000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 4000, 10000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 6000, 10000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 8000, 10000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 10000, 10000, -2000, 0, -2000)] 
    [InlineData(1, 2040, 4000, 2000, -4000, -2800, -1200)] 
    [InlineData(1, 2040, 6000, 2000, -4000, -3600, -400)] 
    [InlineData(1, 2040, 8000, 2000, -4000, -4000, 0)] 
    [InlineData(1, 2040, 10000, 2000, -4000, -4000, 0)] 
    [InlineData(1, 2040, 2000, 4000, -4000, -800, -3200)] 
    [InlineData(1, 2040, 4000, 4000, -4000, -1600, -2400)] 
    [InlineData(1, 2040, 6000, 4000, -4000, -2400, -1600)] 
    [InlineData(1, 2040, 8000, 4000, -4000, -3200, -800)] 
    [InlineData(1, 2040, 10000, 4000, -4000, -4000, 0)] 
    [InlineData(1, 2040, 2000, 6000, -4000, 0, -4000)] 
    [InlineData(1, 2040, 4000, 6000, -4000, -400, -3600)] 
    [InlineData(1, 2040, 6000, 6000, -4000, -1200, -2800)] 
    [InlineData(1, 2040, 8000, 6000, -4000, -2000, -2000)] 
    [InlineData(1, 2040, 10000, 6000, -4000, -2800, -1200)] 
    [InlineData(1, 2040, 2000, 8000, -4000, 0, -4000)] 
    [InlineData(1, 2040, 4000, 8000, -4000, 0, -4000)] 
    [InlineData(1, 2040, 6000, 8000, -4000, 0, -4000)] 
    [InlineData(1, 2040, 8000, 8000, -4000, -800, -3200)] 
    [InlineData(1, 2040, 10000, 8000, -4000, -1600, -2400)] 
    [InlineData(1, 2040, 2000, 10000, -4000, 0, -4000)] 
    [InlineData(1, 2040, 4000, 10000, -4000, 0, -4000)] 
    [InlineData(1, 2040, 6000, 10000, -4000, 0, -4000)] 
    [InlineData(1, 2040, 8000, 10000, -4000, 0, -4000)] 
    [InlineData(1, 2040, 10000, 10000, -4000, -400, -3600)] 
    [InlineData(1, 2040, 2000, 2000, -6000, -2000, -2000)] 
    [InlineData(1, 2040, 4000, 2000, -6000, -4000, -2000)] 
    [InlineData(1, 2040, 6000, 2000, -6000, -4800, -1200)] 
    [InlineData(1, 2040, 8000, 2000, -6000, -5600, -400)] 
    [InlineData(1, 2040, 10000, 2000, -6000, -6000, 0)] 
    [InlineData(1, 2040, 2000, 4000, -6000, -2000, -4000)] 
    [InlineData(1, 2040, 4000, 4000, -6000, -2800, -3200)] 
    [InlineData(1, 2040, 6000, 4000, -6000, -3600, -2400)] 
    [InlineData(1, 2040, 8000, 4000, -6000, -4400, -1600)] 
    [InlineData(1, 2040, 10000, 4000, -6000, -5200, -800)] 
    [InlineData(1, 2040, 2000, 6000, -6000, -800, -5200)] 
    [InlineData(1, 2040, 4000, 6000, -6000, -1600, -4400)] 
    [InlineData(1, 2040, 6000, 6000, -6000, -2400, -3600)] 
    [InlineData(1, 2040, 8000, 6000, -6000, -3200, -2800)] 
    [InlineData(1, 2040, 10000, 6000, -6000, -4000, -2000)] 
    [InlineData(1, 2040, 2000, 8000, -6000, 0, -6000)] 
    [InlineData(1, 2040, 4000, 8000, -6000, -400, -5600)] 
    [InlineData(1, 2040, 6000, 8000, -6000, -1200, -4800)] 
    [InlineData(1, 2040, 8000, 8000, -6000, -2000, -4000)] 
    [InlineData(1, 2040, 10000, 8000, -6000, -2800, -3200)] 
    [InlineData(1, 2040, 2000, 10000, -6000, 0, -6000)] 
    [InlineData(1, 2040, 4000, 10000, -6000, 0, -6000)] 
    [InlineData(1, 2040, 6000, 10000, -6000, 0, -6000)] 
    [InlineData(1, 2040, 8000, 10000, -6000, -800, -5200)] 
    [InlineData(1, 2040, 10000, 10000, -6000, -1600, -4400)] 
    [InlineData(1, 2040, 2000, 2000, -8000, -2000, -2000)] 
    [InlineData(1, 2040, 4000, 2000, -8000, -4000, -2000)] 
    [InlineData(1, 2040, 6000, 2000, -8000, -6000, -2000)] 
    [InlineData(1, 2040, 8000, 2000, -8000, -6800, -1200)] 
    [InlineData(1, 2040, 10000, 2000, -8000, -7600, -400)] 
    [InlineData(1, 2040, 2000, 4000, -8000, -2000, -4000)] 
    [InlineData(1, 2040, 4000, 4000, -8000, -4000, -4000)] 
    [InlineData(1, 2040, 6000, 4000, -8000, -4800, -3200)] 
    [InlineData(1, 2040, 8000, 4000, -8000, -5600, -2400)] 
    [InlineData(1, 2040, 10000, 4000, -8000, -6400, -1600)] 
    [InlineData(1, 2040, 2000, 6000, -8000, -2000, -6000)] 
    [InlineData(1, 2040, 4000, 6000, -8000, -2800, -5200)] 
    [InlineData(1, 2040, 6000, 6000, -8000, -3600, -4400)] 
    [InlineData(1, 2040, 8000, 6000, -8000, -4400, -3600)] 
    [InlineData(1, 2040, 10000, 6000, -8000, -5200, -2800)] 
    [InlineData(1, 2040, 2000, 8000, -8000, -800, -7200)] 
    [InlineData(1, 2040, 4000, 8000, -8000, -1600, -6400)] 
    [InlineData(1, 2040, 6000, 8000, -8000, -2400, -5600)] 
    [InlineData(1, 2040, 8000, 8000, -8000, -3200, -4800)] 
    [InlineData(1, 2040, 10000, 8000, -8000, -4000, -4000)] 
    [InlineData(1, 2040, 2000, 10000, -8000, 0, -8000)] 
    [InlineData(1, 2040, 4000, 10000, -8000, -400, -7600)] 
    [InlineData(1, 2040, 6000, 10000, -8000, -1200, -6800)] 
    [InlineData(1, 2040, 8000, 10000, -8000, -2000, -6000)] 
    [InlineData(1, 2040, 10000, 10000, -8000, -2800, -5200)] 
    [InlineData(1, 2040, 2000, 2000, -10000, -2000, -2000)] 
    [InlineData(1, 2040, 4000, 2000, -10000, -4000, -2000)] 
    [InlineData(1, 2040, 6000, 2000, -10000, -6000, -2000)] 
    [InlineData(1, 2040, 8000, 2000, -10000, -8000, -2000)] 
    [InlineData(1, 2040, 10000, 2000, -10000, -8800, -1200)] 
    [InlineData(1, 2040, 2000, 4000, -10000, -2000, -4000)] 
    [InlineData(1, 2040, 4000, 4000, -10000, -4000, -4000)] 
    [InlineData(1, 2040, 6000, 4000, -10000, -6000, -4000)] 
    [InlineData(1, 2040, 8000, 4000, -10000, -6800, -3200)] 
    [InlineData(1, 2040, 10000, 4000, -10000, -7600, -2400)] 
    [InlineData(1, 2040, 2000, 6000, -10000, -2000, -6000)] 
    [InlineData(1, 2040, 4000, 6000, -10000, -4000, -6000)] 
    [InlineData(1, 2040, 6000, 6000, -10000, -4800, -5200)] 
    [InlineData(1, 2040, 8000, 6000, -10000, -5600, -4400)] 
    [InlineData(1, 2040, 10000, 6000, -10000, -6400, -3600)] 
    [InlineData(1, 2040, 2000, 8000, -10000, -2000, -8000)] 
    [InlineData(1, 2040, 4000, 8000, -10000, -2800, -7200)] 
    [InlineData(1, 2040, 6000, 8000, -10000, -3600, -6400)] 
    [InlineData(1, 2040, 8000, 8000, -10000, -4400, -5600)] 
    [InlineData(1, 2040, 10000, 8000, -10000, -5200, -4800)] 
    [InlineData(1, 2040, 2000, 10000, -10000, -800, -9200)] 
    [InlineData(1, 2040, 4000, 10000, -10000, -1600, -8400)] 
    [InlineData(1, 2040, 6000, 10000, -10000, -2400, -7600)] 
    [InlineData(1, 2040, 8000, 10000, -10000, -3200, -6800)] 
    [InlineData(1, 2040, 10000, 10000, -10000, -4000, -6000)] 
    // testing all scenarios at 100% target ratio
    [InlineData(1, 2039, 2000, 2000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 4000, 2000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 6000, 2000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 8000, 2000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 10000, 2000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 2000, 4000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 4000, 4000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 6000, 4000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 8000, 4000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 10000, 4000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 2000, 6000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 4000, 6000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 6000, 6000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 8000, 6000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 10000, 6000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 2000, 8000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 4000, 8000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 6000, 8000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 8000, 8000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 10000, 8000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 2000, 10000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 4000, 10000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 6000, 10000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 8000, 10000, -2000, 0, -2000)] 
    [InlineData(1, 2039, 10000, 10000, -2000, 0, -2000)]
    [InlineData(1, 2039, 4000, 2000, -4000, -2000, -2000)] 
    [InlineData(1, 2039, 6000, 2000, -4000, -2000, -2000)] 
    [InlineData(1, 2039, 8000, 2000, -4000, -2000, -2000)] 
    [InlineData(1, 2039, 10000, 2000, -4000, -2000, -2000)] 
    [InlineData(1, 2039, 2000, 4000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 4000, 4000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 6000, 4000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 8000, 4000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 10000, 4000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 2000, 6000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 4000, 6000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 6000, 6000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 8000, 6000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 10000, 6000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 2000, 8000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 4000, 8000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 6000, 8000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 8000, 8000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 10000, 8000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 2000, 10000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 4000, 10000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 6000, 10000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 8000, 10000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 10000, 10000, -4000, 0, -4000)] 
    [InlineData(1, 2039, 2000, 2000, -6000, -2000, -2000)] 
    [InlineData(1, 2039, 4000, 2000, -6000, -4000, -2000)] 
    [InlineData(1, 2039, 6000, 2000, -6000, -4000, -2000)] 
    [InlineData(1, 2039, 8000, 2000, -6000, -4000, -2000)] 
    [InlineData(1, 2039, 10000, 2000, -6000, -4000, -2000)] 
    [InlineData(1, 2039, 2000, 4000, -6000, -2000, -4000)] 
    [InlineData(1, 2039, 4000, 4000, -6000, -2000, -4000)] 
    [InlineData(1, 2039, 6000, 4000, -6000, -2000, -4000)] 
    [InlineData(1, 2039, 8000, 4000, -6000, -2000, -4000)] 
    [InlineData(1, 2039, 10000, 4000, -6000, -2000, -4000)] 
    [InlineData(1, 2039, 2000, 6000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 4000, 6000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 6000, 6000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 8000, 6000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 10000, 6000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 2000, 8000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 4000, 8000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 6000, 8000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 8000, 8000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 10000, 8000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 2000, 10000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 4000, 10000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 6000, 10000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 8000, 10000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 10000, 10000, -6000, 0, -6000)] 
    [InlineData(1, 2039, 2000, 2000, -8000, -2000, -2000)] 
    [InlineData(1, 2039, 4000, 2000, -8000, -4000, -2000)] 
    [InlineData(1, 2039, 6000, 2000, -8000, -6000, -2000)] 
    [InlineData(1, 2039, 8000, 2000, -8000, -6000, -2000)] 
    [InlineData(1, 2039, 10000, 2000, -8000, -6000, -2000)] 
    [InlineData(1, 2039, 2000, 4000, -8000, -2000, -4000)] 
    [InlineData(1, 2039, 4000, 4000, -8000, -4000, -4000)] 
    [InlineData(1, 2039, 6000, 4000, -8000, -4000, -4000)] 
    [InlineData(1, 2039, 8000, 4000, -8000, -4000, -4000)] 
    [InlineData(1, 2039, 10000, 4000, -8000, -4000, -4000)] 
    [InlineData(1, 2039, 2000, 6000, -8000, -2000, -6000)] 
    [InlineData(1, 2039, 4000, 6000, -8000, -2000, -6000)] 
    [InlineData(1, 2039, 6000, 6000, -8000, -2000, -6000)] 
    [InlineData(1, 2039, 8000, 6000, -8000, -2000, -6000)] 
    [InlineData(1, 2039, 10000, 6000, -8000, -2000, -6000)] 
    [InlineData(1, 2039, 2000, 8000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 4000, 8000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 6000, 8000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 8000, 8000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 10000, 8000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 2000, 10000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 4000, 10000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 6000, 10000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 8000, 10000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 10000, 10000, -8000, 0, -8000)] 
    [InlineData(1, 2039, 2000, 2000, -10000, -2000, -2000)] 
    [InlineData(1, 2039, 4000, 2000, -10000, -4000, -2000)] 
    [InlineData(1, 2039, 6000, 2000, -10000, -6000, -2000)] 
    [InlineData(1, 2039, 8000, 2000, -10000, -8000, -2000)] 
    [InlineData(1, 2039, 10000, 2000, -10000, -8000, -2000)] 
    [InlineData(1, 2039, 2000, 4000, -10000, -2000, -4000)] 
    [InlineData(1, 2039, 4000, 4000, -10000, -4000, -4000)] 
    [InlineData(1, 2039, 6000, 4000, -10000, -6000, -4000)] 
    [InlineData(1, 2039, 8000, 4000, -10000, -6000, -4000)] 
    [InlineData(1, 2039, 10000, 4000, -10000, -6000, -4000)] 
    [InlineData(1, 2039, 2000, 6000, -10000, -2000, -6000)] 
    [InlineData(1, 2039, 4000, 6000, -10000, -4000, -6000)] 
    [InlineData(1, 2039, 6000, 6000, -10000, -4000, -6000)] 
    [InlineData(1, 2039, 8000, 6000, -10000, -4000, -6000)] 
    [InlineData(1, 2039, 10000, 6000, -10000, -4000, -6000)] 
    [InlineData(1, 2039, 2000, 8000, -10000, -2000, -8000)] 
    [InlineData(1, 2039, 4000, 8000, -10000, -2000, -8000)] 
    [InlineData(1, 2039, 6000, 8000, -10000, -2000, -8000)] 
    [InlineData(1, 2039, 8000, 8000, -10000, -2000, -8000)] 
    [InlineData(1, 2039, 10000, 8000, -10000, -2000, -8000)] 
    [InlineData(1, 2039, 2000, 10000, -10000, 0, -10000)] 
    [InlineData(1, 2039, 4000, 10000, -10000, 0, -10000)] 
    [InlineData(1, 2039, 6000, 10000, -10000, 0, -10000)] 
    [InlineData(1, 2039, 8000, 10000, -10000, 0, -10000)] 
    [InlineData(1, 2039, 10000, 10000, -10000, 0, -10000)] 

    public void CalculateMovementAmountNeededByPositionType_UnderDifferentScenarios_CalculatesCorrectly(
        int currentMonth, int currentYear, decimal currentLong, decimal currentMid, decimal movementAmount,
        decimal expectedLong, decimal expectedMid)
    {
        // Arrange
        var currentDate = new LocalDateTime(currentYear, currentMonth, 1, 0, 0);
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts = AccountCashManagement.DepositCash(accounts, 100000m, currentDate).accounts;
        accounts.Brokerage.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            currentLong, 1, McInvestmentPositionType.LONG_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        accounts.Brokerage.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            currentMid, 1, McInvestmentPositionType.MID_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.SixtyForty);
        model.NumMonthsCashOnHand = 12;
        model.NumMonthsMidBucketOnHand = 6;
        model.DesiredMonthlySpendPreRetirement = 600m;
        model.DesiredMonthlySpendPostRetirement = 800m;
        model.RetirementDate = new LocalDateTime(2040, 1, 1, 0, 0);
        model.NumMonthsPriorToRetirementToBeginRebalance = 12;
        
        var sixtyForty = new SixtyForty();
        
        // Act
        var (actualLong, actualMid) = sixtyForty.CalculateMovementAmountNeededByPositionType(
            accounts, currentDate, movementAmount, model);
        // Assert
        Assert.Equal(Math.Round(expectedLong,1, MidpointRounding.AwayFromZero), 
            Math.Round(actualLong,1, MidpointRounding.AwayFromZero));
        Assert.Equal(Math.Round(expectedMid,1, MidpointRounding.AwayFromZero),
            Math.Round(actualMid,1, MidpointRounding.AwayFromZero));
    }
    
    [Fact]
    public void SellInvestmentsToRmdAmount_WithAllMidTermPositions_SellsWhatsNeeded()
    {
        // Arrange
        var currentDate = new LocalDateTime(2050, 1, 1, 0, 0);
        var amountNeeded = 100000m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.TraditionalIra.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            1, amountNeeded * 1.5m, McInvestmentPositionType.MID_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.SixtyForty);
        var expectedAmountSold = amountNeeded;
        // Act
        var results = model.WithdrawalStrategy.SellInvestmentsToRmdAmount(
            amountNeeded, accounts, ledger, currentDate, model);
        var actualAmountSold = results.amountSold;
        // Assert
        Assert.Equal(expectedAmountSold, actualAmountSold);
    }
    
    [Fact]
    public void SellInvestmentsToRmdAmount_WithAllLongTermPositions_SellsWhatsNeeded()
    {
        // Arrange
        var currentDate = new LocalDateTime(2050, 1, 1, 0, 0);
        var amountNeeded = 100000m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.TraditionalIra.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            1, amountNeeded * 1.5m, McInvestmentPositionType.LONG_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.SixtyForty);
        var expectedAmountSold = amountNeeded;
        // Act
        var results = model.WithdrawalStrategy.SellInvestmentsToRmdAmount(
            amountNeeded, accounts, ledger, currentDate, model);
        var actualAmountSold = results.amountSold;
        // Assert
        Assert.Equal(expectedAmountSold, actualAmountSold);
    }
    
    [Fact]
    public void SellInvestmentsToRmdAmount_WithAllRecentPositions_SellsWhatsNeeded()
    {
        // Arrange
        var currentDate = new LocalDateTime(2050, 1, 1, 0, 0);
        var amountNeeded = 100000m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.TraditionalIra.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            1, amountNeeded * 1.5m, McInvestmentPositionType.LONG_TERM, true, 1m,
            currentDate));
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.SixtyForty);
        var expectedAmountSold = amountNeeded;
        // Act
        var results = model.WithdrawalStrategy.SellInvestmentsToRmdAmount(
            amountNeeded, accounts, ledger, currentDate, model);
        var actualAmountSold = results.amountSold;
        // Assert
        Assert.Equal(expectedAmountSold, actualAmountSold);
    }
    
    [Fact]
    public void SellInvestmentsToRmdAmount_WithNoTaxDeferredPositions_ThrowsException()
    {
        // Arrange
        var currentDate = new LocalDateTime(2050, 1, 1, 0, 0);
        var amountNeeded = 100000m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.Brokerage.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            1, amountNeeded * 1.5m, McInvestmentPositionType.LONG_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.SixtyForty);
        
        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => 
            model.WithdrawalStrategy.SellInvestmentsToRmdAmount(
                amountNeeded, accounts, ledger, currentDate, model));
        Assert.Equal("RMD: Nothing left to try. Not sure how we got here", exception.Message);
    }
    
    [Fact]
    public void SellInvestmentsToRmdAmount_WithNonDeferredPositionsOutOfBalance_StillSellsWhatsRequired()
    {
        // because the security sales is gonna try to rebalance as it sells, it's not gonna sell enough  
        // Arrange
        var currentDate = new LocalDateTime(2050, 1, 1, 0, 0);
        var amountNeeded = 100000m;
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        accounts.TraditionalIra.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            1, amountNeeded * 1.1m, McInvestmentPositionType.LONG_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        accounts.Brokerage.Positions.Add(TestDataManager.CreateTestInvestmentPosition(
            1, amountNeeded * 3.1m, McInvestmentPositionType.MID_TERM, true, 1m,
            currentDate.PlusYears(-2)));
        var ledger = TestDataManager.CreateEmptyTaxLedger();
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.SixtyForty);
        var expectedAmountSold = amountNeeded;
        // Act
        var results = model.WithdrawalStrategy.SellInvestmentsToRmdAmount(
            amountNeeded, accounts, ledger, currentDate, model);
        var actualAmountSold = results.amountSold;
        // Assert
        Assert.Equal(expectedAmountSold, actualAmountSold);
    }
    
    [Fact]
    public void RebalancePortfolio_DoesntChangeNetWorth()
    {
        var person = TestDataManager.CreateTestPerson();
        person.BirthDate = new LocalDateTime(1976, 3, 7, 0, 0);
        person.RequiredMonthlySpend = 1000;
        person.RequiredMonthlySpendHealthCare = 500;
        
        var model = TestDataManager.CreateTestModel(WithdrawalStrategyType.SixtyForty);
        model.RetirementDate = person.BirthDate.PlusYears(62); // the magic age When you are retired but have no medicare
        model.RebalanceFrequency = RebalanceFrequency.MONTHLY;
        model.NumMonthsCashOnHand = 12;
        model.NumMonthsMidBucketOnHand = 6;
        model.NumMonthsPriorToRetirementToBeginRebalance = 12; 
        model.DesiredMonthlySpendPostRetirement = 800;
        model.DesiredMonthlySpendPreRetirement = 600; 
        
        var currentDate = person.BirthDate.PlusYears(63); // Within rebalance window, post retirement, pre-medicare
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        
        var cashNeededOnHand =
            Spend.CalculateCashNeedForNMonths(model, person, accounts, currentDate, model.NumMonthsCashOnHand);
        var midNeededOnHand =
            Spend.CalculateCashNeedForNMonths(model, person, accounts, currentDate, model.NumMonthsMidBucketOnHand);
        
       // add everything to long positions in a tax deferred account
        var position = TestDataManager.CreateTestInvestmentPosition(
            cashNeededOnHand + midNeededOnHand, 1.5m, McInvestmentPositionType.LONG_TERM);
        accounts.InvestmentAccounts.Add(TestDataManager.CreateTestInvestmentAccount([ position ],
            McInvestmentAccountType.TRADITIONAL_IRA));
        var recessionStats = new RecessionStats
        {
            AreWeInARecession = false,
            AreWeInExtremeAusterityMeasures = false,
            AreWeInLivinLargeMode = false
        };
        var prices = TestDataManager.CreateTestCurrentPrices(
            1m, 100m, 50m, 0m);
        var expectedNetWorth = AccountCalculation.CalculateNetWorth(accounts);
        
    
        // Act
        var result = model.WithdrawalStrategy.RebalancePortfolio(
            currentDate, accounts, recessionStats, prices, model, new TaxLedger(), person);
        
        var actualNetWorth = AccountCalculation.CalculateNetWorth(result.accounts);
    
        // Assert
        Assert.Equal(Math.Round(expectedNetWorth  ,2),  Math.Round(actualNetWorth, 2));
    }
    
    [Fact]
    public void RebalancePortfolio_WithTaxFreeImbalanceInMid_MovesPositionsToLong()
    {
        // Arrange
        var startingCash = 1000000m; // enough to not need any movement to cash
        var totalEquities = 100000m;
        var startingMid = 70000m;
        var startingLong = totalEquities - startingMid;
        var assetRows = new RebalanceTestAssetRow[]{
            new ()
            {
                AccountType = McInvestmentAccountType.CASH,
                PositionType = McInvestmentPositionType.SHORT_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingCash,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate,
                ExpectedEndingBalance = startingCash,
            },
            new ()
            {
                AccountType = McInvestmentAccountType.ROTH_IRA,
                PositionType = McInvestmentPositionType.LONG_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingLong,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate.PlusYears(-2),
                ExpectedEndingBalance = totalEquities * 0.6m,
            },
            new ()
            {
                AccountType = McInvestmentAccountType.ROTH_IRA,
                PositionType = McInvestmentPositionType.MID_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingMid,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate.PlusYears(-2),
                ExpectedEndingBalance = totalEquities * 0.4m,
            },
        };
        // Act
        var results = RebalancePrep(assetRows).assetRowsAfter;
    
        // Assert
        foreach (var assetRow in results)
        {
            var expected = assetRow.ExpectedEndingBalance;
            var actual = assetRow.ActualEndingBalance;
            Assert.Equal(expected, actual);
        }
    }
    
    [Fact]
    public void RebalancePortfolio_WithTaxFreeImbalanceInLong_DoesntMovePositionsToMid()
    {
        // Arrange
        var startingCash = 1000000m; // enough to not need any movement to cash
        var startingLong = 70000m;
        var startingMid = 30000m;
        var assetRows = new RebalanceTestAssetRow[]{
            new ()
            {
                AccountType = McInvestmentAccountType.CASH,
                PositionType = McInvestmentPositionType.SHORT_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingCash,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate,
                ExpectedEndingBalance = startingCash,
            },
            new ()
            {
                AccountType = McInvestmentAccountType.ROTH_IRA,
                PositionType = McInvestmentPositionType.LONG_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingLong,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate.PlusYears(-2),
                ExpectedEndingBalance = startingLong,
            },
            new ()
            {
                AccountType = McInvestmentAccountType.ROTH_IRA,
                PositionType = McInvestmentPositionType.MID_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingMid,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate.PlusYears(-2),
                ExpectedEndingBalance = startingMid,
            },
        };
        // Act
        var results = RebalancePrep(assetRows).assetRowsAfter;
    
        // Assert
        foreach (var assetRow in results)
        {
            var expected = assetRow.ExpectedEndingBalance;
            var actual = assetRow.ActualEndingBalance;
            Assert.Equal(expected, actual);
        }
    }
    
    [Fact]
    public void RebalancePortfolio_WithYoungPositions_NothingMoves()
    {
        // Arrange
        var startingCash = 1000000m; // enough to not need any movement to cash
        var startingEquity = 100000m;
        var startingLong = startingEquity / 2m;
        var startingMid = startingEquity - startingLong;
        var assetRows = new []{
            new RebalanceTestAssetRow()
            {
                AccountType = McInvestmentAccountType.CASH,
                PositionType = McInvestmentPositionType.SHORT_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingCash,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate,
                ExpectedEndingBalance = startingCash,
            },
            new RebalanceTestAssetRow()
            {
                AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
                PositionType = McInvestmentPositionType.LONG_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingLong,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate,
                ExpectedEndingBalance = startingLong,
            },
            new RebalanceTestAssetRow()
            {
                AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
                PositionType = McInvestmentPositionType.MID_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingMid,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate,
                ExpectedEndingBalance = startingMid,
            },
        };
        // Act
        var results = RebalancePrep(assetRows).assetRowsAfter;
    
        // Assert
        foreach (var assetRow in results)
        {
            var expected = assetRow.ExpectedEndingBalance;
            var actual = assetRow.ActualEndingBalance;
            Assert.Equal(expected, actual);
        }
    }
    
    [Fact]
    public void RebalancePortfolio_WithYoungDeferredPositions_StillMovesMidToLong()
    {
        // Arrange
        var startingCash = 1000000m; // enough to not need any movement to cash
        var startingEquity = 100000m;
        var startingLong = startingEquity / 2m;
        var startingMid = startingEquity - startingLong;
        var assetRows = new RebalanceTestAssetRow[]{
            new ()
            {
                AccountType = McInvestmentAccountType.CASH,
                PositionType = McInvestmentPositionType.SHORT_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingCash,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate,
                ExpectedEndingBalance = startingCash,
            },
            new ()
            {
                AccountType = McInvestmentAccountType.TRADITIONAL_IRA,
                PositionType = McInvestmentPositionType.LONG_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingLong,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate,
                ExpectedEndingBalance = startingEquity * 0.6m,
            },
            new ()
            {
                AccountType = McInvestmentAccountType.TRADITIONAL_IRA,
                PositionType = McInvestmentPositionType.MID_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingMid,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate,
                ExpectedEndingBalance = startingEquity * 0.4m,
            },
        };
        // Act
        var results = RebalancePrep(assetRows).assetRowsAfter;
    
        // Assert
        foreach (var assetRow in results)
        {
            var expected = assetRow.ExpectedEndingBalance;
            var actual = assetRow.ActualEndingBalance;
            Assert.Equal(expected, actual);
        }
    }
    
    [Fact]
    public void RebalancePortfolio_WithYoungTaxFreePositions_DoesntMoveLongToMid()
    {
        // Arrange
        var startingCash = 1000000m; // enough to not need any movement to cash
        var startingLong = 70000m;
        var startingMid = 30000m;
        var assetRows = new RebalanceTestAssetRow[]{
            new ()
            {
                AccountType = McInvestmentAccountType.CASH,
                PositionType = McInvestmentPositionType.SHORT_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingCash,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate,
                ExpectedEndingBalance = startingCash,
            },
            new ()
            {
                AccountType = McInvestmentAccountType.ROTH_IRA,
                PositionType = McInvestmentPositionType.LONG_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingLong,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate,
                ExpectedEndingBalance = startingLong,
            },
            new ()
            {
                AccountType = McInvestmentAccountType.ROTH_IRA,
                PositionType = McInvestmentPositionType.MID_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingMid,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate,
                ExpectedEndingBalance = startingMid,
            },
        };
        // Act
        var results = RebalancePrep(assetRows).assetRowsAfter;
    
        // Assert
        foreach (var assetRow in results)
        {
            var expected = assetRow.ExpectedEndingBalance;
            var actual = assetRow.ActualEndingBalance;
            Assert.Equal(expected, actual);
        }
    }
    
    [Fact]
    public void RebalancePortfolio_WithLimitedCash_SellsFromTaxableBeforeTraditional()
    {
        // Arrange
        var startingCash = 0m; // enough to not need any movement to cash
        var cashNeededOnHand = 27600m;
        var shortFall = 5000m;
        var expectedRemainder = 3300m;
        var startingTaxable = cashNeededOnHand - shortFall;
        var startingTrad = shortFall + expectedRemainder;
        var assetRows = new RebalanceTestAssetRow[]{
            new ()
            {
                AccountType = McInvestmentAccountType.CASH,
                PositionType = McInvestmentPositionType.SHORT_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingCash,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate,
                ExpectedEndingBalance = cashNeededOnHand,
            },
            new ()
            {
                // this entire position will be wiped out by the initial rebalance into cash
                AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
                PositionType = McInvestmentPositionType.LONG_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingTaxable,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate.PlusYears(-2),
                ExpectedEndingBalance = 0,
            },
            new ()
            {
                // this position will have the shortfall amount sold for cash, but then the remainder will be
                // rebalanced to 60/40
                AccountType = McInvestmentAccountType.TRADITIONAL_IRA,
                PositionType = McInvestmentPositionType.LONG_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingTrad,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate.PlusYears(-2),
                ExpectedEndingBalance = expectedRemainder * 0.6m,
            },
            new ()
            {
                // this position won't start with anything, but then the rebalancing from long-to-mid will add to it
                AccountType = McInvestmentAccountType.TRADITIONAL_IRA,
                PositionType = McInvestmentPositionType.MID_TERM,
                ActualEndingBalance = 0,
                StartingBalance = 0,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate.PlusYears(-2),
                ExpectedEndingBalance = expectedRemainder * 0.4m,
            },
        };
        // Act
        var results = RebalancePrep(assetRows).assetRowsAfter;
    
        // Assert
        foreach (var assetRow in results)
        {
            var expected = assetRow.ExpectedEndingBalance;
            var actual = assetRow.ActualEndingBalance;
            Assert.Equal(expected, actual);
        }
    }
    
    [Fact]
    public void RebalancePortfolio_WithLimitedCash_SellsFullCashNeededAndRebalancesToTarget()
    {
        // Arrange
        var startingCash = 0m; // enough to not need any movement to cash
        var cashNeededOnHand = 27600m;
        var shortFall = 5000m;
        var expectedRemainder = 3300m;
        var startingLong = cashNeededOnHand - shortFall;
        var startingMid = shortFall + expectedRemainder;
        var assetRows = new RebalanceTestAssetRow[]{
            new ()
            {
                AccountType = McInvestmentAccountType.CASH,
                PositionType = McInvestmentPositionType.SHORT_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingCash,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate,
                ExpectedEndingBalance = cashNeededOnHand,
            },
            new ()
            {
                // this entire position will be wiped out by the initial rebalance into cash, but then have the
                // remainder rebalanced back in at 60%
                AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
                PositionType = McInvestmentPositionType.LONG_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingLong,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate.PlusYears(-2),
                ExpectedEndingBalance = expectedRemainder * .6m,
            },
            new ()
            {
                // this position will have the shortfall amount sold for cash, but then the remainder will be
                // rebalanced to 60/40
                AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
                PositionType = McInvestmentPositionType.MID_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingMid,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate.PlusYears(-2),
                ExpectedEndingBalance = expectedRemainder * 0.4m,
            },
        };
        // Act
        var results = RebalancePrep(assetRows).assetRowsAfter;
    
        // Assert
        foreach (var assetRow in results)
        {
            var expected = assetRow.ExpectedEndingBalance;
            var actual = assetRow.ActualEndingBalance;
            Assert.Equal(expected, actual);
        }
    }
    
    [Fact]
    public void RebalancePortfolio_WithRatioNearTarget_DoesntMoveAnything()
    {
        // Arrange
        var startingCash = 1000000m; // enough to not need any movement to cash
        var totalEquities = 100000m;
        var startingLong = totalEquities * 0.596m;
        var startingMid = totalEquities - startingLong;
        var assetRows = new RebalanceTestAssetRow[]{
            new ()
            {
                AccountType = McInvestmentAccountType.CASH,
                PositionType = McInvestmentPositionType.SHORT_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingCash,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate,
                ExpectedEndingBalance = startingCash,
            },
            new ()
            {
                AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
                PositionType = McInvestmentPositionType.LONG_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingLong,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate.PlusYears(-2),
                ExpectedEndingBalance = startingLong,
            },
            new ()
            {
                // this position will have the shortfall amount sold for cash, but then the remainder will be
                // rebalanced to 60/40
                AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
                PositionType = McInvestmentPositionType.MID_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingMid,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate.PlusYears(-2),
                ExpectedEndingBalance = startingMid,
            },
        };
        // Act
        var results = RebalancePrep(assetRows).assetRowsAfter;
    
        // Assert
        foreach (var assetRow in results)
        {
            var expected = assetRow.ExpectedEndingBalance;
            var actual = assetRow.ActualEndingBalance;
            Assert.Equal(expected, actual);
        }
    }
    
    [Fact]
    public void RebalancePortfolio_WithNoCashAndSufficientEquity_MovesAdequateCash()
    {
        // Arrange
        var startingCash = 0m; // enough to not need any movement to cash
        var cashNeededOnHand = 27600m;
        var startingLong = cashNeededOnHand * 2m;
        var startingMid = 0m;
        var assetRows = new RebalanceTestAssetRow[]{
            new ()
            {
                AccountType = McInvestmentAccountType.CASH,
                PositionType = McInvestmentPositionType.SHORT_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingCash,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate,
                ExpectedEndingBalance = cashNeededOnHand,
            },
            new ()
            {
                AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
                PositionType = McInvestmentPositionType.LONG_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingLong,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate.PlusYears(-2),
                ExpectedEndingBalance = cashNeededOnHand * .6m,
            },
            new ()
            {
                AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
                PositionType = McInvestmentPositionType.MID_TERM,
                ActualEndingBalance = 0,
                StartingBalance = startingMid,
                CostModifier = 1m,
                EntryDate = _rebalanceCurrentDate.PlusYears(-2),
                ExpectedEndingBalance = cashNeededOnHand * 0.4m,
            },
        };
        // Act
        var results = RebalancePrep(assetRows).assetRowsAfter;
    
        // Assert
        foreach (var assetRow in results)
        {
            var expected = assetRow.ExpectedEndingBalance;
            var actual = assetRow.ActualEndingBalance;
            Assert.Equal(expected, actual);
        }
    }
}