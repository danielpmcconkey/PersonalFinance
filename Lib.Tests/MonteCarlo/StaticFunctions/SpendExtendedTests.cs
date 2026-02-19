using Lib.DataTypes.Postgres;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using NodaTime;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class SpendExtendedTests
{
    // ── §10.3 — Hospital admissions increase with age ─────────────────────────

    [Fact(DisplayName = "§10.3 — Medicare Part A cost is higher at age 75 than at age 65 due to higher hospital admission rate")]
    public void CalculateMonthlyHealthSpend_MedicareYears_OlderAgeYieldsHigherCost()
    {
        // numHospitalAdmissionsPerYear = 1.5 + (decadesOver65 × 1.0)
        //   Age 65 → 1.5 stays/year;  age 75 (one decade over 65) → 2.5 stays/year.
        // That extra stay costs partADeductiblePerAdmission ($1,676) / year = ~$140/month more.
        // Both dates are inside the Medicare band (65–88), so Part B and D costs are identical;
        // only the Part A deductible component differs between the two dates.
        var person = new PgPerson
        {
            Id                                    = Guid.NewGuid(),
            Name                                  = "Test",
            BirthDate                             = new LocalDateTime(1975, 1, 1, 0, 0),
            AnnualSalary                          = 0m,
            AnnualBonus                           = 0m,
            MonthlyFullSocialSecurityBenefit      = 0m,
            Annual401KMatchPercent                = 0m,
            IsRetired                             = true,
            IsBankrupt                            = false,
            AnnualSocialSecurityWage              = 0m,
            Annual401KContribution                = 0m,
            AnnualHsaContribution                 = 0m,
            AnnualHsaEmployerContribution         = 0m,
            FederalAnnualWithholding              = 0m,
            StateAnnualWithholding                = 0m,
            PreTaxHealthDeductions                = 0m,
            PostTaxInsuranceDeductions            = 0m,
            RequiredMonthlySpend                  = 0m,
            RequiredMonthlySpendHealthCare        = 1500m,  // not used in Medicare band (65–88)
            ThisYearsIncomePreSimStart            = 0m,
            ThisYearsFederalTaxWithholdingPreSimStart = 0m,
            ThisYearsStateTaxWithholdingPreSimStart  = 0m,
        };

        var model = TestDataManager.CreateTestModel();
        model.RetirementDate = new LocalDateTime(2030, 1, 1, 0, 0);  // before both test dates

        // Person born 1975 → age 65 in 2040, age 75 in 2050; both in Medicare band
        var healthAt65 = Spend.CalculateMonthlyHealthSpend(model, person, new LocalDateTime(2040, 1, 1, 0, 0));
        var healthAt75 = Spend.CalculateMonthlyHealthSpend(model, person, new LocalDateTime(2050, 1, 1, 0, 0));

        Assert.True(healthAt75 > healthAt65,
            $"Monthly health spend at 75 ({healthAt75:C}) should exceed spend at 65 ({healthAt65:C}) " +
            "because hospital admission rate rises by 1 per decade");
    }
}
