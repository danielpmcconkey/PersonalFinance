# Test Strategy: FSD-0001 — Track Inflation on Spend

**BRD:** BRD-0001
**FSD:** FSDs/0001-fsd.md
**Author:** Claude Code (Phase 2 Test Planning)
**Date:** 2026-02-20
**Test file:** `Lib.Tests/MonteCarlo/StaticFunctions/InflationAdjustmentTests.cs`
**BusinessOutcomes.md section:** §16

---

## What This Feature Does (Test Perspective)

The simulation currently denominates all spending in fixed "today's dollars." This feature
introduces a `CumulativeCpiMultiplier` that compounds each month from 1.0 and is multiplied
against every non-debt spending value, tax constant, Social Security payment, payroll deduction,
and retirement savings contribution. The key invariant we are testing is:

> **Any value that was a fixed dollar amount must, after this change, scale proportionally with
> the cumulative CPI multiplier at the point of use.**

Fun points are divided by the multiplier (not multiplied) to preserve the purchasing-power-to-fun-points ratio.

---

## Scope

### In scope
- All methods whose signatures gain a `cumulativeCpiMultiplier` parameter per FSD-0001
- The `CumulativeCpiMultiplier` field lifecycle on `CurrentPrices`: initialization, monthly compounding, copy propagation
- IRS contribution limit capping (FSD-019)
- Confirmed no-ops documented as notes (BR-4, BR-7)

### Out of scope
- Integration/end-to-end tests across a full simulated lifetime (out of scope for unit tests)
- Model training outcomes (the effect on `FunPoints` totals across many lifetimes)
- DB data or VAR refitting (BR-7 confirmed no-op via Dan's OD-1 answer)
- Bond/treasury downstream wiring (`CurrentTreasuryCoupon` is still dead code)
- `MidTermGrowthRateModifier` recalibration (separate work item)

---

## Test Groups and Rationale

### Group 1 — Spend functions scale with multiplier (§16.1–16.5)

**Why:** `CalculateMonthlyRequiredSpend`, `CalculateMonthlyFunSpend`, `CalculateMonthlyHealthSpend`,
`CalculateCashNeedForNMonths`, and `CalculateFunPointsForSpend` are the five spend calculation
functions. They are called on every simulation month. If any one of them fails to apply the
multiplier, the simulation silently produces wrong results with no error.

**Key tests:**
- Each function with multiplier=1.0 returns the same value as today's baseline (regression guard)
- Each function with multiplier > 1.0 scales the output proportionally
- `CalculateCashNeedForNMonths` compounds the multiplier forward per month, not flat
- `CalculateFunPointsForSpend` *divides* by the multiplier (not multiplies)
- `CalculateMonthlyHealthSpend`: two `[Theory]` tests with concrete known inputs assert exact expected outputs
  - Path A (pre-65) and Path B (age 88+): `[InlineData]` rows supply birth year, test year, and multiplier; expected value is a hardcoded decimal computed from the formula (e.g., 800 × 1.5 = 1200 for pre-65)
  - Path C (Medicare band, ages 65–87): `[MemberData]` rows supply birth year, test year, and multiplier; expected value is computed by `MedicareBandExpected()`, a private helper that independently encodes the Spend.cs formula (Part A deductible scaled by multiplier × admissions, Part B and Part D constants scaled by multiplier); verified at ages 65 and 70, multipliers 1.0, 1.5, and 2.0

**Edge cases:**
- Multiplier=1.0 → no change (no regression)
- Negative CpiGrowth → multiplier decreases (deflation compounding test in §16.17d)

---

### Group 2 — Tax constants scale with multiplier (§16.6–16.10, 16.19)

**Why:** Tax constants are `public static readonly` and cannot be mutated. The multiplier must
be applied at every call site where a flat-dollar constant is used. There are many such sites
across `Form1040`, `FormD400`, `SocialSecurityBenefitsWorksheet`, `ScheduleD`, bracket tables,
and `CalculateIncomeRoom`. A missed application would silently over-tax the simulated life.

**Key tests:**
- Standard deductions (federal $29,200 and NC $25,500) scale → lower tax liability
- SS worksheet thresholds scale → income that previously triggered taxable SS no longer does
- ScheduleD capital loss limit becomes more negative (larger deduction) at higher multiplier
- OASDI cap and Medicare surcharge threshold scale → high-income taxpayer loses surcharge at 2× multiplier
- Bracket thresholds scale → income that crossed a bracket boundary no longer does
- `FederalWorksheetVsTableThreshold` scales → income just above $100k old threshold uses table not worksheet
- `CalculateIncomeRoom` with multiplier=2.0 returns $247,000 (double the $123,500 baseline)

**Rationale for ScheduleD direction (OD-4):** Confirmed by Dan — multiplying a negative limit by a
multiplier > 1 makes it more negative, i.e., more generous in nominal terms, consistent with all
other dollar thresholds.

---

### Group 3 — Paycheck and SS values scale with multiplier (§16.11–16.16)

**Why:** `ProcessSocialSecurityCheck`, `DeductPreTax`, `DeductPostTax`, `AddPaycheckRelatedRetirementSavings`,
and `WithholdTaxesFromPaycheck` all read fixed dollar values from `PgPerson` (immutable) or
`TaxConstants`. The multiplier is threaded in as a parameter; `PgPerson` is never mutated.

**Key tests:**
- SS monthly payment = `AnnualSocialSecurityWage / 12 × multiplier` (§16.11)
- Pre-tax health deduction scales (§16.12)
- HSA contribution scales (§16.13)
- Roth 401k contribution scales (§16.14)
- Post-tax insurance deduction scales (§16.16)

**Employer match (OD-2):** Confirmed no-op — employer match is salary-percentage-based, not
a flat fee. Not inflated. No test needed.

---

### Group 4 — IRS contribution limit capping (§16.15a, §16.15b)

**Why:** The IRS limits also inflate by the same multiplier (BR-9, OD-3). The cap formula is
`Math.Min(baseContribution, irsLimit) × multiplier`. Two distinct cases must be tested:
- Contribution below the limit: full scaled amount goes through
- Contribution above the limit: result is capped at the scaled limit

---

### Group 5 — CumulativeCpiMultiplier lifecycle (§16.17a–17d)

**Why:** This is the infrastructure everything else depends on. If `SetLongTermGrowthRateAndPrices`
doesn't compound correctly, or `CopyPrices` doesn't propagate the field, all other tests would
fail for the wrong reason. These tests verify the accumulator independently.

**Key tests:**
- §16.17: `1.05 × (1 + 0.01) = 1.0605` — correct compounding arithmetic
- §16.17b: `CopyPrices` preserves `CumulativeCpiMultiplier` — guards against the noted omission bug
- §16.17c: New `CurrentPrices()` defaults to `CumulativeCpiMultiplier = 1.0m`
- §16.17d: Negative CpiGrowth (deflation) correctly decreases the multiplier

---

## Pre-Implementation Notes

All 28 test methods are marked `[Fact(Skip = "Not yet implemented — FSD-NNN")]` or
`[Theory(Skip = "Not yet implemented — FSD-NNN")]`. Each test body contains an
`Assert.True(false, "Not yet implemented — FSD-NNN")` assertion and a detailed comment block
showing the exact call signature expected after implementation.

The §16.3 test methods use `[Theory]` rather than `[Fact]`: one uses `[InlineData]` for
the pre-65 and age-88+ paths (4 rows), and one uses `[MemberData]` for the Medicare band
path (4 rows), for a total of 8 test cases exercised once §16.3 is un-skipped.

The file header (`InflationAdjustmentTests.cs` lines 1–33) documents every source-file method
that does not yet have its new signature, with the current `file:line` reference. This serves
as a checklist for the implementer.

**To un-skip a test after implementing a method:** Remove the `Skip =` portion of the
`[Fact]` or `[Theory]` attribute and replace the `Assert.True(false, ...)` assertion with
the actual call.

---

## BusinessOutcomes.md Coverage

§16 in BusinessOutcomes.md contains 30 rows:
- 28 testable outcomes at status `A` with `InflationAdjustmentTests: §16.N` citation
- 1 confirmed no-op note for BR-4 (debt payments already fixed)
- 1 confirmed no-op note for BR-7 (sp_growth already gross S&P, confirmed by Dan OD-1)
