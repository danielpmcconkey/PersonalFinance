# Business Outcomes — Proposed Test Coverage

Generated: 2026-02-19. Last updated: 2026-02-19 (incorporated Dan's review feedback).
Purpose: Enumerate every testable business outcome from the Lib project, grouped by functional area.
This document is the input to the gap-analysis prompt (Prompt 2).

---

## 1. Federal Tax Calculation

### 1.1 Form 1040
- **W2 income flows to AGI.** W2 income pulled from TaxLedger for the tax year is correctly included in
  total income before the standard deduction.
- **Standard deduction applied correctly.** Taxable income = AGI − $29,200 (MFJ 2024); the result is
  floored at zero when AGI is below the deduction.
- **IRA distributions flow to line 4b — not line 1a.** Sales from TRADITIONAL_401_K and TRADITIONAL_IRA
  are recorded in TaxableIraDistribution and appear on Form 1040 line 4b. They do not appear on line 1a
  (wages) or anywhere else that increases W2Income.
- **Qualified dividends pulled to line 3a.** Qualified dividends from the TaxLedger are pulled to line 3a
  and forwarded to the QD&CG worksheet when triggered.
- **Total dividends pulled to line 3b.** Total dividends (qualified + ordinary) are pulled to line 3b and
  the non-qualified portion adds to ordinary income.
- **Capital gains from Schedule D flow to line 7.** The Form 1040 uses Schedule D's computed line 7 value
  (combined capital gains or loss) directly.
- **Tax routing: table vs. worksheet vs. QD&CG worksheet.** When taxable income is below $100,000 the tax
  table is used; at or above $100,000 the computation worksheet is used; when Schedule D requires the
  QD&CG worksheet, that overrides both.
- **Federal withholding offsets liability.** Federal withholding recorded in the ledger is subtracted from
  total tax; a refund (negative result) is returned correctly.
- **Social Security income drives line 6b.** Taxable SS computed by the SS Benefits Worksheet is added
  to total income via line 6b.
- **Zero-income scenario produces zero liability.** A TaxLedger with no income entries for the year
  produces zero net federal tax.
- **All major income sources route to their correct line simultaneously.** A scenario that includes W2
  wages, IRA distributions, qualified dividends, ordinary dividends, long-term capital gains, short-term
  capital gains, and social security income must route each source to the correct line (1a, 3a, 3b, 4b,
  6b, 7) with no source double-counted or omitted.

### 1.2 Schedule D — Capital Gains and Losses
- **Short-term and long-term gains aggregated correctly.** Short-term and long-term gains are summed
  separately for the tax year and combined on line 16.
- **Net gain (line 16 > 0) triggers QD&CG worksheet.** When both line 15 (LTCG) and line 16
  (combined) are positive, the QD&CG worksheet flag is set.
- **Net loss capped at −$3,000.** When combined capital gains are negative the deductible loss is
  clamped to TaxConstants.ScheduleDMaximumCapitalLoss (−$3,000).
- **Zero capital gains: no special worksheet.** When line 16 == 0 and no qualified dividends exist,
  the QD&CG worksheet is not required.
- **Qualified dividends alone trigger QD&CG worksheet.** Even with zero capital gains, the presence of
  any qualified dividends in the ledger must set the worksheet flag.
- **Mixed loss + qualified dividends scenario.** A net capital loss combined with qualified dividends
  should cap the loss at $3,000 and still require the QD&CG worksheet.

### 1.3 Qualified Dividends and Capital Gains Tax Worksheet
- **Income fully within 0% bracket: zero capital gains tax.** When taxable income is low enough that all
  qualified income falls within the 0% bracket, capital gains tax is zero.
- **Income spanning 0% and 15% brackets.** The portion of qualified income exceeding the 0% ceiling is
  taxed at 15%; ordinary income portion is taxed at regular rates.
- **Income spanning 15% and 20% brackets.** Qualified income above $583,750 is taxed at 20%.
- **Ordinary income taxed at regular rates, not capital gains rates.** The worksheet taxes ordinary income
  (line 5) at the regular tax table/worksheet rate; only the preferred-rate income gets the lower rate.
- **Worksheet result cannot exceed regular tax on full income.** The function returns min(worksheet result,
  regular tax on full taxable income), ensuring the preferred-rate path never costs more than plain
  ordinary income rates.
- **Boundary: income exactly at 0% ceiling.** When line 1 exactly equals the 0% bracket max the capital
  gains tax is zero.

### 1.4 Social Security Benefits Worksheet
- **Zero SS income → zero taxable SS.** A TaxLedger with no social security entries produces zero taxable
  SS.
- **Combined income below $32,000 threshold → zero taxable SS.** When combined income (including 50% of
  SS) does not exceed the worksheet line 8 threshold, no SS is taxable.
- **Partial SS taxation in the middle band.** Combined income between $32,000 and $44,000 produces partial
  taxation at the 50% inclusion rate.
- **Full 85% SS taxation at high income.** High combined income causes the maximum 85% of SS benefits to
  be included in taxable income.
- **Result is capped at 85% of gross SS benefit.** The taxable amount can never exceed
  `grossSsIncome * 0.85`.

### 1.5 NC Form D-400
- **Federal AGI is the NC starting point.** NC taxable income starts from the federal adjusted gross
  income passed in from Form 1040.
- **NC standard deduction applied ($25,500).** The NC deduction reduces the NC taxable base.
- **Flat 4.5% rate applied to NC taxable income.** NC tax = max(0, ncTaxableIncome * 0.045).
- **State withholding offsets NC liability.** State withholding recorded in the TaxLedger for the year is
  subtracted; a negative result (refund) is returned.
- **Zero AGI → zero NC tax.** Zero federal AGI produces zero NC tax after applying the deduction.

---

## 2. Payroll Tax Withholding

- **OASDI calculated at 6.2% of annual gross, capped at the annual maximum ($11,439.00 for 2026).**
  Monthly OASDI = min(OasdiBasePercent × annualGross, OasdiMax) / 12. OasdiMax represents the maximum
  annual OASDI contribution (6.2% × $184,500 maximum taxable earnings = $11,439.00); the cap is applied
  to the annual figure before dividing by 12.
- **Standard Medicare calculated at 1.45% of gross.** Monthly Medicare = 0.0145 × annualGross / 12.
- **Additional Medicare 0.9% applies above $250,000 annual salary.** The additional rate applies only to
  the amount of annual salary exceeding $250,000; below the threshold the additional rate is zero.
- **Federal and state withholding pulled from PgPerson and recorded in ledger.** Monthly withholding =
  annual figure / 12; both recorded in FederalWithholdings and StateWithholdings.
- **Total withholding deducted from gross pay.** Net pay = gross − OASDI − Medicare − federal withholding
  − state withholding.
- **Withholdings added to TotalTaxPaidLifetime.** All payroll taxes (including OASDI and Medicare) are
  added to TotalTaxPaidLifetime in the ledger.

---

## 3. Paycheck Processing

### 3.1 Pre-Retirement Paycheck
- **Gross pay = (salary + bonus) / 12.** The gross monthly pay is computed from the annual figures.
- **Pre-tax deductions reduce both net pay and taxable income.** Health insurance + HSA + traditional 401k
  contributions are deducted from gross before computing taxable income and before depositing net pay.
- **Post-tax deductions reduce net pay but not taxable income.** Roth 401k + post-tax insurance reduce the
  deposited net pay but do not lower the W2 income recorded.
- **W2 income recorded = gross − pre-tax deductions.** The amount entered into TaxLedger.W2Income is
  gross pay minus pre-tax deductions only.
- **W2 income written to TaxLedger.W2Income each month.** Each monthly paycheck appends an entry to
  TaxLedger.W2Income. A ledger with no W2 entries for a working year is a test failure.
- **Net pay deposited to cash.** After all deductions and withholdings, the remaining amount is deposited
  to the cash account.
- **Retired person receives no paycheck.** When `PgPerson.IsRetired == true` the function returns
  immediately with no changes.

### 3.2 Payroll-Related Retirement Savings
- **Roth 401k contribution invested as LONG_TERM in ROTH_401_K.** Monthly = Annual401KPostTax / 12,
  invested via the withdrawal strategy.
- **Traditional 401k contribution invested as LONG_TERM in TRADITIONAL_401_K.** Monthly =
  Annual401KPreTax / 12.
- **HSA contribution invested in HSA account.** Monthly = (employee HSA + employer HSA) / 12.
- **Employer match invested in TRADITIONAL_401_K.** Match = (salary × matchPercent) / 12.
- **Bankrupt or retired person: savings are skipped.** No investments are made when IsRetired or
  IsBankrupt is true.

### 3.3 Social Security Check
- **No SS payment before election date.** ProcessSocialSecurityCheck returns immediately without changes
  if currentDate < model.SocialSecurityStart.
- **Monthly SS = annualSocialSecurityWage / 12.** Correct division of annual benefit into monthly
  payment.
- **SS amount deposited to cash.** Cash account increases by the monthly SS amount.
- **SS income recorded in TaxLedger.** Amount appended to SocialSecurityIncome with the current date.

---

## 4. Interest Accrual

### 4.1 Investment Positions
- **LONG_TERM position price updated to CurrentEquityInvestmentPrice.** After accrual, the position's
  Price equals the current equity price; Quantity is unchanged; value changes accordingly.
- **MID_TERM position price updated to CurrentMidTermInvestmentPrice.** Same logic for mid-term
  positions.
- **SHORT_TERM position price updated to CurrentShortTermInvestmentPrice.** Same logic for short-term.
- **Cash and PRIMARY_RESIDENCE accounts are skipped.** These account types pass through unchanged.
- **TotalInvestmentAccrualLifetime accumulates net value change.** The lifetime accumulator increases by
  exactly (newValue − oldValue) for each position accrued.

### 4.2 Debt Positions
- **Monthly interest = balance × (APR / 12).** Interest is calculated and added to the balance each
  month.
- **Closed debt positions are skipped.** A position with IsOpen == false is passed through unchanged.
- **TotalDebtAccrualLifetime accumulates debt interest.**

### 4.3 Mid-Term Quarterly Dividend Reinvestment
- **Dividends only accrue in months 3, 6, 9, 12.** In any other month the function is a no-op and the
  position collection is unchanged. (Models SCHD ETF payout schedule; NodaTime Month is 1-indexed so
  Month == 3 is March.)
- **Dividend amount = CurrentValue × (MidTermAnnualDividendYield / 4).** The quarterly dividend is
  exactly one quarter of the annual yield applied to the current position value.
- **Reinvestment creates a NEW position; the original position is not modified.** The new position has:
  Quantity = dividendAmount / position.Price, Price = current position Price, InitialCost =
  dividendAmount, Entry = currentDate, InvestmentPositionType = same as source, IsOpen = true. The
  original position's Quantity, Price, and InitialCost are unchanged.
- **New DRIP position value equals dividendAmount.** The new position's CurrentValue (Quantity × Price)
  equals the dividend amount exactly; total account value increases by exactly dividendAmount.
- **Taxable brokerage: dividend recorded in DividendsReceived (full) and QualifiedDividendsReceived
  (95%).** Both lists receive an entry for the current date. The 95%/5% split is driven by
  TaxConstants.DividendPercentQualified = 0.95m (SCHD historical qualified percentage).
- **Taxable brokerage: ordinary dividend portion = 5% of total.** (total − qualified) equals 5% of
  the dividend amount.
- **Tax-advantaged accounts (Traditional, Roth, HSA): no TaxLedger entries.** DividendsReceived and
  QualifiedDividendsReceived lists are unchanged for non-taxable accounts.
- **Non-MID_TERM positions are not affected by this function.** LONG_TERM and SHORT_TERM positions pass
  through unchanged. Dividends are intentionally limited to MID_TERM positions only.
- **Cash and PRIMARY_RESIDENCE accounts are skipped entirely.**
- **Multiple MID_TERM positions in the same account all receive dividends.** Each position is processed
  independently; each generates its own new DRIP position.

---

## 5. Debt Paydown

- **Monthly payment = min(MonthlyPayment, CurrentBalance) per position.** A loan is never overpaid —
  if the scheduled payment exceeds the remaining balance, only the balance is paid.
- **Total cash withdrawn = total across all open DEBT position PAYMENTS MADE THIS MONTH.** The cash
  deducted from the account equals the sum of each open debt position's payment for this month only;
  closed positions contribute zero.
- **Insufficient cash: attempt to liquidate investment assets before declaring failure.** When the cash
  account cannot cover total debt payments, the simulation should attempt to liquidate non-real-estate
  investment positions (following the cascade order in §13) before returning isSuccessful = false.
  Note: current code returns isSuccessful=false without liquidating — this behavior requires a code
  change and the test for it is expected to fail until that change is made.
- **If liquidation also fails: isSuccessful = false, accounts unchanged.** Only if neither cash nor any
  liquidatable investment can cover the total payment should the function signal bankruptcy.
- **Position balance reduced by payment amount.** After a successful payment, CurrentBalance decreases
  by exactly the amount paid.
- **Balance reaching zero closes the position.** When CurrentBalance drops to or below zero, IsOpen is
  set to false and balance is clamped to zero.
- **Closed positions are skipped.** Positions with IsOpen == false are not included in paydown amounts.
- **Internal accounting check: total debited = total credited (within $1).** The implementation verifies
  that cash withdrawn equals total credited to debt positions; a mismatch throws.
- **Net worth is conserved by debt paydown.** Total net worth (investment assets − total debt) before
  paydown equals total net worth after, because cash decreases and outstanding debt decreases by the
  same amount. Tolerance: within floating-point rounding.

---

## 6. Investment Sales

### 6.1 Core Sale Logic
- **Positions sold in the specified type/account order.** Given a sales order, positions belonging to
  earlier-ranked (account, position) pairs are sold before later-ranked ones.
- **Sale stops when amountToSell is reached.** The function does not sell more than requested even if
  additional positions remain.
- **Partial position sale: Quantity and InitialCost proportionally reduced.**
  sharesBeingSold = saleAmount ÷ Price; averageCostPerShare = InitialCost ÷ Quantity (both measured
  before the sale); costOfSharesSold = averageCostPerShare × sharesBeingSold.
  Quantity decreases by sharesBeingSold; InitialCost decreases by costOfSharesSold.
- **Full position sale: IsOpen set to false, quantity set to zero.**
- **Capital gain for taxable brokerage = saleAmount − costOfSharesSold.** For a full sale, costOfSharesSold
  equals the position's full InitialCost. For a partial sale, it is the proportional cost as computed
  above. This formula applies to both long-term and short-term gains.
- **Taxable brokerage: long-term capital gains for positions held > 1 year.** Gain recorded in
  LongTermCapitalGains.
- **Taxable brokerage: short-term capital gains for positions held ≤ 1 year.** Gain recorded in
  ShortTermCapitalGains.
- **Sale proceeds deposited to cash.** The amount sold is added to the cash account.
- **After any sale: cash balance increases by saleAmount; total investment value decreases by the same
  amount.** The sum of all cash positions increases by exactly saleAmount; the sum of all open
  investment position values decreases by exactly saleAmount. Net worth is conserved within
  floating-point tolerance.
- **Roth 401k / Roth IRA / HSA sales recorded as tax-free withdrawals.** No capital gains or ordinary
  income recorded for these account types.
- **Traditional 401k / Traditional IRA sales recorded as IRA distributions, not wages.** Full sale
  amount recorded in TaxableIraDistribution (flows to Form 1040 line 4b). Nothing is added to
  W2Income (line 1a) for these account types.
- **Date filter (minDateExclusive / maxDateInclusive) respected.** Positions outside the date range are
  excluded from the sale query.
- **Cash and PRIMARY_RESIDENCE accounts cannot be sold.** Attempting to sell these account types throws.
- **Net worth is conserved by investment sales.** Total net worth before a sale equals total net worth
  after (cash increases by saleAmount, investment value decreases by saleAmount, net is zero).

### 6.2 Sales Order Helpers
- **CreateSalesOrderAccountTypeFirst: outer loop is account types.** For 3 account types and 2 position
  types the result has 6 entries with account type changing every 2 entries.
- **CreateSalesOrderPositionTypeFirst: outer loop is position types.** Position type changes every N
  account-type entries.

---

## 7. Rebalancing

### 7.1 Timing
- **MONTHLY frequency: always returns true.** Every month is a rebalance month.
- **QUARTERLY frequency: runs in months 1, 4, 7, 10 (zero-indexed mod 3 == 0).** Runs in January,
  April, July, October (months 0, 3, 6, 9 when zero-indexed).
- **YEARLY frequency: runs only in January (month 1, zero-indexed mod 12 == 0).**
- **Pre-retirement: no rebalancing until NumMonthsPriorToRetirementToBeginRebalance before retirement.**
  When currentDate is more than the threshold before retirement, rebalance returns immediately.

### 7.2 Basic Buckets — Cash Top-Up
- **In recession: mid-term positions are sold first to top up cash.** Long-term positions are not
  touched during a recession cash top-up.
- **Not in recession: long-term sold first, then mid-term if still short.** Long-term takes priority
  during non-recession cash top-ups.
- **No cash sold if cash balance already meets the N-month requirement.** The function is a no-op if
  current cash already satisfies the reserve.

### 7.3 Basic Buckets — Long-to-Mid Top-Up
- **In recession: no long-to-mid movement.** Long-term positions are preserved during recessions.
- **Not in recession: first try tax-deferred internal conversion.** Moving within traditional/Roth
  accounts is preferred because it has no tax consequence.
- **Tax-deferred conversion preserves position value exactly.** After moving, oldValue ≈ newValue
  (rounded to 4 decimal places).
- **Partial conversion creates two positions.** When only part of a position needs converting, the
  original is reduced and a new keep position of the source type is created.
- **If tax-deferred conversion is insufficient, taxable brokerage long positions are sold and mid
  positions are bought.** The proceeds go through cash first.
- **Mid-term target already met: no movement.** If the mid-bucket already has enough for N months, no
  assets are moved.

### 7.4 Income Threshold Withdrawal Order
- **With income room: traditional accounts sold first, up to income room.** The function prioritizes
  tax-deferred accounts (traditional 401k, traditional IRA) when income room exists.
- **No income room: tax-free accounts (HSA, Roth) sold first, then taxable, then traditional.**
- **Account type override bypasses the income room logic.** When accountTypeOverride is specified, only
  that account type is used regardless of income room.
- **Position type override limits which position types are sold.** When positionTypeOverride is
  specified, only that position type is eligible.

### 7.5 Universal Net Worth Conservation
- **Net worth is conserved by every rebalancing strategy.** Regardless of which withdrawal strategy
  or bucket rebalance is executed (cash top-up, long-to-mid conversion, income threshold withdrawal,
  or any combination), total net worth before the operation equals total net worth after. The
  operation reclassifies or moves assets; it does not create or destroy value. Tolerance: within
  floating-point rounding.

---

## 8. Account Cleanup / Consolidation

- **Tax-free accounts (Roth 401k, Roth IRA, HSA) consolidated into a single Roth IRA.** Total
  LONG_TERM value and total MID_TERM value across all three account types are merged into two positions.
- **Tax-deferred accounts (Traditional 401k, Traditional IRA) consolidated into a single Traditional
  IRA.** Same consolidation logic for long and mid positions.
- **Consolidation preserves total value.** Sum of all open positions before cleanup equals sum after
  (within floating-point tolerance).
- **Taxable brokerage: long-held (>1yr) positions consolidated, short-held preserved individually.**
  Two synthetic positions (one LONG_TERM, one MID_TERM) represent the combined long-held holdings;
  positions entered within the last year remain as separate entries.
- **Taxable brokerage: cost basis preserved in consolidated positions.** Total InitialCost of original
  long-held positions = InitialCost of the consolidated position.
- **Closed debt positions removed.** After cleanup, no debt account contains a position with
  IsOpen == false.
- **Primary residence: copied unchanged.** If present, it appears in the output unchanged.
- **No primary residence: not added.** If no PRIMARY_RESIDENCE account exists, none is created.
- **Net worth is conserved by account cleanup.** Total net worth before consolidation equals total
  net worth after. Consolidating positions changes their count and grouping but not their aggregate
  value. Tolerance: within floating-point rounding.

---

## 9. Account Calculations

- **CalculateCashBalance: sums open positions in the Cash account only.** Closed positions contribute
  zero.
- **CalculateInvestmentAccountTotalValue: sums open positions in one account.** Closed positions
  excluded.
- **CalculateLongBucketTotalBalance: sums LONG_TERM positions across all non-cash, non-residence
  accounts.**
- **CalculateMidBucketTotalBalance: sums MID_TERM positions across eligible accounts.**
- **CalculateNetWorth: total investment assets (excluding primary residence) minus total debt.**
  Primary residence is explicitly excluded from assets.
- **CalculateTotalBalanceByMultipleFactors: filters by account type, position type, and/or date range.**
  Null parameters mean "all".
- **CalculateDebtPaydownAmounts: min(MonthlyPayment, balance) per open position.** Returns a dictionary
  keyed by position Id.
- **CalculateDebtTotal: sums CurrentBalance across all open debt positions.**

---

## 10. Spend / Fun Points / Healthcare

### 10.1 Fun Points
- **$1 = 1 fun point at age 50; $1 = 0.5 fun points at age 90.** The penalty is linear between ages
  50 and 90 at 0.0125 per year.
- **Fun points capped at the spend amount (no bonus below age 50).** Being younger than 50 does not
  yield more than 1:1.
- **Fun points floored at 0.5× spend (no further penalty above age 90).** The penalty does not worsen
  beyond age 90.

### 10.2 Monthly Fun Spend
- **Pre-retirement: DesiredMonthlySpendPreRetirement from model.** Full fun spend during working years.
- **Retirement to age 65: DesiredMonthlySpendPostRetirement from model.** Early-retirement fun spend.
- **Age 66–88: linear decline from full post-retirement spend to $0.**
- **Age 88+: $0 fun spend.** Assumed facility-based care with no discretionary spending.

### 10.3 Monthly Healthcare Spend
- **Pre-retirement: $0.** Employer coverage assumed; no personal cost modeled.
- **Retired and under 65: full personal premium cost.** Pre-Medicare personal insurance.
- **Age 65–88: Medicare A + B + D costs.** Includes hospital deductible (amortized), Part B premium +
  deductible, Part D premium + drug costs (~$620–800/month combined).
- **Age 88+: assisted living — healthcare costs double.** 2× the Medicare-phase spend.
- **Hospital admissions increase with age.** Base rate + 1 additional admission per decade over 65.

### 10.4 Spending Multipliers (Recession / Austerity / Living Large)
- **In recession: spending multiplied by AusterityRatio (typically 0.6–0.8).** Required and fun spend
  are scaled down during a recession.
- **In extreme austerity: spending multiplied by ExtremeAusterityRatio (typically 0.2–0.4).** Triggered
  when net worth falls below ExtremeAusterityNetWorthTrigger.
- **Extreme austerity persists for 12 months after net worth recovers.** Does not immediately exit
  when net worth exceeds trigger.
- **Living large: spending multiplied by LivinLargeRatio (typically 1.2–1.4).** Triggered when net
  worth exceeds LivinLargeNetWorthTrigger.
- **CalculateCashNeedForNMonths: sums monthly required + fun spend for each of N future months.**
  Each month's figure accounts for the person's age at that future date.

---

## 11. Recession Detection

- **Recession entry: current equity price < price N months ago (RecessionCheckLookBackMonths).**
  The lookback is a model parameter (typically 12–24 months).
- **Recession exit: equity price recovers to (trough price × RecessionRecoveryPointModifier).**
  The modifier (typically 0.8–1.0) prevents whipsaw; a modifier of 0.8 means a 20% recovery cushion
  from the trough is required.
- **Extreme austerity entry: net worth ≤ ExtremeAusterityNetWorthTrigger.**
- **Extreme austerity exit: after 12 consecutive months above the trigger.** The 12-month lag is
  hardcoded; it does not exit on the first month net worth recovers.
- **Living large: net worth ≥ LivinLargeNetWorthTrigger.** Simple threshold, no lag.
- **Recession duration tracked in months.** The duration counter increments each month the recession
  flag is true.

---

## 12. Social Security Claiming Age Adjustments

- **Claiming at full retirement age (67): no adjustment.** Monthly benefit = AnnualSocialSecurityWage
  / 12 with no penalty or credit.
- **Claiming early (age 62–67): permanent monthly penalty.** First 36 months early: 5/9 of 1% per
  month reduction. Each additional month early (months 37–60): 5/12 of 1% per month reduction.
- **Claiming late (age 67–70): permanent monthly credit.** 8/12 of 1% per month for up to 36 months
  late (maximum credit stops at age 70).
- **Earliest possible claiming age: 62 years + 1 month.**
- **Latest meaningful claiming age: 70.** No additional credit accrues after 70.
- **Months-early and months-late are bounded.** Inputs outside [0, 60 months early] or [0, 36 months
  late] are invalid.

---

## 13. Cash Withdrawal Cascade (AccountCashManagement.WithdrawCash)

When cash is insufficient to meet a withdrawal and investment liquidation is required, the cascade
attempts sales in the following order, stopping as soon as enough cash is raised:

1. **MID_TERM positions held > 1 year** (long-term capital gains — lowest tax cost).
2. **LONG_TERM positions held > 1 year** (long-term capital gains).
3. **MID_TERM positions held ≤ 1 year** (short-term capital gains — higher tax cost).
4. **Any position, any date** (last resort; worst-case tax consequences).

**Testable outcomes:**
- **Sufficient cash: no investments sold.** If cash covers the withdrawal, no positions are touched.
- **First-tier sufficient: only long-held mid-term positions sold.** If tier 1 covers the need, tiers
  2–4 are not reached.
- **Bankruptcy: none of the four tiers can cover the withdrawal.** Returns isSuccessful = false with
  accounts unmodified.
- **Each tier correctly records capital gain type.** Long-held sales record in LongTermCapitalGains;
  short-held record in ShortTermCapitalGains.

---

## 14. VAR Pricing / Growth Rate Generation

- **Equity price grows multiplicatively.** `newEquityPrice = old × (1 + SpGrowth)`.
- **Mid-term price grows at MidTermGrowthRateModifier (0.5) of equity growth, multiplicatively.**
  `newMidPrice = old × (1 + SpGrowth × 0.5)`.
- **Short-term price unchanged (ShortTermGrowthRateModifier = 0).** Short-term investments have no
  price movement.
- **Treasury coupon updated additively.** `newCoupon = old + TreasuryGrowth`. TreasuryGrowth is an
  absolute decimal delta (e.g., 0.001 = a 0.1 pp move), not a percentage change.
- **Same lifeIndex always produces the same sequence.** VarLifetimeGenerator is seeded with
  `new Random(lifeIndex)`; two calls with the same index and model produce identical results.
- **Treasury rate bounded between 0.1% and 20%.** The generator clamps the running rate level within
  [0.001, 0.20] regardless of the VAR shock.
- **OU mean reversion pulls treasury rate toward TreasuryOuTheta.** The corrected delta is larger in
  magnitude than the raw VAR delta when the current rate is far from theta.
- **CopyPrices propagates CurrentTreasuryCoupon.** The copied struct contains the same coupon value
  as the original (regression guard against the additive-treasury bug fixed 2026-02-19).

---

## 15. Income Room Calculation

- **Baseline: income room = 12%-bracket ceiling + standard deduction.** Starting point is $94,300 +
  $29,200 = $123,500 before any reductions.
- **W2 income, IRA distributions, interest, and non-qualified dividends all reduce room.** Each income
  source consumes available room.
- **Income room cannot go below zero.** `Math.Max(headRoom, 0)` enforced.
- **In SS election year: only partial-year SS projected.** Months from election start to Dec 31 are
  counted; prior months use zero SS.
- **Before SS election: zero SS projection.** SS income is excluded entirely if the election date has
  not been reached.
- **After SS election year: full 12-month SS projection.** `monthly × 12` used for years after the
  election year.

---

## Open Ambiguities

No open ambiguities at this time. All design questions have been resolved.
