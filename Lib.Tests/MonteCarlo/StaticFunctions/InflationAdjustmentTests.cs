using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

/// <summary>
/// §16 — Inflation adjustment tests for FSD-0001 (BRD-0001).
///
/// All tests in this file are marked [Skip] because none of the implementation has been written
/// yet. The new method signatures (cumulativeCpiMultiplier parameters, CumulativeCpiMultiplier
/// field on CurrentPrices, etc.) do not yet exist. Each test body is replaced with a TODO
/// assertion so the compiler accepts the file and the test runner reports correctly.
///
/// Source-file references (pre-implementation state):
///   - CurrentPrices.cs line 14: field is CurrentCpi (not yet renamed to CumulativeCpiMultiplier)
///   - Pricing.cs line 125: CopyPrices does not yet copy CurrentCpi
///   - Spend.cs line 16:  CalculateCashNeedForNMonths has no cumulativeCpiMultiplier parameter
///   - Spend.cs line 32:  CalculateFunPointsForSpend has no cumulativeCpiMultiplier parameter
///   - Spend.cs line 59:  CalculateMonthlyFunSpend has no cumulativeCpiMultiplier parameter
///   - Spend.cs line 88:  CalculateMonthlyHealthSpend has no cumulativeCpiMultiplier parameter
///   - Spend.cs line 157: CalculateMonthlyRequiredSpend has no cumulativeCpiMultiplier parameter
///   - Payday.cs line 12: ProcessSocialSecurityCheck has no cumulativeCpiMultiplier parameter
///   - Payday.cs line 115: AddPaycheckRelatedRetirementSavings has no cumulativeCpiMultiplier parameter
///   - Payday.cs line 167: DeductPostTax has no cumulativeCpiMultiplier parameter
///   - Payday.cs line 180: DeductPreTax has no cumulativeCpiMultiplier parameter
///   - Payday.cs line 211: WithholdTaxesFromPaycheck has no cumulativeCpiMultiplier parameter
///   - TaxCalculation.cs line 174: CalculateTaxLiabilityForYear has no cumulativeCpiMultiplier parameter
///   - TaxCalculation.cs line 49: CalculateIncomeRoom has no cumulativeCpiMultiplier parameter
///   - TaxConstants.cs: Irs401KElectiveDeferralLimit and IrsHsaFamilyContributionLimit do not yet exist
/// </summary>
public class InflationAdjustmentTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a PgPerson with a fixed BirthDate for age-sensitive tests.
    /// Do NOT use TestDataManager.CreateTestPerson() in this file because it uses
    /// DateTime.Now.AddYears(-30), which makes ages non-deterministic.
    /// </summary>
    private static PgPerson CreatePersonBornIn(int birthYear)
    {
        return new PgPerson
        {
            Id                                      = Guid.NewGuid(),
            Name                                    = "Inflation Test Person",
            BirthDate                               = new LocalDateTime(birthYear, 1, 1, 0, 0),
            AnnualSalary                            = 120_000m,
            AnnualBonus                             = 10_000m,
            MonthlyFullSocialSecurityBenefit        = 2_000m,
            Annual401KMatchPercent                  = 0.05m,
            IsRetired                               = false,
            IsBankrupt                              = false,
            AnnualSocialSecurityWage                = 24_000m,   // 2000/month × 12
            Annual401KContribution                  = 0m,
            Annual401KPostTax                       = 6_000m,    // $500/month Roth 401k
            Annual401KPreTax                        = 17_000m,   // $1,417/month traditional 401k
            AnnualHsaContribution                   = 4_000m,
            AnnualHsaEmployerContribution           = 1_000m,
            FederalAnnualWithholding                = 12_000m,
            StateAnnualWithholding                  = 4_000m,
            PreTaxHealthDeductions                  = 3_600m,    // $300/month pre-tax health
            PostTaxInsuranceDeductions              = 1_200m,    // $100/month post-tax insurance
            RequiredMonthlySpend                    = 3_000m,
            RequiredMonthlySpendHealthCare          = 800m,
            ThisYearsIncomePreSimStart              = 0m,
            ThisYearsFederalTaxWithholdingPreSimStart = 0m,
            ThisYearsStateTaxWithholdingPreSimStart = 0m,
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §16.1 — FSD-001 (BR-1): Required spend × cumulativeCpiMultiplier
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Not yet implemented — FSD-001",
          DisplayName = "§16.1 — CalculateMonthlyRequiredSpend multiplies non-debt spend by cumulativeCpiMultiplier")]
    public void CalculateMonthlyRequiredSpend_WithMultiplier_ScalesNonDebtSpend()
    {
        // TODO: implement FSD-001
        // Arrange
        // Base required monthly spend = $3,000 (person.RequiredMonthlySpend)
        // cumulativeCpiMultiplier = 1.05 (5% accumulated inflation)
        // Expected non-debt, non-healthcare standard spend = $3,000 × 1.05 = $3,150
        //
        // Signature expected after implementation:
        //   Spend.CalculateMonthlyRequiredSpend(model, person, currentDate, accounts, cumulativeCpiMultiplier: 1.05m)
        //
        // var person = CreatePersonBornIn(1980);
        // person.RequiredMonthlySpend = 3_000m;
        // var model  = TestDataManager.CreateTestModel();
        // model.RetirementDate = new LocalDateTime(2050, 1, 1, 0, 0);
        // var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        // var testDate = new LocalDateTime(2035, 1, 1, 0, 0); // working, not retired
        //
        // var result = Spend.CalculateMonthlyRequiredSpend(model, person, testDate, accounts, 1.05m);
        //
        // // Debt is zero (empty accounts). Standard spend should be $3,000 × 1.05 = $3,150.
        // // Healthcare is $0 because person is not retired.
        // Assert.Equal(3_150m, result.TotalSpend);
        Assert.True(false, "Not yet implemented — FSD-001");
    }

    [Fact(Skip = "Not yet implemented — FSD-001",
          DisplayName = "§16.1b — CalculateMonthlyRequiredSpend with multiplier=1.0 returns same value as baseline (no regression)")]
    public void CalculateMonthlyRequiredSpend_MultiplierOne_MatchesCurrentBehavior()
    {
        // TODO: implement FSD-001
        // A multiplier of exactly 1.0 must produce the same result as the current pre-inflation implementation.
        // This is the no-regression guard.
        //
        // var person   = CreatePersonBornIn(1980);
        // person.RequiredMonthlySpend = 3_000m;
        // var model    = TestDataManager.CreateTestModel();
        // model.RetirementDate = new LocalDateTime(2050, 1, 1, 0, 0);
        // var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        // var testDate = new LocalDateTime(2035, 1, 1, 0, 0);
        //
        // var withMultiplier = Spend.CalculateMonthlyRequiredSpend(model, person, testDate, accounts, 1.0m);
        // var baseline       = Spend.CalculateMonthlyRequiredSpend(model, person, testDate, accounts);
        //
        // Assert.Equal(baseline.TotalSpend, withMultiplier.TotalSpend);
        Assert.True(false, "Not yet implemented — FSD-001");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §16.2 — FSD-002 (BR-1): Fun spend × cumulativeCpiMultiplier
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Not yet implemented — FSD-002",
          DisplayName = "§16.2 — CalculateMonthlyFunSpend with multiplier=1.10 returns 10% more than multiplier=1.0")]
    public void CalculateMonthlyFunSpend_WithMultiplier_ScalesFunSpend()
    {
        // TODO: implement FSD-002
        // Signature expected after implementation:
        //   Spend.CalculateMonthlyFunSpend(model, person, currentDate, cumulativeCpiMultiplier)
        //
        // var person    = CreatePersonBornIn(1980);
        // var model     = TestDataManager.CreateTestModel();
        // model.RetirementDate = new LocalDateTime(2050, 1, 1, 0, 0);
        // model.DesiredMonthlySpendPreRetirement = 5_000m;
        // var testDate  = new LocalDateTime(2035, 6, 1, 0, 0); // pre-retirement
        //
        // var atBaseMultiplier = Spend.CalculateMonthlyFunSpend(model, person, testDate, 1.0m);
        // var atHighMultiplier = Spend.CalculateMonthlyFunSpend(model, person, testDate, 1.1m);
        //
        // // With multiplier=1.10, fun spend should be exactly 10% higher
        // Assert.Equal(atBaseMultiplier * 1.1m, atHighMultiplier);
        Assert.True(false, "Not yet implemented — FSD-002");
    }

    [Fact(Skip = "Not yet implemented — FSD-002",
          DisplayName = "§16.2b — CalculateMonthlyFunSpend with multiplier=1.0 returns same value as no-multiplier baseline")]
    public void CalculateMonthlyFunSpend_MultiplierOne_MatchesBaseline()
    {
        // TODO: implement FSD-002
        // var person   = CreatePersonBornIn(1980);
        // var model    = TestDataManager.CreateTestModel();
        // model.RetirementDate = new LocalDateTime(2050, 1, 1, 0, 0);
        // model.DesiredMonthlySpendPreRetirement = 5_000m;
        // var testDate = new LocalDateTime(2035, 6, 1, 0, 0);
        //
        // var withMultiplier = Spend.CalculateMonthlyFunSpend(model, person, testDate, 1.0m);
        // var baseline       = Spend.CalculateMonthlyFunSpend(model, person, testDate);
        //
        // Assert.Equal(baseline, withMultiplier);
        Assert.True(false, "Not yet implemented — FSD-002");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §16.3 — FSD-003 (BR-1): Medicare constants × cumulativeCpiMultiplier
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes the exact expected result for CalculateMonthlyHealthSpend in the Medicare band
    /// (ages 65–87) using the same formula as Spend.cs lines 131–155, with each hardcoded dollar
    /// constant scaled by cumulativeCpiMultiplier. numHospitalAdmissionsPerYear is age-derived
    /// and is NOT scaled by the multiplier.
    ///
    /// Constants sourced from Spend.cs (as of FSD-003 baseline):
    ///   partADeductiblePerAdmission         = 1676m
    ///   age65NumberOfHospitalAdmissionsPerYear = 1.5m
    ///   numberOfHospitalAdmissionsIncreaseByDecade = 1m
    ///   partBPremiumMonthly                 = 370m
    ///   partBAnnualDeductible               = 514m
    ///   partDPremiumMonthly                 = 93m
    ///   partDAverageMonthlyDrugCost         = 150m
    /// </summary>
    private static decimal MedicareBandExpected(int age, decimal cumulativeCpiMultiplier)
    {
        const decimal partADeductiblePerAdmission                  = 1676m;
        const decimal age65NumberOfHospitalAdmissionsPerYear       = 1.5m;
        const decimal numberOfHospitalAdmissionsIncreaseByDecade   = 1m;
        const decimal partBPremiumMonthly                          = 370m;
        const decimal partBAnnualDeductible                        = 514m;
        const decimal partDPremiumMonthly                          = 93m;
        const decimal partDAverageMonthlyDrugCost                  = 150m;

        var yearsOver65             = age - 65;
        var decadesOver65           = yearsOver65 / 10m;
        var numHospitalAdmissions   = age65NumberOfHospitalAdmissionsPerYear
                                      + decadesOver65 * numberOfHospitalAdmissionsIncreaseByDecade;

        var totalPartACostPerMonth  = partADeductiblePerAdmission * cumulativeCpiMultiplier
                                      * numHospitalAdmissions / 12m;
        var totalPartBCostPerMonth  = partBPremiumMonthly * cumulativeCpiMultiplier
                                      + partBAnnualDeductible * cumulativeCpiMultiplier / 12m;
        var totalPartDCostPerMonth  = partDPremiumMonthly * cumulativeCpiMultiplier
                                      + partDAverageMonthlyDrugCost * cumulativeCpiMultiplier;

        return totalPartACostPerMonth + totalPartBCostPerMonth + totalPartDCostPerMonth;
    }

    public static IEnumerable<object[]> MedicareBandTestCases()
    {
        // Each row: (birthYear, testYear, cumulativeCpiMultiplier)
        // Expected result is computed by MedicareBandExpected(age, multiplier).
        // Two ages and two multiplier values are chosen to verify both age-scaling of
        // numHospitalAdmissions and dollar-constant scaling by cumulativeCpiMultiplier.
        yield return [1965, 2030, 1.0m];  // age 65, baseline multiplier
        yield return [1965, 2030, 2.0m];  // age 65, doubled multiplier
        yield return [1960, 2030, 1.0m];  // age 70, baseline multiplier
        yield return [1960, 2030, 1.5m];  // age 70, 1.5× multiplier
    }

    /// <summary>
    /// §16.3 — CalculateMonthlyHealthSpend: pre-65 (Path A) and age-88+ (Path B) paths
    /// produce exact known outputs for given inputs.
    ///
    /// Path A (age &lt; 65, retired): result = RequiredMonthlySpendHealthCare * multiplier
    /// Path B (age >= 88):            result = RequiredMonthlySpendHealthCare * 2m * multiplier
    ///
    /// CreatePersonBornIn sets RequiredMonthlySpendHealthCare = 800m.
    /// </summary>
    [Theory(Skip = "Not yet implemented — FSD-003")]
    [InlineData(1985, 2030, 1.0,  800.0)]   // Path A: age 45, multiplier 1.0 → 800 × 1.0  = 800
    [InlineData(1985, 2030, 1.5, 1200.0)]   // Path A: age 45, multiplier 1.5 → 800 × 1.5  = 1200
    [InlineData(1940, 2030, 1.0, 1600.0)]   // Path B: age 90, multiplier 1.0 → 800 × 2 × 1.0 = 1600
    [InlineData(1940, 2030, 2.0, 3200.0)]   // Path B: age 90, multiplier 2.0 → 800 × 2 × 2.0 = 3200
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1042", Justification = "decimal parameters passed as double in InlineData; cast is safe for these exact values")]
    public void CalculateMonthlyHealthSpend_PreMedicareAndAssistedLiving_KnownInputsMatchExpected(
        int birthYear, int testYear, double multiplierDouble, double expectedDouble)
    {
        // TODO: implement FSD-003
        // Signature expected after implementation:
        //   Spend.CalculateMonthlyHealthSpend(model, person, currentDate, cumulativeCpiMultiplier)
        //
        // var multiplier = (decimal)multiplierDouble;
        // var expected   = (decimal)expectedDouble;
        //
        // var person   = CreatePersonBornIn(birthYear);  // RequiredMonthlySpendHealthCare = 800m
        // var model    = TestDataManager.CreateTestModel();
        // model.RetirementDate = new LocalDateTime(2025, 1, 1, 0, 0);  // already retired
        // var testDate = new LocalDateTime(testYear, 1, 1, 0, 0);
        //
        // var result = Spend.CalculateMonthlyHealthSpend(model, person, testDate, multiplier);
        //
        // Assert.Equal(expected, result);
        Assert.True(false, "Not yet implemented — FSD-003");
    }

    /// <summary>
    /// §16.3 — CalculateMonthlyHealthSpend: Medicare band (Path C, ages 65–87) produces exact
    /// known outputs for given inputs.
    ///
    /// The expected output is computed by MedicareBandExpected(), which encodes the same formula
    /// as Spend.cs lines 131–155 with each hardcoded dollar constant scaled by cumulativeCpiMultiplier.
    /// numHospitalAdmissionsPerYear is derived from age and is NOT scaled by the multiplier.
    ///
    /// Test data supplied by MedicareBandTestCases(): two ages (65 and 70) × two multiplier values.
    /// </summary>
    [Theory(Skip = "Not yet implemented — FSD-003")]
    [MemberData(nameof(MedicareBandTestCases))]
    public void CalculateMonthlyHealthSpend_MedicareBand_KnownInputsMatchExpected(
        int birthYear, int testYear, decimal cumulativeCpiMultiplier)
    {
        // TODO: implement FSD-003
        // Signature expected after implementation:
        //   Spend.CalculateMonthlyHealthSpend(model, person, currentDate, cumulativeCpiMultiplier)
        //
        // var person   = CreatePersonBornIn(birthYear);  // RequiredMonthlySpendHealthCare = 800m
        // var model    = TestDataManager.CreateTestModel();
        // model.RetirementDate = new LocalDateTime(2025, 1, 1, 0, 0);  // already retired
        // var testDate = new LocalDateTime(testYear, 1, 1, 0, 0);
        // var age      = testYear - birthYear;
        //
        // var result   = Spend.CalculateMonthlyHealthSpend(model, person, testDate, cumulativeCpiMultiplier);
        // var expected = MedicareBandExpected(age, cumulativeCpiMultiplier);
        //
        // Assert.Equal(expected, result);
        Assert.True(false, "Not yet implemented — FSD-003");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §16.4 — FSD-004 (BR-2): CalculateCashNeedForNMonths compounds CPI forward
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Not yet implemented — FSD-004",
          DisplayName = "§16.4 — CalculateCashNeedForNMonths with 1% monthly CPI is greater than with 0% CPI")]
    public void CalculateCashNeedForNMonths_WithCpiGrowth_ExceedsZeroCpiTotal()
    {
        // TODO: implement FSD-004
        // Source: Spend.cs line 16-29. New signature expected:
        //   Spend.CalculateCashNeedForNMonths(model, person, accounts, currentDate, nMonths,
        //       cumulativeCpiMultiplier, currentCpiGrowthRate)
        //
        // Month i uses iterationMultiplier = cumulativeCpiMultiplier × (1 + currentCpiGrowthRate)^i
        // Debt component (Spend.cs lines 163-166) remains fixed regardless of CPI (BR-4).
        //
        // var person   = CreatePersonBornIn(1980);
        // person.RequiredMonthlySpend = 3_000m;
        // var model    = TestDataManager.CreateTestModel();
        // model.RetirementDate = new LocalDateTime(2050, 1, 1, 0, 0);
        // model.DesiredMonthlySpendPreRetirement = 5_000m;
        // var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        // var testDate = new LocalDateTime(2035, 1, 1, 0, 0);
        //
        // var withCpi    = Spend.CalculateCashNeedForNMonths(model, person, accounts, testDate, 3, 1.0m, 0.01m);
        // var withoutCpi = Spend.CalculateCashNeedForNMonths(model, person, accounts, testDate, 3, 1.0m, 0.00m);
        //
        // Assert.True(withCpi > withoutCpi,
        //     $"3-month projection with 1% monthly CPI ({withCpi:C}) must exceed zero-CPI total ({withoutCpi:C})");
        Assert.True(false, "Not yet implemented — FSD-004");
    }

    [Fact(Skip = "Not yet implemented — FSD-004",
          DisplayName = "§16.4b — CalculateCashNeedForNMonths compounds CPI correctly: month 0 uses multiplier, month 1 uses multiplier×1.01, month 2 uses multiplier×1.01²")]
    public void CalculateCashNeedForNMonths_CompoundsCorrectlyPerMonth()
    {
        // TODO: implement FSD-004
        // With a known per-month spend and 1% CPI, we can compute the expected total analytically.
        // Base spend (standard only, no debt, pre-retirement): person.RequiredMonthlySpend = 1000m
        // cumulativeCpiMultiplier = 1.0, currentCpiGrowthRate = 0.01 (1%)
        //
        // Month 0 multiplier: 1.0         → spend = 1000 × 1.0         = 1000.00
        // Month 1 multiplier: 1.0 × 1.01  → spend = 1000 × 1.01        = 1010.00
        // Month 2 multiplier: 1.0 × 1.01² → spend = 1000 × 1.0201      = 1020.10
        // Total expected (no fun spend) ≈ 3030.10
        //
        // var person   = CreatePersonBornIn(1980);
        // person.RequiredMonthlySpend = 1_000m;
        // var model    = TestDataManager.CreateTestModel();
        // model.RetirementDate = new LocalDateTime(2050, 1, 1, 0, 0);
        // model.DesiredMonthlySpendPreRetirement = 0m;  // zero fun spend so math is clean
        // var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        // var testDate = new LocalDateTime(2035, 1, 1, 0, 0);
        //
        // var result = Spend.CalculateCashNeedForNMonths(model, person, accounts, testDate, 3, 1.0m, 0.01m);
        //
        // // Expected = 1000 + 1010 + 1020.10 = 3030.10
        // Assert.Equal(3030.10m, result);
        Assert.True(false, "Not yet implemented — FSD-004");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §16.5 — FSD-005 (BR-3): Fun points ÷ cumulativeCpiMultiplier
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Not yet implemented — FSD-005",
          DisplayName = "§16.5 — CalculateFunPointsForSpend with multiplier=2.0 earns half the fun points vs multiplier=1.0")]
    public void CalculateFunPointsForSpend_DoubleMultiplier_EarnsHalfPoints()
    {
        // TODO: implement FSD-005
        // Source: Spend.cs line 32. New signature expected:
        //   Spend.CalculateFunPointsForSpend(funSpend, person, currentDate, cumulativeCpiMultiplier)
        //
        // Same dollar amount spent, but with 2× the accumulated inflation, should yield half the fun points.
        // The age component is held constant (person is 50 → 1:1 fun points per dollar at base).
        //
        // var person     = CreatePersonBornIn(1976);
        // var testDate   = new LocalDateTime(2026, 1, 1, 0, 0); // age 50 → no age penalty
        // decimal spend  = 10_000m;
        //
        // var atOne = Spend.CalculateFunPointsForSpend(spend, person, testDate, 1.0m);
        // var atTwo = Spend.CalculateFunPointsForSpend(spend, person, testDate, 2.0m);
        //
        // // With multiplier=2.0: funPoints = (age-based points) / 2.0
        // Assert.Equal(atOne / 2.0m, atTwo);
        Assert.True(false, "Not yet implemented — FSD-005");
    }

    [Fact(Skip = "Not yet implemented — FSD-005",
          DisplayName = "§16.5b — CalculateFunPointsForSpend with multiplier=1.0 matches baseline (no regression)")]
    public void CalculateFunPointsForSpend_MultiplierOne_MatchesBaseline()
    {
        // TODO: implement FSD-005
        // var person   = CreatePersonBornIn(1976);
        // var testDate = new LocalDateTime(2026, 1, 1, 0, 0);
        // decimal spend = 10_000m;
        //
        // var withMultiplier = Spend.CalculateFunPointsForSpend(spend, person, testDate, 1.0m);
        // var baseline       = Spend.CalculateFunPointsForSpend(spend, person, testDate);
        //
        // Assert.Equal(baseline, withMultiplier);
        Assert.True(false, "Not yet implemented — FSD-005");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §16.6 — FSD-007/008 (BR-5): Standard deductions scale with multiplier
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Not yet implemented — FSD-007 / FSD-008",
          DisplayName = "§16.6 — CalculateTaxLiabilityForYear with multiplier=2.0 applies double the federal and NC standard deductions")]
    public void CalculateTaxLiabilityForYear_DoubleMultiplier_DoublesStandardDeductions()
    {
        // TODO: implement FSD-007 / FSD-008
        // Source: TaxCalculation.cs line 174. New signature expected:
        //   TaxCalculation.CalculateTaxLiabilityForYear(ledger, taxYear, cumulativeCpiMultiplier)
        //
        // With a fixed W2 income that sits above the deduction amount, doubling the standard deduction
        // should measurably reduce the tax liability. The test verifies direction, not an exact value.
        //
        // var ledger = new TaxLedger();
        // var income = 60_000m;
        // // Record W2 income for tax year 2035
        // ledger.W2Income.Add((new LocalDateTime(2035, 6, 1, 0, 0), income));
        //
        // var taxAtOne = TaxCalculation.CalculateTaxLiabilityForYear(ledger, 2035, 1.0m).amount;
        // var taxAtTwo = TaxCalculation.CalculateTaxLiabilityForYear(ledger, 2035, 2.0m).amount;
        //
        // // More deduction → less taxable income → lower or equal tax liability
        // Assert.True(taxAtTwo < taxAtOne,
        //     $"Tax with 2× deduction ({taxAtTwo:C}) should be less than tax with 1× deduction ({taxAtOne:C})");
        Assert.True(false, "Not yet implemented — FSD-007 / FSD-008");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §16.7 — FSD-009 (BR-5): SS worksheet thresholds scale with multiplier
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Not yet implemented — FSD-009",
          DisplayName = "§16.7 — SS worksheet threshold scales with multiplier: combined income just above base threshold at multiplier=1.0 is below threshold at multiplier=1.5")]
    public void CalculateTaxLiabilityForYear_SsWorksheetThreshold_ScalesWithMultiplier()
    {
        // TODO: implement FSD-009
        // TaxConstants.SocialSecurityWorksheetCreditLine8 = $32,000 (Spend.cs line 84).
        // At multiplier=1.0 threshold = $32,000. Combined income = $33,000 → partially taxable SS.
        // At multiplier=1.5 threshold = $48,000. Combined income = $33,000 → below threshold → zero taxable SS.
        //
        // var ledger = new TaxLedger();
        // // Combined income = (ordinary income) + 50% SS = 0 + 0.5*(66,000) = 33,000 → just above $32k threshold
        // var ssAnnual = 66_000m;
        // ledger.SocialSecurityIncome.Add((new LocalDateTime(2035, 6, 1, 0, 0), ssAnnual));
        // ledger.SocialSecurityElectionStartDate = new LocalDateTime(2030, 1, 1, 0, 0); // already receiving SS
        // ledger.SocialSecurityWageMonthly = ssAnnual / 12m;
        //
        // var taxAtOne  = TaxCalculation.CalculateTaxLiabilityForYear(ledger, 2035, 1.0m).amount;
        // var taxAtOnePointFive = TaxCalculation.CalculateTaxLiabilityForYear(ledger, 2035, 1.5m).amount;
        //
        // // Higher threshold means less SS is taxable → lower or equal total tax
        // Assert.True(taxAtOnePointFive <= taxAtOne,
        //     "Tax with 1.5× threshold should be no greater than tax with 1.0× threshold for the same income");
        Assert.True(false, "Not yet implemented — FSD-009");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §16.8 — FSD-010 (BR-5): ScheduleD capital loss limit becomes more negative
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Not yet implemented — FSD-010",
          DisplayName = "§16.8 — ScheduleD capital loss limit with multiplier=1.5 becomes -$4,500, not -$3,000")]
    public void ScheduleD_CapitalLossLimit_ScalesNegativelyWithMultiplier()
    {
        // TODO: implement FSD-010
        // TaxConstants.ScheduleDMaximumCapitalLoss = -3000m (TaxConstants.cs line 86)
        // With multiplier=1.5: effective limit = -3000m × 1.5 = -4500m (more negative → larger deduction)
        //
        // A net capital loss of -$5,000 should be capped at:
        //   multiplier=1.0 → -$3,000 deduction
        //   multiplier=1.5 → -$4,500 deduction
        //
        // A larger deduction reduces taxable income → lower tax.
        //
        // var ledger = new TaxLedger();
        // ledger.W2Income.Add((new LocalDateTime(2035, 6, 1, 0, 0), 80_000m));
        // ledger.ShortTermCapitalGains.Add((new LocalDateTime(2035, 6, 1, 0, 0), -5_000m));
        //
        // var taxAtOne         = TaxCalculation.CalculateTaxLiabilityForYear(ledger, 2035, 1.0m).amount;
        // var taxAtOnePointFive = TaxCalculation.CalculateTaxLiabilityForYear(ledger, 2035, 1.5m).amount;
        //
        // // With a larger deductible loss cap, taxable income is lower → lower tax
        // Assert.True(taxAtOnePointFive < taxAtOne,
        //     $"Tax with 1.5× loss cap (-$4,500) ({taxAtOnePointFive:C}) should be less than with 1.0× (-$3,000) ({taxAtOne:C})");
        Assert.True(false, "Not yet implemented — FSD-010");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §16.9 — FSD-011/012 (BR-5): OASDI max and Medicare surcharge threshold scale
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Not yet implemented — FSD-011 / FSD-012",
          DisplayName = "§16.9 — WithholdTaxesFromPaycheck with multiplier=2.0 applies double the OASDI cap and Medicare surcharge threshold")]
    public void WithholdTaxesFromPaycheck_DoubleMultiplier_ScalesOasdiCapAndMedicareSurcharge()
    {
        // TODO: implement FSD-011 / FSD-012
        // Source: Payday.cs lines 220-228. New signature expected:
        //   Payday.WithholdTaxesFromPaycheck(person, currentDate, ledger, grossMonthlyPay, cumulativeCpiMultiplier)
        //
        // OASDI max: $11,439 at multiplier=1.0 → $22,878 at multiplier=2.0
        // Medicare surcharge threshold: $250,000 at multiplier=1.0 → $500,000 at multiplier=2.0
        //
        // With a gross salary of $300,000:
        //   At multiplier=1.0: OASDI is capped at $11,439; Medicare surcharge applies to $50,000.
        //   At multiplier=2.0: OASDI cap is $22,878 (no change for low-salary person);
        //                      Medicare surcharge threshold doubles to $500,000 → no surcharge on $300k.
        //
        // var person    = CreatePersonBornIn(1980);
        // person.AnnualSalary = 300_000m;
        // person.AnnualBonus  = 0m;
        // var testDate  = new LocalDateTime(2035, 1, 1, 0, 0);
        // var ledger    = new TaxLedger();
        // var grossMonthly = person.AnnualSalary / 12m;
        //
        // var atOne = Payday.WithholdTaxesFromPaycheck(person, testDate, ledger, grossMonthly, 1.0m);
        // var atTwo = Payday.WithholdTaxesFromPaycheck(person, testDate, ledger, grossMonthly, 2.0m);
        //
        // // With higher threshold at multiplier=2.0, the $300k salary is below the surcharge threshold
        // // → total withholding should be lower (no additional 0.9% Medicare)
        // Assert.True(atTwo.amount < atOne.amount,
        //     "Higher Medicare threshold with 2× multiplier should reduce total withholding for a $300k salary");
        Assert.True(false, "Not yet implemented — FSD-011 / FSD-012");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §16.10 — FSD-013/014 (BR-5): Bracket thresholds and worksheet threshold scale
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Not yet implemented — FSD-013 / FSD-014",
          DisplayName = "§16.10 — Tax bracket thresholds scale with multiplier: income at old 22% boundary stays in 12% bracket at higher multiplier")]
    public void CalculateTaxLiabilityForYear_BracketThresholds_ScaleWithMultiplier()
    {
        // TODO: implement FSD-013 / FSD-014
        // TaxConstants.Federal1040TaxTableBrackets[1].max = $94,300 (12%/22% boundary)
        // At multiplier=1.0: income of $95,000 falls in the 22% bracket (above $94,300 threshold).
        // At multiplier=1.5: threshold becomes $94,300 × 1.5 = $141,450 → income of $95,000 stays in 12%.
        // Lower bracket rate → lower tax liability.
        //
        // var ledger = new TaxLedger();
        // // $95,000 W2 income — just above the 12%/22% boundary before standard deduction
        // // After standard deduction ($29,200): taxable = $65,800 — but the threshold also scales
        // // (The boundary in taxable income terms is bracket.max, applied after deduction)
        // ledger.W2Income.Add((new LocalDateTime(2035, 6, 1, 0, 0), 95_000m));
        //
        // var taxAtOne         = TaxCalculation.CalculateTaxLiabilityForYear(ledger, 2035, 1.0m).amount;
        // var taxAtOnePointFive = TaxCalculation.CalculateTaxLiabilityForYear(ledger, 2035, 1.5m).amount;
        //
        // // With scaled thresholds, the same income falls in a lower bracket → lower tax
        // Assert.True(taxAtOnePointFive < taxAtOne,
        //     "Scaled bracket thresholds at 1.5× should yield lower tax on the same income");
        Assert.True(false, "Not yet implemented — FSD-013 / FSD-014");
    }

    [Fact(Skip = "Not yet implemented — FSD-014",
          DisplayName = "§16.10b — FederalWorksheetVsTableThreshold scales with multiplier: income just above old threshold uses table at higher multiplier")]
    public void CalculateTaxLiabilityForYear_WorksheetThreshold_ScalesWithMultiplier()
    {
        // TODO: implement FSD-014
        // TaxConstants.FederalWorksheetVsTableThreshold = $100,000 (TaxConstants.cs line 87)
        // At multiplier=1.0: income just above $100,000 routes to the computation worksheet.
        // At multiplier=2.0: threshold becomes $200,000 → same income uses the simpler table.
        // Both paths should produce mathematically consistent results (same tax on same income),
        // so this test verifies the routing does not cause an exception or obviously wrong result.
        //
        // var ledger = new TaxLedger();
        // ledger.W2Income.Add((new LocalDateTime(2035, 6, 1, 0, 0), 101_000m));
        //
        // // Both calls should complete without throwing and produce a positive tax amount
        // var taxAtOne = TaxCalculation.CalculateTaxLiabilityForYear(ledger, 2035, 1.0m).amount;
        // var taxAtTwo = TaxCalculation.CalculateTaxLiabilityForYear(ledger, 2035, 2.0m).amount;
        //
        // Assert.True(taxAtOne > 0m, "Tax at multiplier=1.0 must be positive");
        // Assert.True(taxAtTwo > 0m, "Tax at multiplier=2.0 must be positive");
        Assert.True(false, "Not yet implemented — FSD-014");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §16.11 — FSD-015 (BR-6): SS monthly payment scales with multiplier
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Not yet implemented — FSD-015",
          DisplayName = "§16.11 — ProcessSocialSecurityCheck with multiplier=1.20 deposits 20% more cash than multiplier=1.0")]
    public void ProcessSocialSecurityCheck_WithMultiplier_DepositsScaledAmount()
    {
        // TODO: implement FSD-015
        // Source: Payday.cs line 29: amount = person.AnnualSocialSecurityWage / 12m
        // New signature expected:
        //   Payday.ProcessSocialSecurityCheck(person, currentDate, bookOfAccounts, ledger, lifetimeSpend, model, cumulativeCpiMultiplier)
        //
        // person.AnnualSocialSecurityWage = 24,000m → base monthly = 2,000m
        // At multiplier=1.20: monthly payment = 2,000 × 1.20 = 2,400m
        //
        // var person   = CreatePersonBornIn(1960);
        // person.AnnualSocialSecurityWage = 24_000m;
        // person.IsRetired = true;
        // var model    = TestDataManager.CreateTestModel();
        // model.SocialSecurityStart = new LocalDateTime(2025, 1, 1, 0, 0); // already receiving SS
        // var testDate = new LocalDateTime(2026, 1, 1, 0, 0);
        // var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        // var ledger   = new TaxLedger();
        // var spend    = new LifetimeSpend();
        //
        // var cashBefore = AccountCalculation.CalculateCashBalance(accounts);
        //
        // var resultAtOne = Payday.ProcessSocialSecurityCheck(
        //     person, testDate, accounts, ledger, spend, model, 1.0m);
        // var cashAfterAtOne = AccountCalculation.CalculateCashBalance(resultAtOne.bookOfAccounts);
        //
        // var resultAtOnePointTwo = Payday.ProcessSocialSecurityCheck(
        //     person, testDate, accounts, ledger, spend, model, 1.2m);
        // var cashAfterAtOnePointTwo = AccountCalculation.CalculateCashBalance(resultAtOnePointTwo.bookOfAccounts);
        //
        // var depositAtOne        = cashAfterAtOne - cashBefore;
        // var depositAtOnePointTwo = cashAfterAtOnePointTwo - cashBefore;
        //
        // Assert.Equal(depositAtOne * 1.2m, depositAtOnePointTwo);
        Assert.True(false, "Not yet implemented — FSD-015");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §16.12 — FSD-016 (BR-8): Pre-tax health deductions scale with multiplier
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Not yet implemented — FSD-016",
          DisplayName = "§16.12 — DeductPreTax with multiplier=1.5 deducts 50% more for health deductions")]
    public void DeductPreTax_WithMultiplier_ScalesHealthDeductions()
    {
        // TODO: implement FSD-016
        // Source: Payday.cs line 188: annualPreTaxHealthDeductions = person.PreTaxHealthDeductions
        // New signature expected:
        //   Payday.DeductPreTax(person, lifetimeSpend, currentDate, cumulativeCpiMultiplier)
        //
        // person.PreTaxHealthDeductions = 3,600m → base monthly health deduction = 300m
        // At multiplier=1.5: monthly health deduction = 300 × 1.5 = 450m
        //
        // var person   = CreatePersonBornIn(1980);
        // person.PreTaxHealthDeductions = 3_600m;
        // person.AnnualHsaContribution  = 0m;      // isolate health component
        // person.Annual401KPreTax       = 0m;       // isolate health component
        // var testDate = new LocalDateTime(2035, 1, 1, 0, 0);
        // var spend    = new LifetimeSpend();
        //
        // var atOne  = Payday.DeductPreTax(person, spend, testDate, 1.0m);
        // var atOnePointFive = Payday.DeductPreTax(person, spend, testDate, 1.5m);
        //
        // // Total deduction at 1.5× should be 50% larger than at 1.0×
        // Assert.Equal(atOne.amount * 1.5m, atOnePointFive.amount);
        Assert.True(false, "Not yet implemented — FSD-016");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §16.13 — FSD-017 (BR-8/9): HSA contributions scale with multiplier
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Not yet implemented — FSD-017",
          DisplayName = "§16.13 — AddPaycheckRelatedRetirementSavings with multiplier=1.10 invests 10% more in HSA")]
    public void AddPaycheckRelatedRetirementSavings_WithMultiplier_ScalesHsaContribution()
    {
        // TODO: implement FSD-017
        // Source: Payday.cs line 134: hsaAmount = (person.AnnualHsaContribution + person.AnnualHsaEmployerContribution) / 12m
        // New signature expected:
        //   Payday.AddPaycheckRelatedRetirementSavings(person, currentDate, bookOfAccounts, model, prices, cumulativeCpiMultiplier)
        //
        // Base HSA: ($4,000 employee + $1,000 employer) / 12 = $416.67/month
        // At multiplier=1.10: $416.67 × 1.10 = $458.33/month
        //
        // var person   = CreatePersonBornIn(1980);
        // person.IsRetired = false;
        // person.IsBankrupt = false;
        // person.AnnualHsaContribution = 4_000m;
        // person.AnnualHsaEmployerContribution = 1_000m;
        // person.Annual401KPreTax  = 0m;    // isolate HSA
        // person.Annual401KPostTax = 0m;    // isolate HSA
        // person.Annual401KMatchPercent = 0m;
        // var testDate = new LocalDateTime(2035, 1, 1, 0, 0);
        // var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        // var model    = TestDataManager.CreateTestModel();
        // model.RetirementDate = new LocalDateTime(2050, 1, 1, 0, 0);
        // var prices   = TestDataManager.CreateTestCurrentPrices(0.07m, 150m, 100m, 100m);
        //
        // var hsaBeforeAtOne = AccountCalculation.CalculateInvestmentAccountTotalValue(accounts.Hsa);
        // var resultAtOne   = Payday.AddPaycheckRelatedRetirementSavings(person, testDate, accounts, model, prices, 1.0m);
        // var hsaAfterAtOne = AccountCalculation.CalculateInvestmentAccountTotalValue(resultAtOne.accounts.Hsa);
        //
        // var resultAtOnePointOne   = Payday.AddPaycheckRelatedRetirementSavings(person, testDate, accounts, model, prices, 1.1m);
        // var hsaAfterAtOnePointOne = AccountCalculation.CalculateInvestmentAccountTotalValue(resultAtOnePointOne.accounts.Hsa);
        //
        // var depositAtOne        = hsaAfterAtOne - hsaBeforeAtOne;
        // var depositAtOnePointOne = hsaAfterAtOnePointOne - hsaBeforeAtOne;
        //
        // Assert.Equal(depositAtOne * 1.1m, depositAtOnePointOne);
        Assert.True(false, "Not yet implemented — FSD-017");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §16.14 — FSD-018 (BR-8/9): 401k contributions scale with multiplier
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Not yet implemented — FSD-018",
          DisplayName = "§16.14 — AddPaycheckRelatedRetirementSavings with multiplier=1.10 invests 10% more in Roth 401k")]
    public void AddPaycheckRelatedRetirementSavings_WithMultiplier_ScalesRoth401KContribution()
    {
        // TODO: implement FSD-018
        // Source: Payday.cs lines 128-131: roth401KAmount = person.Annual401KPostTax / 12m
        // New signature expected (same as FSD-017 — both on AddPaycheckRelatedRetirementSavings):
        //   Payday.AddPaycheckRelatedRetirementSavings(person, currentDate, bookOfAccounts, model, prices, cumulativeCpiMultiplier)
        //
        // Base Roth 401k: $6,000 / 12 = $500/month
        // At multiplier=1.10: $500 × 1.10 = $550/month
        //
        // var person   = CreatePersonBornIn(1980);
        // person.IsRetired = false;
        // person.IsBankrupt = false;
        // person.Annual401KPostTax = 6_000m;
        // person.Annual401KPreTax = 0m;     // isolate Roth
        // person.AnnualHsaContribution = 0m;
        // person.AnnualHsaEmployerContribution = 0m;
        // person.Annual401KMatchPercent = 0m;
        // var testDate = new LocalDateTime(2035, 1, 1, 0, 0);
        // var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        // var model    = TestDataManager.CreateTestModel();
        // model.RetirementDate = new LocalDateTime(2050, 1, 1, 0, 0);
        // var prices   = TestDataManager.CreateTestCurrentPrices(0.07m, 150m, 100m, 100m);
        //
        // var roth401kBefore  = AccountCalculation.CalculateInvestmentAccountTotalValue(accounts.Roth401K);
        // var resultAtOne     = Payday.AddPaycheckRelatedRetirementSavings(person, testDate, accounts, model, prices, 1.0m);
        // var roth401kAtOne   = AccountCalculation.CalculateInvestmentAccountTotalValue(resultAtOne.accounts.Roth401K);
        //
        // var resultAt1pt1    = Payday.AddPaycheckRelatedRetirementSavings(person, testDate, accounts, model, prices, 1.1m);
        // var roth401kAt1pt1  = AccountCalculation.CalculateInvestmentAccountTotalValue(resultAt1pt1.accounts.Roth401K);
        //
        // var depositAtOne   = roth401kAtOne - roth401kBefore;
        // var depositAt1pt1  = roth401kAt1pt1 - roth401kBefore;
        //
        // Assert.Equal(depositAtOne * 1.1m, depositAt1pt1);
        Assert.True(false, "Not yet implemented — FSD-018");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §16.15 — FSD-019 (BR-9): IRS limit cap
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Not yet implemented — FSD-019",
          DisplayName = "§16.15a — 401k contribution below IRS limit: full scaled amount is contributed")]
    public void AddPaycheckRelatedRetirementSavings_ContributionBelowIrsLimit_FullScaledAmountContributed()
    {
        // TODO: implement FSD-019
        // TaxConstants.Irs401KElectiveDeferralLimit = $23,000 (to be added per FSD-019)
        // person.Annual401KPreTax = $17,000 → monthly = $1,416.67
        // At multiplier=1.10: scaled = $17,000 × 1.10 = $18,700 → below IRS limit × 1.10 = $25,300
        // → full $18,700 annual / $1,558.33 monthly is contributed
        //
        // var person   = CreatePersonBornIn(1980);
        // person.IsRetired = false;
        // person.IsBankrupt = false;
        // person.Annual401KPreTax  = 17_000m; // below $23,000 IRS limit
        // person.Annual401KPostTax = 0m;
        // person.AnnualHsaContribution = 0m;
        // person.AnnualHsaEmployerContribution = 0m;
        // person.Annual401KMatchPercent = 0m;
        // var testDate = new LocalDateTime(2035, 1, 1, 0, 0);
        // var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        // var model    = TestDataManager.CreateTestModel();
        // model.RetirementDate = new LocalDateTime(2050, 1, 1, 0, 0);
        // var prices   = TestDataManager.CreateTestCurrentPrices(0.07m, 150m, 100m, 100m);
        //
        // var trad401kBefore = AccountCalculation.CalculateInvestmentAccountTotalValue(accounts.Traditional401K);
        // var result    = Payday.AddPaycheckRelatedRetirementSavings(person, testDate, accounts, model, prices, 1.1m);
        // var trad401kAfter = AccountCalculation.CalculateInvestmentAccountTotalValue(result.accounts.Traditional401K);
        //
        // var expectedMonthlyContribution = (17_000m * 1.1m) / 12m;
        // Assert.Equal(expectedMonthlyContribution, trad401kAfter - trad401kBefore, 2);
        Assert.True(false, "Not yet implemented — FSD-019");
    }

    [Fact(Skip = "Not yet implemented — FSD-019",
          DisplayName = "§16.15b — 401k contribution above IRS limit: capped at IRS limit × multiplier")]
    public void AddPaycheckRelatedRetirementSavings_ContributionAboveIrsLimit_CappedAtScaledLimit()
    {
        // TODO: implement FSD-019
        // TaxConstants.Irs401KElectiveDeferralLimit = $23,000 (to be added per FSD-019)
        // person.Annual401KPreTax = $30,000 → above $23,000 IRS limit
        // At multiplier=1.0: effective contribution = Math.Min($30,000, $23,000) × 1.0 = $23,000
        // At multiplier=1.5: effective contribution = Math.Min($30,000, $23,000) × 1.5 = $34,500
        //   BUT the individual elective deferral limit also scales: $23,000 × 1.5 = $34,500
        //   → result is still capped at $34,500 (same as uncapped in this case — need base above IRS limit)
        //
        // Simpler test: person.Annual401KPreTax = $30,000; multiplier = 1.0
        // Expected: Math.Min($30,000, $23,000) × 1.0 = $23,000 annual = $1,916.67 monthly
        //
        // var person   = CreatePersonBornIn(1980);
        // person.IsRetired = false;
        // person.IsBankrupt = false;
        // person.Annual401KPreTax  = 30_000m;  // above $23,000 IRS limit
        // person.Annual401KPostTax = 0m;
        // person.AnnualHsaContribution = 0m;
        // person.AnnualHsaEmployerContribution = 0m;
        // person.Annual401KMatchPercent = 0m;
        // var testDate = new LocalDateTime(2035, 1, 1, 0, 0);
        // var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        // var model    = TestDataManager.CreateTestModel();
        // model.RetirementDate = new LocalDateTime(2050, 1, 1, 0, 0);
        // var prices   = TestDataManager.CreateTestCurrentPrices(0.07m, 150m, 100m, 100m);
        //
        // var trad401kBefore = AccountCalculation.CalculateInvestmentAccountTotalValue(accounts.Traditional401K);
        // var result    = Payday.AddPaycheckRelatedRetirementSavings(person, testDate, accounts, model, prices, 1.0m);
        // var trad401kAfter = AccountCalculation.CalculateInvestmentAccountTotalValue(result.accounts.Traditional401K);
        //
        // var expectedMonthlyContribution = (Math.Min(30_000m, TaxConstants.Irs401KElectiveDeferralLimit) * 1.0m) / 12m;
        // Assert.Equal(expectedMonthlyContribution, trad401kAfter - trad401kBefore, 2);
        Assert.True(false, "Not yet implemented — FSD-019");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §16.16 — FSD-020 (BR-8): Post-tax insurance deductions scale with multiplier
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Not yet implemented — FSD-020",
          DisplayName = "§16.16 — DeductPostTax with multiplier=1.25 deducts 25% more for insurance")]
    public void DeductPostTax_WithMultiplier_ScalesInsuranceDeductions()
    {
        // TODO: implement FSD-020
        // Source: Payday.cs line 170: annualInsuranceDeductions = person.PostTaxInsuranceDeductions
        // New signature expected:
        //   Payday.DeductPostTax(person, currentDate, cumulativeCpiMultiplier)
        //
        // person.PostTaxInsuranceDeductions = 1,200m → base monthly = 100m
        // At multiplier=1.25: monthly insurance deduction = 100 × 1.25 = 125m
        // (Note: post-tax 401k at Payday.cs line 169 is coordinated with FSD-018 — test isolates insurance)
        //
        // var person   = CreatePersonBornIn(1980);
        // person.PostTaxInsuranceDeductions = 1_200m;
        // person.Annual401KPostTax = 0m;  // isolate insurance deductions
        // var testDate = new LocalDateTime(2035, 1, 1, 0, 0);
        //
        // var atOne         = Payday.DeductPostTax(person, testDate, 1.0m);
        // var atOnePointTwo = Payday.DeductPostTax(person, testDate, 1.25m);
        //
        // Assert.Equal(atOne.amount * 1.25m, atOnePointTwo.amount);
        Assert.True(false, "Not yet implemented — FSD-020");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §16.17 — FSD-021 (BR-1/2): CumulativeCpiMultiplier compounds in CurrentPrices
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Not yet implemented — FSD-021",
          DisplayName = "§16.17 — SetLongTermGrowthRateAndPrices with CpiGrowth=0.01 on multiplier=1.05 yields 1.0605")]
    public void SetLongTermGrowthRateAndPrices_CpiGrowth_CompoundsMultiplier()
    {
        // TODO: implement FSD-021
        // Source: Pricing.cs line 102. After implementation:
        //   result.CumulativeCpiMultiplier = prices.CumulativeCpiMultiplier * (1m + rates.CpiGrowth)
        //
        // CurrentPrices.cs line 14 must be renamed from CurrentCpi to CumulativeCpiMultiplier.
        //
        // Starting multiplier: 1.05 (representing 5% accumulated inflation so far this lifetime)
        // CpiGrowth this month: 0.01 (1%)
        // Expected result: 1.05 × 1.01 = 1.0605
        //
        // var prices = new CurrentPrices
        // {
        //     CurrentEquityInvestmentPrice    = 100m,
        //     CurrentMidTermInvestmentPrice   = 100m,
        //     CurrentShortTermInvestmentPrice = 100m,
        //     CurrentTreasuryCoupon           = 0.04m,
        //     CumulativeCpiMultiplier         = 1.05m,  // RENAMED from CurrentCpi
        // };
        // var rates = new HypotheticalLifeTimeGrowthRate
        // {
        //     SpGrowth       = 0m,
        //     CpiGrowth      = 0.01m,
        //     TreasuryGrowth = 0m,
        // };
        //
        // var result = Pricing.SetLongTermGrowthRateAndPrices(prices, rates);
        //
        // Assert.Equal(1.0605m, result.CumulativeCpiMultiplier);
        Assert.True(false, "Not yet implemented — FSD-021");
    }

    [Fact(Skip = "Not yet implemented — FSD-021",
          DisplayName = "§16.17b — CopyPrices preserves CumulativeCpiMultiplier")]
    public void CopyPrices_CumulativeCpiMultiplier_IsPreservedInCopy()
    {
        // TODO: implement FSD-021
        // Source: Pricing.cs line 125. CopyPrices currently does NOT copy CurrentCpi (confirmed).
        // After implementation, CumulativeCpiMultiplier must be copied.
        //
        // const decimal OriginalMultiplier = 1.0825m;  // 8.25% cumulative inflation
        // var original = new CurrentPrices
        // {
        //     CurrentEquityInvestmentPrice    = 100m,
        //     CurrentMidTermInvestmentPrice   = 100m,
        //     CurrentShortTermInvestmentPrice = 100m,
        //     CurrentTreasuryCoupon           = 0.04m,
        //     CumulativeCpiMultiplier         = OriginalMultiplier,
        // };
        //
        // var copy = Pricing.CopyPrices(original);
        //
        // Assert.Equal(OriginalMultiplier, copy.CumulativeCpiMultiplier);
        Assert.True(false, "Not yet implemented — FSD-021");
    }

    [Fact(Skip = "Not yet implemented — FSD-021",
          DisplayName = "§16.17c — CumulativeCpiMultiplier starts at 1.0 and initializes correctly")]
    public void CurrentPrices_CumulativeCpiMultiplier_DefaultsToOne()
    {
        // TODO: implement FSD-021
        // After CurrentPrices.cs line 14 is renamed from CurrentCpi to CumulativeCpiMultiplier,
        // the default value must remain 1.0m (the correct starting value for a multiplicative accumulator).
        //
        // var prices = new CurrentPrices();
        //
        // Assert.Equal(1.0m, prices.CumulativeCpiMultiplier);
        Assert.True(false, "Not yet implemented — FSD-021");
    }

    [Fact(Skip = "Not yet implemented — FSD-021",
          DisplayName = "§16.17d — SetLongTermGrowthRateAndPrices with negative CpiGrowth decreases multiplier (deflation scenario)")]
    public void SetLongTermGrowthRateAndPrices_NegativeCpiGrowth_DecreasesMultiplier()
    {
        // TODO: implement FSD-021
        // Starting multiplier: 1.05, CpiGrowth: -0.01 (1% deflation this month)
        // Expected: 1.05 × (1 + (-0.01)) = 1.05 × 0.99 = 1.0395
        //
        // var prices = new CurrentPrices
        // {
        //     CurrentEquityInvestmentPrice    = 100m,
        //     CurrentMidTermInvestmentPrice   = 100m,
        //     CurrentShortTermInvestmentPrice = 100m,
        //     CurrentTreasuryCoupon           = 0.04m,
        //     CumulativeCpiMultiplier         = 1.05m,
        // };
        // var rates = new HypotheticalLifeTimeGrowthRate
        // {
        //     SpGrowth       = 0m,
        //     CpiGrowth      = -0.01m,
        //     TreasuryGrowth = 0m,
        // };
        //
        // var result = Pricing.SetLongTermGrowthRateAndPrices(prices, rates);
        //
        // Assert.Equal(1.0395m, result.CumulativeCpiMultiplier);
        Assert.True(false, "Not yet implemented — FSD-021");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §16.18 — FSD-006 (BR-4): Debt payment confirmed no-op — documented below
    // See BusinessOutcomes.md §16 for the confirmed-no-op note. No test written
    // because the behavior is already correct: Spend.cs lines 163-166 read fixed
    // MonthlyPayment from each open debt position, which is not multiplied by
    // cumulativeCpiMultiplier per FSD-006.
    // ─────────────────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────────────────
    // §16.19 — Additional: CalculateIncomeRoom with multiplier scales bracket and deduction
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(Skip = "Not yet implemented — FSD-013",
          DisplayName = "§16.19 — CalculateIncomeRoom with multiplier=2.0 returns double the baseline headroom (empty ledger)")]
    public void CalculateIncomeRoom_WithDoubleMultiplier_DoublesHeadroom()
    {
        // TODO: implement FSD-013
        // Source: TaxCalculation.cs line 49. New signature expected:
        //   TaxCalculation.CalculateIncomeRoom(ledger, currentDate, cumulativeCpiMultiplier)
        //
        // With empty ledger and multiplier=1.0:
        //   headRoom = ($94,300 × 1.0) + ($29,200 × 1.0) = $123,500
        // With empty ledger and multiplier=2.0:
        //   headRoom = ($94,300 × 2.0) + ($29,200 × 2.0) = $247,000
        //
        // var ledger   = new TaxLedger();
        // var testDate = new LocalDateTime(2035, 1, 1, 0, 0);
        //
        // var roomAtOne = TaxCalculation.CalculateIncomeRoom(ledger, testDate, 1.0m);
        // var roomAtTwo = TaxCalculation.CalculateIncomeRoom(ledger, testDate, 2.0m);
        //
        // Assert.Equal(roomAtOne * 2.0m, roomAtTwo);
        Assert.True(false, "Not yet implemented — FSD-013");
    }
}
