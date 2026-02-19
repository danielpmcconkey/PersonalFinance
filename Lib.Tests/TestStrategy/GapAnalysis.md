# Test Gap Analysis

Generated: 2026-02-19
Source: All outcomes from BusinessOutcomes.md classified against the full test suite.

Classification key:
- **(A) Covered** — at least one existing test directly exercises this outcome with a meaningful assertion
- **(B) Partial** — a test touches this area but has a specific gap (gap described in Notes)
- **(C) Not covered** — no existing test covers this outcome

---

## §1. Federal Tax Calculation

### §1.1 Form 1040

| Outcome | Class | Notes |
|---|---|---|
| W2 income flows to AGI | A | Form1040Tests: `CalculateTaxLiability_VariousScenarios_CalculatesCorrectly` (setup 2, W2-only); SimulationTests: `ProcessPayday_PreRetirement_ProcessesCorrectly` asserts reportedW2 |
| Standard deduction applied correctly | B | End-to-end result implicitly depends on the deduction. No test isolates it or asserts the $29,200 constant is applied before flooring at zero. |
| IRA distributions flow to line 4b — not line 1a | B | TaxTests: `RecordTaxableIraDistribution_AddsDistributionCorrectly` confirms ledger entry lands in TaxableIraDistribution. No test explicitly asserts the distribution appears on line 4b and is absent from line 1a. |
| Qualified dividends pulled to line 3a | B | Form1040Tests setup 1 passes qualified dividends; end-to-end liability is asserted, but line 3a value is never checked in isolation. |
| Total dividends pulled to line 3b | B | Same as line 3a — exercised in setup 1 but no explicit assertion on line 3b value. |
| Capital gains from Schedule D flow to line 7 | B | Form1040Tests includes gains in several scenarios; end-to-end liability is correct, but line 7 value is never asserted independently. |
| Tax routing: table vs. worksheet vs. QD&CG worksheet | B | The three paths are each tested individually (TaxTableTests, TaxComputationWorksheetTests, QualifiedDividendsAndCapitalGainTaxWorksheetTests), but no test asserts which routing branch is selected for a given input (e.g., verifying that income < $100k uses the table rather than the worksheet). |
| Federal withholding offsets liability | A | Form1040Tests: `CalculateTaxLiability_VariousScenarios_CalculatesCorrectly` setup 3 (`federalWithholdings=40000`, result reflects refund). |
| Social Security income drives line 6b | B | SS worksheet is fully tested. Form1040Tests includes SS income; end-to-end result is asserted, but line 6b is never verified independently. |
| Zero-income scenario produces zero liability | A | Form1040Tests: `CalculateTaxLiability_VariousScenarios_CalculatesCorrectly` ("broke" inline: all income zero except SS=$32k, liability=0). |
| All major income sources route to correct lines simultaneously | B | Form1040Tests setup 1 passes W2, SS, dividends, qualified dividends, withholding, and capital gains together and checks total liability. A routing error that cancels out in the total would go undetected; no per-line assertion exists. |

### §1.2 Schedule D

| Outcome | Class | Notes |
|---|---|---|
| Short-term and long-term gains aggregated correctly | C | ScheduleDTests.cs contains only commented-out tests. Form1040Tests exercises Schedule D implicitly but has no active unit test that directly calls Schedule D methods and asserts aggregation. |
| Net gain (line 16 > 0) triggers QD&CG worksheet | C | No active test. Form1040Tests exercises this path implicitly (setups 1 and 2 have net gains) but no test verifies the routing decision. |
| Net loss capped at −$3,000 | C | No active test. ScheduleDTests commented out. Form1040Tests setup 2 (`shortTermCapitalGains=-8000`) happens to reflect the cap in the final liability, but no assertion on the capped Schedule D line. |
| Zero capital gains: no special worksheet | C | No active test. |
| Qualified dividends alone trigger QD&CG worksheet | C | No active test. Form1040Tests setup 2 has qualified dividends with a capital loss but does not assert worksheet selection. |
| Mixed loss + qualified dividends scenario | C | No active test. |

### §1.3 Qualified Dividends and Capital Gains Tax Worksheet

| Outcome | Class | Notes |
|---|---|---|
| Income fully within 0% bracket: zero capital gains tax | A | QualifiedDividendsAndCapitalGainTaxWorksheetTests: `CalculateTaxOwed_VariousScenarios` scenario `(35000,125,185000,215044,0)`. |
| Income spanning 0% and 15% brackets | A | QualifiedDividendsAndCapitalGainTaxWorksheetTests: `CalculateTaxOwed_VariousScenarios` scenarios spanning the 0%/15% boundary. |
| Income spanning 15% and 20% brackets | A | QualifiedDividendsAndCapitalGainTaxWorksheetTests: `CalculateTaxOwed_VariousScenarios` scenario `(403000,125,5000,6000,83461.75)`. |
| Ordinary income taxed at regular rates, not capital gains rates | A | QualifiedDividendsAndCapitalGainTaxWorksheetTests: `CalculateTaxOwed_VariousScenarios` scenario `(308721,125,-1123,-3000,60166.79)` — nearly all tax from ordinary rates. |
| Worksheet result cannot exceed regular tax on full income | C | No test computes regular tax on full income and asserts the worksheet result is ≤ that amount. |
| Boundary: income exactly at 0% ceiling | C | No scenario places taxable income precisely at the $94,050 0%/15% boundary. |

### §1.4 Social Security Benefits Worksheet

| Outcome | Class | Notes |
|---|---|---|
| Zero SS income → zero taxable SS | A | SocialSecurityBenefitsWorksheetTests: `CalculateLine1SocialSecurityIncome_WithNoIncome_ReturnsZero`; `CalculateTaxableSocialSecurityBenefits_EdgeCases`. |
| Combined income below $32,000 → zero taxable SS | A | SocialSecurityBenefitsWorksheetTests: `CalculateTaxableSocialSecurityBenefits_VariousScenarios` low-income rows. |
| Partial SS taxation in the middle band | A | SocialSecurityBenefitsWorksheetTests: `CalculateTaxableSocialSecurityBenefits_VariousScenarios` row `(1525.88, 30517.58, 305.18, 3989.02)`. |
| Full 85% SS taxation at high income | A | SocialSecurityBenefitsWorksheetTests: `CalculateTaxableSocialSecurityBenefits_VariousScenarios` high-income rows. |
| Result is capped at 85% of gross SS benefit | A | SocialSecurityBenefitsWorksheetTests: `CalculateTaxableSocialSecurityBenefits_MaximumTaxableAmount` asserts `result == socialSecurityBenefits * MaxSocialSecurityTaxPercent`. |

### §1.5 NC Form D-400

| Outcome | Class | Notes |
|---|---|---|
| Federal AGI is the NC starting point | B | TaxCalculationTests: `CalculateNorthCarolinaTaxLiabilityForYear_CalculatesCorrectTax` passes AGI directly; no test isolates the link from Form1040.AdjustedGrossIncome to the NC starting value. |
| NC standard deduction applied ($25,500) | B | Results consistent with the $25,500 deduction, but no test explicitly asserts the constant or verifies it is applied before the floor. |
| Flat 4.5% rate applied to NC taxable income | A | TaxCalculationTests: `CalculateNorthCarolinaTaxLiabilityForYear_CalculatesCorrectTax` — arithmetic verified (e.g., `(62500-25500) × 0.045 - 20 withholding = 1645`). |
| State withholding offsets NC liability | A | TaxCalculationTests: `CalculateNorthCarolinaTaxLiabilityForYear_CalculatesCorrectTax` — withholding subtracted in every scenario. |
| Zero AGI → zero NC tax | A | TaxCalculationTests: `CalculateNorthCarolinaTaxLiabilityForYear_CalculatesCorrectTax` — income below standard deduction yields net refund of withholding. |

---

## §2. Payroll Tax Withholding

| Outcome | Class | Notes |
|---|---|---|
| OASDI at 6.2%, capped at OasdiMax | B | SimulationTests: `ProcessPayday_PreRetirement_ProcessesCorrectly` verifies the 6.2% rate. The test intentionally keeps salary below OasdiMax (comment states this); the cap is never exercised. |
| Standard Medicare at 1.45% | A | SimulationTests: `ProcessPayday_PreRetirement_ProcessesCorrectly` — `StandardMedicareTaxRate * grossMonthlyPay` included in withholdings assertion. |
| Additional Medicare 0.9% above $250,000 | C | No test exercises a salary above $250,000 to verify the surcharge. |
| Federal and state withholding recorded in ledger | A | SimulationTests: `ProcessPayday_PreRetirement_ProcessesCorrectly` asserts `reportedFederalWithholding` and `reportedStateWithholding`. |
| Total withholding deducted from gross pay | A | SimulationTests: `ProcessPayday_PreRetirement_ProcessesCorrectly` asserts `actualCash == gross - tax - preTax - postTax`. |
| Withholdings added to TotalTaxPaidLifetime | A | SimulationTests: `ProcessPayday_PreRetirement_ProcessesCorrectly` asserts `reportedTaxesPaid == expectedTaxesPaid`. |

---

## §3. Paycheck Processing

### §3.1 Pre-Retirement Paycheck

| Outcome | Class | Notes |
|---|---|---|
| Gross pay = (salary + bonus) / 12 | A | SimulationTests: `ProcessPayday_PreRetirement_ProcessesCorrectly`. |
| Pre-tax deductions reduce net pay and taxable income | A | SimulationTests: `ProcessPayday_PreRetirement_ProcessesCorrectly` — preTaxDeductions subtracted from both `expectedNetIncome` and `expectedW2Income`; both asserted. |
| Post-tax deductions reduce net pay but not taxable income | A | SimulationTests: `ProcessPayday_PreRetirement_ProcessesCorrectly` — postTaxDeductions subtracted from `expectedNetIncome` only; both assertions verified. |
| W2 income recorded = gross − pre-tax deductions | A | SimulationTests: `ProcessPayday_PreRetirement_ProcessesCorrectly` — `expectedW2Income = grossMonthlyPay - preTaxDeductions`; asserted. |
| W2 income written to TaxLedger.W2Income each month | A | SimulationTests: `ProcessPayday_PreRetirement_ProcessesCorrectly` — `ledger.W2Income.Sum(x => x.amount)` asserted; TaxTests: `RecordW2Income_AddsIncomeCorrectly`. |
| Net pay deposited to cash | A | SimulationTests: `ProcessPayday_PreRetirement_ProcessesCorrectly` — `Assert.Equal(expectedNetIncome, actualCash)`. |
| Retired person receives no paycheck | A | SimulationTests: `ProcessPayday_PostRetirementWithSocialSecurity_ProcessesCorrectly` — `expectedW2Income = 0` asserted. |

### §3.2 Payroll-Related Retirement Savings

| Outcome | Class | Notes |
|---|---|---|
| Roth 401k contribution → LONG_TERM in ROTH_401_K | A | SimulationTests: `ProcessPayday_PreRetirement_ProcessesCorrectly` — `accounts.Roth401K.Positions.Sum(x => x.CurrentValue)` asserted. |
| Traditional 401k contribution → LONG_TERM in TRADITIONAL_401_K | A | SimulationTests: `ProcessPayday_PreRetirement_ProcessesCorrectly` — includes employer match. |
| HSA contribution → HSA account | A | SimulationTests: `ProcessPayday_PreRetirement_ProcessesCorrectly` — `accounts.Hsa.Positions.Sum(x => x.CurrentValue)` asserted. |
| Employer match → TRADITIONAL_401_K | A | SimulationTests: `ProcessPayday_PreRetirement_ProcessesCorrectly` — match amount included in `expected401KTraditional` and asserted. |
| Bankrupt or retired person: savings skipped | B | SimulationTests: `ProcessPayday_PostRetirementWithSocialSecurity_ProcessesCorrectly` checks W2=0 for retired person but does not assert 401k/HSA positions remain empty. No bankrupt scenario exists. |

### §3.3 Social Security Check

| Outcome | Class | Notes |
|---|---|---|
| No SS payment before election date | B | `ProcessPayday_PreRetirementWithSocialSecurity_ProcessesBoth` confirms SS is recorded when date has passed, but no test makes a positive assertion that `SocialSecurityIncome` is empty before the election date. |
| Monthly SS = annualSocialSecurityWage / 12 | A | SimulationTests: `ProcessPayday_PostRetirementWithSocialSecurity_ProcessesCorrectly` asserts `expectedSsIncome = person.AnnualSocialSecurityWage / 12`. |
| SS amount deposited to cash | A | SimulationTests: `ProcessPayday_PostRetirementWithSocialSecurity_ProcessesCorrectly` — cash equals the SS monthly amount. |
| SS income recorded in TaxLedger | A | SimulationTests: `ProcessPayday_PostRetirementWithSocialSecurity_ProcessesCorrectly`; TaxTests: `RecordSocialSecurityIncome_AddsIncomeCorrectly`. |

---

## §4. Interest Accrual

### §4.1 Investment Positions

| Outcome | Class | Notes |
|---|---|---|
| LONG_TERM price updated to CurrentEquityInvestmentPrice | A | AccountInterestAccrualTests: `AccrueInterest_LongTermInvestment_UpdatesPriceCorrectly`. |
| MID_TERM price updated to CurrentMidTermInvestmentPrice | A | AccountInterestAccrualTests: `AccrueInterest_MidTermInvestment_UpdatesPriceCorrectly`. |
| SHORT_TERM price updated to CurrentShortTermInvestmentPrice | A | AccountInterestAccrualTests: `AccrueInterest_ShortTermInvestment_UpdatesPriceCorrectly`. |
| Cash and PRIMARY_RESIDENCE accounts skipped | A | AccountInterestAccrualTests: `AccrueInterest_SkipsCashAndPrimaryResidence`. |
| TotalInvestmentAccrualLifetime accumulates net value change | A | AccountInterestAccrualTests: `AccrueInterest_UpdatesLifetimeSpend`; `AccrueInterest_InDebugMode_UpdatesLifetimeSpend`. |

### §4.2 Debt Positions

| Outcome | Class | Notes |
|---|---|---|
| Monthly interest = balance × (APR / 12) | A | AccountInterestAccrualTests: `AccrueInterestOnDebtPosition_CalculatesInterestCorrectly` — 1000 × (0.12/12) = 10; balance becomes 1010. |
| Closed debt positions skipped | A | AccountInterestAccrualTests: `AccrueInterestOnDebtPosition_DoesNothingForClosedPosition`. |
| TotalDebtAccrualLifetime accumulates debt interest | A | AccountInterestAccrualTests: `AccrueInterestOnDebtPosition_UpdatesLifetimeSpendCorrectly`. |

### §4.3 Mid-Term Quarterly Dividend Reinvestment

| Outcome | Class | Notes |
|---|---|---|
| Dividends only accrue in months 3, 6, 9, 12 | C | No test exercises `AccrueMidTermDividends`. |
| Dividend amount = CurrentValue × (MidTermAnnualDividendYield / 4) | C | Not tested. |
| Reinvestment creates a NEW position; original unchanged | C | Not tested. This is also a known code bug (current code updates Quantity on existing position). |
| New DRIP position value = dividendAmount | C | Not tested. |
| Taxable brokerage: dividend recorded in DividendsReceived (full) and QualifiedDividendsReceived (95%) | C | Not tested. |
| Taxable brokerage: ordinary dividend portion = 5% of total | C | Not tested. |
| Tax-advantaged accounts: no TaxLedger entries | C | Not tested. |
| Non-MID_TERM positions not affected | C | Not tested. |
| Cash and PRIMARY_RESIDENCE skipped | C | `AccrueInterest_SkipsCashAndPrimaryResidence` tests price-update skipping, not dividend skipping specifically. |
| Multiple MID_TERM positions in same account all receive dividends | C | Not tested. |

---

## §5. Debt Paydown

| Outcome | Class | Notes |
|---|---|---|
| Monthly payment = min(MonthlyPayment, CurrentBalance) per position | B | AccountDebtPaymentTests: `CreditDebtPosition_WithValidPayment_ReducesBalance` (200 against 1000) and `CreditDebtPosition_WithFullPayment_ClosesPosition` (exact full payment). No test has `MonthlyPayment > CurrentBalance` to verify the min() clip. |
| Total cash withdrawn = sum of open DEBT position payments this month | A | AccountDebtPaymentTests: `PayDownLoans_WithSufficientFunds_SuccessfullyPaysLoans` — cash decremented by payment; `CreditDebtAccount_WithMultiplePositions_CreditsCorrectly` asserts `totalCredited == 1500` for two positions. |
| Insufficient cash: attempt to liquidate investments before failure | C | AccountDebtPaymentTests: `PayDownLoans_WithInsufficientFunds_ReturnsFalse` jumps straight to failure. No test exercises the intermediate liquidation-attempt path. (Also a known code gap: current code does not liquidate.) |
| If liquidation fails: isSuccessful = false, accounts unchanged | B | `PayDownLoans_WithInsufficientFunds_ReturnsFalse` asserts `isSuccessful == false`. Accounts-unchanged assertion is absent; liquidation path is untested. |
| Position balance reduced by payment amount | A | AccountDebtPaymentTests: `CreditDebtPosition_WithValidPayment_ReducesBalance` — 1000 → 800. |
| Balance reaching zero closes the position | A | AccountDebtPaymentTests: `CreditDebtPosition_WithFullPayment_ClosesPosition`. |
| Closed positions skipped | A | AccountDebtPaymentTests: `CreditDebtPosition_WithClosedPosition_ReturnsUnchanged`. |
| Internal accounting check: debited = credited (within $1) | C | No test verifies this reconciliation invariant. |
| Net worth conserved by debt paydown | C | No test asserts net worth before and after `PayDownLoans` is equal. |

---

## §6. Investment Sales

### §6.1 Core Sale Logic

| Outcome | Class | Notes |
|---|---|---|
| Positions sold in specified type/account order | A | InvestmentSalesTests: `SellInvestmentsToDollarAmount_WithSufficientPositions_SellsInCorrectOrder`; `SellInvestmentsToDollarAmount_WithVarryingAccountTypes_SellsInCorrectOrder`. |
| Sale stops when amountToSell is reached | A | InvestmentSalesTests: `SellInvestmentsToDollarAmount_WithSufficientFundsInOnePosition_SellsCorrectAmount`; InvestmentSalesTestsExtended: `SellInvestmentsToDollarAmount_WithSufficientInvestments_SellsCorrectAmount`. |
| Partial sale: Quantity and InitialCost proportionally reduced | B | InvestmentSalesTestsExtended: `SellInvestmentsToDollarAmount_PartialSale_UpdatesPositionCorrectly` checks `IsOpen`, `CurrentValue`, and `Quantity > 0`, but does not assert that `InitialCost` decreases by exactly `averageCostPerShare × sharesBeingSold`. |
| Full sale: IsOpen = false, quantity = 0 | A | InvestmentSalesTestsExtended: `SellInvestmentsToDollarAmount_FullSale_ClosesPosition`. |
| Capital gain = saleAmount − costOfSharesSold | B | InvestmentSalesTests: `SellInvestmentsToDollarAmount_WithTaxablePosition_SellsAndUpdatesTaxLedgerLongTermCapitalGains` uses a position where cost = 50% of price, implicitly verifying the formula. No test uses an arbitrary cost/price combination to verify the per-share calculation explicitly. |
| Long-term gains (held > 1 yr) → LongTermCapitalGains | A | InvestmentSalesTests: `SellInvestmentsToDollarAmount_WithTaxablePosition_SellsAndUpdatesTaxLedgerLongTermCapitalGains`. |
| Short-term gains (held ≤ 1 yr) → ShortTermCapitalGains | A | InvestmentSalesTests: `SellInvestmentsToDollarAmount_WithTaxablePosition_SellsAndUpdatesTaxLedgerShortTermCapitalGains`. |
| Sale proceeds deposited to cash | A | InvestmentSalesTests: `SellInvestmentsToDollarAmount_WithValidPosition_SellsAndUpdatesCash`. |
| Cash increases by saleAmount; investment value decreases by same | A | InvestmentSalesTests: `SellInvestmentsToDollarAmount_WithValidPosition_SellsAndUpdatesCash` — both sides asserted. |
| Roth 401k / Roth IRA / HSA → tax-free withdrawals | B | InvestmentSalesTests: `SellInvestmentsToDollarAmount_WithTaxFreePosition_SellsAndDoesntUpdateTaxLedger` (HSA); InvestmentSalesTestsExtended: `SellInvestmentsToDollarAmount_WithRothAccount_RecordsTaxFreeWithdrawal` (Roth IRA, checks `Count > 0` only). No test asserts the recorded amount equals proceeds; Roth 401k not individually tested. |
| Traditional 401k / IRA → TaxableIraDistribution, not W2Income | A | InvestmentSalesTests: `SellInvestmentsToDollarAmount_WithTaxDeferredPosition_SellsAndUpdatesTaxLedgerDistribution`. |
| Date filter respected | A | InvestmentSalesTestsExtended: `SellInvestmentsToDollarAmount_WithDateFilters_RespectsDateConstraints`. |
| Cash and PRIMARY_RESIDENCE accounts cannot be sold | C | No test attempts a sale from CASH or PRIMARY_RESIDENCE and verifies it is blocked. |
| Net worth conserved by investment sales | A | InvestmentSalesTests: `SellInvestmentsToDollarAmount_NetWorthStaysFlat`. |

### §6.2 Sales Order Helpers

| Outcome | Class | Notes |
|---|---|---|
| CreateSalesOrderAccountTypeFirst: outer loop is account types | A | InvestmentSalesTestsExtended: `CreateSalesOrderAccountTypeFirst_WithValidInputs_CreatesCorrectOrder`. |
| CreateSalesOrderPositionTypeFirst: outer loop is position types | A | InvestmentSalesTestsExtended: `CreateSalesOrderPositionTypeFirst_WithValidInputs_CreatesCorrectOrder`. |

---

## §7. Rebalancing

### §7.1 Timing

| Outcome | Class | Notes |
|---|---|---|
| MONTHLY frequency: always returns true | A | RebalanceTests: `CalculateWhetherItsBucketRebalanceTime_ChecksFrequencyCorrectly` — all 12 months asserted true. |
| QUARTERLY frequency: months 1, 4, 7, 10 | A | RebalanceTests: `CalculateWhetherItsBucketRebalanceTime_ChecksFrequencyCorrectly` — all 12 months checked. |
| YEARLY frequency: January only | A | RebalanceTests: `CalculateWhetherItsBucketRebalanceTime_ChecksFrequencyCorrectly` — only month 1 returns true. |
| Pre-retirement: no rebalancing before threshold | B | All RebalanceTests use a date already within the rebalance window (2029 vs 2030 retirement). No test uses a date far before the threshold to assert the function returns false. |

### §7.2 Basic Buckets — Cash Top-Up

| Outcome | Class | Notes |
|---|---|---|
| In recession: mid-term sold first to top up cash | B | Tests confirm no long-to-mid movement during recession. The recession-specific mid-first cash top-up ordering (as opposed to the non-recession long-first ordering) is not explicitly isolated in a BasicBuckets strategy test. |
| Not in recession: long-term sold first, then mid-term | A | BasicBucketsIncomeThresholdTests: `RebalancePortfolio_AtRebalanceTime_ExecutesRebalancingCorrectly`; BasicBucketsTaxableFirstTests equivalent. |
| No cash sold if balance already meets N-month requirement | A | NoMidIncomeThresholdTests: `RebalancePortfolio_AtRightTimeAndWithEnoughCash_DoesNothing`. |

### §7.3 Basic Buckets — Long-to-Mid Top-Up

| Outcome | Class | Notes |
|---|---|---|
| In recession: no long-to-mid movement | A | BasicBucketsIncomeThresholdTests: `RebalanceLongToMid_DuringRecession_DoesNotRebalance`; BasicBucketsTaxableFirstTests equivalent. |
| Not in recession: first try tax-deferred internal conversion | A | BasicBucketsIncomeThresholdTests: `RebalanceLongToMid_MovesFromTaxDeferredAccountsFirst`. |
| Tax-deferred conversion preserves position value exactly | A | BasicBucketsIncomeThresholdTests: `RebalancePortfolio_WithTaxDeferredLong_MovesToMinWithoutTaxConsequences` — asserts `expectedTax == 0`. |
| Partial conversion creates two positions | C | No test verifies that a partial long-to-mid conversion within a tax-deferred account produces two positions with correct quantities and costs. |
| If tax-deferred conversion insufficient, taxable brokerage sold | A | BasicBucketsIncomeThresholdTests: `RebalanceLongToMid_MovesFromTaxDeferredAccountsFirst` — traditional exhausted, brokerage long sold, capital gains recorded. |
| Mid-term target already met: no movement | C | No test starts with the mid bucket at or above target and asserts no movement occurs. |

### §7.4 Income Threshold Withdrawal Order

| Outcome | Class | Notes |
|---|---|---|
| With income room: traditional accounts sold first | A | BasicBucketsIncomeThresholdTests: `SellInvestmentsToDollarAmount_WithIncomeRoom_SellsInCorrectOrder`. |
| No income room: tax-free → taxable → traditional | B | BasicBucketsTaxableFirstTests: `SellInvestmentsToDollarAmount_SellsInCorrectOrder` tests taxable-first. No test explicitly zeros income room and verifies the tax-free → taxable → traditional fallback within the IncomeThreshold strategy. |
| Account type override bypasses income room logic | C | No test passes an account type override and verifies income room calculation is skipped. |
| Position type override limits eligible positions | B | InvestmentSalesTests: `SellInvestmentsToDollarAmount_WithVarryingAccountTypes_SellsInCorrectOrder` passes `LONG_TERM` override and verifies only long positions are touched. Mid and short overrides not tested in isolation. |

### §7.5 Universal Net Worth Conservation

| Outcome | Class | Notes |
|---|---|---|
| Net worth conserved by every rebalancing strategy | A | BasicBucketsIncomeThresholdTests: `RebalancePortfolio_DoesntChangeNetWorth`; BasicBucketsTaxableFirstTests: `RebalancePortfolio_DoesntChangeNetWorth`; NoMidIncomeThresholdTests: `RebalancePortfolio_DoesntChangeNetWorth`. |

---

## §8. Account Cleanup / Consolidation

| Outcome | Class | Notes |
|---|---|---|
| Tax-free accounts consolidated into a single Roth IRA | A | AccountCleanupTests: `CleanUpAccounts_AfterCleanup_ThereIsOnlyOnePositionForEachType`. |
| Tax-deferred accounts consolidated into a single Traditional IRA | A | AccountCleanupTests: `CleanUpAccounts_AfterCleanup_ThereIsOnlyOnePositionForEachType`. |
| Consolidation preserves total value | A | AccountCleanupTests: `CleanUpAccounts_AfterCleanup_TotalTaxFreeIsTheSameAsBeforeCleanup`; `CleanUpAccounts_AfterCleanup_TotalTaxDeferredIsTheSameAsBeforeCleanup`. |
| Taxable brokerage: long-held consolidated, short-held preserved | A | AccountCleanupTests: `CleanUpAccounts_AfterCleanup_ThereIsOnlyOnePositionForEachType`; `CleanUpAccounts_AfterCleanup_ShortlyHeldBrokeragePositionCountIsTheSame`. |
| Taxable brokerage: cost basis preserved in consolidated positions | B | AccountCleanupTests: `CleanUpAccounts_AfterCleanup_TotalLongHeldIsTheSameAsBeforeCleanup` checks total value. `InitialCost` on the consolidated position is never directly asserted. |
| Closed debt positions removed | A | AccountCleanupTests: `RemoveClosedDebtPositions_RemovesClosedDebtPositions`. |
| Primary residence: copied unchanged | A | AccountCleanupTests: `CleanUpAccounts_AfterCleanup_TotalPrimaryResidenceIsTheSameAsBeforeCleanup`. |
| No primary residence: not added | C | No test uses a book of accounts without a primary residence and asserts the output also has none. |
| Net worth conserved by account cleanup | A | AccountCleanupTests: `CleanUpAccounts_AfterCleanup_NetWorthIsTheSameAsBeforeCleanup`. |

---

## §9. Account Calculations

| Outcome | Class | Notes |
|---|---|---|
| CalculateCashBalance: sums open positions in Cash only | A | AccountCalculationTests: `CalculateCashBalance_WithSinglePosition_ReturnsCorrectBalance`; `CalculateCashBalance_WithClosedPositions_IgnoresClosedPositions`. |
| CalculateInvestmentAccountTotalValue: sums open positions in one account | A | AccountCalculationTests: `CalculateInvestmentAccountTotalValue_WithSinglePosition_ReturnsCorrectValue`; `_WithMultiplePositions_ReturnsCorrectTotal`; `_WithClosedPositions_IgnoresClosedPositions`. |
| CalculateLongBucketTotalBalance: LONG_TERM across all eligible accounts | A | AccountCalculationTests: `CalculateLongBucketTotalBalance_WithMixedPositions_OnlyCountsLongTerm`. |
| CalculateMidBucketTotalBalance: MID_TERM across eligible accounts | A | AccountCalculationTests: `CalculateMidBucketTotalBalance_WithMixedPositions_OnlyCountsMidTerm`. |
| CalculateNetWorth: investment assets minus total debt | A | AccountCalculationTests: `CalculateNetWorth_WithValidAccounts_ReturnsCorrectBalance`; `CalculateNetWorth_ExcludesPrimaryResidence`. |
| CalculateTotalBalanceByMultipleFactors: filters by type/date | A | AccountCalculationTests: multiple targeted Theory tests covering minDate, maxDate, both dates, account type, and position type filters. |
| CalculateDebtPaydownAmounts: min(MonthlyPayment, balance) per position | C | No test observed for this method. |
| CalculateDebtTotal: sums CurrentBalance across open debt positions | A | AccountCalculationTests: `CalculateDebtTotal_SingleAccountSinglePosition_ReturnsCorrectTotal`; `_MultipleAccountsMultiplePositions`; `_IgnoresClosedPositions`. |

---

## §10. Spend / Fun Points / Healthcare

### §10.1 Fun Points

| Outcome | Class | Notes |
|---|---|---|
| $1 = 1 fun point at age 50; $1 = 0.5 at age 90 | A | SpendTests: `CalculateFunPointsForSpend_AgeAffectsPoints` InlineData at 50→1000, 90→500. |
| Fun points capped at spend amount (no bonus below 50) | A | SpendTests: `CalculateFunPointsForSpend_AgeAffectsPoints` — age 45 → 1000 (same as spend). |
| Fun points floored at 0.5× (no further penalty above 90) | A | SpendTests: `CalculateFunPointsForSpend_AgeAffectsPoints` — age 95 → 500 (same as age 90). |

### §10.2 Monthly Fun Spend

| Outcome | Class | Notes |
|---|---|---|
| Pre-retirement: DesiredMonthlySpendPreRetirement | A | SpendTests: `CalculateMonthlyFunSpend_AgeAffectsSpending` — year 2025 → 5000. |
| Retirement to age 65: DesiredMonthlySpendPostRetirement | A | SpendTests: `CalculateMonthlyFunSpend_AgeAffectsSpending` — years 2030, 2035, 2040 → 6000. |
| Age 66–88: linear decline to $0 | A | SpendTests: `CalculateMonthlyFunSpend_AgeAffectsSpending` — multiple InlineData from 2045–2062 with declining values. |
| Age 88+: $0 fun spend | A | SpendTests: `CalculateMonthlyFunSpend_AgeAffectsSpending` — years 2063, 2065 → 0. |

### §10.3 Monthly Healthcare Spend

| Outcome | Class | Notes |
|---|---|---|
| Pre-retirement: $0 | A | SpendTests: `CalculateMonthlyHealthSpend_AgeAffectsHealthCosts` — year 2025 → 0. |
| Retired and under 65: full personal premium | A | SpendTests: `CalculateMonthlyHealthSpend_AgeAffectsHealthCosts` — years 2030, 2035 → 3000. |
| Age 65–88: Medicare A + B + D costs | A | SpendTests: `CalculateMonthlyHealthSpend_AgeAffectsHealthCosts` — years 2040–2060. |
| Age 88+: assisted living — costs double | A | SpendTests: `CalculateMonthlyHealthSpend_AgeAffectsHealthCosts` — year 2063 → 6000. |
| Hospital admissions increase with age | C | No test exercises hospital admission rate logic as an isolated assertion. |

### §10.4 Spending Multipliers

| Outcome | Class | Notes |
|---|---|---|
| In recession: spending × AusterityRatio | A | SpendTests: `CalculateSpendOverride_AppliesCorrectRatio` InlineData `(true, false, false, 0.8)`. |
| In extreme austerity: spending × ExtremeAusterityRatio | A | SpendTests: `CalculateSpendOverride_AppliesCorrectRatio` InlineData `(true, true, false, 0.6)`. |
| Extreme austerity persists 12 months after net worth recovers | C | No test verifies the 12-month lag before exiting extreme austerity. RecessionTests covers the austerity entry but not persistence across recovery. |
| Living large: spending × LivinLargeRatio | A | SpendTests: `CalculateSpendOverride_AppliesCorrectRatio` InlineData `(false, false, true, 1.5)`. |
| CalculateCashNeedForNMonths: sums monthly required + fun for N months | A | SpendTests: `CalculateCashNeedForNMonths_ReturnsCorrectTotal`. |

---

## §11. Recession Detection

| Outcome | Class | Notes |
|---|---|---|
| Recession entry: equity price < price N months ago | A | RecessionTests: `CalculateAreWeInARecession_PriceDropYearOverYear_EntersRecession`; `CalculateRecessionStats_EnteringRecession_UpdatesRecessionStatsCorrectly`; `CalculateAreWeInARecession_WithMultipleMonthsHistory_UsesCorrectPricePointInTheArray`. |
| Recession exit: price recovers to trough × modifier | A | RecessionTests: `CalculateAreWeInARecession_InRecession_RecoveryReached_ExitsRecession`; `CalculateRecessionStats_ExitingRecession_UpdatesStats`; `CalculateRecessionStats_RecoveryBelowModifier_StaysInRecession`. |
| Extreme austerity entry: net worth ≤ trigger | A | RecessionTests: `CalculateExtremeAusterityMeasures_BelowTrigger_EntersAusterity`. |
| Extreme austerity exit: 12 consecutive months above trigger | A | RecessionTests: `CalculateExtremeAusterityMeasures_AboveTrigger_ExitsAusterityAtRightTime` — parameterised; month 12 still in, month 13 exits. |
| Living large: net worth ≥ trigger | A | RecessionTests: `WeLivinLarge_ReturnsCorrectValue`; `CalculateRecessionStats_WhenRich_SetsWeLivinLargeToTrue`. |
| Recession duration tracked in months | A | RecessionTests: `CalculateAreWeInARecession_InRecession_BelowRecovery_IncrementsDuration`; `CalculateRecessionStats_ExitingRecession_UpdatesStats` (resets to 0 on exit). |

---

## §12. Social Security Claiming Age Adjustments

| Outcome | Class | Notes |
|---|---|---|
| Claiming at FRA (67): no adjustment | A | PersonTests: `CalculateMonthlySocialSecurityWage_CalculatesCorrectly` InlineData `(2043, 3, 100)` — exact FRA month. |
| Early penalty: first 36 months (5/9 of 1%/month) and months 37–60 (5/12 of 1%/month) | A | PersonTests: `CalculateMonthlySocialSecurityWage_CalculatesCorrectly` — InlineData covers both penalty tiers including `(2038, 4, 70.42)` (~54 months early, second band). |
| Late credit: 8/12 of 1%/month, stops at 70 | A | PersonTests: `CalculateMonthlySocialSecurityWage_CalculatesCorrectly` — InlineData `(2044,10,112.67)`, `(2045,6,118)`, `(2046,3,124)`. |
| Earliest possible claiming age: 62 + 1 month | B | PersonTests: `CalculateMonthlySocialSecurityWage_WhenElectionDateIsBeforeAge62AndOneMonth_ThrowsInvalidDataException` confirms exactly age 62 (no extra months) throws. No positive test confirms 62+1 month is accepted and produces the expected benefit. |
| Latest meaningful claiming age: 70 | B | PersonTests: `CalculateMonthlySocialSecurityWage_WhenElectionDateIsAfterAge70_ThrowsInvalidDataException` confirms 70+1 throws. No test asserts age 70 exactly is valid and produces the maximum credit. |
| Months-early and months-late are bounded | B | Enforcement tested only via the two throw tests above. No test exercises internal clamping of edge-case values. |

---

## §13. Cash Withdrawal Cascade

| Outcome | Class | Notes |
|---|---|---|
| Sufficient cash: no investments sold | A | AccountCashManagementTests: `WithdrawCash_WithSufficientCash_SucceedsAndUpdatesBalance`; AccountCashManagementTestsExtended: `WithdrawCash_WithSufficientCash_ReturnsSuccessfully`. |
| First-tier sufficient: only long-held MID_TERM sold; tiers 2–4 untouched | B | AccountCashManagementTests: `WithdrawCash_WithInSufficientCash_PullsFromInvestmentAccounts` exercises the multi-tier cascade but does not isolate the case where tier 1 alone is sufficient and confirm tiers 2–4 are untouched. |
| Bankruptcy: none of the four tiers covers the withdrawal | A | AccountCashManagementTestsExtended: `WithdrawCash_WithInsufficientCashAndNoInvestments_ReturnsFalse`. |
| Each tier records correct capital gain type | A | InvestmentSalesTests: `SellInvestmentsToDollarAmount_WithTaxablePosition_SellsAndUpdatesTaxLedgerLongTermCapitalGains`; `_ShortTermCapitalGains` — long-held → LTCG, short-held → STCG. |

---

## §14. VAR Pricing / Growth Rate Generation

| Outcome | Class | Notes |
|---|---|---|
| Equity price grows multiplicatively | A | PricingTests: `SetLongTermGrowthRateAndPrices_UpdatesAllPricesCorrectly`; `_HandlesNegativeGrowthRate`; `_HandlesZeroGrowthRate`. |
| Mid-term price grows at 0.5× equity growth | A | PricingTests: `SetLongTermGrowthRateAndPrices_UpdatesAllPricesCorrectly` — uses `MidTermGrowthRateModifier = 0.5`. |
| Short-term price unchanged | A | PricingTests: `SetLongTermGrowthRateAndPrices_UpdatesAllPricesCorrectly` — `ShortTermGrowthRateModifier = 0`. |
| Treasury coupon updated additively: newCoupon = old + TreasuryGrowth | C | PricingTests does not set a non-zero `TreasuryGrowth` or assert `CurrentTreasuryCoupon`. No test verifies the additive update. |
| Same lifeIndex always produces the same sequence | C | No test exercises `VarLifetimeGenerator.Generate`. |
| Treasury rate bounded between 0.1% and 20% | C | `VarLifetimeGenerator` floor/ceiling clamping is completely untested. |
| OU mean reversion pulls rate toward TreasuryOuTheta | C | OU correction logic in `VarLifetimeGenerator` is completely untested. |
| CopyPrices propagates CurrentTreasuryCoupon | C | No test calls `Pricing.CopyPrices` and asserts `CurrentTreasuryCoupon` is faithfully copied. |

---

## §15. Income Room Calculation

| Outcome | Class | Notes |
|---|---|---|
| Baseline: $123,500 (12%-bracket ceiling + standard deduction) | C | No test calls `CalculateIncomeRoom` with an empty ledger (zero income, no SS) to assert the raw $123,500 floor. |
| W2, IRA distributions, interest, and non-qualified dividends reduce room | A | TaxCalculationTests: `CalculateIncomeRoom_ReturnsCorrectAmount` — theory includes W2=1000, IRA=2000, interest=3000, dividends=4000; all reduce headroom; `CalculateIncomeRoom_NegativeRoom_ReturnsZero` uses large W2 + IRA. |
| Income room cannot go below zero | A | TaxCalculationTests: `CalculateIncomeRoom_NegativeRoom_ReturnsZero`. |
| In SS election year: only partial-year SS projected | A | TaxCalculationTests: `CalculateIncomeRoom_ReturnsCorrectAmount` — 2035 InlineData (election 2035-07-01) produces different headroom than pre- and post-election years. |
| Before SS election: zero SS projection | A | TaxCalculationTests: `CalculateIncomeRoom_ReturnsCorrectAmount` — 2034 InlineData produces headroom with no SS component. |
| After SS election year: full 12-month SS projected | A | TaxCalculationTests: `CalculateIncomeRoom_ReturnsCorrectAmount` — 2036 InlineData produces lowest headroom of the three years. |

---

## Summary

| Section | A (Covered) | B (Partial) | C (Not covered) | Total |
|---|---|---|---|---|
| §1 Federal Tax | 15 | 10 | 8 | 33 |
| §2 Payroll Tax | 4 | 1 | 1 | 6 |
| §3 Paycheck Processing | 14 | 2 | 0 | 16 |
| §4 Interest Accrual | 8 | 0 | 10 | 18 |
| §5 Debt Paydown | 4 | 2 | 3 | 9 |
| §6 Investment Sales | 12 | 3 | 1 | 16 |
| §7 Rebalancing | 11 | 4 | 3 | 18 |
| §8 Account Cleanup | 7 | 1 | 1 | 9 |
| §9 Account Calculations | 7 | 0 | 1 | 8 |
| §10 Spend / Healthcare | 15 | 0 | 2 | 17 |
| §11 Recession Detection | 6 | 0 | 0 | 6 |
| §12 SS Claiming Age | 3 | 3 | 0 | 6 |
| §13 Cash Cascade | 3 | 1 | 0 | 4 |
| §14 VAR / Pricing | 3 | 0 | 5 | 8 |
| §15 Income Room | 5 | 0 | 1 | 6 |
| **Total** | **117** | **27** | **36** | **180** |

---

## All C items — Not Covered (36 total)

**§1 Federal Tax (8)**
- §1.2: Schedule D — all 6 outcomes (ScheduleDTests.cs is fully commented out)
- §1.3: Worksheet result cannot exceed regular tax on full income
- §1.3: Boundary: income exactly at 0% ceiling

**§2 Payroll Tax (1)**
- §2: Additional Medicare 0.9% applies above $250,000

**§4 Interest Accrual (10)**
- §4.3: All 10 dividend reinvestment outcomes (feature is untested; code also has a known bug — creates new position vs. updates quantity)

**§5 Debt Paydown (3)**
- §5: Insufficient cash → attempt to liquidate investments (also a known code gap)
- §5: Internal accounting check: debited = credited
- §5: Net worth conserved by debt paydown

**§6 Investment Sales (1)**
- §6.1: Cash and PRIMARY_RESIDENCE accounts cannot be sold

**§7 Rebalancing (3)**
- §7.3: Partial conversion creates two positions
- §7.3: Mid-term target already met: no movement
- §7.4: Account type override bypasses income room logic

**§8 Account Cleanup (1)**
- §8: No primary residence: not added

**§9 Account Calculations (1)**
- §9: CalculateDebtPaydownAmounts

**§10 Spend / Healthcare (2)**
- §10.3: Hospital admissions increase with age
- §10.4: Extreme austerity persists 12 months after net worth recovers

**§14 VAR / Pricing (5)**
- §14: Treasury coupon updated additively
- §14: Same lifeIndex always produces same sequence
- §14: Treasury rate bounded between 0.1% and 20%
- §14: OU mean reversion pulls rate toward TreasuryOuTheta
- §14: CopyPrices propagates CurrentTreasuryCoupon

**§15 Income Room (1)**
- §15: Baseline $123,500 with empty ledger

---

## All B items — Partial Coverage (27 total, gaps described above)

**§1 (10):** Standard deduction isolated; IRA→4b not 1a routing; Qualified dividends line 3a; Total dividends line 3b; Capital gains line 7; Tax routing branch selection; SS income line 6b; All income sources simultaneously; Federal AGI as NC starting point; NC standard deduction constant

**§2 (1):** OASDI cap (high-salary path never exercised)

**§3 (2):** Bankrupt/retired savings skipped; No SS before election date (no positive zero-assertion)

**§5 (2):** Monthly payment min() clips to balance; Liquidation-fails accounts-unchanged assertion

**§6 (3):** Partial sale InitialCost formula; Capital gain per-share arithmetic; Roth/HSA amount asserted (only count > 0)

**§7 (4):** Pre-retirement rebalancing threshold; Recession mid-first cash top-up; No income room fallback sequence; Position type override (LONG_TERM only tested)

**§8 (1):** Taxable brokerage consolidated position InitialCost

**§12 (3):** Earliest 62+1 month positive case; Latest age 70 positive case; Internal bounds clamping
