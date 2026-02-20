# Implementation Plan: FSD-0001 — Track Inflation on Spend

**BRD:** BRD-0001
**FSD:** FSDs/0001-fsd.md
**Author:** Claude Code (Phase 3 Implementation Planning)
**Date:** 2026-02-20

---

## Overview

This plan covers 14 source files and 1 test file. The changes thread a single `decimal cumulativeCpiMultiplier` value — stored on `CurrentPrices` — from the point where it is compounded each month in `Pricing.SetLongTermGrowthRateAndPrices` through every function that currently reads a fixed base value from `PgPerson` or `TaxConstants`. A second new field `CurrentCpiGrowthRate` is also added to `CurrentPrices` to avoid extra parameter proliferation at projection call sites.

Files changed: `CurrentPrices.cs`, `TaxConstants.cs`, `Pricing.cs`, `TaxCalculation.cs`, `SocialSecurityBenefitsWorksheet.cs`, `ScheduleD.cs`, `Form1040.cs`, `FormD400.cs`, `QualifiedDividendsAndCapitalGainTaxWorksheet.cs`, `TaxTable.cs`, `TaxComputationWorksheet.cs`, `Spend.cs`, `Payday.cs`, `Simulation.cs`. Test file `InflationAdjustmentTests.cs` has all 29 tests currently marked `[Skip]`; they move to `[Fact]` as implementation proceeds.

---

## Dependency Order

The changes must proceed in this order because each layer consumes the layer below it:

1. **Data types first**: `CurrentPrices` holds the accumulated state; `TaxConstants` holds new IRS limit constants. Nothing else can be changed until these compile.
2. **The multiplier source next**: `Pricing.SetLongTermGrowthRateAndPrices` and `Pricing.CopyPrices` compound and propagate the multiplier. Every downstream function receives it from `CurrentPrices`.
3. **Tax helper next**: The new `TaxCalculation.ScaleBracketThresholds` helper must exist before any tax form calls it. `TaxCalculation.CalculateTaxLiabilityForYear` and `CalculateIncomeRoom` are modified here too.
4. **Tax forms in dependency order**: `ScheduleD` and `SocialSecurityBenefitsWorksheet` are leaf forms (they call no other form). `Form1040` calls both, so it comes after them. `QualifiedDividendsAndCapitalGainTaxWorksheet`, `TaxTable`, and `TaxComputationWorksheet` are called from inside `Form1040.CalculateTax()`, so they are changed alongside it. `FormD400` is independent of the other forms and can be done in the same pass.
5. **Spend.cs**: Leaf-level spend calculations. Modified after data types are stable, before callers.
6. **Payday.cs**: Calls `Spend` sub-functions and tax functions; must come after those are updated.
7. **Simulation.cs**: The mid-tier orchestrator that calls `Spend` and `Payday` functions directly. Must come after both.
8. **Withdrawal strategy call sites**: The four files that call `CalculateCashNeedForNMonths` must be updated after `Spend.cs` changes its signature.
9. **`LifeSimulator.cs`**: Calls `Simulation` methods and extracts `CurrentPrices.CumulativeCpiMultiplier` to pass downstream. Final layer; everything it calls is already updated.
10. **Tests**: Uncomment and activate the `[Skip]` tests in `InflationAdjustmentTests.cs` after each step's code compiles.

---

## Step-by-Step Changes

---

### Step 1: `CurrentPrices.cs`

- **File:** `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/DataTypes/MonteCarlo/CurrentPrices.cs`

- **What changes:**
  - At line 14, rename the field `CurrentCpi` to `CumulativeCpiMultiplier`. The default value remains `1.00m` — this is correct as a multiplicative accumulator starting at 1 (meaning "no inflation yet applied"). Remove the comment "set it to $1 to start" and replace it with "Cumulative product of (1 + CpiGrowth) for each month elapsed. Starts at 1.0 (base year). Never reset mid-simulation."
  - Add a new field immediately after: `public decimal CurrentCpiGrowthRate { get; set; } = 0m;` with comment "The raw CpiGrowth value from the most recent HypotheticalLifeTimeGrowthRate. Used by projection functions to estimate future inflation without an extra parameter."

- **Why this step:** Every downstream step reads `CumulativeCpiMultiplier` from this struct. If the field name does not exist, nothing else compiles.

- **Tests unlocked:** §16.17c (`CurrentPrices_CumulativeCpiMultiplier_DefaultsToOne`) — confirms the default value is 1.0m.

- **Risk:** Any existing code that references `CurrentCpi` by name will fail to compile. A global search confirms `CurrentCpi` appears only in this file (line 14) and nowhere else in the solution — it was initialized but never read. Verify this assumption with a grep before implementing. If any reference is found, it is updated at the same time.

---

### Step 2: `TaxConstants.cs`

- **File:** `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/StaticConfig/TaxConstants.cs`

- **What changes:**
  - In the `#region Paycheck deductions` block (currently ends at line 99), add two new constants after `AdditionalMedicareThreshold`:
    - `public const decimal Irs401KElectiveDeferralLimit = 23000m;` — combined pre+post-tax annual elective deferral limit (base year, inflated at runtime)
    - `public const decimal IrsHsaFamilyContributionLimit = 8300m;` — annual HSA family contribution limit (base year, inflated at runtime)
  - No existing constants are changed. `TaxConstants` fields remain compile-time base values throughout.

- **Why this step:** `Payday.AddPaycheckRelatedRetirementSavings` (Step 10) needs these constants to enforce the inflated IRS cap check. Declaring them early means all later steps can reference them by name without magic numbers.

- **Tests unlocked:** §16.15a and §16.15b (`AddPaycheckRelatedRetirementSavings` IRS cap tests) reference `TaxConstants.Irs401KElectiveDeferralLimit` directly; the tests will not compile until this constant exists.

- **Risk:** No breaking changes. These are purely additive constants.

---

### Step 3: `Pricing.cs`

- **File:** `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/StaticFunctions/Pricing.cs`

- **What changes:**
  - In `SetLongTermGrowthRateAndPrices` (line 102), after the line that updates `CurrentTreasuryCoupon` (line 113), add two new lines:
    - `result.CumulativeCpiMultiplier = prices.CumulativeCpiMultiplier * (1m + rates.CpiGrowth);`
    - `result.CurrentCpiGrowthRate = rates.CpiGrowth;`
    The formula matches the BRD Q2 compounding answer exactly: `multiplier_new = multiplier_old * (1 + CpiGrowth)`.
  - In `CopyPrices` (line 125–136), add two fields to the returned object initializer:
    - `CumulativeCpiMultiplier = originalPrices.CumulativeCpiMultiplier,`
    - `CurrentCpiGrowthRate = originalPrices.CurrentCpiGrowthRate,`
    Without this, every call to `CopyPrices` would reset both fields to their defaults (1.0 and 0.0), silently breaking the entire feature.

- **Why this step:** `Pricing.SetLongTermGrowthRateAndPrices` is the single location where all `CurrentPrices` state is updated each month. Centralizing the CPI compound here is consistent with existing architecture.

- **Tests unlocked:** §16.17 (`SetLongTermGrowthRateAndPrices_CpiGrowth_CompoundsMultiplier`), §16.17b (`CopyPrices_CumulativeCpiMultiplier_IsPreservedInCopy`), §16.17d (deflation scenario).

- **Risk:** `CopyPrices` is called many times per simulation month. The two new fields add negligible cost. The only failure mode is forgetting one of them, which the §16.17b test will immediately catch.

---

### Step 4: `TaxCalculation.cs` — add helper and update entry points

- **File:** `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/StaticFunctions/TaxCalculation.cs`

- **What changes:**
  - **Add new public static helper `ScaleBracketThresholds`:**
    - Signature: takes a `(decimal rate, decimal min, decimal max)[]` array and a `decimal cumulativeCpiMultiplier`.
    - Returns a new array of the same tuple type.
    - For each element: `rate` is unchanged; `min` and `max` are multiplied by `cumulativeCpiMultiplier`, EXCEPT when `max` is `decimal.MaxValue` — the sentinel value for the top bracket must remain `decimal.MaxValue` unchanged, otherwise the bracket loop breaks.
    - This helper is consumed by `TaxTable.CalculatePreciseLiability`, `TaxComputationWorksheet.CalculateTaxOwed`, `QualifiedDividendsAndCapitalGainTaxWorksheet.CalculateTaxOwed`, and `CalculateIncomeRoom`.

  - **Update `CalculateTaxLiabilityForYear` (line 174):**
    - Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter (default value keeps build green).
    - Pass `cumulativeCpiMultiplier` to `Form1040` constructor and `FormD400` constructor.

  - **Update `CalculateIncomeRoom` (line 49):**
    - Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter.
    - At line 52, replace `TaxConstants.Federal1040TaxTableBrackets[1].max` with `TaxConstants.Federal1040TaxTableBrackets[1].max * cumulativeCpiMultiplier`.
    - At line 55, replace `TaxConstants.FederalStandardDeduction` with `TaxConstants.FederalStandardDeduction * cumulativeCpiMultiplier`.
    - At lines 65 and 71 where `ledger.SocialSecurityWageMonthly` is used to project annual SS income, the value stored in the ledger is already the inflation-adjusted monthly wage (because `Payday.ProcessSocialSecurityCheck` now deposits `person.AnnualSocialSecurityWage / 12m * cumulativeCpiMultiplier` — see Step 10). Therefore `SocialSecurityWageMonthly` in the ledger does not need further adjustment here; it is already the correct nominal value. No additional multiplication is needed at these lines.

- **Why this step:** `Form1040`, `FormD400`, and the worksheets all need the multiplier. They get it from `CalculateTaxLiabilityForYear`, which is the single entry point into all tax forms. `CalculateIncomeRoom` is the income-room gatekeeper for withdrawal strategies; its brackets and deduction must scale for the strategy to remain correctly calibrated.

- **Tests unlocked:** §16.19 (`CalculateIncomeRoom_WithDoubleMultiplier_DoublesHeadroom`).

- **Risk:** `CalculateIncomeRoom` is currently called from `SharedWithdrawalFunctions.IncomeThreasholdSellInvestmentsToDollarAmount` (line 315) with two arguments. Adding a third parameter breaks that call site. The call site fix is described in Step 11. Using default parameter value `1m` keeps the build green until then.

---

### Step 5: `SocialSecurityBenefitsWorksheet.cs`

- **File:** `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/TaxForms/Federal/SocialSecurityBenefitsWorksheet.cs`

- **What changes:**
  - Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter of `CalculateTaxableSocialSecurityBenefits` (line 16).
  - At line 36, replace `TaxConstants.SocialSecurityWorksheetCreditLine8` with `TaxConstants.SocialSecurityWorksheetCreditLine8 * cumulativeCpiMultiplier`.
  - At line 44, replace `TaxConstants.SocialSecurityWorksheetCreditLine10` with `TaxConstants.SocialSecurityWorksheetCreditLine10 * cumulativeCpiMultiplier`.

- **Why this step:** `SocialSecurityBenefitsWorksheet` is a leaf called from `Form1040.CalculateTaxLiability` (line 53). It must be updated before `Form1040`.

- **Tests unlocked:** §16.7 (`CalculateTaxLiabilityForYear_SsWorksheetThreshold_ScalesWithMultiplier`).

- **Risk:** Only one call site: `Form1040.CalculateTaxLiability` line 53–54. That call site is updated in Step 7.

---

### Step 6: `ScheduleD.cs`

- **File:** `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/TaxForms/Federal/ScheduleD.cs`

- **What changes:**
  - Add `decimal cumulativeCpiMultiplier = 1m` as a constructor parameter (the existing constructor is at line 26: `public ScheduleD(TaxLedger ledger, int taxYear)`). Store it in a private field.
  - In `Complete()` (line 32), at line 45, replace `TaxConstants.ScheduleDMaximumCapitalLoss` with `TaxConstants.ScheduleDMaximumCapitalLoss * cumulativeCpiMultiplier`. Because the constant is negative (-3000m) and `cumulativeCpiMultiplier > 1`, the product becomes more negative (e.g. -4500m at 1.5×), allowing a larger nominal capital loss deduction — confirmed as correct behavior by Dan's OD-4 decision.

- **Why this step:** `ScheduleD` is created inside `Form1040.CalculateTaxLiability` (line 15 and line 48). It must compile before `Form1040` can pass the multiplier to it.

- **Tests unlocked:** §16.8 (`ScheduleD_CapitalLossLimit_ScalesNegativelyWithMultiplier`).

- **Risk:** `ScheduleD` is only instantiated in `Form1040` (line 15). That instantiation is updated in Step 7.

---

### Step 7: `Form1040.cs`, `TaxTable.cs`, `TaxComputationWorksheet.cs`, `QualifiedDividendsAndCapitalGainTaxWorksheet.cs`

These four files are changed in a single pass because they are tightly coupled inside `Form1040.CalculateTax()`.

- **File:** `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/TaxForms/Federal/Form1040.cs`

- **What changes to Form1040:**
  - Change the primary constructor (line 7) from `Form1040(TaxLedger ledger, int taxYear)` to `Form1040(TaxLedger ledger, int taxYear, decimal cumulativeCpiMultiplier = 1m)`. Store `cumulativeCpiMultiplier` in a private field.
  - In `CalculateTaxLiability` (line 25):
    - At line 15 (where `_scheduleD` is initialized), pass `cumulativeCpiMultiplier` to the `ScheduleD` constructor.
    - At line 53–54 (where `SocialSecurityBenefitsWorksheet.CalculateTaxableSocialSecurityBenefits` is called), pass `cumulativeCpiMultiplier` as the final argument.
    - At line 58, replace the `const decimal line12 = TaxConstants.FederalStandardDeduction;` with a regular variable: `var line12 = TaxConstants.FederalStandardDeduction * cumulativeCpiMultiplier;`. Note: line 58 currently uses `const decimal` because `FederalStandardDeduction` is a compile-time constant. After this change, `line12` must become a `var` or `decimal` (not `const`) because it is now a runtime calculation.
    - At line 114, replace `TaxConstants.FederalWorksheetVsTableThreshold` with `TaxConstants.FederalWorksheetVsTableThreshold * cumulativeCpiMultiplier` in `CalculateTax()`.
  - In `CalculateTax()` (line 104): pass `cumulativeCpiMultiplier` to `QualifiedDividendsAndCapitalGainTaxWorksheet.CalculateTaxOwed`, `TaxTable.CalculateTaxOwed`, and `TaxComputationWorksheet.CalculateTaxOwed`.

- **File:** `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/TaxForms/Federal/TaxTable.cs`

- **What changes to TaxTable:**
  - Add `decimal cumulativeCpiMultiplier = 1m` parameter to `CalculateTaxOwed` (line 11).
  - Pass `cumulativeCpiMultiplier` to `CalculatePreciseLiability`.
  - Add `decimal cumulativeCpiMultiplier = 1m` parameter to `CalculatePreciseLiability` (line 52).
  - In `CalculatePreciseLiability`, before the `foreach` loop on `TaxConstants.Federal1040TaxTableBrackets`, call `TaxCalculation.ScaleBracketThresholds(TaxConstants.Federal1040TaxTableBrackets, cumulativeCpiMultiplier)` to get a scaled local copy of the brackets. Use the scaled copy inside the loop instead of the original.
  - Update the guard at line 13: replace `TaxConstants.FederalWorksheetVsTableThreshold` with `TaxConstants.FederalWorksheetVsTableThreshold * cumulativeCpiMultiplier`.

- **File:** `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/TaxForms/Federal/TaxComputationWorksheet.cs`

- **What changes to TaxComputationWorksheet:**
  - Add `decimal cumulativeCpiMultiplier = 1m` parameter to `CalculateTaxOwed` (line 12).
  - Before the `foreach` loop on `TaxConstants.Fed1040TaxComputationWorksheetBrackets`, apply scaling inline: create a local array where each element has `min = bracket.min * cumulativeCpiMultiplier`, `max = bracket.max * cumulativeCpiMultiplier` (with `decimal.MaxValue` sentinel preserved), and `subtractions = bracket.subtractions * cumulativeCpiMultiplier`. Scaling subtractions is preferred (approach b) because they are derived as `(rate_high - rate_low) * threshold` and must scale by the same multiplier to remain mathematically self-consistent.
  - *(See Open Item 2 for the decision on whether subtractions should scale.)*

- **File:** `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/TaxForms/Federal/QualifiedDividendsAndCapitalGainTaxWorksheet.cs`

- **What changes to QualifiedDividendsAndCapitalGainTaxWorksheet:**
  - Add `decimal cumulativeCpiMultiplier = 1m` parameter to `CalculateTaxOwed` (line 7).
  - At line 26, multiply `TaxConstants.FederalCapitalGainsBrackets[0].max` by `cumulativeCpiMultiplier`.
  - At line 32, multiply `TaxConstants.FederalCapitalGainsBrackets[1].max` by `cumulativeCpiMultiplier`.
  - At lines 41 and 44, pass `cumulativeCpiMultiplier` to both `TaxTable.CalculateTaxOwed` and `TaxComputationWorksheet.CalculateTaxOwed` calls. Also update the threshold checks to use `TaxConstants.FederalWorksheetVsTableThreshold * cumulativeCpiMultiplier`.

- **Why this step:** After Step 6, `ScheduleD` passes its constructor. `Form1040` calls `ScheduleD` (line 15), `SocialSecurityBenefitsWorksheet` (line 53), and all three tax calculators (line 106–120). All dependencies must be updated together so `Form1040` compiles.

- **Tests unlocked:** §16.6 (`CalculateTaxLiabilityForYear_DoubleMultiplier_DoublesStandardDeductions`), §16.10 (bracket thresholds scale), §16.10b (worksheet threshold routing).

- **Risk (breaking change):** `TaxCalculation.CalculateTaxLiabilityForYear` is called at `Simulation.PayTaxForYear` (line 447). Default parameter values `1m` on all new parameters keep the build green through Step 12.

---

### Step 8: `FormD400.cs`

- **File:** `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/TaxForms/NC/FormD400.cs`

- **What changes:**
  - Add `decimal cumulativeCpiMultiplier = 1m` as the fourth parameter in the constructor (line 15). Store it in a private field.
  - In `CalculateTaxLiability` (line 22), at line 29, replace `var line11 = TaxConstants.NcStandardDeduction;` with `var line11 = TaxConstants.NcStandardDeduction * cumulativeCpiMultiplier;`.

- **Why this step:** `FormD400` is constructed in `TaxCalculation.CalculateTaxLiabilityForYear` (line 189). Step 4 already passes `cumulativeCpiMultiplier` to both form constructors. This step makes `FormD400` ready to receive it.

- **Tests unlocked:** §16.6 (covers both federal and NC deductions indirectly via `CalculateTaxLiabilityForYear`).

- **Risk:** `TaxCalculation.CalculateNorthCarolinaTaxLiabilityForYear` (line 206–210) also instantiates `FormD400`. See Open Item 3.

---

### Step 9: `Spend.cs`

- **File:** `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/StaticFunctions/Spend.cs`

- **What changes:**

  **`CalculateMonthlyFunSpend` (line 59):**
  - Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter.
  - Multiply the return value at every non-zero return point by `cumulativeCpiMultiplier`.

  **`CalculateMonthlyHealthSpend` (line 88):**
  - Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter.
  - The five `const decimal` Medicare constants at lines 131–138 must become runtime `decimal` variables: `partADeductiblePerAdmission`, `partBPremiumMonthly`, `partBAnnualDeductible`, `partDPremiumMonthly`, `partDAverageMonthlyDrugCost`. Change each to `var constantName = originalValue * cumulativeCpiMultiplier;`.
  - At line 124, multiply `person.RequiredMonthlySpendHealthCare` by `cumulativeCpiMultiplier`.
  - At line 127 (age >= 88 path), multiply `person.RequiredMonthlySpendHealthCare * 2m` by `cumulativeCpiMultiplier`.

  **`CalculateMonthlyRequiredSpend` (line 157):**
  - Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter.
  - At line 161, multiply `person.RequiredMonthlySpend` by `cumulativeCpiMultiplier`.
  - Pass `cumulativeCpiMultiplier` to the `CalculateMonthlyHealthSpend` call at line 162.
  - The debt component (lines 163–166) reads `MonthlyPayment` directly from open debt positions — leave this unchanged per FSD-006.

  **`CalculateMonthlyRequiredSpendWithoutDebt` (line 175):**
  - Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter.
  - Pass it through to the `CalculateMonthlyRequiredSpend` call at line 183.

  **`CalculateFunPointsForSpend` (line 32):**
  - Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter.
  - After the final `funPoints` value is computed (after the two `Math.Max`/`Math.Min` caps at lines 55–56), before the return, divide by `cumulativeCpiMultiplier`: `return funPoints / cumulativeCpiMultiplier;`. This preserves the purchasing-power-to-fun-points ratio as specified.

  **`CalculateCashNeedForNMonths` (line 16):**
  - Add `decimal cumulativeCpiMultiplier = 1m` and `decimal currentCpiGrowthRate = 0m` as the last two parameters.
  - Inside the loop body, for each iteration `i`, compute a forward-looking multiplier: `var iterationMultiplier = cumulativeCpiMultiplier * (decimal)Math.Pow((double)(1m + currentCpiGrowthRate), i);`. Use `iterationMultiplier` when calling `CalculateMonthlyFunSpend` and `CalculateMonthlyRequiredSpend`. Iteration `i=0` uses the current month's multiplier; `i=1` compounds once, etc.

- **Why this step:** `Spend` functions are leaf-level for the spend calculation chain. Callers are `Simulation.PayForStuff`, `Simulation.RecordFunAndAnxiety`, and the withdrawal strategy rebalance methods.

- **Tests unlocked:** §16.1, §16.1b, §16.2, §16.2b, §16.3, §16.3b, §16.4, §16.4b, §16.5, §16.5b.

- **Risk (breaking changes):** Multiple callers exist in `Simulation.cs` and withdrawal strategy files. All updated in Steps 11 and 12. Default parameter values keep the build green during transition.

---

### Step 10: `Payday.cs`

- **File:** `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/StaticFunctions/Payday.cs`

- **What changes:**

  **`DeductPostTax` (line 167):**
  - Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter.
  - At line 170, multiply `person.PostTaxInsuranceDeductions` by `cumulativeCpiMultiplier`.
  - Also multiply `annual401KPostTax` by `cumulativeCpiMultiplier` to keep accounting consistent (take-home deduction = what was invested in `AddPaycheckRelatedRetirementSavings`).

  **`DeductPreTax` (line 180):**
  - Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter.
  - At line 188, multiply `person.PreTaxHealthDeductions` by `cumulativeCpiMultiplier`.
  - Scale `annualHsaContribution` and `annual401KPreTax` by `cumulativeCpiMultiplier` to match inflated investment amounts.
  - In the `RecordMultiSpend` call at lines 197–200, the health spend portion must use the scaled value.

  **`AddPaycheckRelatedRetirementSavings` (line 115):**
  - Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter.
  - At line 128, replace `person.Annual401KPostTax / 12m` with `Math.Min(person.Annual401KPostTax, TaxConstants.Irs401KElectiveDeferralLimit) * cumulativeCpiMultiplier / 12m`.
  - At lines 130–131, same pattern for `Annual401KPreTax`.
  - At line 132, `monthly401KMatch` — per Dan's OD-2 decision: do NOT inflate. Remains `(person.AnnualSalary * person.Annual401KMatchPercent) / 12m` unchanged.
  - At lines 133–134, replace `(person.AnnualHsaContribution + person.AnnualHsaEmployerContribution) / 12m` with `Math.Min(person.AnnualHsaContribution + person.AnnualHsaEmployerContribution, TaxConstants.IrsHsaFamilyContributionLimit) * cumulativeCpiMultiplier / 12m`.

  **`WithholdTaxesFromPaycheck` (line 211):**
  - Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter.
  - At line 220–223, replace `TaxConstants.OasdiMax` with `TaxConstants.OasdiMax * cumulativeCpiMultiplier`.
  - At line 226, replace `TaxConstants.AdditionalMedicareThreshold` with `TaxConstants.AdditionalMedicareThreshold * cumulativeCpiMultiplier`.

  **`ProcessSocialSecurityCheck` (line 11):**
  - Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter.
  - At line 29, replace `person.AnnualSocialSecurityWage / 12m` with `(person.AnnualSocialSecurityWage / 12m) * cumulativeCpiMultiplier`. This is the COLA effect on the SS payment.

  **`ProcessPreRetirementPaycheck` (line 49):**
  - Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter.
  - Thread it to all sub-calls: `WithholdTaxesFromPaycheck` (line 70), `DeductPreTax` (line 75), `DeductPostTax` (line 81), `AddPaycheckRelatedRetirementSavings` (line 94–95).

- **Why this step:** All payday functions need the multiplier so the full pre-retirement paycheck calculation is inflation-adjusted.

- **Tests unlocked:** §16.9, §16.11, §16.12, §16.13, §16.14, §16.15a, §16.15b, §16.16.

- **Risk (breaking changes):** `ProcessPreRetirementPaycheck` is called from `Simulation.ProcessPaycheck` (line 493), and `ProcessSocialSecurityCheck` from `Simulation.ProcessSocialSecurityCheck` (line 503). Both are updated in Step 12. Default parameter values keep the build green.

---

### Step 11: Withdrawal strategy call sites

- **Files:**
  - `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/WithdrawalStrategy/SixtyForty.cs`
  - `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/WithdrawalStrategy/SharedWithdrawalFunctions.cs`
  - `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/WithdrawalStrategy/NoMidIncomeThreshold.cs`
  - `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/WithdrawalStrategy/BasicBucketsIncomeThreshold.cs` *(see Open Item 5)*
  - `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/WithdrawalStrategy/BasicBucketsTaxableFirst.cs` *(see Open Item 5)*

- **What changes:**

  **`SixtyForty.RebalancePortfolio` (line 104):** `CurrentPrices currentPrices` is already in the signature. At line 131, update `Spend.CalculateCashNeedForNMonths` call to add `currentPrices.CumulativeCpiMultiplier` and `currentPrices.CurrentCpiGrowthRate`.

  **`SharedWithdrawalFunctions.BasicBucketsRebalance` (line 28):** `CurrentPrices currentPrices` is present. At lines 55–57, update `CalculateCashNeedForNMonths` call to add `currentPrices.CumulativeCpiMultiplier` and `currentPrices.CurrentCpiGrowthRate`.

  **`SharedWithdrawalFunctions.BasicBucketsRebalanceLongToMid` (line 81):** `CurrentPrices prices` is present. At lines 110–111, update `CalculateCashNeedForNMonths` call to add `prices.CumulativeCpiMultiplier` and `prices.CurrentCpiGrowthRate`.

  **`SharedWithdrawalFunctions.CalculateExcessCash` (line 370):** This method does NOT currently receive a `CurrentPrices` parameter. Add `CurrentPrices prices` as the last parameter. At line 376, pass `prices.CumulativeCpiMultiplier` and `prices.CurrentCpiGrowthRate` to `CalculateCashNeedForNMonths`. The two callers of `CalculateExcessCash` (`SixtyForty.InvestExcessCash` line 31, `SharedWithdrawalFunctions.InvestExcessCashIntoLongTermBrokerage` line 406) must also pass their existing `prices` argument through.

  **`SharedWithdrawalFunctions.IncomeThreasholdSellInvestmentsToDollarAmount` (line 296):** Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter. At line 315, pass it to `TaxCalculation.CalculateIncomeRoom`. Callers (`NoMidIncomeThreshold.SellInvestmentsToDollarAmount` line 119, `BasicBucketsIncomeThreshold`, `BasicBucketsTaxableFirst`) must pass it through.

  **`NoMidIncomeThreshold.RebalancePortfolio` (line 46):** `currentPrices` is present at line 48. At line 65, update `Spend.CalculateCashNeedForNMonths` call to add `currentPrices.CumulativeCpiMultiplier` and `currentPrices.CurrentCpiGrowthRate`.

- **Why this step:** The withdrawal strategies perform cash-need projections. Without inflated projections, rebalancing logic will systematically under-estimate how much cash is needed in future months.

- **Tests unlocked:** No new §16 tests directly, but existing withdrawal strategy tests must not break. Default parameter values (`1m` / `0m`) keep them passing.

- **Risk:** `BasicBucketsIncomeThreshold` and `BasicBucketsTaxableFirst` were not in the original files-to-read list. Inspect them for calls to `CalculateCashNeedForNMonths` and `IncomeThreasholdSellInvestmentsToDollarAmount` and apply the same pattern. See Open Item 5.

---

### Step 12: `Simulation.cs`

- **File:** `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/StaticFunctions/Simulation.cs`

- **What changes:**

  **`PayForStuff` (line 370):** Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter.
  - At line 375, pass `cumulativeCpiMultiplier` to `Spend.CalculateMonthlyFunSpend`.
  - At line 376, pass `cumulativeCpiMultiplier` to `Spend.CalculateMonthlyRequiredSpendWithoutDebt`.
  - At line 413, pass `cumulativeCpiMultiplier` to `Spend.CalculateFunPointsForSpend`.

  **`RecordFunAndAnxiety` (line 546):** Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter.
  - At line 557, pass `cumulativeCpiMultiplier` to `Spend.CalculateMonthlyRequiredSpend`.

  **`ProcessPaycheck` (line 489):** Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter. Pass it to `Payday.ProcessPreRetirementPaycheck` at line 493.

  **`ProcessSocialSecurityCheck` (line 499):** Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter. Pass it to `Payday.ProcessSocialSecurityCheck` at line 503.

  **`ProcessPayday` (line 508):** Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter. Thread it to `ProcessPaycheck` (line 524) and `ProcessSocialSecurityCheck` (line 538).

  **`PayTaxForYear` (line 431):** Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter. At line 447, pass it to `TaxCalculation.CalculateTaxLiabilityForYear`.

  **`CalculateIsIncomeInflection` (line 15):** Add `decimal cumulativeCpiMultiplier = 1m` as the last parameter. Pass it to `Payday.WithholdTaxesFromPaycheck` at line 27. The static cache (`_hasCalculatedSpendablePay`, `_spendablePay`) means the cached value is always computed at the first call's multiplier (1.0). See Open Item 1.

- **Why this step:** `Simulation.cs` is the mid-layer orchestrator. After this step, the full call chain from `LifeSimulator` → `Simulation` → `Spend`/`Payday`/`TaxCalculation` is closed.

- **Tests unlocked:** No new §16 tests directly, but §16.1–§16.16 are now fully exercisable end-to-end.

- **Risk:** `CalculateIsIncomeInflection` static cache issue — see Open Item 1.

---

### Step 13: `LifeSimulator.cs`

- **File:** `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/LifeSimulator.cs`

- **What changes:**
  - In `PayForStuff` (private method, line 457): include `_simData.CurrentPrices.CumulativeCpiMultiplier` in the `Simulation.PayForStuff` call at line 467.
  - In `RecordFunAndAnxiety` (private method, line 575): include `_simData.CurrentPrices.CumulativeCpiMultiplier` in the `Simulation.RecordFunAndAnxiety` call at line 584.
  - In `ProcessPayday` (private method, line 516): include `_simData.CurrentPrices.CumulativeCpiMultiplier` in the `Simulation.ProcessPayday` call at line 525.
  - In `PayTax` (private method, line 485): include `_simData.CurrentPrices.CumulativeCpiMultiplier` in the `Simulation.PayTaxForYear` call at line 499.
  - `CheckForInflection` (line 244): pass `_simData.CurrentPrices.CumulativeCpiMultiplier` to `CalculateIsIncomeInflection`. (Approximation noted in Open Item 1.)
  - No changes to `SetGrowthAndPrices` (line 597) — the multiplier is already compounded there via Step 3.

- **Why this step:** `LifeSimulator` is the top-level monthly loop driver. It holds `_simData.CurrentPrices` and must propagate the multiplier from it to every `Simulation` function call.

- **Tests unlocked:** No new §16 tests. End-to-end integration is now fully wired.

- **Risk:** Order of operations matters. `SetGrowthAndPrices` is called first (line 140), which updates `_simData.CurrentPrices.CumulativeCpiMultiplier` for the current month. All downstream calls within the same month iteration see the updated multiplier. This is correct — inflation for month N is applied to all spending in month N.

---

### Step 14: Activate `InflationAdjustmentTests.cs`

- **File:** `/media/dan/fdrive/codeprojects/PersonalFinance/Lib.Tests/MonteCarlo/StaticFunctions/InflationAdjustmentTests.cs`

- **What changes:**
  - Remove `Skip = "..."` from each `[Fact]` attribute.
  - Replace each `Assert.True(false, ...)` stub with the actual method call shown in the test's comment block.
  - Do this group by group as each step compiles — not all at once — to maintain a continuously green build.

- **Why this step:** All 29 tests are currently guarded by `[Skip]`. After Steps 1–13, all method signatures match what the tests expect.

- **Tests unlocked:** All §16 tests: §16.1a, §16.1b, §16.2, §16.2b, §16.3, §16.3b, §16.4, §16.4b, §16.5, §16.5b, §16.6, §16.7, §16.8, §16.9, §16.10, §16.10b, §16.11, §16.12, §16.13, §16.14, §16.15a, §16.15b, §16.16, §16.17, §16.17b, §16.17c, §16.17d, §16.19.

- **Risk:** §16.3 (`CalculateMonthlyHealthSpend_MedicareBand_MultiplierDoubles`) asserts an exact doubling at multiplier=2.0. The number of hospital admissions (`1.5 + (yearsOver65/10)`) is age-based and does NOT scale with the multiplier — only the per-admission deductible scales. The test assertion of exact doubling will fail. See Open Item 4 before activating §16.3.

---

## Existing Test Impact

The following currently-passing tests are at risk of breaking due to signature changes:

1. **Tests calling `Spend.CalculateMonthlyFunSpend`, `CalculateMonthlyRequiredSpend`, `CalculateMonthlyHealthSpend`, `CalculateFunPointsForSpend`, `CalculateCashNeedForNMonths`** — Fix: default parameter `= 1m` (and `= 0m` for `currentCpiGrowthRate`) on all new parameters. Existing callers compile and produce identical results.

2. **Tests calling `Payday.WithholdTaxesFromPaycheck`, `DeductPreTax`, `DeductPostTax`, `AddPaycheckRelatedRetirementSavings`, `ProcessSocialSecurityCheck`, `ProcessPreRetirementPaycheck`** — Fix: default parameter `= 1m`.

3. **Tests calling `TaxCalculation.CalculateTaxLiabilityForYear` or `CalculateIncomeRoom`** — Fix: default parameter `= 1m`.

4. **`TaxCalculationExtendedTests2.cs` §15 test** (`CalculateIncomeRoom_EmptyLedger_ReturnsExpectedHeadroom`) asserts `$123,500` headroom. With default `= 1m`, this test continues to pass unchanged.

5. **`VarPricingExtendedTests.cs`** — tests `CopyPrices` propagates `CurrentTreasuryCoupon`. After Step 3 adds two new fields to `CopyPrices`, these tests continue to pass because the new fields do not affect `CurrentTreasuryCoupon`.

**Recommended approach for all of the above:** add `decimal cumulativeCpiMultiplier = 1m` as a default parameter on every new parameter added in Steps 4–12. This ensures zero existing test breakage.

---

## Open Items

*All items must be resolved before implementation begins.*

1. **`CalculateIsIncomeInflection` static cache interaction:** The static fields `_hasCalculatedSpendablePay` and `_spendablePay` (`Simulation.cs` lines 13–14) are computed on the first call and reused. After adding `cumulativeCpiMultiplier`, the cached value will always reflect the multiplier at first call (1.0 at simulation start). The inflection threshold will not increase with inflation over the simulation's lifetime. Should this cache be invalidated each call, or is the approximation acceptable?
   Dan's decision: ___ if this question is entirely encapsulated in the income inflection feature, I plan to remove that feature in future, so it doesn't matter. If this question is bigger than the income inflection feature, please help me understand the impact better.

2. **`TaxComputationWorksheet.cs` — `subtractions` field scaling:** The `Fed1040TaxComputationWorksheetBrackets` array has a `subtractions` field per bracket (e.g. `9894m` for the 22% bracket). These are derived from bracket structure math. Should they be scaled by `cumulativeCpiMultiplier` (mathematically self-consistent), or remain fixed (simpler but slightly incorrect at high inflation)?
   Dan's decision: ___ the subtractions amount is derived. But, since you only use it when income is above \$100,000 , it's difficult for me to see the derivation. Regardless, it should grow as the min and max grow.

3. **`TaxCalculation.CalculateNorthCarolinaTaxLiabilityForYear` (line 206):** This second entry point into `FormD400` is not called from the main simulation path, but it is a public method. Should it also receive `cumulativeCpiMultiplier` for completeness?
   Dan's decision: ___ It should also receive the multiplier. I'm not sure what you mean that it isn't called in the simulation path. Regardless, I don't want a method in this code base that contradicts a major premise of the simulation's ideals.

4. **§16.3 test precision:** `CalculateMonthlyHealthSpend_MedicareBand_MultiplierDoubles` asserts an exact doubling at multiplier=2.0. The hospital admission count formula `1.5 + (yearsOver65/10)` is age-based and does NOT scale — only the per-admission deductible scales. The exact-doubling assertion will fail. Should the test be changed to a directional assertion (result at 2.0× is greater than at 1.0×), or should the admission count formula also scale?
   Dan's decision: ___ I see what you mean. This test is flawed by design. I believe there should be a test that asserts correct outputs given known inputs (via theories and inline data). Otherwise the test is meaningless. Of course it'll be more expensive if the multiplier is higher. That's not testing anything meaningful to me. It could be that there's already a separate test that tests as I describe here. Please have the test agent review this response and address this concern with an update to the tests and project test documents. 

5. **`BasicBucketsIncomeThreshold` and `BasicBucketsTaxableFirst` call sites:** These two withdrawal strategy files were not in the implementation-plan read list. They almost certainly call `CalculateCashNeedForNMonths` and/or `IncomeThreasholdSellInvestmentsToDollarAmount`. The implementer must inspect these files and apply the same call-site updates as Step 11. No blocking decision needed — just a flag to ensure they are not missed.
   Dan's decision: ___There appears to be no question for me, but I agree with the assertion that they should be included.

---

## Critical Files for Implementation

- `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/DataTypes/MonteCarlo/CurrentPrices.cs` — State carrier; must be changed first as every other step depends on it
- `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/StaticFunctions/Pricing.cs` — Single location where `CumulativeCpiMultiplier` is compounded each month; `CopyPrices` must propagate both new fields
- `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/StaticFunctions/Spend.cs` — Six methods changed; leaf-level spend functions consumed by all withdrawal strategies and `Simulation.PayForStuff`
- `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/StaticFunctions/TaxCalculation.cs` — Entry point into all tax forms; new `ScaleBracketThresholds` helper lives here; changes to `CalculateTaxLiabilityForYear` and `CalculateIncomeRoom` cascade to every tax form and every withdrawal strategy
- `/media/dan/fdrive/codeprojects/PersonalFinance/Lib/MonteCarlo/LifeSimulator.cs` — Top-level monthly loop; extracts `_simData.CurrentPrices.CumulativeCpiMultiplier` and passes it to every `Simulation` call; final integration point confirming the full chain is wired
