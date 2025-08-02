using System.Text;
using Lib.DataTypes.MonteCarlo;
using NodaTime;
using System.Collections.Concurrent;
using Lib.DataTypes;
using Lib.MonteCarlo.StaticFunctions;
using Lib.MonteCarlo.TaxForms.Federal;
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

        public LifeSimulator(
            Logger logger, McModel simParams, PgPerson person, List<McInvestmentAccount> investmentAccounts,
            List<McDebtAccount> debtAccounts, Dictionary<LocalDateTime, Decimal> hypotheticalPrices) 
        {
            // need to create a book of accounts before you can normalize positions
            var accounts = Account.CreateBookOfAccounts(
                AccountCopy.CopyInvestmentAccounts(investmentAccounts), AccountCopy.CopyDebtAccounts(debtAccounts));
            // set up a CurrentPrices sheet at the default starting rates
            var prices = new CurrentPrices();
            // set investment positions in terms of the default long, middle, and short-term prices
            accounts = Investment.NormalizeInvestmentPositions(accounts, prices);
            // set up the monthly social security wage, add it to both person and ledger
            var monthlySocialSecurityWage = Person.CalculateMonthlySocialSecurityWage(person,
                simParams.SocialSecurityStart);
            var copiedPerson = Person.CopyPerson(person, false);
            copiedPerson.AnnualSocialSecurityWage = monthlySocialSecurityWage * 12; // todo: set other caLculated fields for a person here
            var ledger = new TaxLedger();
            ledger.SocialSecurityWageMonthly = monthlySocialSecurityWage;
            ledger.SocialSecurityElectionStartDate = simParams.SocialSecurityStart;
            
            // set up the sim struct to be used to keep track of all the sim data
            _sim = new MonteCarloSim()
            {
                Log = logger,
                SimParameters = simParams,
                BookOfAccounts = accounts,
                PgPerson = copiedPerson,
                CurrentDateInSim = StaticConfig.MonteCarloConfig.MonteCarloSimStartDate,
                CurrentPrices = prices,
                RecessionStats = new RecessionStats(),
                TaxLedger = ledger,
                LifetimeSpend = new LifetimeSpend(),
            };
            _hypotheticalPrices = hypotheticalPrices;
            _measurements = [];
        }
        
        public List<NetWorthMeasurement> Run()
        {
            // todo: have static functions return a set of log messages that get added to the recon in this class. that
            // way we can stop counting on only 1 log class being accessed at any given time 
            try
            {
                while (_sim.CurrentDateInSim <= StaticConfig.MonteCarloConfig.MonteCarloSimEndDate)
                {
                    if (StaticConfig.MonteCarloConfig.DebugMode)
                    {
                        Reconciliation.AddFullReconLine(_sim, 0, $"Starting new month: {_sim.CurrentDateInSim}");
                        _sim.Log.Debug($"Starting new month: {_sim.CurrentDateInSim}");
                    }

                    if (!_sim.PgPerson.IsBankrupt)
                    {
                        // net worth is still positive. keep calculating stuff
                        SetGrowthAndPrices();
                        CheckForRetirement();
                        AccrueInterest();
                        ProcessPayday();
                        PayDownLoans();
                        UpdateRecessionStats();
                        RebalancePortfolio();
                        PayForStuff();
                        
                        if (_sim.CurrentDateInSim.Month == 1)
                        {
                            CleanUpAccounts();
                            PayTax();
                        }

                        if (_sim.CurrentDateInSim.Month == 12)
                        {
                            MeetRmdRequirements();
                        }
                    }

                    var measurement = Account.CreateNetWorthMeasurement(_sim);
                    if (measurement.NetWorth <= 0 || _sim.PgPerson.IsBankrupt)
                    {
                        // zero it out to make reporting cleaner
                        // and don't bother calculating anything further
                        measurement.TotalAssets = 0;
                        measurement.TotalLiabilities = 0;
                        _sim.PgPerson.IsBankrupt = true;
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

        #region Private methods
        
        private void AccrueInterest()
        {
            if (_sim.PgPerson.IsBankrupt) return;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Accruing interest");
            }
            var result = AccountInterestAccrual.AccrueInterest(
                _sim.CurrentDateInSim, _sim.BookOfAccounts, _sim.CurrentPrices, _sim.LifetimeSpend);
            _sim.BookOfAccounts = result.newAccounts;
            _sim.LifetimeSpend = result.newSpend;
        
            if (StaticConfig.MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, 0M, "Accrued interest");
            }
        }
        
        private void CheckForRetirement()
        {
            // if already retired, no need to check again
            if (_sim.PgPerson.IsRetired) return; 
            
            // if not retired, check if we're retiring
            var result = Simulation.SetIsRetiredFlagIfNeeded(
                _sim.CurrentDateInSim, _sim.PgPerson, _sim.SimParameters);
            
            // if result is still false, just return
            if(!result.isRetired) return;
            
            // retirement day; update the person object and log the event
            _sim.PgPerson = result.person;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, 0, "Retirement day!");
            }
        }
        
        private void CleanUpAccounts()
        {
            if (_sim.PgPerson.IsBankrupt) return;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Cleaning up accounts");
            }
            _sim.BookOfAccounts = AccountCleanup.CleanUpAccounts(_sim.CurrentDateInSim, _sim.BookOfAccounts, _sim.CurrentPrices);
            if (StaticConfig.MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, 0M, "Cleaned up accounts");
            }
            return;
        }
        
        private void DeclareBankruptcy()
        {
            _sim.PgPerson.IsBankrupt = true;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Bankruptcy declared");
            }
            return;
        }
        
        private void MeetRmdRequirements()
        {
            if (_sim.PgPerson.IsBankrupt) return;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Meeting RMD requirements");
            }
            var age = _sim.CurrentDateInSim.Year - _sim.PgPerson.BirthDate.Year;
            // todo: determine if MeetRmdRequirements needs to return amountSold. it probably already logs it
            var result = Tax.MeetRmdRequirements(
                _sim.TaxLedger, _sim.CurrentDateInSim, _sim.BookOfAccounts, age);
            _sim.BookOfAccounts = result.newBookOfAccounts;
            _sim.TaxLedger = result.newLedger;
        
            if (StaticConfig.MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, result.amountSold, "RMD requirements met");
            }
            return;
        }
        
        private void PayDownLoans()
        {
            if (_sim.PgPerson.IsBankrupt)
            {
                return;
            }
        
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Paying down loans");
            }

            var paymentResult = AccountDebtPayment.PayDownLoans(
                _sim.BookOfAccounts, _sim.CurrentDateInSim, _sim.TaxLedger, _sim.LifetimeSpend);
            _sim.BookOfAccounts = paymentResult.newBookOfAccounts;
            _sim.TaxLedger = paymentResult.newLedger;
            _sim.LifetimeSpend = paymentResult.newSpend;
        
            if (paymentResult.isSuccessful == false)
            {
                DeclareBankruptcy();
                return;
            }
            if (MonteCarloConfig.DebugMode == true)
            {
                Reconciliation.AddFullReconLine(_sim, 0, "Pay down debt completed");
            }
            return;
        }
        
        private void PayForStuff()
        {
            if (_sim.PgPerson.IsBankrupt) return;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Time to spend the money");
            }

            var results = Simulation.PayForStuff(_sim.SimParameters, _sim.PgPerson, 
                _sim.CurrentDateInSim, _sim.RecessionStats, _sim.TaxLedger, _sim.LifetimeSpend, _sim.BookOfAccounts);
            _sim.BookOfAccounts = results.accounts;
            _sim.TaxLedger = results.ledger;
            _sim.LifetimeSpend = results.spend;
            if (results.isSuccessful == false) DeclareBankruptcy();
      
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, 0, "Monthly spend spent");
            }
            return ;
        }
        
        private void PayTax()
        {
            if (_sim.PgPerson.IsBankrupt) return;
        
            var taxYear = _sim.CurrentDateInSim.Year - 1;
            var newTaxLedger = _sim.TaxLedger;
        
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, $"Paying taxes for tax year {taxYear}");
            }

            var taxResult = Simulation.PayTaxForYear(_sim.PgPerson, _sim.CurrentDateInSim,
                _sim.TaxLedger, _sim.LifetimeSpend, _sim.BookOfAccounts, _sim.CurrentDateInSim.Year -1);
            _sim.BookOfAccounts = taxResult.accounts;
            _sim.TaxLedger = taxResult.ledger;
            _sim.LifetimeSpend = taxResult.spend;
        
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, 0, "Paid taxes");
            }

            return;
        }
        
        private void ProcessPayday()
        {
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, 0, "processing payday");
            }
            
            
            var results = Simulation.ProcessPayday(_sim.PgPerson, _sim.CurrentDateInSim,
                _sim.BookOfAccounts, _sim.TaxLedger, _sim.LifetimeSpend, _sim.SimParameters, _sim.CurrentPrices);
            _sim.BookOfAccounts = results.accounts;
            _sim.TaxLedger = results.ledger;
            _sim.LifetimeSpend = results.spend;
            
            
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, 0, "processed payday");
            }
        }
        
        private void RebalancePortfolio()
        {
            // todo: create a UT that ensures that excess cash gets invested pre-retirement
            if (_sim.PgPerson.IsBankrupt) return;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Rebalancing portfolio");
            }
        
            var results = Rebalance.RebalancePortfolio(_sim.CurrentDateInSim, _sim.BookOfAccounts, _sim.RecessionStats, 
                _sim.CurrentPrices, _sim.SimParameters, _sim.TaxLedger, _sim.PgPerson);
            _sim.BookOfAccounts = results.newBookOfAccounts;
            _sim.TaxLedger = results.newLedger;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, 0M, "Rebalanced portfolio");
            }
        }
        
        private void SetGrowthAndPrices()
        {
            _sim.CurrentPrices = Simulation.SetNewPrices(
                _sim.CurrentPrices, _hypotheticalPrices, _sim.CurrentDateInSim);
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim, _sim.CurrentPrices.CurrentLongTermGrowthRate,
                    "Updated prices with new long term growth rate");
            }
        }
        
        private void UpdateRecessionStats()
        {
            // do our recession checking every month, regardless of whether
            // it's time to move money around. this gives us a finer grain for
            // determining down years
            _sim.RecessionStats = Recession.CalculateRecessionStats(
                _sim.RecessionStats, _sim.CurrentPrices, _sim.SimParameters, _sim.BookOfAccounts,
                _sim.CurrentDateInSim);
        }
        
        #endregion
    }
}