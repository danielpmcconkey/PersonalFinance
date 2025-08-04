using Lib.DataTypes;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using NodaTime;

namespace Lib.Tests;

internal static class TestDataManager
{
    private static Dictionary<LocalDateTime, decimal>[]? _hypotheticalPrices = null;
    /// <summary>
    /// creates a book of accounts with empty positions
    /// </summary>
    internal static BookOfAccounts CreateEmptyBookOfAccounts()
    {
        List<McInvestmentAccount> investmentAccounts =
        [
            new McInvestmentAccount
            {
                Id = Guid.NewGuid(), Name = "test cash account", AccountType = McInvestmentAccountType.CASH,
                Positions = []
            },

            new McInvestmentAccount()
            {
                Id = Guid.Empty, AccountType = McInvestmentAccountType.ROTH_401_K, Name = "test Roth 401k",
                Positions = []
            },

            new McInvestmentAccount()
            {
                Id = Guid.Empty, AccountType = McInvestmentAccountType.ROTH_IRA, Name = "test Roth IRA", Positions = []
            },

            new McInvestmentAccount()
            {
                Id = Guid.Empty, AccountType = McInvestmentAccountType.TRADITIONAL_401_K,
                Name = "test traditional 401k", Positions = []
            },

            new McInvestmentAccount()
            {
                Id = Guid.Empty, AccountType = McInvestmentAccountType.TRADITIONAL_IRA, Name = "test traditional IRA",
                Positions = []
            },

            new McInvestmentAccount()
            {
                Id = Guid.Empty, AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
                Name = "test taxable brokerage", Positions = []
            },

            new McInvestmentAccount()
            {
                Id = Guid.Empty, AccountType = McInvestmentAccountType.HSA, Name = "test taxable brokerage",
                Positions = []
            }
        ];
        return Account.CreateBookOfAccounts(investmentAccounts, new List<McDebtAccount>());
    }
    
    internal static McInvestmentPosition CreateTestInvestmentPosition(
        decimal price, decimal quantity, McInvestmentPositionType positionType, bool isOpen = true)
    {
        return new McInvestmentPosition
        {
            Id = Guid.NewGuid(),
            IsOpen = isOpen,
            Name = $"Test Position {positionType}",
            Entry = new LocalDateTime(2023, 1, 1, 0, 0),
            InvestmentPositionType = positionType,
            Price = price,
            Quantity = quantity,
            InitialCost = price * quantity
        };
    }

    internal static McDebtPosition CreateTestDebtPosition(bool isOpen, decimal annualPercentageRate, decimal monthlyPayment, decimal currentBalance)
    {
        return new McDebtPosition()
        {
            Id = Guid.NewGuid(),
            IsOpen = isOpen,
            Name = "Test Position",
            Entry = new LocalDateTime(2025, 1, 1, 0, 0),
            AnnualPercentageRate = annualPercentageRate,
            MonthlyPayment = monthlyPayment,
            CurrentBalance = currentBalance,
        };
    }

    internal static McDebtAccount CreateTestDebtAccount(List<McDebtPosition> positions)
    {
        return new McDebtAccount()
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            Positions = positions
        };
    }

    internal static McInvestmentAccount CreateTestInvestmentAccount(List<McInvestmentPosition> positions, McInvestmentAccountType accountType)
    {
        return new McInvestmentAccount()
        {
            Id = Guid.NewGuid(),
            Name = "Test Account",
            AccountType = accountType,
            Positions = positions
        };
    }

    internal static CurrentPrices CreateTestCurrentPrices(
        decimal longTermGrowthRate, decimal longTermInvestmentPrice, decimal midTermInvestmentPrice,
        decimal shortTermInvestmentPrice)
    {
        return new CurrentPrices()
        {
            CurrentLongTermGrowthRate = longTermGrowthRate,
            CurrentLongTermInvestmentPrice = longTermInvestmentPrice,
            CurrentMidTermInvestmentPrice = midTermInvestmentPrice,
            CurrentShortTermInvestmentPrice = shortTermInvestmentPrice,
            LongRangeInvestmentCostHistory = []
        };
    }
    
    /// <summary>
    /// this process takes about 15 seconds to run and it always produces the same result. so only do it once
    /// </summary>
    internal static Dictionary<LocalDateTime, decimal>[] CreateOrFetchHypotheticalPricingForRuns()
    {
        // return it if it already exists
        if (_hypotheticalPrices != null) return _hypotheticalPrices;
        
        // create it
        var sAndP500HistoricalTrends = Pricing.FetchSAndP500HistoricalTrends();
        _hypotheticalPrices = Pricing.CreateHypotheticalPricingForRuns(sAndP500HistoricalTrends);
        return _hypotheticalPrices;
    }
    
    internal static PgPerson CreateTestPerson()
    {
        return new PgPerson
        {
            Id = Guid.NewGuid(),
            Name = "Test Person",
            BirthDate = LocalDateTime.FromDateTime(DateTime.Now.AddYears(-30)),
            AnnualSalary = 50000M,
            AnnualBonus = 5000M,
            MonthlyFullSocialSecurityBenefit = 2000M,
            Annual401KMatchPercent = 0.05M,
            IsRetired = true,
            IsBankrupt = false,
            AnnualSocialSecurityWage = 1500M * 12m,
            Annual401KContribution = 1,
            AnnualHsaContribution = 2,
            AnnualHsaEmployerContribution = 3,
            FederalAnnualWithholding = 4,
            StateAnnualWithholding = 5,
            PreTaxHealthDeductions = 12,
            PostTaxInsuranceDeductions = 13,
            RequiredMonthlySpend = 14,
            RequiredMonthlySpendHealthCare = 15,
        };
    }
    internal static McModel CreateTestModel()
    {
        return new McModel
        {
            RetirementDate = new LocalDateTime(2045,2,1,0,0),
            NumMonthsCashOnHand = 12,
            NumMonthsMidBucketOnHand = 24,
            NumMonthsPriorToRetirementToBeginRebalance = 60,
            RebalanceFrequency = RebalanceFrequency.MONTHLY,
            Id = Guid.Empty,
            PersonId = Guid.Empty,
            ParentAId = Guid.Empty,
            ParentBId = Guid.Empty, AusterityRatio = 0m,
            DesiredMonthlySpendPostRetirement = 0,
            DesiredMonthlySpendPreRetirement = 0,
            ExtremeAusterityNetWorthTrigger = 0,
            ExtremeAusterityRatio = 0,
            ModelCreatedDate = new LocalDateTime(2025, 1, 1, 0, 0),
            Percent401KTraditional = 0,
            RecessionCheckLookBackMonths = 0,
            RecessionRecoveryPointModifier = 0,
            SimEndDate = new LocalDateTime(2025, 1, 1, 0, 0),
            SimStartDate = new LocalDateTime(2025, 1, 1, 0, 0),
            SocialSecurityStart = new LocalDateTime(2025, 1, 1, 0, 0),
        };
    }

    internal static LifetimeSpend CreateEmptySpend()
    {
        return new LifetimeSpend();
    }
    
    internal static TaxLedger CreateEmptyTaxLedger()
    {
        return new TaxLedger();
    }
}