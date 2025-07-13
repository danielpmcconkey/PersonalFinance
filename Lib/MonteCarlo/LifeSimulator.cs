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
            accounts = Investment.NormalizeInvestmentPositions(accounts, prices);
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
            _sim.Person.Monthly401kMatch = Person.CalculateMonthly401KMatch(_sim.Person);
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
                    _sim.RecessionStats = DetermineRecessionStats(measurement, _sim.RecessionStats);
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

        private RecessionStats DetermineRecessionStats(NetWorthMeasurement measurement, RecessionStats stats)
        {
            // todo: move the DetermineRecessionStats into the static functions and add unit tests
            // todo: move all recession determination functionality into DetermineRecessionStats
            
            var recessionStats = stats;
            // see if we're in extreme austerity measures based on total net worth
            if (measurement.NetWorth <= _sim.SimParameters.ExtremeAusterityNetWorthTrigger)
            {

                recessionStats.AreWeInExtremeAusterityMeasures = true;
                // set the end date to now. if we stay below the line, the date
                // will keep going up with it
                recessionStats.LastExtremeAusterityMeasureEnd = _sim.CurrentDateInSim;
            }
            else
            {
                // has it been within 12 months that we were in an extreme measure?
                if (_sim.RecessionStats.LastExtremeAusterityMeasureEnd < _sim.CurrentDateInSim.PlusYears(-1))
                {

                    recessionStats.AreWeInExtremeAusterityMeasures = false;
                }
            }
            return recessionStats;
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
            Tax.RecordIncome(_sim.TaxLedger, _sim.CurrentDateInSim, grossMonthlyPay);
            AccountCashManagement.DepositCash(_sim.BookOfAccounts, grossMonthlyPay, _sim.CurrentDateInSim);;
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
            Tax.RecordIncome(_sim.TaxLedger, _sim.CurrentDateInSim, grossMonthlyPay);
            AccountCashManagement.DepositCash(_sim.BookOfAccounts, grossMonthlyPay, _sim.CurrentDateInSim);;
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
            var result = Tax.MeetRmdRequirements(
                _sim.TaxLedger, _sim.CurrentDateInSim, _sim.BookOfAccounts, _sim.CurrentPrices);
            
            _sim.BookOfAccounts = result.newBookOfAccounts;
            _sim.TaxLedger = result.newLedger;
            
            if (StaticConfig.MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, result.amountSold, "RMD requirements met");
            }
        }
        private void RebalancePortfolio()
        {
            if (_sim.Person.IsBankrupt) return;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Rebalancing portfolio");
            }
            // do our recession checking every month, regardless of whether
            // it's time to move money around. this gives us a finer grain for
            // determining down years
            _sim.RecessionStats = Recession.CalculateRecessionStats(_sim.RecessionStats, _sim.CurrentPrices, _sim.SimParameters);
            // now rebalance
            Rebalance.RebalancePortfolio(_sim.CurrentDateInSim, _sim.BookOfAccounts, _sim.RecessionStats, 
                _sim.CurrentPrices, _sim.SimParameters, _sim.TaxLedger, _sim.Person);
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
            _sim.BookOfAccounts = AccountCleanup.CleanUpAccounts(_sim.CurrentDateInSim, _sim.BookOfAccounts, _sim.CurrentPrices);
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
            (BookOfAccounts newAccounts, LifetimeSpend newSpend)  result = AccountInterestAccrual.AccrueInterest(
                _sim.CurrentDateInSim, _sim.BookOfAccounts, _sim.CurrentPrices, _sim.LifetimeSpend);
            _sim.BookOfAccounts = result.newAccounts;
            _sim.LifetimeSpend = result.newSpend;
            
            if (StaticConfig.MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, 0M, "Accrued interest");
            }
        }
        private void PayTax()
        {
            if (_sim.Person.IsBankrupt) return;
            
            var taxYear = _sim.CurrentDateInSim.Year - 1;
            
            var newTaxLedger = _sim.TaxLedger;
            
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, $"Paying taxes for tax year {taxYear}");
            }
            var taxLiability = TaxCalculation.CalculateTaxLiabilityForYear(newTaxLedger, taxYear);
            newTaxLedger.TotalTaxPaid += taxLiability;
            
            // set the ledger's income target for next year
            newTaxLedger = Tax.UpdateIncomeTarget(newTaxLedger, taxYear);

            // todo: don't spend cash in the PayTax function. Use some other non-fun means of recording this spend
            SpendCash(taxLiability);
            
            _sim.TaxLedger = newTaxLedger;
            
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, taxLiability, "Paid taxes");
            }
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
            var localResult = AccountCashManagement.DepositCash(_sim.BookOfAccounts, amount, _sim.CurrentDateInSim);
            
            _sim.LifetimeSpend = Spend.RecordSocialSecurityWage(_sim.LifetimeSpend, amount, _sim.CurrentDateInSim);
            
            Tax.RecordSocialSecurityIncome(_sim.TaxLedger, _sim.CurrentDateInSim, amount);
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
            
            var withdrawalResults = AccountCashManagement.WithdrawCash(_sim.BookOfAccounts, amount, _sim.CurrentDateInSim, _sim.TaxLedger);
            _sim.BookOfAccounts = withdrawalResults.newAccounts;
            _sim.TaxLedger = withdrawalResults.newLedger;
            if (!withdrawalResults.isSuccessful)
            {
                DeclareBankruptcy();
            }
            _sim.LifetimeSpend = Spend.RecordSpend(_sim.LifetimeSpend, amount, _sim.CurrentDateInSim);;
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

            var funSpend = Spend.CalculateMonthlyFunSpend(_sim.SimParameters, _sim.Person, _sim.CurrentDateInSim);
            var notFunSpend = Spend.CalculateMonthlyRequiredSpend(_sim.SimParameters, _sim.Person, _sim.CurrentDateInSim);
            
            // required spend can't move. But your fun spend can go down if we're in a recession
            funSpend = Spend.CalculateRecessionSpendOverride(_sim.SimParameters, funSpend, _sim.RecessionStats);
            
            var withdrawalAmount = funSpend + notFunSpend;
            SpendCash(withdrawalAmount);
            
            // calculate and record fun points
            var funPoints = Spend.CalculateFunPointsForSpend(funSpend, _sim.Person, _sim.CurrentDateInSim);
            _sim.LifetimeSpend = Spend.RecordFunPoints(_sim.LifetimeSpend, funPoints, _sim.CurrentDateInSim);
            
          
            
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, withdrawalAmount, "Monthly spend spent");
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
            if (_sim.Person.IsBankrupt)
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
