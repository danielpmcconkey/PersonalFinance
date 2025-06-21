using System.Text;
using Lib.DataTypes.MonteCarlo;
using NodaTime;
using System.Collections.Concurrent;

namespace Lib.MonteCarlo
{
    public class Simulator
    {
        private Logger _logger;
        private List<McInvestmentAccount> _investmentAccounts;
        private List<McDebtAccount> _debtAccounts;
        private McPerson _mcPerson;
        private McModel _simParams;
        private List<NetWorthMeasurement> _measurements;
        //private decimal[] _sAndP500HistoricalTrends;
        private LocalDateTime _currentDate;
        private LocalDateTime _endDate;
        private LocalDateTime _retirementDate;
        private bool _isRetired;
        private bool _isBankrupt;
        private decimal _monthly401kMatch = 0M;
        //private decimal _netWorthAtRetirement = 0M;
        private int _simMonthCount = 0;
        //private int _historicalMonthsCount = 0;
        //private int _historicalTrendsPointer = 0;
        private decimal _monthlySocialSecurityWage = 0;
        private decimal _totalSpend;

        private Dictionary<LocalDateTime, Decimal> _hypotheticalPrices;


        private Bank _bank;
        private TaxFiler _taxFiler;
        
        private CorePackage _corePackage;

        



        public Simulator(CorePackage corePackage, McModel simParams, McPerson mcPerson,
            List<McInvestmentAccount> investmentAccounts, List<McDebtAccount> debtAccounts,
            Dictionary<LocalDateTime, Decimal> hypotheticalPrices) 
        {
            _corePackage = corePackage;
            _logger = corePackage.Log;
            _simParams = simParams;
            _mcPerson = mcPerson;
            _investmentAccounts = investmentAccounts;
            _debtAccounts = debtAccounts;
            _hypotheticalPrices = hypotheticalPrices;
            _measurements = [];
            _currentDate = NormalizeDate(_simParams.SimStartDate);
            _endDate = NormalizeDate(_simParams.SimEndDate);
            _retirementDate = NormalizeDate(_simParams.RetirementDate);
            _isRetired = false;
            _isBankrupt = false;
            _taxFiler = new TaxFiler(_corePackage);
            _bank = new (_corePackage,_simParams, _investmentAccounts, _debtAccounts,
                _retirementDate, _taxFiler);
            _taxFiler.SetBank(_bank);
            _monthlySocialSecurityWage = CalculateMonthlySocialSecurityWage();
            _totalSpend = 0M;

            //ResetTrendsPointer();

            

            _monthly401kMatch = _mcPerson.AnnualSalary * _mcPerson.Annual401kMatchPercent / 12;
        }
        public List<NetWorthMeasurement> Run()
        {
            while (_currentDate <= _endDate)
            {
                _logger.Debug(_logger.FormatHeading($"New month {_currentDate:MMM, yyyy}"));
                _simMonthCount++;
                decimal priceGrowthRate = 0.0M;

                if (!_isBankrupt)
                {
                    // net worth is still positive.
                    // keep calculating stuff

                    if (_currentDate == _retirementDate)
                    {
                        Retire();
                        
                    }
                    if (_currentDate >= _simParams.SocialSecurityStart)
                    {
                        // payday

                        GetSocialSecurityCheck();

                    }

                    if (!_hypotheticalPrices.TryGetValue(_currentDate, out priceGrowthRate))
                    {
                        throw new InvalidDataException("_currentDate not found in _hypotheticalPrices");
                    }
                    _bank.SetLongTermGrowthRate(priceGrowthRate);

                    _bank.AccrueInterest(_currentDate);


                    PayDownLoans();

                    if (_currentDate.Month == 1)
                    {
                        _bank.CleanUpAccounts(_currentDate);
                    }
                    _bank.Rebalance(_currentDate);


                    if (!_isRetired)
                    {
                        // still in savings mode
                        AddMonthlySavings();
                    }
                    else
                    {
                        // calculate draw downs, taxes, etc.
                        if (_currentDate.Month == 12) _bank.MeetRmdRequirements(_currentDate);
                        if (_currentDate.Month == 1)
                        {
                            PayTax();
                        }
                        PayForStuff();
                    }
                }
                var measurement = _bank.MeasureNetWorth(_currentDate);
                measurement.TotalSpend = _totalSpend;
                if (measurement.NetWorth <= 0 || _isBankrupt)
                {
                    // zero it out to make reporting cleaner
                    // and don't bother calculating anything further
                    measurement.TotalAssets = 0;
                    measurement.TotalLiabilities = 0;
                    _isBankrupt = true;
                }

                _measurements.Add(measurement);
                _currentDate = _currentDate.PlusMonths(1);
            }
            _logger.Debug(_logger.FormatHeading("End of simulated lifetime"));
            _bank.PrintReconciliation();
            return _measurements;
        }
        private decimal PayTax()
        {
            if (_isBankrupt) return 0.0M;
            var taxLiability = _taxFiler.CalculateTaxLiabilityForYear(_currentDate, _currentDate.Year - 1);

            if (!SpendCash(taxLiability))
                DeclareBankruptcy();
            
            if (_corePackage.DebugMode == true)
            {
                _bank.AddReconLine(
                    _currentDate,
                    ReconciliationLineItemType.Debit,
                    taxLiability,
                    "Paid taxes"
                );
            }
            return taxLiability;
        }
        private void GetSocialSecurityCheck()
        {
            _bank.DepositSocialSecurityCheck(_monthlySocialSecurityWage, _currentDate);
            _taxFiler.LogSocialSecurityIncome(_currentDate, _monthlySocialSecurityWage);
            if (_corePackage.DebugMode == true)
            {
                _bank.AddReconLine(
                    _currentDate,
                    ReconciliationLineItemType.Credit,
                    _monthlySocialSecurityWage,
                    "Social Security check processed"
                );
            }
        }
        private decimal CalculateMonthlySocialSecurityWage()
        {
            var maxWage = _mcPerson.MonthlyFullSocialSecurityBenefit;
            var benefitElectionStart = _simParams.SocialSecurityStart;
            var fullRetirementDate = _mcPerson.BirthDate.PlusYears(67);
            var timeSpanEarly = (fullRetirementDate - benefitElectionStart);
            int monthsEarly = (int)Math.Round(
                timeSpanEarly.Days / 365.25 * 12, 0);
            decimal penalty = 0.0M;
            if (monthsEarly <= 36)
            {
                penalty += 0.01M * (5M / 9M) * monthsEarly;
            }
            else
            {
                penalty += 0.01M * (5M / 9M) * 36;
                penalty += 0.01M * (5M / 12M) * (monthsEarly - 36);
            }
            penalty = Math.Max(penalty, 0M); // don't want to add on to max if I made a date math error
            var primaryWage = maxWage - (maxWage * penalty);
            return primaryWage;
        }
        private bool SpendCash(decimal amount)
        {
            // prior to retirement, don't debit the cash account as it's
            // assumed we're just paying our bills pre-retirement with our
            // surplus income
            if (!_isRetired) return true;
            else
            {
                return _bank.WithdrawCash(amount, _currentDate);
            }
        }
        private void Retire()
        {
            _isRetired = true;
            _logger.Debug(_logger.FormatHeading("Retirement"));
            if (_corePackage.DebugMode == true)
            {
                _bank.AddReconLine(
                    _currentDate,
                    ReconciliationLineItemType.Credit,
                    0,
                    "Retirement date"
                );
            }
        }
        
        private void PayForStuff()
        {
            if (_isBankrupt) return;
            var spendAmount = _simParams.DesiredMonthlySpend;
            if (_bank.AreWeInAusterityMeasures)
            {
                spendAmount = _simParams.DesiredMonthlySpend *
                    _simParams.AusterityRatio;
            }
            if (_bank.AreWeInExtremeAusterityMeasures)
            {
                spendAmount = _simParams.DesiredMonthlySpend *
                    _simParams.ExtremeAusterityRatio;
            }
            if (!SpendCash(spendAmount))
            {
                DeclareBankruptcy();
                return;
            }
            if (_corePackage.DebugMode == true)
            {
                _bank.AddReconLine(
                    _currentDate,
                    ReconciliationLineItemType.Debit,
                    spendAmount,
                    "Monthly spend"
                );
            }
            _totalSpend += spendAmount;
            return;
        }
        
        private void DeclareBankruptcy()
        {
            _isBankrupt = true;
        }


        private void PayDownLoans()
        {
            if (_isBankrupt)
            {
                return;
            }

            foreach (var account in _debtAccounts)
            {
                if (_isBankrupt) break;
                foreach (var p in account.Positions)
                {
                    if (_isBankrupt) break;
                    if (!p.IsOpen) continue;
                    decimal amount = Math.Round(p.MonthlyPayment, 2);
                    if (amount > p.CurrentBalance) amount = p.CurrentBalance;
                    if (!SpendCash(amount))
                    {
                        DeclareBankruptcy();
                        break;
                    }

                    p.CurrentBalance -= amount;
                    if(p.CurrentBalance <= 0)
                    {
                         _logger.Debug($"Paid off {p.Name}");
                        p.CurrentBalance = 0;
                        p.IsOpen = false;
                    }
                }
            }
        }
        private void AddMonthlySavings()
        {
            if (_isBankrupt)
            {
                return;
            }

            _bank.InvestFunds(_currentDate, _simParams.MonthlyInvest401kRoth,
                McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.ROTH_401_K);
            _bank.InvestFunds(_currentDate, _simParams.MonthlyInvest401kTraditional,
                McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TRADITIONAL_401_K);
            _bank.InvestFunds(_currentDate, _simParams.MonthlyInvestBrokerage,
                McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TAXABLE_BROKERAGE);
            _bank.InvestFunds(_currentDate, _simParams.MonthlyInvestHSA,
                McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.HSA);
            _bank.InvestFunds(_currentDate, _monthly401kMatch,
                McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TRADITIONAL_401_K);
        }
        /// <summary>
        /// reads any LocalDateTime and returns the first of the month closest to it
        /// </summary>
        private LocalDateTime NormalizeDate(LocalDateTime providedDate)
        {
            var firstOfThisMonth = new LocalDateTime(providedDate.Year, providedDate.Month, 1, 0, 0);
            var firstOfNextMonth = firstOfThisMonth.PlusMonths(1);
            var timeSpanToThisFirst = providedDate - firstOfThisMonth;
            var timeSpanToNextFirst = firstOfNextMonth - providedDate;
            return (timeSpanToThisFirst.Days <= timeSpanToNextFirst.Days) ?
                firstOfThisMonth : // t2 is longer, return this first
                firstOfNextMonth; // t1 is longer than t2, return next first
            
           
        }
        private static int GetRandomInt(int minInclusive, int maxInclusive)
        {
            CryptoRandom cr = new CryptoRandom();
            return cr.Next(minInclusive, maxInclusive + 1);
        }
    }
}
