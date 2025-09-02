using Lib.DataTypes.Postgres;
using Xunit;
using NodaTime;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.Tests;
using Model = Lib.DataTypes.MonteCarlo.Model;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class RebalanceTests
{
    private readonly LocalDateTime _baseDate = new(2025, 1, 1, 0, 0);
    private readonly LocalDateTime _retirementDate = new(2030, 1, 1, 0, 0);

    private Model CreateTestModel(RebalanceFrequency frequency = RebalanceFrequency.MONTHLY)
    {
        var model = TestDataManager.CreateTestModel();
        model.RetirementDate = _retirementDate;
        model.RebalanceFrequency = frequency;
        return model;
    }
    private PgPerson CreateTestPerson()
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
            IsBankrupt = true,
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
            ThisYearsIncomePreSimStart = 0,
            ThisYearsFederalTaxWithholdingPreSimStart = 0,
            ThisYearsStateTaxWithholdingPreSimStart = 0,
        };
    }

    

    [Theory]
    [InlineData(RebalanceFrequency.MONTHLY, 1, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 2, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 3, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 4, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 5, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 6, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 7, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 8, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 9, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 10, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 11, true)]
    [InlineData(RebalanceFrequency.MONTHLY, 12, true)]
    [InlineData(RebalanceFrequency.QUARTERLY, 1, true)]
    [InlineData(RebalanceFrequency.QUARTERLY, 2, false)]
    [InlineData(RebalanceFrequency.QUARTERLY, 3, false)]
    [InlineData(RebalanceFrequency.QUARTERLY, 4, true)]
    [InlineData(RebalanceFrequency.QUARTERLY, 5, false)]
    [InlineData(RebalanceFrequency.QUARTERLY, 6, false)]
    [InlineData(RebalanceFrequency.QUARTERLY, 7, true)]
    [InlineData(RebalanceFrequency.QUARTERLY, 8, false)]
    [InlineData(RebalanceFrequency.QUARTERLY, 9, false)]
    [InlineData(RebalanceFrequency.QUARTERLY, 10, true)]
    [InlineData(RebalanceFrequency.QUARTERLY, 11, false)]
    [InlineData(RebalanceFrequency.QUARTERLY, 12, false)]
    [InlineData(RebalanceFrequency.YEARLY, 1, true)]
    [InlineData(RebalanceFrequency.YEARLY, 2, false)]
    [InlineData(RebalanceFrequency.YEARLY, 3, false)]
    [InlineData(RebalanceFrequency.YEARLY, 4, false)]
    [InlineData(RebalanceFrequency.YEARLY, 5, false)]
    [InlineData(RebalanceFrequency.YEARLY, 6, false)]
    [InlineData(RebalanceFrequency.YEARLY, 7, false)]
    [InlineData(RebalanceFrequency.YEARLY, 8, false)]
    [InlineData(RebalanceFrequency.YEARLY, 9, false)]
    [InlineData(RebalanceFrequency.YEARLY, 10, false)]
    [InlineData(RebalanceFrequency.YEARLY, 11, false)]
    [InlineData(RebalanceFrequency.YEARLY, 12, false)]
    public void CalculateWhetherItsBucketRebalanceTime_ChecksFrequencyCorrectly(
        RebalanceFrequency frequency, int month, bool expectedResult)
    {
        // Arrange
        var model = CreateTestModel(frequency);
        var currentDate = new LocalDateTime(2029, month, 1, 0, 0); // Within rebalance window

        // Act
        var result = Rebalance.CalculateWhetherItsBucketRebalanceTime(currentDate, model);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    

    

    

    
}