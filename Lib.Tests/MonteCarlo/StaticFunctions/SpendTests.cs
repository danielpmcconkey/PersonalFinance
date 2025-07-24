using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class SpendTests
{
    private readonly LocalDateTime _baseDate = new(2025, 1, 1, 0, 0);
    private readonly McPerson _testPerson = new() { BirthDate = new LocalDateTime(1975, 1, 1, 0, 0) };

    private McModel CreateTestModel()
    {
        return new McModel
        {
            RetirementDate = new LocalDateTime(2030, 1, 1, 0, 0),
            DesiredMonthlySpendPreRetirement = 5000m,
            DesiredMonthlySpendPostRetirement = 5000m,
            RequiredMonthlySpend = 3000m,
            RequiredMonthlySpendHealthCare = 1500m,
            AusterityRatio = 0.8m,
            ExtremeAusterityRatio = 0.6m,
            RecessionRecoveryPointModifier = 1.05m,
            RecessionCheckLookBackMonths = 12,
            NumMonthsCashOnHand = 12,
            NumMonthsMidBucketOnHand = 24,
            NumMonthsPriorToRetirementToBeginRebalance = 60,
            RebalanceFrequency = RebalanceFrequency.QUARTERLY,
            Id = Guid.Empty,
            PersonId = Guid.Empty,
            ParentAId = Guid.Empty,
            ParentBId = Guid.Empty, 
            ExtremeAusterityNetWorthTrigger = 0,
            ModelCreatedDate = new LocalDateTime(2025, 1, 1, 0, 0),
            MonthlyInvest401kRoth = 0,
            MonthlyInvest401kTraditional = 0,
            MonthlyInvestBrokerage = 0,
            MonthlyInvestHSA = 0,
            SimEndDate = new LocalDateTime(2066, 3, 1, 0, 0),
            SimStartDate = new LocalDateTime(2025, 1, 1, 0, 0),
            SocialSecurityStart = new LocalDateTime(2025, 1, 1, 0, 0),
        };
    }

    [Fact]
    public void CalculateCashNeedForNMonths_ReturnsCorrectTotal()
    {
        /*
         * this is a weak test as it doesn't test any of the age variability. But we do that later with the specific
         * functions anyway
         */
        // Arrange
        var simParams = CreateTestModel();
        var months = 3;

        // Act
        var result = Spend.CalculateCashNeedForNMonths(simParams, _testPerson, _baseDate, months);

        // Assert
        var expectedMonthlyTotal = simParams.DesiredMonthlySpendPreRetirement + simParams.RequiredMonthlySpend;
        Assert.Equal(expectedMonthlyTotal * months, result);
    }

    [Theory]
    [InlineData(45, 1000, 1000)]
    [InlineData(50, 1000, 1000)]
    [InlineData(55, 1000, 937.5)]
    [InlineData(60, 1000, 875)]
    [InlineData(65, 1000, 812.5)]
    [InlineData(70, 1000, 750)]
    [InlineData(75, 1000, 687.5)]
    [InlineData(80, 1000, 625)]
    [InlineData(85, 1000, 562.5)]
    [InlineData(90, 1000, 500)]
    [InlineData(95, 1000, 500)] // penalty should be capped
    public void CalculateFunPointsForSpend_AgeAffectsPoints(int age, decimal spend, decimal expectedPoints)
    {
        // Arrange
        var person = new McPerson { BirthDate = new LocalDateTime(2025 - age, 1, 1, 0, 0) };

        // Act
        var result = Spend.CalculateFunPointsForSpend(spend, person, _baseDate);

        // Assert
        Assert.Equal(expectedPoints, result, 0);
    }

    [Theory]
    [InlineData(2025, 5000)] // Pre-retirement
    [InlineData(2030, 6000)] // Post-retirement, under 66
    [InlineData(2035, 6000)] // Post-retirement, under 66
    [InlineData(2040, 6000)] // Post-retirement, under 66
    [InlineData(2045, 4695.65)] // Age 70, reduced spending
    [InlineData(2050, 3391.3)] // Age 75, reduced spending
    [InlineData(2055, 2086.96)] // Age 80, reduced spending
    [InlineData(2060, 782.61)]    // Age 85, reduced spending
    [InlineData(2062, 260.87)]    // Age 87, the last year of fun spending
    [InlineData(2063, 0)]    // Age 88, the first year of 0 fun spending
    [InlineData(2065, 0)]    // Age 90, no fun spending
    public void CalculateMonthlyFunSpend_AgeAffectsSpending(int currentYear, decimal expectedSpend)
    {
        // Arrange
        var simParams = CreateTestModel();
        simParams.DesiredMonthlySpendPreRetirement = 5000m;
        simParams.DesiredMonthlySpendPostRetirement = 6000m;
        var currentDate = new LocalDateTime(currentYear, 1, 1, 0, 0);

        // Act
        var result = Spend.CalculateMonthlyFunSpend(simParams, _testPerson, currentDate);

        // Assert
        Assert.Equal(expectedSpend, result, 0);
    }

    [Theory]
    /*
     * these expectations were calculated using the "HealthcareSpend" tab of the TaxTesting.ods file
     */
    [InlineData(2025, 0)] 
    [InlineData(2030, 3000)] // retirement age, no medicare
    [InlineData(2035, 3000)] 
    [InlineData(2040, 865.33)] // age 65, medicare kicks in
    [InlineData(2045, 935.17)] 
    [InlineData(2050, 1005)] 
    [InlineData(2055, 1074.83)] 
    [InlineData(2060, 1144.67)] 
    [InlineData(2065, 6000)] 
    [InlineData(2063, 6000)] // age 88, first year of assisted living

    public void CalculateMonthlyHealthSpend_AgeAffectsHealthCosts(int currentYear, decimal expectedSpend)
    {
        // Arrange
        var simParams = CreateTestModel();
        simParams.RequiredMonthlySpendHealthCare = 3000m;
        var currentDate = new LocalDateTime(currentYear, 1, 1, 0, 0);

        // Act
        var result = Spend.CalculateMonthlyHealthSpend(simParams, _testPerson, currentDate);

        // Assert
        Assert.Equal(expectedSpend, result, 0);
    }

    [Fact]
    public void CalculateMonthlyRequiredSpend_CombinesStandardAndHealthSpend()
    {
        // this is a weak test, but the health spend individual test is quite rigorous and required spend never changes
        
        // Arrange
        var simParams = CreateTestModel();
        var currentDate = _baseDate.PlusYears(13); // Age 63, full health costs

        // Act
        var result = Spend.CalculateMonthlyRequiredSpend(simParams, _testPerson, currentDate);

        // Assert
        var expectedTotal = simParams.RequiredMonthlySpend + simParams.RequiredMonthlySpendHealthCare;
        Assert.Equal(expectedTotal, result);
    }

    [Theory]
    [InlineData(true, true, 0.6)]   // Extreme austerity
    [InlineData(true, false, 0.8)]   // Regular recession
    [InlineData(false, false, 1.0)]  // No recession
    public void CalculateRecessionSpendOverride_AppliesCorrectRatio(
        bool inRecession, bool inExtremeAusterity, decimal expectedRatio)
    {
        // Arrange
        var simParams = CreateTestModel();
        var recessionStats = new RecessionStats
        {
            AreWeInARecession = inRecession,
            AreWeInExtremeAusterityMeasures = inExtremeAusterity
        };
        var standardAmount = 1000m;

        // Act
        var result = Spend.CalculateRecessionSpendOverride(simParams, standardAmount, recessionStats);

        // Assert
        Assert.Equal(standardAmount * expectedRatio, result);
    }

    [Fact]
    public void CopyLifetimeSpend_CreatesDeepCopy()
    {
        // Arrange
        var original = new LifetimeSpend
        {
            TotalSpendLifetime = 1000m,
            TotalInvestmentAccrualLifetime = 2000m,
            TotalDebtAccrualLifetime = 300m,
            TotalSocialSecurityWageLifetime = 4000m,
            TotalDebtPaidLifetime = 500m,
            TotalFunPointsLifetime = 600m
        };

        // Act
        var copy = Spend.CopyLifetimeSpend(original);

        // Assert
#pragma warning disable xUnit2005
        // it wants us to use Assert.NotEqual, but that doesn't pass. I'm not sure the value of this assertion, I guess
        // to make sure it doesn't just pass back the same object 
        Assert.NotSame(original, copy);
#pragma warning restore xUnit2005
        Assert.Equal(original.TotalSpendLifetime, copy.TotalSpendLifetime);
        Assert.Equal(original.TotalInvestmentAccrualLifetime, copy.TotalInvestmentAccrualLifetime);
        Assert.Equal(original.TotalDebtAccrualLifetime, copy.TotalDebtAccrualLifetime);
        Assert.Equal(original.TotalSocialSecurityWageLifetime, copy.TotalSocialSecurityWageLifetime);
        Assert.Equal(original.TotalDebtPaidLifetime, copy.TotalDebtPaidLifetime);
        Assert.Equal(original.TotalFunPointsLifetime, copy.TotalFunPointsLifetime);
    }

    [Fact]
    public void RecordFunctions_UpdateCorrectValues()
    {
        // Arrange
        var lifetimeSpend = new LifetimeSpend();
        var amount = 100m;

        // Act & Assert
        var spendResult = Spend.RecordSpend(lifetimeSpend, amount, _baseDate);
        Assert.Equal(amount, spendResult.TotalSpendLifetime);

        var debtAccrualResult = Spend.RecordDebtAccrual(lifetimeSpend, amount, _baseDate);
        Assert.Equal(amount, debtAccrualResult.TotalDebtAccrualLifetime);

        var debtPaymentResult = Spend.RecordDebtPayment(lifetimeSpend, amount, _baseDate);
        Assert.Equal(amount, debtPaymentResult.TotalDebtPaidLifetime);

        var investmentResult = Spend.RecordInvestmentAccrual(lifetimeSpend, amount, _baseDate);
        Assert.Equal(amount, investmentResult.TotalInvestmentAccrualLifetime);

        var ssWageResult = Spend.RecordSocialSecurityWage(lifetimeSpend, amount, _baseDate);
        Assert.Equal(amount, ssWageResult.TotalSocialSecurityWageLifetime);

        var funPointsResult = Spend.RecordFunPoints(lifetimeSpend, amount, _baseDate);
        Assert.Equal(amount, funPointsResult.TotalFunPointsLifetime);
    }
}