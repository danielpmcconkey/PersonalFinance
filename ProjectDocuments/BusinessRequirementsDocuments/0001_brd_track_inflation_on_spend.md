# BRD-0001: Track inflation on spend

**Author:** Dan
**Date:** 2026-02-19 18:45 EST
**Status:** Draft

## Background / problem statement
The current version of the Monte Carlo simulation denominates everything in terms of today's dollars. But this is not 
the best way of doing it. Hypothetical growth on assets is always expressed in dollars of that time. For example, the 
S&P500 closed at \$329.08 in January of 1990. That same \$329.08 would have the same purchasing power as \$815.00 in 
February of 2026. If I bought 100 shares of some fund that tracked the S&P500 perfectly back in January of 1990, it 
would've cost \$32,908.00 in 1990 dollars, and grown to almost $700,000 by today. But that $700,000 gets you so much
less purchasing power than it did back in 1990. The market goes up about 10% CAGR, but inflation chips away at the 
purchasing power.

The current simulation tracks S&P500 growth **net of inflation**. This allowed me to keep all monthly expenses (required
and fun) denominated in today's dollars while projecting 30 years ahead. But there are two problems.
- Debt payments are locked into a fixed monthly payment through an amortization schedule, so the simulation doesn't 
    track how debt payments get progressively cheaper in terms of inflation.
- I want to implement a 2-year treasury note that will have a fixed coupon and a fixed value at the end of the period

## Desired business changes and outcomes
### BR-1: Increment spend by CPI over time
Increment all required spend and fun spend according to that simulated lifetime's CPI growth rate for that month 
### BR-2: Functions that project spend into the future should increment by CPI
All functions in the Spend.cs static function that project a future spend (e.g. CalculateCashNeedForNMonths) increment
projected non-debt spends with an assumed static CPI growth rate. As an example:
  - It is the simulated month of November 2043
  - The CPI growth rate for that hypothetical lifetime's market prices is 0.002
  - If your want to project the next 10 months' worth of cash needed by calling CalculateCashNeedForNMonths, that 
      function should increment the spend need by 0.002 for each of those 10 months
### BR-3: Fun points attributed to spending money should decrease by CPI
When calculating how many fun points the simulated life should obtain from spending money (e.g. 
    CalculateFunPointsForSpend) the function, and any that do similar fun point calculation for money spending, should 
    discount future spend by inflation. It's not as fun to spend \$10,000 today as it would've been in 1950.
### BR-4 Debt exclusion
Monthly payments for debt should NOT increase with inflation. They remain at their starting value until the debt is paid
off or the simulation ends
### BR-5 Tax constants
Any constants used in the tax forms that are flat rate (not percentage based) should only use the constant as the value 
at the start of the simulation. From thereafter, those values should increase with CPI inflation
### BR-6 Social security payments
Social security payments calculated at the beginning of the simulation should also be given a cost of living adjustment 
that tracks CPI growth. Note, I believe the simulation calculates the payment before it is ever used so the calculated 
value in the background will need to increase as well
### BR-7 Investment accrual uses unadjusted S&P growth
The version of the simulation prior to implementing the VAR logic used S&P500 growth net of inflation for the long-term
asset price.
- Confirm whether the VAR calculation uses the pure S&P500 value or the net value.
- Ensure we use the pure value moving forward
### BR-8 Payday values for working income
Flat-fee payroll deductions like dental, health, life insurance, etc. will need to increase with inflation 
### BR-9 Savings values increase
401k and HSA savings that are triggered by payday events while still working (not retired) should also increase per
inflation

## Out of Scope
- updates to the database tables where some of the initial spend values originate
- updates to any constants defined in code or config that initialize values pre-simulation start

## Open Questions
TBD

## General comment to the agent
Please help this become a world-class BRD. Feel free to make suggestions of requirements you think I've missed that
align to the background / problem statement 
