using Lib.DataTypes.MonteCarlo;
using NodaTime;

namespace Lib.MonteCarlo
{
    internal class Bank
    {
        private Logger _logger;
        private McModel _simParams;
        private bool _areWeInADownYear = false;
        public bool AreWeInADownYear { get { return  _areWeInADownYear; } }
        private decimal _downYearCounter = 0M;
        public bool AreWeInAusterityMeasures { get { return _downYearCounter >= 1.0M; } }
        private bool _areWeInExtremeAusterityMeasures = false;
        private LocalDateTime _lastExtremeAusterityMeasureEnd;
        public bool AreWeInExtremeAusterityMeasures { get { return _areWeInExtremeAusterityMeasures; } }
        private decimal _recessionRecoveryPoint = 0M;
        private LocalDateTime _retirementDate;
        private TaxFiler _taxFiler;
        /// <summary>
        /// list of total distributions by year that qualify against RMD requirements
        /// </summary>
        private Dictionary<int, decimal> _rmdDistributions;
        
        private CorePackage _corePackage;
        private ReconciliationLedger _reconciliationLedger;

        private decimal _currentLongTermGrowthRate = 0.0M;

        private decimal _totalSpendLifetime = 0.0M;
        private decimal _totalInvestmentAccrualLifetime = 0.0M;
        private decimal _totalDebtAccrualLifetime = 0.0M;
        private decimal _totalSocialSecurityWageLifetime = 0.0M;
        private decimal _totalDebtPaidLifetime = 0.0M;
        private bool _isBankrupt = false;




        // account pointers
        private McInvestmentAccount _roth401k;
        private McInvestmentAccount _rothIra;
        private McInvestmentAccount _traditional401k;
        private McInvestmentAccount _traditionalIra;
        private McInvestmentAccount _brokerage;
        private McInvestmentAccount _hsa;
        private McInvestmentAccount _cash;
        private List<McInvestmentAccount> _investmentAccounts;
        private List<McDebtAccount> _debtAccounts;

        // price pointers
        private List<decimal> _longRangeInvestmentCostHistory = [];
        public decimal CurrentLongRangeInvestmentCost
        {
            get { return _currentLongRangeInvestmentCost; }
        }
        private decimal _currentLongRangeInvestmentCost = 100.0M;
        private decimal _currentMidRangeInvestmentCost = 100.0M;
        private decimal _currentShortRangeInvestmentCost = 100.0M;

        private McInvestmentAccountType[] _salesOrderWithNoRoom = [
                // no tax, period
                McInvestmentAccountType.HSA,
                McInvestmentAccountType.ROTH_IRA,
                McInvestmentAccountType.ROTH_401_K,
                // tax on growth only
                McInvestmentAccountType.TAXABLE_BROKERAGE,
                // tax deferred
                McInvestmentAccountType.TRADITIONAL_401_K,
                McInvestmentAccountType.TRADITIONAL_IRA,
                ];
        private McInvestmentAccountType[] _salesOrderWithRoom = [
            // tax deferred
                McInvestmentAccountType.TRADITIONAL_401_K,
                McInvestmentAccountType.TRADITIONAL_IRA,
                // tax on growth only
                McInvestmentAccountType.TAXABLE_BROKERAGE,
                // no tax, period
                McInvestmentAccountType.HSA,
                McInvestmentAccountType.ROTH_IRA,
                McInvestmentAccountType.ROTH_401_K,
                ];
        private McInvestmentAccountType[] _salesOrderRmd = [
            // tax deferred
                McInvestmentAccountType.TRADITIONAL_401_K,
                McInvestmentAccountType.TRADITIONAL_IRA,
                ];

        public Bank(CorePackage corePackage, McModel simParams, List<McInvestmentAccount> investmentAccounts,
            List<McDebtAccount> debtAccounts, LocalDateTime retirementDate, TaxFiler taxFiler)
        {
            _corePackage = corePackage;
            _logger = _corePackage.Log;
            _simParams = simParams;
            _investmentAccounts = investmentAccounts;
            _debtAccounts = debtAccounts;
            _retirementDate = retirementDate;
            _taxFiler = taxFiler;
            // set the account pointers
            Func<McInvestmentAccountType, McInvestmentAccount> getOrCreateAccount = (McInvestmentAccountType accountType) =>
            {
                var firstAccount = _investmentAccounts.Where(x => x.AccountType == accountType)
                    .FirstOrDefault();
                if (firstAccount is null)
                {
                    McInvestmentAccount defaultAccount = new()
                    {
                        Id = Guid.NewGuid(),
                        // PersonId = Guid.NewGuid(),
                        AccountType = accountType,
                        Name = $"default {accountType.ToString()} account",
                        Positions = []
                    };
                    _investmentAccounts.Add(defaultAccount);
                    return defaultAccount;
                }
                return firstAccount;
            };
            _roth401k = getOrCreateAccount(McInvestmentAccountType.ROTH_401_K);
            _rothIra = getOrCreateAccount(McInvestmentAccountType.ROTH_IRA);
            _traditional401k = getOrCreateAccount(McInvestmentAccountType.TRADITIONAL_401_K);
            _traditionalIra = getOrCreateAccount(McInvestmentAccountType.TRADITIONAL_IRA);
            _brokerage = getOrCreateAccount(McInvestmentAccountType.TAXABLE_BROKERAGE);
            _hsa = getOrCreateAccount(McInvestmentAccountType.HSA);
            _cash = getOrCreateAccount(McInvestmentAccountType.CASH);

            _rmdDistributions = [];
            _lastExtremeAusterityMeasureEnd = _simParams.SimStartDate;


            if (_corePackage.DebugMode == true)
            {
                _reconciliationLedger = new ReconciliationLedger(_corePackage);
            
                AddReconLine(
                    _simParams.SimStartDate,
                    ReconciliationLineItemType.Credit,
                    0.0M,
                    $"Opening bank account"
                );
            }
                
        }

        public void PrintReconciliation()
        {
            _reconciliationLedger.ExportToSpreadsheet();
        }

        public void AddReconLine(LocalDateTime currentDate, ReconciliationLineItemType type,
            Decimal amount, string description)
        {
            if (_corePackage.DebugMode == false) return;
            
            var person = _simParams.Person;
            if (person is null)
            {
                // pull it from the DB
                // todo: move the db reads from datastgage and this one to a single, consolidated DAL 
                using var context = new PgContext();
                var pgperson = context.PgPeople.FirstOrDefault(x => x.Id == _simParams.PersonId);
                if (pgperson is null) throw new InvalidDataException();
                
                person = new McPerson()
                {
                    Id = pgperson.Id,
                    Name = pgperson.Name,
                    BirthDate = pgperson.BirthDate,
                    AnnualSalary = pgperson.AnnualSalary,
                    AnnualBonus = pgperson.AnnualBonus,
                    MonthlyFullSocialSecurityBenefit = pgperson.MonthlyFullSocialSecurityBenefit,
                    Annual401kMatchPercent = pgperson.Annual401kMatchPercent,
                    InvestmentAccounts = _investmentAccounts,
                    DebtAccounts = _debtAccounts,
                };
                _simParams.Person = person;
            }

            var ageTimeSpan = (currentDate - person.BirthDate);
            var yearsOld = ageTimeSpan.Years;
            var monthsOld = ageTimeSpan.Months;
            var daysOld = ageTimeSpan.Days;
            var age = yearsOld + (monthsOld / 12.0M) + (daysOld / 365.25M);
            var line = new ReconciliationLineItem(
                currentDate, 
                age,
                amount, 
                description, 
                type,
                _currentLongTermGrowthRate,
                MeasureNetWorth(currentDate).NetWorth,
                Recon_GetAssetTotalByType(McInvestmentPositionType.LONG_TERM),
                Recon_GetAssetTotalByType(McInvestmentPositionType.MID_TERM),
                Recon_GetAssetTotalByType(McInvestmentPositionType.SHORT_TERM),
                GetCashBalance(),
                Recon_GetDebtTotal(),
                _totalSpendLifetime,
                _totalInvestmentAccrualLifetime,
                _totalDebtAccrualLifetime,
                _totalSocialSecurityWageLifetime,
                _totalDebtPaidLifetime,
                currentDate >= _simParams.RetirementDate, 
                _isBankrupt,
                _areWeInADownYear,
                _areWeInExtremeAusterityMeasures
            );
            _reconciliationLedger.AddLine(line);
        }
        #region public interface

        public void SetLongTermGrowthRate(decimal longTermGrowthRate)
        {
            _currentLongTermGrowthRate = longTermGrowthRate;
        }
        public void AccrueInterest(LocalDateTime currentDate)
        {
            decimal midTermGrowthRate = _currentLongTermGrowthRate * 0.5M;
            decimal shortTermGrowthRate = 0.0M;


            _currentLongRangeInvestmentCost +=
                (_currentLongRangeInvestmentCost * _currentLongTermGrowthRate);
            _currentMidRangeInvestmentCost +=
                (_currentMidRangeInvestmentCost * midTermGrowthRate);
            _currentShortRangeInvestmentCost +=
                (_currentShortRangeInvestmentCost * shortTermGrowthRate);

            _longRangeInvestmentCostHistory.Add(_currentLongRangeInvestmentCost);

            decimal getGrowthRate(McInvestmentPositionType investmentPositionType) =>
                investmentPositionType switch
                {
                    McInvestmentPositionType.SHORT_TERM => shortTermGrowthRate,
                    McInvestmentPositionType.MID_TERM => midTermGrowthRate,
                    McInvestmentPositionType.LONG_TERM => _currentLongTermGrowthRate,
                    _ => throw new NotImplementedException(),
                };

            foreach (var account in _investmentAccounts)
            {
                // good number goes up
                foreach (var p in account.Positions)
                {

                    decimal oldPrice = p.Price;

                    if (p is not McInvestmentPosition) break;
                    if (!p.IsOpen) break;
                    var growthRate = getGrowthRate(p.InvenstmentPositionType);
                    p.Price = Math.Round(p.Price + (p.Price * growthRate), 4);

                    decimal oldValue = oldPrice * p.Quantity;
                    decimal newValue = p.Price * p.Quantity;
                    _totalInvestmentAccrualLifetime += (newValue - oldValue);
                    if (_corePackage.DebugMode == true)
                    {
                        AddReconLine(
                            currentDate,
                            ReconciliationLineItemType.Credit,
                            newValue - oldValue,
                            $"Investment interest accrual for account {account.Name}"
                        );
                    }
                }
            }

            foreach (var account in _debtAccounts)
            {
                // bad number goes up
                foreach (var p in account.Positions)
                {

                    decimal oldBalance = p.CurrentBalance;

                    if (p is not McDebtPosition) break;
                    if (!p.IsOpen) break;
                    
                    decimal amount = Math.Round(p.CurrentBalance * (p.AnnualPercentageRate / 12), 2);
                    p.CurrentBalance += amount;

                    _totalDebtAccrualLifetime += (p.CurrentBalance - oldBalance);
                    if (_corePackage.DebugMode == true)
                    {
                        AddReconLine(
                            currentDate,
                            ReconciliationLineItemType.Debit,
                            amount,
                            $"Debt accrual for account {account.Name}"
                        );
                    }
                }
            }
            return;
        }

        /// <summary>
        /// removes closed positions and splits up large investment positions
        /// </summary>
        public void CleanUpAccounts(LocalDateTime currentDate)
        {
            RemoveClosedPositions();
            SplitPositionsAtMaxIndividualValue();
            if (_corePackage.DebugMode == true)
            {
                AddReconLine(
                    currentDate,
                    ReconciliationLineItemType.Debit,
                    0,
                    $"Routine closed account maintenance and large position splitting"
                );
            }
        }

        public void DepositSocialSecurityCheck(decimal amount, LocalDateTime currentDate)
        {
            DepositCash(amount, currentDate);
            _totalSocialSecurityWageLifetime += amount;
            if (_corePackage.DebugMode == true)
            {
                AddReconLine(
                    currentDate,
                    ReconciliationLineItemType.Credit,
                    amount,
                    $"Social Security check deposit"
                );
            }
        }

        private void DepositCash(decimal amount, LocalDateTime currentDate)
        {

            var totalCash = GetCashBalance();
            totalCash += amount;
            UpdateCashAccountBalance(totalCash, currentDate);
            if (_corePackage.DebugMode == true)
            {
                AddReconLine(
                    currentDate,
                    ReconciliationLineItemType.Credit,
                    amount,
                    $"Generic deposit"
                );
            }

        }
        public void InvestFunds(LocalDateTime _currentDate, decimal dollarAmount, 
            McInvestmentPositionType mcInvestmentPositionType, McInvestmentAccountType accountType)
        {
            if (dollarAmount <= 0) return;
            
            // figure out the correct account pointer
            McInvestmentAccount GetAccount() =>
                accountType switch
                {
                    McInvestmentAccountType.CASH => _cash,
                    McInvestmentAccountType.HSA => _hsa,
                    McInvestmentAccountType.ROTH_401_K => _roth401k,
                    McInvestmentAccountType.ROTH_IRA => _rothIra,
                    McInvestmentAccountType.TAXABLE_BROKERAGE => _brokerage,
                    McInvestmentAccountType.TRADITIONAL_IRA => _traditionalIra,
                    McInvestmentAccountType.TRADITIONAL_401_K => _traditional401k,
                    _ => throw new NotImplementedException(),
                };
            

            var roundedDollarAmount = Math.Round(dollarAmount, 2);

            decimal getPrice() =>
            mcInvestmentPositionType switch
            {
                McInvestmentPositionType.SHORT_TERM => _currentShortRangeInvestmentCost,
                McInvestmentPositionType.MID_TERM => _currentMidRangeInvestmentCost,
                McInvestmentPositionType.LONG_TERM => _currentLongRangeInvestmentCost,
                _ => throw new NotImplementedException(),
            };
            decimal price = getPrice();
            decimal quantity = Math.Round(roundedDollarAmount / price, 4);
            var account = GetAccount();
            account.Positions.Add(new McInvestmentPosition()
            {
                Id = Guid.NewGuid(),
                // InvestmentAccountId = account.Id,
                Entry = _currentDate,
                InitialCost = dollarAmount,
                InvenstmentPositionType = mcInvestmentPositionType,
                IsOpen = true,
                Name = "automated investment",
                Price = price,
                Quantity = quantity
            });
            
            if (_corePackage.DebugMode == true)
            {
                AddReconLine(
                    _currentDate,
                    ReconciliationLineItemType.Debit,
                    dollarAmount,
                    $"Investment in account {account.Name}"
                );
            }
        }
        public NetWorthMeasurement MeasureNetWorth(LocalDateTime _currentDate)
        {
            var totalAssets = 0M;
            var totalLiabilities = 0M;
            foreach (var account in _investmentAccounts)
            {
                if (account.AccountType is not McInvestmentAccountType.PRIMARY_RESIDENCE)
                {
                    totalAssets += account.Positions.Where(x => x.IsOpen).Sum(x => {
                        McInvestmentPosition ip = (McInvestmentPosition)x;
                        return ip.CurrentValue;
                    });
                }
            }
            foreach (var account in _debtAccounts)
            {
                totalLiabilities += account.Positions.Where(x => x.IsOpen).Sum(x => {
                    McDebtPosition dp = (McDebtPosition)x;
                    return dp.CurrentBalance;
                });
            }
            NetWorthMeasurement measurement = new NetWorthMeasurement()
            {
                MeasuredDate = _currentDate,
                TotalAssets = totalAssets,
                TotalLiabilities = totalLiabilities,
                TotalCash = GetCashBalance(),
                TotalMidTermInvestments = GetMidBucketTotalBalance(),
                TotalLongTermInvestments = GetLongBucketTotalBalance(),
                TotalSpend = 0,
                TotalTax = _taxFiler.TotalTaxPaid,
            };

            // see if we're in extreme austerity measures based on total net worth
            if (measurement.NetWorth <= _simParams.ExtremeAusterityNetWorthTrigger)
            {

                _areWeInExtremeAusterityMeasures = true;
                // set the end date to now. if we stay below the line, the date
                // will keep going up with it
                _lastExtremeAusterityMeasureEnd = _currentDate;
            }
            else
            {
                // has it been within 12 months that we were in an extreme measure?
                if (_lastExtremeAusterityMeasureEnd < _currentDate.PlusYears(-1))
                {

                    _areWeInExtremeAusterityMeasures = false;
                }
            }
            return measurement;
        }
        public void MeetRmdRequirements(LocalDateTime currentDate)
        {
            var year = currentDate.Year;
            var rmdRate = _taxFiler.GetRmdRateByYear(year);
            if(rmdRate is null) { return; } // no requirement this year

            var rate = (decimal)rmdRate;

            // get total balance in rmd-relevant accounts
            var relevantAccounts = _investmentAccounts
                .Where(x => x.AccountType is McInvestmentAccountType.TRADITIONAL_401_K
                || x.AccountType is McInvestmentAccountType.TRADITIONAL_IRA);
            var balance = 0M;
            foreach (var account in relevantAccounts)
                balance += GetInvestmentAccountTotalValue(account);

            var totalRmdRequirement = balance / rate;
            if(!_rmdDistributions.TryGetValue(year, out decimal totalRmdSoFar))
            {
                _rmdDistributions[year] = 0;
                totalRmdSoFar = 0;
            }

            if (totalRmdSoFar >= totalRmdRequirement) return;

            var amountLeft = totalRmdRequirement - totalRmdSoFar;

            // start with long-term investments as you're most likely to have them there
            var cashSold =  SellInvestment(amountLeft,
                McInvestmentPositionType.LONG_TERM, currentDate, true);
            if (_corePackage.DebugMode == true)
            {
                AddReconLine(
                    currentDate,
                    ReconciliationLineItemType.Credit,
                    cashSold,
                    "RMD: Sold long-term investment"
                );
            }

            // and invest it back into mid-term
            InvestFunds(currentDate, cashSold, McInvestmentPositionType.MID_TERM, McInvestmentAccountType.TAXABLE_BROKERAGE);
            if (_corePackage.DebugMode == true)
            {
                AddReconLine(
                    currentDate,
                    ReconciliationLineItemType.Debit,
                    cashSold,
                    "RMD: Bought mid-term investment"
                );
            }
            amountLeft -= cashSold;
            if (amountLeft <= 0) return;

            // try mid-term investments for remainder
            cashSold = SellInvestment(amountLeft,
                McInvestmentPositionType.MID_TERM, currentDate, true);
            if (_corePackage.DebugMode == true)
            {
                AddReconLine(
                    currentDate,
                    ReconciliationLineItemType.Credit,
                    cashSold,
                    "RMD: Sold mid-term investment"
                );
            }
            // and invest it back into mid-term
            InvestFunds(currentDate, cashSold, McInvestmentPositionType.MID_TERM, McInvestmentAccountType.TAXABLE_BROKERAGE);
            if (_corePackage.DebugMode == true)
            {
                AddReconLine(
                    currentDate,
                    ReconciliationLineItemType.Debit,
                    cashSold,
                    "RMD: Bought mid-term investment"
                );
            }
            amountLeft -= cashSold;
            if (amountLeft <= 0) return;

            // nothing's left to try. not sure how we got here
            throw new InvalidDataException();
        }
        public void Rebalance(LocalDateTime currentDate)
        {
            // do our recession checking every month, regardless of whether
            // it's time to move money around. this gives us a finer grain for
            // determining down years

            // see if we're already in a recession based on prior checks
            if (_areWeInADownYear)
            {
                // we were previously in a down year. Let's see if we've made
                // it out yet
                if (_currentLongRangeInvestmentCost > 
                    (_recessionRecoveryPoint * _simParams.RecessionRecoveryPointModifier))
                {
                    // we've eclipsed the prior recovery point, we've made it
                    // out. go ahead and set the recovery point to today's cost
                    // just to keep it near a modern number
                    _areWeInADownYear = false;
                    _recessionRecoveryPoint = _currentLongRangeInvestmentCost;
                    _downYearCounter = 0M;
                }
                else
                {
                    // we're still in a dip. keep us here, but increment the down year counter
                    decimal _downYearCounterIncrement() =>
                    _simParams.RebalanceFrequency switch
                    {
                        RebalanceFrequency.MONTHLY => 1M / 12M,
                        RebalanceFrequency.QUARTERLY => 0.25M,
                        RebalanceFrequency.YEARLY => 1M,
                        _ => throw new NotImplementedException(),
                    };
                    _downYearCounter += _downYearCounterIncrement();
                }
            }
            else
            {
                // we weren't previously in a down year. check to see if stocks
                // have gone down year over year
                var numMonthsOfHistory = _longRangeInvestmentCostHistory.Count;
                // if we don't have 13 months of history, we can't check last
                // year's price and won't know if we need to do any rebalancing yet
                if (numMonthsOfHistory < _simParams.RecessionCheckLookBackMonths) return;
                var lookbackPrice = _longRangeInvestmentCostHistory[
                    numMonthsOfHistory - _simParams.RecessionCheckLookBackMonths];
                if (lookbackPrice > _currentLongRangeInvestmentCost)
                {
                    // prices are down year over year. Set the recovery point
                    _areWeInADownYear = true;
                    _recessionRecoveryPoint =
                        (lookbackPrice > _recessionRecoveryPoint) ?
                        lookbackPrice :
                        _recessionRecoveryPoint;
                }
                else
                {
                    // we're not in a down year update the recovery point if
                    // it's a new high water mark
                    _recessionRecoveryPoint =
                        (_currentLongRangeInvestmentCost > _recessionRecoveryPoint) ?
                        _currentLongRangeInvestmentCost :
                        _recessionRecoveryPoint;
                }
            }
            // now check whether it's time to move funds
            bool isTime = false;
            int currentMonthNum = currentDate.Month - 1; // we want it zero-indexed to make the modulus easier
            if (
                (
                    // check whether it's close enough to retirement to think about rebalancing
                    currentDate >= _retirementDate
                        .PlusMonths(-1 * _simParams.NumMonthsPriorToRetirementToBeginRebalance)
                ) &&
                (
                    // check whether our frequency aligns to the calendar
                    _simParams.RebalanceFrequency is RebalanceFrequency.MONTHLY) ||
                    (_simParams.RebalanceFrequency is RebalanceFrequency.QUARTERLY
                        && currentMonthNum % 3 == 0) ||
                    (_simParams.RebalanceFrequency is RebalanceFrequency.YEARLY
                        && currentMonthNum % 12 == 0)
                )
            {
                isTime = true;
            }
            if (isTime)
            {
                // blammo. time to make the donuts.
                TopUpCashBucket(currentDate);
                TopUpMidBucket(currentDate);
            }
        }

        /// <summary>
        /// deduct cash from the cash account
        /// </summary>
        /// <returns>true if able to pay. false if not</returns>
        public bool WithdrawCash(decimal amount, LocalDateTime currentDate)
        {
            var totalCashOnHand = GetCashBalance();

            if (totalCashOnHand < amount)
            {
                // can we pull it from the mid bucket?
                var amountNeeded = amount - totalCashOnHand;
                var cashSold = SellInvestment(amountNeeded, McInvestmentPositionType.MID_TERM, currentDate);
                if (_corePackage.DebugMode == true)
                {
                    AddReconLine(
                        currentDate,
                        ReconciliationLineItemType.Credit,
                        cashSold,
                        "Investment sales from mid-term to support cash withdrawal"
                    );
                }
                totalCashOnHand += cashSold;
                if (totalCashOnHand < amount)
                {
                    // can we pull it from the long-term bucket?
                    cashSold = SellInvestment(amountNeeded, McInvestmentPositionType.LONG_TERM, currentDate);
                    if (_corePackage.DebugMode == true)
                    {
                        AddReconLine(
                            currentDate,
                            ReconciliationLineItemType.Credit,
                            cashSold,
                            "Investment sales from long-term to support cash withdrawal"
                        );
                    }
                    totalCashOnHand += cashSold;
                    if (totalCashOnHand < amount)
                    {
                        // we broke. update the account balance just in case.
                        // returning false here should result in a bankruptcy
                        // witch sets everything to 0, but we may change code
                        // flow later and it's important to add our sales
                        // proceeds to the cash account
                        UpdateCashAccountBalance(totalCashOnHand, currentDate);
                        _isBankrupt = true;
                        return false;
                    }
                }
                    
            }
            totalCashOnHand -= amount;
            UpdateCashAccountBalance(totalCashOnHand, currentDate);
            _totalSpendLifetime += amount;
            if (_corePackage.DebugMode == true)
            {
                AddReconLine(
                    currentDate,
                    ReconciliationLineItemType.Debit,
                    amount,
                    "Cash withdrawal"
                );
            }
            return true;
        }

#endregion public interface

#region Reconciliation methods
// these methods are used to validate that the code is producing the correct results
// they should not be used in the normal running of the sim



        public decimal Recon_GetAssetTotalByType(McInvestmentPositionType t)
        {
            decimal total = 0.0M;
            var accounts = _investmentAccounts
                .Where(x => x.AccountType != McInvestmentAccountType.PRIMARY_RESIDENCE
                    && x.AccountType != McInvestmentAccountType.CASH)
                .ToList();
            foreach (var a in accounts)
            {
                var positions = a.Positions
                    .Where(x => x.InvenstmentPositionType == t && x.IsOpen)
                    .ToList();
                foreach (var p in positions)
                {
                    total += p.CurrentValue;
                }
            }
            return total;
        }
        public decimal Recon_GetDebtTotal()
        {
            decimal total = 0.0M;
            foreach (var a in _debtAccounts)
            {
                var positions = a.Positions
                    .Where(x => x.IsOpen)
                    .ToList();
                foreach (var p in positions)
                {
                    total += p.CurrentBalance;
                }
            }
            return total;
        }
        



#endregion



        #region private methods

        private void AddRmdDistribution(LocalDateTime currentDate, decimal amount)
        {
            int year = currentDate.Year;
            if (_rmdDistributions.ContainsKey(year))
            {
                _rmdDistributions[year] += amount;
            }
            else _rmdDistributions[year] = amount;
        }
        private decimal GetInvestmentAccountTotalValue(McInvestmentAccount account)
        {
            return account.Positions.Sum(x => {
                if (x.IsOpen && x is McInvestmentPosition)
                {
                    var ip = x as McInvestmentPosition;
                    if (ip is null) return 0;
                    return ip.CurrentValue;
                }
                return 0;
            });
        }
        public decimal GetCashBalance()
        {
            return _cash.Positions.Sum(x => {
                if (!x.IsOpen) return 0M;
                var ip = (McInvestmentPosition)x;
                return ip.CurrentValue;
            });
        }
        private List<McInvestmentPosition> GetInvestmentPositionsByAccountTypeAndPositionType(
            McInvestmentAccountType accountType, McInvestmentPositionType mcInvestmentPositionType,
            LocalDateTime currentDate)
        {
            var positions = _investmentAccounts
                .Where(x => x.AccountType == accountType)
                .SelectMany(x => x.Positions.Where(y =>
                {
                    if (!y.IsOpen) return false;
                    if (y.Entry > currentDate.PlusYears(-1)) return false;
                    if (y is not McInvestmentPosition) return false;
                    var ip = y as McInvestmentPosition;
                    if (ip is null) return false;
                    if (ip.InvenstmentPositionType != mcInvestmentPositionType) return false;
                    return true;
                }
                    ))
                .ToList();

            List<McInvestmentPosition> investmentPositions = [];
            foreach (var p in positions)
            {
                var ip = p as McInvestmentPosition;
                if (ip is null) continue;
                investmentPositions.Add(ip);
            }
            return investmentPositions;
        }
        private decimal GetLongBucketTotalBalance()
        {
            return GetTotalBalanceByBucketType(McInvestmentPositionType.LONG_TERM);
        }
        private decimal GetMidBucketTotalBalance()
        {
            return GetTotalBalanceByBucketType(McInvestmentPositionType.MID_TERM);
        }
        private decimal GetTotalBalanceByBucketType(McInvestmentPositionType bucketType)
        {
            var totalBalance = 0M;
            var accounts = _investmentAccounts.Where(x => {
                if (x.AccountType == McInvestmentAccountType.PRIMARY_RESIDENCE) return false;
                if (x.AccountType == McInvestmentAccountType.CASH) return false;
                return true;
            });
            foreach (var account in accounts)
            {
                var totalValueBegin = GetInvestmentAccountTotalValue(account);
                totalBalance += account.Positions
                    .Where(x => x.IsOpen && x is McInvestmentPosition)
                    .Sum(x => {
                        var ip = x as McInvestmentPosition;
                        if (ip is null) return 0M;
                        if (ip.InvenstmentPositionType != bucketType) return 0M;
                        return ip.CurrentValue;
                    });
            }
            return totalBalance;
        }
        private void RemoveClosedPositions()
        {
            foreach (var account in _investmentAccounts)
            {
                account.Positions = account.Positions.Where(account => account.IsOpen).ToList();
            }
        }

        /// <summary>
        /// go through the accounts in order of sales preference and sell out
        /// of positions until you've met the amount needed. Also add taxable
        /// income to the tax sheet. Does not add the proceeds to the cash
        /// account because sometimes you just want to reinvest them into a
        /// different bucket
        /// </summary>
        /// <returns>the amount actually sold (may go under or over)</returns>
        private decimal SellInvestment(decimal amountNeededGlobal,
            McInvestmentPositionType mcInvestmentPositionType, LocalDateTime currentDate
            , bool isRmd = false)
        {
            decimal amountSoldGlobal = 0M;
            var incomeRoom = _taxFiler.CalculateIncomeRoom(currentDate.Year);



            // pull all relevant positions and sell until you've reached a
            // given amount or more. return what was actually sold
            Func<McInvestmentAccountType, decimal, decimal> sellToAmount = (McInvestmentAccountType accountType, decimal cap) =>
            {
                decimal amountSoldLocal = 0M; // this is the amount sold in just this internal amount
                var positions = GetInvestmentPositionsByAccountTypeAndPositionType(
                        accountType, mcInvestmentPositionType, currentDate);
                foreach (var p in positions)
                {
                    if (amountSoldGlobal >= amountNeededGlobal) break;
                    if (amountSoldLocal >= cap) break;
                    

                    // sell the whole thing; we should have split these
                    // up into small enough pieces that that's okay
                    _taxFiler.LogInvestmentSale(currentDate, p, accountType);
                    amountSoldGlobal += p.CurrentValue;
                    amountSoldLocal += p.CurrentValue;
                    p.Quantity = 0;
                    p.IsOpen = false;
                }
                // add amount to RMD if qualified
                if (accountType is McInvestmentAccountType.TRADITIONAL_401_K ||
                    accountType is McInvestmentAccountType.TRADITIONAL_IRA)
                    AddRmdDistribution(currentDate, amountSoldLocal);

                return amountSoldLocal;
            };
            if(isRmd)
            {
                // this is a special circumstance just for EOY RMD requirements
                // meeting. It should have no concern for income room as, by
                // definition RMDs are a way for the IRS to get their money no
                // matter how well you've tax-advantaged your approach
                foreach (var accountType in _salesOrderRmd)
                {
                    if (amountSoldGlobal >= amountNeededGlobal) break;
                    sellToAmount(accountType, amountNeededGlobal - amountSoldGlobal);
                }
            }
            else if (incomeRoom <= 0)
            {
                // no more income room. try to fill the order from Roth or HSA
                // accounts, then brokerage, then traditional accounts
                foreach (var accountType in _salesOrderWithNoRoom)
                {
                    if (amountSoldGlobal >= amountNeededGlobal) break;
                    sellToAmount(accountType, amountNeededGlobal - amountSoldGlobal);
                }

            }
            else
            {
                // sell up to the incomeRoom amount using traditional accounts,
                // then, once the income room is reached, fulfill from
                // Roth / HSA, then brokerage, then traditional

                decimal amountSoldLocal = 0M; // just the amount sold in first pass (the salesOrderWithRoom flow)
                foreach (var accountType in _salesOrderWithRoom)
                {
                    if (amountSoldGlobal >= amountNeededGlobal) break;
                    if (amountSoldLocal >= incomeRoom) break;

                    /*
                     * scenario 1:
                     *    amountNeededGlobal = 35,000.00
                     *    amountSoldGlobal = 0
                     *    globalPendingSale = 35,000.00 (amountNeededGlobal - amountSoldGlobal)
                     *    incomeRoom = 15,000.00
                     *    amountSoldLocal = 0
                     *    incomeRoomLeft = 15,000.00 (incomeRoom - amountSoldLocal)
                     *    amountNeededLocal= 15,000.00 (Math.Min(globalPendingSale, incomeRoomLeft))
                     *    
                     * scenario 2:
                     *    amountNeededGlobal = 35,000.00
                     *    amountSoldGlobal = 5,000.00
                     *    globalPendingSale = 30,000.00 (amountNeededGlobal - amountSoldGlobal)
                     *    incomeRoom = 15,000.00
                     *    amountSoldLocal = 5,000.00
                     *    incomeRoomLeft = 10,000.00 (incomeRoom - amountSoldLocal)
                     *    amountNeededLocal= 10,000.00 (Math.Min(globalPendingSale, incomeRoomLeft))
                     *    
                     * scenario 3:
                     *    amountNeededGlobal = 12,000.00
                     *    amountSoldGlobal = 5,000.00
                     *    globalPendingSale = 7,000.00 (amountNeededGlobal - amountSoldGlobal)
                     *    incomeRoom = 15,000.00
                     *    amountSoldLocal = 5,000.00
                     *    incomeRoomLeft = 10,000.00 (incomeRoom - amountSoldLocal)
                     *    amountNeededLocal= 7,000.00 (Math.Min(globalPendingSale, incomeRoomLeft))
                     *    
                     * */
                    decimal globalPendingSale = amountNeededGlobal - amountSoldGlobal;
                    decimal incomeRoomLeft = incomeRoom - amountSoldLocal;
                    decimal amountNeededLocal = Math.Min(globalPendingSale, incomeRoomLeft);
                    amountSoldLocal += sellToAmount(accountType, amountNeededLocal);
                }
                if (amountSoldGlobal < amountNeededGlobal)
                {
                    // still need more, but we've reached our income limit. Try
                    // to fill with tax free sales
                    foreach (var accountType in _salesOrderWithNoRoom)
                    {
                        if (amountSoldGlobal >= amountNeededGlobal) break;
                        sellToAmount(accountType, amountNeededGlobal - amountSoldGlobal);
                    }
                }
            }
#if DEBUGMODE
            thisMonthSold += amountSoldGlobal;
            totalSold += amountSoldGlobal;
#endif
            return amountSoldGlobal;

        }

        /// <summary>
        /// break up large positions into byte-sized chunks. this makes it
        /// easier to sell them. if we keep the sizes small, we don't have to
        /// worry about selling partial holdings. there is no tax implication
        /// as we're just making it easier to do math later on
        /// </summary>
        private void SplitPositionsAtMaxIndividualValue()
        {
            const decimal maxPositionValue = 1000M;
            var accounts = _investmentAccounts.Where(x => {
                if (x.AccountType == McInvestmentAccountType.PRIMARY_RESIDENCE) return false;
                if (x.AccountType == McInvestmentAccountType.CASH) return false;
                return true;
            });
            foreach (var account in accounts)
            {
                var totalValueBegin = GetInvestmentAccountTotalValue(account);
                var positions = account.Positions.Where(x => x.IsOpen && x is McInvestmentPosition).ToList();
                //List<Position> positionsToAdd = []; // you can't modify 
                foreach (var position in positions)
                {
                    var ip = position as McInvestmentPosition;
                    // if a position is greater than 1.5x the max, split it up.
                    // This gives an already split position room to grow before
                    // getting split again after growing by 1 cent
                    if (ip == null || ip.CurrentValue <= (1.5M * maxPositionValue)) continue;

                    var totalValue = ip.CurrentValue;
                    var averageCostBasis = Math.Round(ip.InitialCost / ip.Quantity, 4);
                    var totalSplitSoFar = 0M;
                    while (totalSplitSoFar < (totalValue - 1.0M))
                    {
                        var amountLeft = totalValue - totalSplitSoFar;
                        var amountInNewPosition = (amountLeft >= maxPositionValue) ?
                            maxPositionValue : amountLeft;
                        var quantityInNewPosition = Math.Round(amountInNewPosition / ip.Price, 4);
                        var costOfNewPosition = Math.Round(quantityInNewPosition * averageCostBasis, 4);
                        // add the new position
                        account.Positions.Add(new McInvestmentPosition()
                        {
                            Id = Guid.NewGuid(),
                            // InvestmentAccountId = account.Id,
                            Entry = ip.Entry,
                            InitialCost = costOfNewPosition,
                            InvenstmentPositionType = ip.InvenstmentPositionType,
                            IsOpen = true,
                            Name = "automated investment",
                            Price = ip.Price,
                            Quantity = quantityInNewPosition
                        });
                        // close the old one 
                        ip.IsOpen = false;
                        ip.InitialCost = 0;
                        ip.Quantity = 0;
                        // update the totalSplitSoFar
                        totalSplitSoFar += Math.Round(quantityInNewPosition * ip.Price, 4);
                    }
                    if (Math.Abs(totalSplitSoFar - totalValue) > 1.0M)
                    {
                        throw new InvalidDataException();
                    }
                }
                var totalValueEnd = GetInvestmentAccountTotalValue(account);
                var absDiff = Math.Abs(totalValueBegin - totalValueEnd);
                if (absDiff > 5.0M)
                {
                    // change one of the positions quantities to account for
                    // the difference. still not sure why this difference gets so great
                    var firstPosition = account.Positions.Where(x =>
                    {
                        if (!x.IsOpen) return false;
                        if (x is not McInvestmentPosition) return false;
                        var ip = x as McInvestmentPosition;
                        if (ip is null) return false;
                        if (ip.CurrentValue < absDiff) return false;
                        return true;
                    }).FirstOrDefault() as McInvestmentPosition;
                    if (firstPosition is not null)
                    {
                        var correctionAmount = totalValueBegin - totalValueEnd;
                        // if positive, you need to make your position bigger
                        // if negative, make it smaller
                        var newTotalValueOfPosition =
                            firstPosition.CurrentValue + correctionAmount;
                        var newQuantity = newTotalValueOfPosition / firstPosition.Price;
                        firstPosition.Quantity = newQuantity;
                    }
                    else
                    {
                        // forget about it, you tried
                    }
                }
            }
        }
        private void TopUpCashBucket(LocalDateTime currentDate)
        {
            // if it's been a good year, sell long-term growth assets and
            // top-up cash. if it's been a bad year, sell mid-term assets to
            // top-up cash.

            
            // todo: there's a lot of DRY violation with the reconciliation lines
            
            int numMonths = _simParams.NumMonthsCashOnHand;
            // subtract the number of months until retirement because you don't need to have it all at once
            if (currentDate < _retirementDate)
                numMonths -= (int)(Math.Round((_retirementDate - currentDate).Days / 30f, 0));

            if (numMonths <= 0) return;

            decimal totalCashWanted = numMonths * _simParams.DesiredMonthlySpend;
            decimal cashOnHand = GetCashBalance();
            decimal cashNeeded = totalCashWanted - cashOnHand;
            if (cashNeeded > 0)
            {
                // need to pull from one of the buckets. 
                if (_areWeInADownYear)
                {
                    // pull what we can from the mid-term bucket
                    var cashSold = SellInvestment(cashNeeded, McInvestmentPositionType.MID_TERM, currentDate);
                    if (_corePackage.DebugMode == true)
                    {
                        AddReconLine(
                            currentDate,
                            ReconciliationLineItemType.Debit,
                            cashSold,
                            "Rebalance: Selling mid-term investment"
                        );
                    }
                    DepositCash(cashSold, currentDate);
                    if (_corePackage.DebugMode == true)
                    {
                        AddReconLine(
                            currentDate,
                            ReconciliationLineItemType.Credit,
                            cashSold,
                            "Rebalance: Depositing investment sales proceeds"
                        );
                    }

                    // pull any remaining from the long-term bucket
                    cashNeeded = cashNeeded - cashSold;
                    if (cashNeeded > 0)
                    {
                        cashSold = SellInvestment(cashNeeded, McInvestmentPositionType.LONG_TERM, currentDate);
                        if (_corePackage.DebugMode == true)
                        {
                            AddReconLine(
                                currentDate,
                                ReconciliationLineItemType.Debit,
                                cashSold,
                                "Rebalance: Selling long-term investment"
                            );
                        }
                        DepositCash(cashSold, currentDate);
                        if (_corePackage.DebugMode == true)
                        {
                            AddReconLine(
                                currentDate,
                                ReconciliationLineItemType.Credit,
                                cashSold,
                                "Rebalance: Depositing investment sales proceeds"
                            );
                        }
                    }
                }
                else
                {
                    // pull what we can from the long-term bucket
                    var cashSold = SellInvestment(cashNeeded, McInvestmentPositionType.LONG_TERM, currentDate);
                    if (_corePackage.DebugMode == true)
                    {
                        AddReconLine(
                            currentDate,
                            ReconciliationLineItemType.Debit,
                            cashSold,
                            "Rebalance: Selling long-term investment"
                        );
                    }
                    DepositCash(cashSold, currentDate);
                    if (_corePackage.DebugMode == true)
                    {
                        AddReconLine(
                            currentDate,
                            ReconciliationLineItemType.Credit,
                            cashSold,
                            "Rebalance: Depositing investment sales proceeds"
                        );
                    }
                    // pull any remaining from the mid-term bucket
                    cashNeeded = cashNeeded - cashSold;
                    if (cashNeeded > 0)
                    {
                        cashSold = SellInvestment(cashNeeded, McInvestmentPositionType.MID_TERM, currentDate);
                        if (_corePackage.DebugMode == true)
                        {
                            AddReconLine(
                                currentDate,
                                ReconciliationLineItemType.Debit,
                                cashSold,
                                "Rebalance: Selling mid-term investment"
                            );
                        }
                        DepositCash(cashSold, currentDate);
                        if (_corePackage.DebugMode == true)
                        {
                            AddReconLine(
                                currentDate,
                                ReconciliationLineItemType.Credit,
                                cashSold,
                                "Rebalance: Depositing investment sales proceeds"
                            );
                        }
                    }
                }
            }
        }
        private void TopUpMidBucket(LocalDateTime currentDate)
        {
            // if it's been a good year, sell long-term growth assets and top-up mid-term.
            // if it's been a bad year, sit tight and hope the recession doesn't
            // outlast your mid-term bucket.

            int numMonths = _simParams.NumMonthsMidBucketOnHand;
            // subtract the number of months until retirement because you don't need to have it all at once
            if (currentDate < _retirementDate)
                numMonths -= (int)(Math.Round((_retirementDate - currentDate).Days / 30f, 0));

            if (numMonths <= 0) return;

            decimal totalAmountWanted = numMonths * _simParams.DesiredMonthlySpend;
            decimal amountOnHand = GetMidBucketTotalBalance();
            decimal amountNeeded = totalAmountWanted - amountOnHand;
            decimal maxAmountToPull = _simParams.DesiredMonthlySpend * _simParams.NumMonthsCashOnHand; // don't pull more in one go than the cash on hand goal
            amountNeeded = Math.Min(amountNeeded, maxAmountToPull);
            if (amountNeeded > 0)
            {
                // need to pull from one of the buckets. 
                if (_areWeInADownYear)
                {
                    // rub some dirt on it, sissy
                }
                else
                {
                    // pull what we can from the long-term bucket
                    var cashSold = SellInvestment(amountNeeded, McInvestmentPositionType.LONG_TERM, currentDate);
                    if (_corePackage.DebugMode == true)
                    {
                        AddReconLine(
                            currentDate,
                            ReconciliationLineItemType.Credit,
                            cashSold,
                            "Rebalance: Selling long-term investment"
                        );
                    }
                    // and invest it back into mid-term
                    InvestFunds(currentDate, cashSold, McInvestmentPositionType.MID_TERM, McInvestmentAccountType.TAXABLE_BROKERAGE);
                    if (_corePackage.DebugMode == true)
                    {
                        AddReconLine(
                            currentDate,
                            ReconciliationLineItemType.Debit,
                            cashSold,
                            "Rebalance: adding investment sales to mid-term investment"
                        );
                    }
                }
            }
        }
        private void UpdateCashAccountBalance(decimal newBalance, LocalDateTime _currentDate)
        {
            _cash.Positions = [
                    new McInvestmentPosition(){
                        Id = Guid.NewGuid(),
                        // InvestmentAccountId = _cash.Id,
                        Entry = _currentDate,
                        Price = 1,
                        Quantity = newBalance,
                        InitialCost = 0,
                        InvenstmentPositionType = McInvestmentPositionType.SHORT_TERM,
                        IsOpen = true,
                        Name = "default cash account"}
                    ];
        }

        #endregion private methods
    }
}