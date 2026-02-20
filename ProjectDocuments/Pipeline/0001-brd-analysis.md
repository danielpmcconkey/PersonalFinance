# BRD-0001 Analysis Report: Track Inflation on Spend

## Summary

This BRD proposes transitioning from a today's-dollars-only simulation model (where S&P500 growth
is tracked net of inflation) to a dual-tracking model where monthly expenses and certain financial
values are adjusted for inflation during the simulation lifecycle. Spending values, tax constants,
Social Security benefits, payroll deductions, and savings contributions should grow monthly
according to the simulated CPI growth rate, while debt payments remain fixed. Investment growth
should use unadjusted (gross) S&P500 growth rather than inflation-adjusted growth, and fun points
from spending should be discounted by accumulated inflation.

---

## Domain Term Glossary

| Term | Found? | Location |
|------|--------|----------|
| **CalculateCashNeedForNMonths** | ✓ | `Lib/MonteCarlo/StaticFunctions/Spend.cs:16` |
| **CalculateFunPointsForSpend** | ✓ | `Lib/MonteCarlo/StaticFunctions/Spend.cs:32` |
| **HypotheticalLifeTimeGrowthRate** | ✓ | `Lib/DataTypes/MonteCarlo/HypotheticalLifeTimeGrowthRate.cs:3` |
| **CpiGrowth** | ✓ | `Lib/DataTypes/MonteCarlo/HypotheticalLifeTimeGrowthRate.cs:6` |
| **SpGrowth** | ✓ | `Lib/DataTypes/MonteCarlo/HypotheticalLifeTimeGrowthRate.cs:5` |
| **TaxConstants** | ✓ | `Lib/StaticConfig/TaxConstants.cs:3` |
| **required spend** | ✓ | `Lib/DataTypes/Postgres/PgPerson.cs:60-61` |
| **fun spend** | ✓ | `Lib/DataTypes/MonteCarlo/Model.cs:113-120` |
| **Payday** | ✓ | `Lib/MonteCarlo/StaticFunctions/Payday.cs:9` |
| **ProcessSocialSecurityCheck** | ✓ | `Lib/MonteCarlo/StaticFunctions/Payday.cs:11-13` |
| **ProcessPreRetirementPaycheck** | ✓ | `Lib/MonteCarlo/StaticFunctions/Payday.cs:49-51` |
| **Social security** | ✓ | `Lib/MonteCarlo/StaticFunctions/Payday.cs:29` |
| **HSA** | ✓ | `Lib/DataTypes/MonteCarlo/McInvestmentAccountType.cs:16` |
| **401k** | ✓ | `Lib/DataTypes/MonteCarlo/McInvestmentAccountType.cs:12-13` |
| **CurrentPrices** | ✓ | `Lib/DataTypes/MonteCarlo/CurrentPrices.cs:3-16` |
| **CurrentCpi** | ✓ | `Lib/DataTypes/MonteCarlo/CurrentPrices.cs:14` |
| **DesiredMonthlySpendPreRetirement** | ✓ | `Lib/DataTypes/MonteCarlo/Model.cs:114` |
| **DesiredMonthlySpendPostRetirement** | ✓ | `Lib/DataTypes/MonteCarlo/Model.cs:120` |

---

## Requirement-by-Requirement Analysis

### BR-1: Increment spend by CPI over time
**Requirement:** "Increment all required spend and fun spend according to that simulated lifetime's
CPI growth rate for that month"

**Codebase evidence:**
- `Spend.CalculateMonthlyFunSpend()`: `Spend.cs:59` — currently returns fixed values based on
  age/date, no CPI multiplier
- `Spend.CalculateMonthlyRequiredSpend()`: `Spend.cs:157-168` — returns fixed `RequiredMonthlySpend`
  without CPI adjustment
- `Spend.CalculateMonthlyHealthSpend()`: `Spend.cs:88-156` — hardcoded Medicare costs
  (partADeductible=1676m, partBPremiumMonthly=370m, etc.)
- `HypotheticalLifeTimeGrowthRate.CpiGrowth`: `HypotheticalLifeTimeGrowthRate.cs:6` — exists as
  monthly percentage rate
- `CurrentPrices.CurrentCpi`: `CurrentPrices.cs:14` — initialized 1.00m, never updated

**Clarity:** AMBIGUOUS
- **Q1:** Should spend increments accumulate from a baseline (e.g.,
  `adjustedSpend = baselineSpend * cumulativeCpiMultiplier`), or be applied incrementally each month?
- **Q2:** Does BR-1 include healthcare (Medicare) costs? Medicare constants are hardcoded in
  `CalculateMonthlyHealthSpend`; should they also inflate?

---

### BR-2: Functions that project spend into the future should increment by CPI
**Requirement:** "All functions in the Spend.cs static function that project a future spend
(e.g. CalculateCashNeedForNMonths) increment projected non-debt spends with an assumed static CPI
growth rate."

**Codebase evidence:**
- `Spend.CalculateCashNeedForNMonths()`: `Spend.cs:16-30` — loops n months forward, calls
  `CalculateMonthlyFunSpend()` and `CalculateMonthlyRequiredSpend()` which ignore CPI; does NOT
  receive `CurrentPrices`
- Current signature: `CalculateCashNeedForNMonths(Model model, PgPerson person, BookOfAccounts accounts,
  LocalDateTime currentDate, int nMonths)`
- Called from: `SixtyForty.cs:131`, `SharedWithdrawalFunctions.cs:56,110,376`,
  `NoMidIncomeThreshold.cs:65`

**Clarity:** MISSING_INFO
- **Q3:** Should the function signature be extended to accept `CurrentPrices` (to get current CPI
  rate), or should the caller apply CPI adjustment to the result?
- **Q4:** Does "static CPI growth rate" mean using the current month's CpiGrowth for all n projected
  months, or something else?

---

### BR-3: Fun points attributed to spending money should decrease by CPI
**Requirement:** "When calculating how many fun points the simulated life should obtain from spending
money (e.g. CalculateFunPointsForSpend) the function, and any that do similar fun point calculation
for money spending, should discount future spend by inflation. It's not as fun to spend $10,000
today as it would've been in 1950."

**Codebase evidence:**
- `Spend.CalculateFunPointsForSpend()`: `Spend.cs:32-58` — applies only age-based penalty
  (0.5 to 1.0 multiplier), no inflation discount
- Current signature: `CalculateFunPointsForSpend(decimal funSpend, PgPerson person,
  LocalDateTime currentDate)` — no CPI/inflation context
- Called from: `Simulation.cs:413`

**Clarity:** AMBIGUOUS
- **Q5:** What is the discount formula? Options:
  - `funPoints / cumulativeCpiMultiplier`
  - `funPoints * (1 / cumulativeCpiMultiplier)`
  - Some other formula?
- **Q6:** Is cumulative inflation measured from simulation start, or from some other baseline?
- **Q7:** Should `CurrentPrices` (with cumulative CPI) be added to the function signature, or
  applied by the caller before passing `funSpend`?

---

### BR-4: Debt exclusion
**Requirement:** "Monthly payments for debt should NOT increase with inflation. They remain at their
starting value until the debt is paid off or the simulation ends"

**Codebase evidence:**
- `Spend.CalculateMonthlyRequiredSpend()`: `Spend.cs:163-166` — reads fixed `MonthlyPayment` from
  each debt position
- `AccountInterestAccrual.AccrueInterestOnDebtPosition()`: `AccountInterestAccrual.cs:61-90` —
  applies APR to balance but does not adjust `MonthlyPayment`

**Clarity:** CLEAR
- Debt payments are already fixed in the existing code. **No code changes required for BR-4** — this
  BR documents an explicit exclusion, not a new behavior.

---

### BR-5: Tax constants
**Requirement:** "Any constants used in the tax forms that are flat rate (not percentage based) should
only use the constant as the value at the start of the simulation. From thereafter, those values
should increase with CPI inflation"

**Codebase evidence (TaxConstants.cs:1-105):**
- **Flat-rate constants (candidates for CPI adjustment):**
  - `FederalStandardDeduction = 29200.0M` (line 82)
  - `NcStandardDeduction = 25500m` (line 75)
  - `SocialSecurityWorksheetCreditLine8 = 32000m` (line 84)
  - `SocialSecurityWorksheetCreditLine10 = 12000m` (line 85)
  - `ScheduleDMaximumCapitalLoss = -3000m` (line 86)
  - `OasdiMax = 11439.0m` (line 96)
- **Percentage-based constants (do NOT adjust per BR-5):**
  - `Federal1040TaxTableBrackets[]` — rate percentages
  - `OasdiBasePercent`, `StandardMedicareTaxRate`, `AdditionalMedicareTaxRate` — all percentages
- `TaxConstants` fields are `public static readonly` — cannot be modified in-place

**Clarity:** AMBIGUOUS
- **Q8:** Should flat-rate tax constants adjust once per calendar year (Jan 1) or every month?
- **Q9:** Where should the inflation-adjusted value live at runtime? Options:
  - Pass computed values into the tax functions as parameters
  - Store in `CurrentPrices` or a new `TaxAdjustments` struct
  - Other pattern?
- **Q10:** Should the inflation-adjusted tax bracket upper and lower bounds also adjust? The bracket
  *rates* are percentages, but the *thresholds* (e.g., 12% bracket tops at $94,300) are flat amounts.

---

### BR-6: Social security payments
**Requirement:** "Social security payments calculated at the beginning of the simulation should also
be given a cost of living adjustment that tracks CPI growth. Note, I believe the simulation
calculates the payment before it is ever used so the calculated value in the background will need
to increase as well"

**Codebase evidence:**
- `PgPerson.AnnualSocialSecurityWage`: `PgPerson.cs:100` (NotMapped field — calculated somewhere)
- `Payday.ProcessSocialSecurityCheck()`: `Payday.cs:11-48`
  - Line 15: Returns early if `currentDate < model.SocialSecurityStart`
  - Line 29: `var amount = person.AnnualSocialSecurityWage / 12m;` — uses fixed annual wage
- **The method/location that calculates and stores `AnnualSocialSecurityWage` was not found in the
  searched files.** This is a technical gap (see Q11 below).

**Clarity:** AMBIGUOUS
- **Q11 (technical gap):** Where is `AnnualSocialSecurityWage` calculated? The calculation method
  was not found — it needs to be located before this BR can be fully specified.
- **Q12:** Should COLA increase the stored `AnnualSocialSecurityWage` value each month, or should
  the Payday function apply a CPI multiplier to the base amount each time it runs?

---

### BR-7: Investment accrual uses unadjusted S&P growth
**Requirement:** "Confirm whether the VAR calculation uses the pure S&P500 value or the net value.
Ensure we use the pure value moving forward"

**Codebase evidence:**
- `Pricing.SetLongTermGrowthRateAndPrices()`: `Pricing.cs:108`
  - `result.CurrentEquityInvestmentPrice += (result.CurrentEquityInvestmentPrice * rates.SpGrowth);`
  - Uses `rates.SpGrowth` directly
- `VarLifetimeGenerator.Generate()`: `VarLifetimeGenerator.cs:81-82`
  - `SpGrowth = (decimal)Y[0], CpiGrowth = (decimal)Y[1]` — returned as separate values
- `Pricing.LoadAndFitVarModel()`: `Pricing.cs:55-62`
  - Loads historical `SpGrowth` and `CpiGrowth` as separate columns from DB
- **AgentContext.txt (lines ~102-105) explicitly states:**
  "All hypothetical growth rates are made up of real-world S&P500 growth rates less a real-world
  change in CPI. In other words, all assumed growth is net of inflation."

**Clarity:** AMBIGUOUS
- **Q13:** AgentContext confirms `SpGrowth` stored in DB = S&P500 **minus CPI**. Does BR-7 require
  changing this to **pure** S&P500 (before subtracting CPI)?
  - If yes: historical DB data must be recalculated and VAR model refitted with new inputs
  - If no: "confirm we use the pure value" means the VAR separation of SpGrowth/CpiGrowth already
    provides the right answer, and no change is needed
- **Q14:** This is a potentially material change to all simulation forecasts. Please explicitly
  confirm the intended direction.

---

### BR-8: Payday values for working income
**Requirement:** "Flat-fee payroll deductions like dental, health, life insurance, etc. will need
to increase with inflation"

**Codebase evidence:**
- `Payday.DeductPreTax()`: `Payday.cs:188`
  - `var annualPreTaxHealthDeductions = person.PreTaxHealthDeductions;` (fixed DB value)
- `Payday.DeductPostTax()`: `Payday.cs:170`
  - `var annualInsuranceDeductions = person.PostTaxInsuranceDeductions;` (fixed DB value)
- `PgPerson.PreTaxHealthDeductions`: `PgPerson.cs:51` (database value, read-only in simulation)
- `PgPerson.PostTaxInsuranceDeductions`: `PgPerson.cs:54` (database value, read-only in simulation)

**Clarity:** AMBIGUOUS
- **Q15:** Should the inflation-adjusted deduction be calculated on-the-fly in the Payday functions
  (using a CPI multiplier passed in), or maintained as a growing state value?
- **Q16:** Should the deduction increase monthly or annually?

---

### BR-9: Savings values increase
**Requirement:** "401k and HSA savings that are triggered by payday events while still working
(not retired) should also increase per inflation"

**Codebase evidence:**
- `Payday.AddPaycheckRelatedRetirementSavings()`: `Payday.cs:129-134`
  - `var roth401KAmount = person.Annual401KPostTax / 12m;`
  - `var hsaAmount = (person.AnnualHsaContribution + person.AnnualHsaEmployerContribution) / 12m;`
- `PgPerson.Annual401KContribution`, `PgPerson.AnnualHsaContribution`: `PgPerson.cs:36,39`
  (database values, read-only in simulation)

**Clarity:** AMBIGUOUS
- **Q17:** Should contribution amounts increase monthly or annually?
- **Q18:** The BRD scope excludes DB updates. Should IRS contribution limits (which also inflate
  annually) be modeled, or should the simulation just scale the user's elected contribution by CPI
  without limit checking?

---

## Risks and Dependencies

### Existing Code Patterns That May Conflict

1. **Spending functions have no access to CPI** (`Spend.cs:16-168`)
   - `CalculateCashNeedForNMonths`, `CalculateMonthlyFunSpend`, `CalculateMonthlyRequiredSpend`
     receive no `CurrentPrices` parameter
   - Risk: Adding CPI requires changing signatures and updating 5+ call sites across withdrawal
     strategy files

2. **`CurrentPrices.CurrentCpi` is never updated** (`CurrentPrices.cs:14`)
   - Field exists (initialized to 1.00m) but no code ever accumulates CPI into it
   - Risk: The infrastructure for tracking cumulative inflation needs to be built from scratch

3. **Tax constants are static readonly** (`TaxConstants.cs`)
   - Cannot be modified in-place; all tax forms reference them directly
   - Risk: Requires a new pattern for passing inflation-adjusted values into tax computations

4. **`PgPerson` data is read-only in simulation** (AgentContext)
   - All source spending values (`PreTaxHealthDeductions`, `PostTaxInsuranceDeductions`,
     `Annual401KContribution`, etc.) live in an immutable DB-sourced object
   - Risk: CPI adjustments must be tracked as multipliers or deltas in simulation state, not
     as mutations to `PgPerson`

5. **VAR model fitted on net-of-inflation S&P growth** (`Pricing.cs:55-65`, AgentContext)
   - If BR-7 means switching to pure S&P growth, the historical DB column and VAR fitting
     would need to change — a significant scope item

6. **`AnnualSocialSecurityWage` origin unknown**
   - The field is `NotMapped` on `PgPerson` (calculated somewhere), but the calculation site
     was not located. BR-6 cannot be fully specified until this is found.

---

## Unanswered Questions

### Questions for Dan

1. **(BR-7)** AgentContext confirms `SpGrowth` = S&P500 **minus CPI** (net of inflation). Does
   BR-7 require switching to **pure** S&P500 growth? This would require recalculating historical
   data and refitting the VAR model — a significant change. Please confirm explicitly.

2. **(BR-1)** Should spend amounts accumulate from a "today's dollars" baseline using a running CPI
   multiplier (e.g., `adjustedSpend = baseSpend * cumulativeCpi`), or be incremented by that month's
   rate applied to the prior month's adjusted value?

3. **(BR-1)** Does BR-1 include the hardcoded Medicare cost constants in `CalculateMonthlyHealthSpend`
   (Part A deductible, Part B monthly premium, etc.)? Or are those excluded?

4. **(BR-3)** What is the exact fun point discount formula?
   - Option A: `funPoints / cumulativeCpiMultiplier`
   - Option B: `funPoints * (baselinePrice / currentPrice)`
   - Option C: other?
   And is the baseline "simulation start" (month 0) or "today"?

5. **(BR-5)** Should flat-rate tax constants (standard deduction, SS worksheet thresholds, etc.)
   adjust **monthly** or **once per year on January 1**?

6. **(BR-5)** Should tax bracket **thresholds** (e.g., the $94,300 upper bound of the 12% bracket)
   also inflate? They are flat dollar amounts, but the BRD says "flat rate (not percentage based)"
   — bracket thresholds are not rates. Please clarify.

7. **(BR-6/BR-8/BR-9)** For Social Security COLA, payroll deduction increases, and 401k/HSA
   contribution increases — should all of these adjust **monthly** or **once per year on January 1**?

8. **(BR-9)** Should the simulation model IRS contribution limits that also inflate annually, or
   simply scale the user's elected contribution amount by CPI with no limit check?

### Technical Gaps (Need Codebase Research Before FSD)

9. **(BR-6)** Where is `PgPerson.AnnualSocialSecurityWage` calculated? The field is `NotMapped`
   and the calculation site was not found. This must be located before BR-6 can be designed.

10. **(BR-2/BR-1)** Where should the cumulative CPI multiplier live in simulation state? Options:
    - Extend `CurrentPrices` with a `CumulativeCpiMultiplier` field
    - Add it to `SimData` or another state object
    Dan's input on the right home for this value would guide the FSD significantly.
