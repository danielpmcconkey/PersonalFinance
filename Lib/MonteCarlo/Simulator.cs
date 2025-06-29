using System.Text;
using Lib.DataTypes.MonteCarlo;
using NodaTime;
using System.Collections.Concurrent;
using Lib.MonteCarlo.StaticFunctions;

namespace Lib.MonteCarlo
{
    public class Simulator
    {
        /// <summary>
        /// our list of hypothetical price history to be used, passed in by whatever created the simulation
        /// </summary>
        private Dictionary<LocalDateTime, Decimal> _hypotheticalPrices;
        /// <summary>
        /// our month-over-month net worth measurements 
        /// </summary>
        private List<NetWorthMeasurement> _measurements;
        private LocalDateTime _retirementDate;
        
        // private Logger _logger;
        // private List<McInvestmentAccount> _investmentAccounts;
        // private List<McDebtAccount> _debtAccounts;
        // private McPerson _mcPerson;
        // private McModel _simParams;
        // //private long[] _sAndP500HistoricalTrends;
        // private LocalDateTime _currentDate;
        // private LocalDateTime _endDate;
     
        // //private long _netWorthAtRetirement = 0M;
        // //private int _historicalMonthsCount = 0;
        // //private int _historicalTrendsPointer = 0;
        
        // private long _totalSpend;
        //
        //
        //
        // private Bank _bank;
        // private TaxFiler _taxFiler;
        //
        // private CorePackage _corePackage;

        private MonteCarloSim _sim;
        



        public Simulator(McModel simParams, McPerson mcPerson,
            List<McInvestmentAccount> investmentAccounts, List<McDebtAccount> debtAccounts,
            Dictionary<LocalDateTime, Decimal> hypotheticalPrices) 
        {
            string logDir = ConfigManager.ReadStringSetting("LogDir");
            string timeSuffix = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
            string logFilePath = $"{logDir}MonteCarloLog{timeSuffix}.txt";
            _sim = new MonteCarloSim()
            {
                Log = new Logger(
                    StaticConfig.MonteCarloConfig.LogLevel,
                    logFilePath
                ),
                SimParameters = simParams,
                BookOfAccounts = Account.CreateBookOfAccounts(investmentAccounts, debtAccounts),
                CurrentDateInSim = StaticConfig.MonteCarloConfig.MonteCarloSimStartDate,
                Person = mcPerson,
            };
            _hypotheticalPrices = hypotheticalPrices;
            _measurements = [];
            _sim.Person.MonthlySocialSecurityWage = Person.CalculateMonthlySocialSecurityWage(_sim.Person,
                _sim.SimParameters.SocialSecurityStart);
            _sim.Person.Monthly401kMatch = Person.CalculateMonthly401kMatch(_sim.Person);
        }
        public List<NetWorthMeasurement> Run()
        {
            while (_sim.CurrentDateInSim <= StaticConfig.MonteCarloConfig.MonteCarloSimEndDate)
            {
                var priceGrowthRate = 0M;

                if (!_sim.Person.IsBankrupt)
                {
                    // net worth is still positive.
                    // keep calculating stuff

                    if (_sim.CurrentDateInSim == _retirementDate)
                    {
                        Retire();
                    }
                    if (_sim.CurrentDateInSim >= _sim.SimParameters.SocialSecurityStart)
                    {
                        // payday
                        GetSocialSecurityCheck();
                    }

                    if (!_hypotheticalPrices.TryGetValue(_sim.CurrentDateInSim, out priceGrowthRate))
                    {
                        throw new InvalidDataException("_currentDate not found in _hypotheticalPrices");
                    }
                    Pricing.SetLongTermGrowthRateAndPrices(_sim.CurrentPrices, priceGrowthRate);
                    Account.AccrueInterest(_sim.CurrentDateInSim, _sim.BookOfAccounts, _sim.CurrentPrices, _sim.LifetimeSpend);
                    PayDownLoans();
                    

                    if (_sim.CurrentDateInSim.Month == 1)
                    {
                        CleanUpAccounts();
                    }

                    RebalancePortfolio();


                    if (!_sim.Person.IsRetired)
                    {
                        // still in savings mode
                        AddMonthlySavings();
                    }
                    else
                    {
                        
                        
                        
                        
                        
                        
                        
                        
                        
                        /*
                         * you are here. we're still refactoring...forever
                         */
                        
                        
                        
                        
                        
                        
                        
                        
                        
                        
                        
                        
                        
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
        private void RebalancePortfolio()
        {
            if (_sim.Person.IsBankrupt) return;
            Rebalance.RebalancePortfolio(_sim.CurrentDateInSim, _sim.BookOfAccounts, _sim.RecessionStats, 
                _sim.CurrentPrices, _sim.SimParameters, _sim.TaxLedger);
            if (StaticConfig.MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, 0M, "Rebalanced portfolio");
            }
        }
        private void CleanUpAccounts()
        {
            if (_sim.Person.IsBankrupt) return;
            Account.CleanUpAccounts(_sim.CurrentDateInSim, _sim.BookOfAccounts, _sim.CurrentPrices);
            if (StaticConfig.MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, 0M, "Cleaned up accounts");
            }
        }
        private void AccrueInterest()
        {
            if (_sim.Person.IsBankrupt) return;
            Account.AccrueInterest(_sim.CurrentDateInSim, _sim.BookOfAccounts, _sim.CurrentPrices, _sim.LifetimeSpend);
            if (StaticConfig.MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, 0M, "Accrued interest");
            }
        }
        private long PayTax()
        {
            if (_isBankrupt) return 0;
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
        
        private bool SpendCash(long amount)
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
                    
                    var og_balance = p.CurrentBalance; // only used for debug recon
                    
                    long amount = p.MonthlyPayment;
                    if (amount > p.CurrentBalance) amount = p.CurrentBalance;
                    if (!SpendCash(amount))
                    {
                        DeclareBankruptcy();
                        break;
                    }

                    p.CurrentBalance -= amount;
                    _bank.RecordDebtPayment(amount, _currentDate);
                    
                    if (_corePackage.DebugMode == true)
                    {
                        _bank.AddReconLine(
                            _currentDate,
                            ReconciliationLineItemType.Debit,
                            amount,
                            $"Pay down debt {account.Name} {p.Name} from {og_balance} to {p.CurrentBalance}"
                        );
                    }
                    if(p.CurrentBalance <= 0)
                    {
                         _logger.Debug($"Paid off {p.Name}");
                        p.CurrentBalance = 0;
                        p.IsOpen = false;
                    }
                }
            }
            if (_corePackage.DebugMode == true)
            {
                _bank.AddReconLine(
                    _currentDate,
                    ReconciliationLineItemType.Debit,
                    0,
                    $"Pay down debt completed"
                );
            }
        }
        private void AddMonthlySavings()
        {
            if (_sim.Person.IsBankrupt)
            {
                return;
            }

            Investment.InvestFunds(_sim.BookOfAccounts, _sim.CurrentDateInSim, _sim.SimParameters.MonthlyInvest401kRoth,
                McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.ROTH_401_K, _sim.CurrentPrices);

            Investment.InvestFunds(_sim.BookOfAccounts, _sim.CurrentDateInSim, _sim.SimParameters.MonthlyInvest401kTraditional,
                McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TRADITIONAL_401_K, _sim.CurrentPrices);

            Investment.InvestFunds(_sim.BookOfAccounts, _sim.CurrentDateInSim, _sim.SimParameters.MonthlyInvestBrokerage,
                McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TAXABLE_BROKERAGE, _sim.CurrentPrices);

            Investment.InvestFunds(_sim.BookOfAccounts, _sim.CurrentDateInSim, _sim.SimParameters.MonthlyInvestHSA,
                McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.HSA, _sim.CurrentPrices);

            Investment.InvestFunds(_sim.BookOfAccounts, _sim.CurrentDateInSim, _sim.Person.Monthly401kMatch,
                McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TRADITIONAL_401_K, _sim.CurrentPrices);
            
            if (StaticConfig.MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, 0M, "Add monthly savings completed");
            }
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
