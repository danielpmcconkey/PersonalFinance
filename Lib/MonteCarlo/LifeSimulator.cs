using System.Text;
using Lib.DataTypes.MonteCarlo;
using NodaTime;
using System.Collections.Concurrent;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;

namespace Lib.MonteCarlo
{
    /// <summary>
    /// Simulates a single life from sim start to sim end and creates a list of NetWorthMeasurements
    /// </summary>
    public class LifeSimulator
    {
        /// <summary>
        /// our list of hypothetical price history to be used, passed in by whatever created the simulation
        /// </summary>
        private Dictionary<LocalDateTime, Decimal> _hypotheticalPrices;
        /// <summary>
        /// our month-over-month net worth measurements 
        /// </summary>
        private List<NetWorthMeasurement> _measurements;
        
        
        /// <summary>
        /// this houses all of our simulation data: history and current state
        /// </summary>
        private MonteCarloSim _sim;
        



        public LifeSimulator(Logger logger, McModel simParams, McPerson person,
            Dictionary<LocalDateTime, Decimal> hypotheticalPrices) 
        {
            // need to create a book of accounts before you can normalize positions
            var accounts = Account.CreateBookOfAccounts(person.InvestmentAccounts, person.DebtAccounts);
            // set up a CurrentPrices sheet at the default starting rates
            var prices = new CurrentPrices();
            // set investment positions in terms of the default long, middle, and short-term prices
            Investment.NormalizeInvestmentPositions(accounts, prices);
            // set up the sim struct to be used to keep track of all the sim data
            _sim = new MonteCarloSim()
            {
                Log = logger,
                SimParameters = simParams,
                BookOfAccounts = accounts,
                Person = person,
                CurrentDateInSim = StaticConfig.MonteCarloConfig.MonteCarloSimStartDate,
                CurrentPrices = prices,
                RecessionStats = new RecessionStats(),
                TaxLedger = new TaxLedger(),
                LifetimeSpend = new LifetimeSpend(),
            };
            _hypotheticalPrices = hypotheticalPrices;
            _measurements = [];
            _sim.Person.MonthlySocialSecurityWage = Person.CalculateMonthlySocialSecurityWage(_sim.Person,
                _sim.SimParameters.SocialSecurityStart);
            _sim.Person.Monthly401kMatch = Person.CalculateMonthly401kMatch(_sim.Person);
        }
        public List<NetWorthMeasurement> Run()
        {
            try
            {
                while (_sim.CurrentDateInSim <= StaticConfig.MonteCarloConfig.MonteCarloSimEndDate)
                {
                    if (StaticConfig.MonteCarloConfig.DebugMode)
                    {
                        Reconciliation.AddFullReconLine(_sim, 0, $"Starting new month: {_sim.CurrentDateInSim}");
                        _sim.Log.Debug($"Starting new month: {_sim.CurrentDateInSim}");
                    }
                    
                    

                    if (!_sim.Person.IsBankrupt)
                    {
                        // net worth is still positive.
                        // keep calculating stuff

                        SetGrowthAndPrices();
                        
                        CheckForRetirement();
                        
                        AccrueInterest();

                        Payday();
                        
                        PayDownLoans();
                        
                        AddRetirementSavings();

                        RebalancePortfolio();
                        
                        PayForStuff();
                        
                        if (_sim.CurrentDateInSim.Month == 1)
                        {
                            CleanUpAccounts();

                            GetBonusPayment();
                            
                            PayTax();
                        }

                        if (_sim.CurrentDateInSim.Month == 12)
                        {
                            MeetRmdRequirements();
                        }
                    }

                    var measurement = Account.CreateNetWorthMeasurement(_sim);
                    if (measurement.NetWorth <= 0 || _sim.Person.IsBankrupt)
                    {
                        // zero it out to make reporting cleaner
                        // and don't bother calculating anything further
                        measurement.TotalAssets = 0;
                        measurement.TotalLiabilities = 0;
                        _sim.Person.IsBankrupt = true;
                    }

                    _measurements.Add(measurement);
                    _sim.CurrentDateInSim = _sim.CurrentDateInSim.PlusMonths(1);
                }

                Reconciliation.ExportToSpreadsheet();
                
                _sim.Log.Debug(_sim.Log.FormatHeading("End of simulated lifetime"));

            }
            catch (Exception e)
            {
                _sim.Log.Error(_sim.Log.FormatHeading("Error in Run()"));
                _sim.Log.Error(e.ToString());
                Reconciliation.ExportToSpreadsheet();
                throw;
            }
            
            
            return _measurements;
        }

        private void SetGrowthAndPrices()
        {
            if (!_hypotheticalPrices.TryGetValue(_sim.CurrentDateInSim, out var priceGrowthRate))
            {
                throw new InvalidDataException("CurrentDate not found in _hypotheticalPrices");
            }

            Pricing.SetLongTermGrowthRateAndPrices(_sim.CurrentPrices, priceGrowthRate);
        }

        private void CheckForRetirement()
        {
            if (_sim.CurrentDateInSim != _sim.SimParameters.RetirementDate) return;
            _sim.Person.IsRetired = true;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, 0, "Retired!");
            }
        }

        private void Payday()
        {
            GetSocialSecurityCheck();
            GetWorkingPaycheck();
        }

        
        private void GetBonusPayment()
        {
            if (_sim.Person.IsRetired) return;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Collecting bonus");
            }

            var grossMonthlyPay = _sim.Person.AnnualBonus;
            Tax.LogIncome(_sim.TaxLedger, _sim.CurrentDateInSim, grossMonthlyPay);
            Account.DepositCash(_sim.BookOfAccounts, grossMonthlyPay, _sim.CurrentDateInSim);;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Collected bonus");
            }
        }
        private void GetWorkingPaycheck()
        {
            if (_sim.Person.IsRetired) return;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Collecting paycheck");
            }

            var grossMonthlyPay = _sim.Person.AnnualSalary / 12m;
            Tax.LogIncome(_sim.TaxLedger, _sim.CurrentDateInSim, grossMonthlyPay);
            Account.DepositCash(_sim.BookOfAccounts, grossMonthlyPay, _sim.CurrentDateInSim);;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Collected paycheck");
            }
        }

        private void MeetRmdRequirements()
        {
            if (_sim.Person.IsBankrupt) return;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Meeting RMD requirements");
            }
            var totalRmd = Tax.MeetRmdRequirements(
                _sim.TaxLedger, _sim.CurrentDateInSim, _sim.BookOfAccounts, _sim.CurrentPrices);
            if (StaticConfig.MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, totalRmd, "RMD requirements met");
            }
        }
        private void RebalancePortfolio()
        {
            if (_sim.Person.IsBankrupt) return;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Rebalancing portfolio");
            }
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
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Cleaning up accounts");
            }
            Account.CleanUpAccounts(_sim.CurrentDateInSim, _sim.BookOfAccounts, _sim.CurrentPrices);
            if (StaticConfig.MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, 0M, "Cleaned up accounts");
            }
        }
        private void AccrueInterest()
        {
            if (_sim.Person.IsBankrupt) return;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Accruing interest");
            }
            Account.AccrueInterest(_sim.CurrentDateInSim, _sim.BookOfAccounts, _sim.CurrentPrices, _sim.LifetimeSpend);
            if (StaticConfig.MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, 0M, "Accrued interest");
            }
        }
        private decimal PayTax()
        {
            if (_sim.Person.IsBankrupt) return 0;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Tax day");
            }
            var taxLiability = Tax.CalculateTaxLiabilityForYear(_sim.TaxLedger, _sim.CurrentDateInSim.Year - 1);

            SpendCash(taxLiability);
            
            _sim.TaxLedger.TotalTaxPaid += taxLiability;
            
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, taxLiability, "Paid taxes");
            }
            return taxLiability;
        }
        private void GetSocialSecurityCheck()
        {
            if (_sim.CurrentDateInSim < _sim.SimParameters.SocialSecurityStart)
            {
                return;
            }
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Social Security payday");
            }
            var amount = _sim.Person.MonthlySocialSecurityWage;
            Account.DepositCash(_sim.BookOfAccounts, amount, _sim.CurrentDateInSim);
            _sim.LifetimeSpend.TotalSocialSecurityWageLifetime += amount;
            Tax.LogSocialSecurityIncome(_sim.TaxLedger, _sim.CurrentDateInSim, amount);
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, amount, "Social Security check processed");
            }
        }
        
        private void SpendCash(decimal amount)
        {
            // prior to retirement, don't debit the cash account as it's
            // assumed we're just paying our bills pre-retirement with our
            // surplus income
            if (!_sim.Person.IsRetired) return; // todo: re-jigger spending pre-retirement to calc "fun" points
            
            var couldAfford = Account.WithdrawCash(_sim.BookOfAccounts, amount, _sim.CurrentDateInSim, _sim.TaxLedger);
            if (!couldAfford)
            {
                DeclareBankruptcy();
            }
            _sim.LifetimeSpend.TotalSpendLifetime += amount;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim, amount, "Spent cash");
            }
        }
        
        
        private void PayForStuff()
        {
            if (_sim.Person.IsBankrupt) return;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Time to spend the money");
            }
            var spendAmount = _sim.SimParameters.DesiredMonthlySpend;
            if (_sim.RecessionStats.AreWeInAusterityMeasures)
            {
                spendAmount = _sim.SimParameters.DesiredMonthlySpend *
                    _sim.SimParameters.AusterityRatio;
            }
            if (_sim.RecessionStats.AreWeInExtremeAusterityMeasures)
            {
                spendAmount = _sim.SimParameters.DesiredMonthlySpend *
                    _sim.SimParameters.ExtremeAusterityRatio;
            }
            SpendCash(spendAmount);
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, spendAmount, "Monthly spend spent");
            }
        }
        
        private void DeclareBankruptcy()
        {
            _sim.Person.IsBankrupt = true;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Bankruptcy declared");
            }
        }


        private void PayDownLoans()
        {
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Paying down loans");
            }
            if (_sim.Person.IsBankrupt)
            {
                return;
            }

            if (Account.PayDownLoans(
                    _sim.BookOfAccounts, _sim.CurrentDateInSim, _sim.Person, _sim.TaxLedger, _sim.LifetimeSpend) ==
                false)
            {
                DeclareBankruptcy();
                return;
            }
            if (MonteCarloConfig.DebugMode == true)
            {
                Reconciliation.AddFullReconLine(_sim, 0, "Pay down debt completed");
            }
        }
        private void AddRetirementSavings()
        {
            if (_sim.Person.IsBankrupt || _sim.Person.IsRetired) return;
            
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Adding retirement savings");
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
                Reconciliation.AddFullReconLine(_sim, 0M, "Added retirement savings");
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
