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
                        // net worth is still positive.
                        // keep calculating stuff

                        SetGrowthAndPrices();
                        
                        CheckForRetirement();
                        
                        AccrueInterest();

                        Payday();
                        
                        PayDownLoans();
                        
                        AddRetirementSavings();
                        
                        UpdateRecessionStats();

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
                    // _sim.RecessionStats = DetermineRecessionStats(measurement, _sim.RecessionStats);
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
            _sim.PgPerson.IsRetired = true;
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
            if (_sim.PgPerson.IsRetired) return;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Collecting bonus");
            }

            var grossMonthlyPay = _sim.PgPerson.AnnualBonus;
            Tax.RecordW2Income(_sim.TaxLedger, _sim.CurrentDateInSim, grossMonthlyPay);
            AccountCashManagement.DepositCash(_sim.BookOfAccounts, grossMonthlyPay, _sim.CurrentDateInSim);;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Collected bonus");
            }
        }
        private void GetWorkingPaycheck()
        {
            // todo: move paycheck logic to a static function
            // todo: create a UT suite for GetWorkingPaycheck
            // todo: combine monthly "savings" investing into this function
            // todo: create a UT that ensures that excess cash gets invested pre-retirement
            // todo: calculate pre and post tax 401k contributions and assign to the person
            // todo: note that McPerson.MonthlySocialSecurityWage is now AnnualSocialSecurityWage
            
            if (_sim.PgPerson.IsRetired) return;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Collecting paycheck");
            }

            // income
            var grossMonthlyPay = (_sim.PgPerson.AnnualSalary + _sim.PgPerson.AnnualBonus) / 12m;
            var netMonthlyPay = grossMonthlyPay;
            var taxableMonthlyPay = grossMonthlyPay;
            
            // taxes withheld
            var federalWithholding = _sim.PgPerson.FederalAnnualWithholding / 12m;
            var stateWithholding = _sim.PgPerson.StateAnnualWithholding / 12m;
            var monthlyOasdi = (Math.Min(
                TaxConstants.OasdiBasePercent * (grossMonthlyPay * 12m),
                TaxConstants.OasdiMax))
                / 12m;
            var annualStandardMedicare = TaxConstants.StandardMedicareTaxRate * grossMonthlyPay;
            var amountOfSalaryOverMedicareThreshold = 
                (grossMonthlyPay * 12) - TaxConstants.AdditionalMedicareThreshold;
            var annualAdditionalMedicare = 
                TaxConstants.AdditionalMedicareTaxRate * amountOfSalaryOverMedicareThreshold;
            var annualTotalMedicare = annualStandardMedicare + annualAdditionalMedicare;
            var monthlyMedicare = annualTotalMedicare / 12m;
            netMonthlyPay -= (federalWithholding + stateWithholding + monthlyOasdi + monthlyMedicare);
            _sim.TaxLedger = Tax.RecordWithholdings(
                _sim.TaxLedger, _sim.CurrentDateInSim, federalWithholding, stateWithholding);
            _sim.TaxLedger = Tax.RecordTaxPaid(
                _sim.TaxLedger, _sim.CurrentDateInSim, monthlyOasdi + monthlyMedicare);
            
            
            // pre-tax deductions
            var annualPreTaxHealthDeductions = _sim.PgPerson.PreTaxHealthDeductions;
            var annualHsaContribution = _sim.PgPerson.AnnualHsaContribution;
            var annual401KPreTax = _sim.PgPerson.Annual401KPreTax;
            var preTaxDeductions = 
                (annualPreTaxHealthDeductions + annualHsaContribution + annual401KPreTax) / 12m;
            netMonthlyPay -= preTaxDeductions;
            taxableMonthlyPay -= preTaxDeductions;
            _sim.LifetimeSpend = Spend.RecordHealthcareSpend(_sim.LifetimeSpend, annualPreTaxHealthDeductions, _sim.CurrentDateInSim);
                
            // post-tax deductions
            var annual401KPostTax = _sim.PgPerson.Annual401KPostTax;
            var annualInsuranceDeductions = _sim.PgPerson.PostTaxInsuranceDeductions;
            netMonthlyPay -= ((annual401KPostTax + annualInsuranceDeductions) / 12m);
            
            // record final w2 income (gross, less pre-tax)
            _sim.TaxLedger = Tax.RecordW2Income(_sim.TaxLedger, _sim.CurrentDateInSim, taxableMonthlyPay);
            
            // finally, deposit what's left
            AccountCashManagement.DepositCash(_sim.BookOfAccounts, netMonthlyPay, _sim.CurrentDateInSim);;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Collected paycheck");
            }
        }

        private void MeetRmdRequirements()
        {
            if (_sim.PgPerson.IsBankrupt) return;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Meeting RMD requirements");
            }
            var age = _sim.CurrentDateInSim.Year - _sim.PgPerson.BirthDate.Year;
            var result = Tax.MeetRmdRequirements(
                _sim.TaxLedger, _sim.CurrentDateInSim, _sim.BookOfAccounts, age);
            
            _sim.BookOfAccounts = result.newBookOfAccounts;
            _sim.TaxLedger = result.newLedger;
            
            if (StaticConfig.MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, result.amountSold, "RMD requirements met");
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
        private void RebalancePortfolio()
        {
            if (_sim.PgPerson.IsBankrupt) return;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Rebalancing portfolio");
            }
            
            Rebalance.RebalancePortfolio(_sim.CurrentDateInSim, _sim.BookOfAccounts, _sim.RecessionStats, 
                _sim.CurrentPrices, _sim.SimParameters, _sim.TaxLedger, _sim.PgPerson);
            if (StaticConfig.MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, 0M, "Rebalanced portfolio");
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
        }
        private void AccrueInterest()
        {
            if (_sim.PgPerson.IsBankrupt) return;
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
        private bool PayTax()
        {
            if (_sim.PgPerson.IsBankrupt) return false;
            
            var taxYear = _sim.CurrentDateInSim.Year - 1;
            
            var newTaxLedger = _sim.TaxLedger;
            
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, $"Paying taxes for tax year {taxYear}");
            }
            var taxLiability = TaxCalculation.CalculateTaxLiabilityForYear(newTaxLedger, taxYear);
            newTaxLedger.TotalTaxPaid += taxLiability;
            
            
            if(!SpendCash(taxLiability, false)) return false;
            
            _sim.TaxLedger = newTaxLedger;
            
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, taxLiability, "Paid taxes");
            }

            return true;
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
            var amount = _sim.PgPerson.AnnualSocialSecurityWage / 12m;
            _sim.BookOfAccounts = AccountCashManagement.DepositCash(_sim.BookOfAccounts, amount, _sim.CurrentDateInSim);
            
            _sim.LifetimeSpend = Spend.RecordSocialSecurityWage(_sim.LifetimeSpend, amount, _sim.CurrentDateInSim);
            
            Tax.RecordSocialSecurityIncome(_sim.TaxLedger, _sim.CurrentDateInSim, amount);
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, amount, "Social Security check processed");
            }
        }
        
        private bool SpendCash(decimal amount, bool isFun)
        {
            var withdrawalResults = AccountCashManagement.WithdrawCash(
                _sim.BookOfAccounts, amount, _sim.CurrentDateInSim, _sim.TaxLedger);
            _sim.BookOfAccounts = withdrawalResults.newAccounts;
            _sim.TaxLedger = withdrawalResults.newLedger;
            if (!withdrawalResults.isSuccessful)
            {
                DeclareBankruptcy();
                return false;
            }
            _sim.LifetimeSpend = Spend.RecordSpend(_sim.LifetimeSpend, amount, _sim.CurrentDateInSim);
            if (isFun)
            {
                var funPoints = Spend.CalculateFunPointsForSpend(amount, _sim.PgPerson, _sim.CurrentDateInSim);
                _sim.LifetimeSpend = Spend.RecordFunPoints(_sim.LifetimeSpend, funPoints, _sim.CurrentDateInSim);
            }
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim, amount, "Spent cash");
            }
            return true;
        }
        
        
        private bool PayForStuff()
        {
            if (_sim.PgPerson.IsBankrupt) return false;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Time to spend the money");
            }

            var funSpend = Spend.CalculateMonthlyFunSpend(_sim.SimParameters, _sim.PgPerson, _sim.CurrentDateInSim);
            var notFunSpend = Spend.CalculateMonthlyRequiredSpend(_sim.SimParameters, _sim.PgPerson, _sim.CurrentDateInSim);
            
            // required spend can't move. But your fun spend can go down if we're in a recession
            funSpend = Spend.CalculateRecessionSpendOverride(_sim.SimParameters, funSpend, _sim.RecessionStats);
            
            var withdrawalAmount = funSpend + notFunSpend;
            if(!SpendCash(notFunSpend, false)) return false;;
            if(!SpendCash(funSpend, true)) return false;
          
            
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddFullReconLine(_sim, withdrawalAmount, "Monthly spend spent");
            }
            return true;
        }
        
        private void DeclareBankruptcy()
        {
            _sim.PgPerson.IsBankrupt = true;
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Bankruptcy declared");
            }
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
        }
        private void AddRetirementSavings()
        {
            // todo: move pre-retirement investments into the Paycheck method
            // todo: UT AddRetirementSavings
            if (_sim.PgPerson.IsBankrupt || _sim.PgPerson.IsRetired) return;
            
            if (MonteCarloConfig.DebugMode)
            {
                Reconciliation.AddMessageLine(_sim.CurrentDateInSim,0, "Adding retirement savings");
            }

            var roth401KAmount = 
                _sim.PgPerson.Annual401KContribution * (1 - _sim.SimParameters.Percent401KTraditional) / 12m; 
            var traditional401KAmount = 
                _sim.PgPerson.Annual401KContribution * (_sim.SimParameters.Percent401KTraditional) / 12m; 
            var monthly401KMatch = (_sim.PgPerson.AnnualSalary * _sim.PgPerson.Annual401KMatchPercent) / 12m;
            var taxDefferedAmount = traditional401KAmount + monthly401KMatch;
            var hsaAmount = 
                (_sim.PgPerson.AnnualHsaContribution + _sim.PgPerson.AnnualHsaEmployerContribution) / 12m;
            
            Investment.InvestFunds(_sim.BookOfAccounts, _sim.CurrentDateInSim, roth401KAmount,
                McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.ROTH_401_K, _sim.CurrentPrices);

            Investment.InvestFunds(
                _sim.BookOfAccounts, _sim.CurrentDateInSim, taxDefferedAmount,
                McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TRADITIONAL_401_K, _sim.CurrentPrices);

            Investment.InvestFunds(
                _sim.BookOfAccounts, _sim.CurrentDateInSim, hsaAmount,
                McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.HSA, _sim.CurrentPrices);

            Investment.InvestFunds(_sim.BookOfAccounts, _sim.CurrentDateInSim, monthly401KMatch,
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
