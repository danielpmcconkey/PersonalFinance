﻿//#define PERFORMANCEPROFILING
using System.Text;
using Lib.DataTypes.MonteCarlo;
using NodaTime;
using System.Collections.Concurrent;
using System.Diagnostics;
using Lib.DataTypes;
using Lib.MonteCarlo.StaticFunctions;
using Lib.MonteCarlo.TaxForms.Federal;
using Lib.StaticConfig;

namespace Lib.MonteCarlo;

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
    private List<SimSnapshot> _snapshots;

    /// <summary>
    /// this houses all of our simulation data: history and current state
    /// </summary>
    private MonteCarloSim _sim;

    private ReconciliationLedger _reconciliationLedger = new();
    private bool _isReconcilingTime = false;
    
    /// <summary>
    /// which run within the grander model run this is
    /// </summary>
    private int _lifeNum;

    public LifeSimulator(
        Logger logger, McModel simParams, PgPerson person, List<McInvestmentAccount> investmentAccounts,
        List<McDebtAccount> debtAccounts, Dictionary<LocalDateTime, Decimal> hypotheticalPrices, int lifeNum)
    {
#if PERFORMANCEPROFILING
        // set up a run that will last
        simParams.DesiredMonthlySpendPostRetirement = 500m;
        simParams.DesiredMonthlySpendPreRetirement = 500m;
        simParams.NumMonthsCashOnHand = 12;
        simParams.NumMonthsMidBucketOnHand = 12;
        simParams.AusterityRatio = 0.5m;
        simParams.ExtremeAusterityRatio = 0.5m;
        simParams.ExtremeAusterityNetWorthTrigger = 1000000m;
#endif 
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
        copiedPerson.AnnualSocialSecurityWage =
            monthlySocialSecurityWage * 12; 
        copiedPerson.Annual401KPreTax = copiedPerson.Annual401KContribution * simParams.Percent401KTraditional;
        copiedPerson.Annual401KPostTax = Math.Max(
            0, copiedPerson.Annual401KContribution - copiedPerson.Annual401KPreTax);
        copiedPerson.IsBankrupt = false;
        copiedPerson.IsRetired = false;
        var ledger = new TaxLedger();
        ledger.SocialSecurityWageMonthly = monthlySocialSecurityWage;
        ledger.SocialSecurityElectionStartDate = simParams.SocialSecurityStart;
        
        _lifeNum = lifeNum;

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
        _snapshots = [];
    }

    public List<SimSnapshot> Run()
    {
        try
        {
            NormalizeDates(_sim.PgPerson, _sim.SimParameters);
            
            _sim.Log.Debug($"Beginning lifetime {_lifeNum}.");
            
            while (_sim.CurrentDateInSim <= StaticConfig.MonteCarloConfig.MonteCarloSimEndDate)
            {
                SetReconClutch();
                    
                     
                /*
                 *
                 *
                 * you are here 
                 *
                 *  
                 *
                 * you probably need to adjust your fudge factor in the RNG tests that fail in the model mating
                 *
                 *
                 * you want to implement a "how much have we already earned and had withheld" ability to make the first
                 * year's tax payment more realistic
                 *
                 * you want to figure out the "end of a life sim" output (replacement NetWorthMeasurement) and, once you
                 * do, you want to re-implement the percentile functions, the model training, etc. 
                 *
                 * 
                 */
                
                if (MonteCarloConfig.DebugMode)
                {
                    _reconciliationLedger.AddFullReconLine(
                        _sim, $"Starting new month: {_sim.CurrentDateInSim}");
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
                    
                    InvestExcessCash();
                    RecordFunAndAnxiety();
                }

                CreateMonthEndReport();
                
                _sim.CurrentDateInSim = _sim.CurrentDateInSim.PlusMonths(1);
            }

            _sim.Log.Debug("Done with this lifetime.");
            if (!MonteCarloConfig.DebugMode) return _snapshots;
            
            _sim.Log.Debug("Writing reconciliation data to spreadsheet");
            _reconciliationLedger.ExportToSpreadsheet();
            _sim.Log.Debug(_sim.Log.FormatHeading("End of simulated lifetime"));
            return _snapshots;
        }
        catch (Exception e)
        {
            _sim.Log.Error(_sim.Log.FormatHeading("Error in Run()"));
            _sim.Log.Error(e.ToString());
            _reconciliationLedger.ExportToSpreadsheet();
            throw;
        }
    }

    

    #region Private methods

    private void AccrueInterest()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        if (_sim.PgPerson.IsBankrupt) return;
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcileInterestAccrual && _isReconcilingTime)
        {
            _reconciliationLedger.AddFullReconLine(_sim, "Accruing interest");
        }

        var result = AccountInterestAccrual.AccrueInterest(
            _sim.CurrentDateInSim, _sim.BookOfAccounts, _sim.CurrentPrices, _sim.LifetimeSpend);
        _sim.BookOfAccounts = result.newAccounts;
        _sim.LifetimeSpend = result.newSpend;
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Accruing interest took {stopwatch.ElapsedMilliseconds} ms");
#endif 

        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcileInterestAccrual || !_isReconcilingTime) return;

        _reconciliationLedger.AddMessages(result.messages);
        _reconciliationLedger.AddFullReconLine(_sim, "Accrued interest");
    }

    private void CheckForRetirement()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        // if already retired, no need to check again
        if (_sim.PgPerson.IsRetired) return;

        // if not retired, check if we're retiring
        var result = Simulation.SetIsRetiredFlagIfNeeded(
            _sim.CurrentDateInSim, _sim.PgPerson, _sim.SimParameters);

        // if result is still false, just return
        if (!result.isRetired) return;

        // retirement day; update the person object and log the event
        _sim.PgPerson = result.person;
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Checking for retirement took {stopwatch.ElapsedMilliseconds} ms");
#endif 
        
        if (!MonteCarloConfig.DebugMode || !_isReconcilingTime) return;
        _reconciliationLedger.AddFullReconLine(_sim, "Retirement day!");
    }

    private void CleanUpAccounts()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        if (_sim.PgPerson.IsBankrupt) return;
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcileInterestAccrual && _isReconcilingTime)
        {
            _reconciliationLedger.AddFullReconLine(_sim, "Cleaning up accounts");
        }

        _sim.BookOfAccounts =
            AccountCleanup.CleanUpAccounts(_sim.CurrentDateInSim, _sim.BookOfAccounts, _sim.CurrentPrices);
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Cleaning up accounts took {stopwatch.ElapsedMilliseconds} ms");
#endif 

        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcileAccountCleanUp || !_isReconcilingTime)
            return;

        _reconciliationLedger.AddFullReconLine(_sim, "Cleaned up accounts");
    }

    private void CreateMonthEndReport()
    {
        var snapshot = Simulation.CreateSimSnapshot(_sim);
        if (snapshot.NetWorth <= 0 || _sim.PgPerson.IsBankrupt)
        {
            // zero it out everything to make reporting cleaner
            snapshot.NetWorth = 0;
        }

        _snapshots.Add(snapshot);
    }

    private void DeclareBankruptcy()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        _sim.PgPerson.IsBankrupt = true;
        // zero out everything to make reporting easier
        List<McInvestmentAccount> emptyInvestAccounts = [];
        List<McDebtAccount> emptyDebtAccounts = [];
        _sim.BookOfAccounts = Account.CreateBookOfAccounts(emptyInvestAccounts, emptyDebtAccounts);
        if (MonteCarloConfig.DebugMode)
        {
            _reconciliationLedger.AddFullReconLine(_sim, "Bankruptcy declared");
            _sim.Log.Debug("Bankruptcy declared");
        }
         
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Declaring bankruptcy took {stopwatch.ElapsedMilliseconds} ms");
#endif 
        
    }

    private void InvestExcessCash()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        _sim.Log.Debug("Rebalancing portfolio");
        if (_sim.PgPerson.IsBankrupt) return;
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcileRebalancing && _isReconcilingTime) 
            _reconciliationLedger.AddFullReconLine(_sim, "Rebalancing portfolio");
        
        var results = Rebalance.InvestExcessCash(
            _sim.CurrentDateInSim, _sim.BookOfAccounts, _sim.CurrentPrices, _sim.SimParameters, _sim.PgPerson);
        _sim.BookOfAccounts = results.newBookOfAccounts;
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Investing excess cash took {stopwatch.ElapsedMilliseconds} ms");
#endif 
        
        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcileRebalancing || !_isReconcilingTime) return;
        _reconciliationLedger.AddMessages(results.messages);
        _reconciliationLedger.AddFullReconLine(_sim, "Rebalanced portfolio");
    }

    private void MeetRmdRequirements()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        if (_sim.PgPerson.IsBankrupt) return;
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcileRmd && _isReconcilingTime)
            _reconciliationLedger.AddFullReconLine(_sim, "Meeting RMD requirements");

        var age = _sim.CurrentDateInSim.Year - _sim.PgPerson.BirthDate.Year;
        var result = Tax.MeetRmdRequirements(
            _sim.TaxLedger, _sim.CurrentDateInSim, _sim.BookOfAccounts, age);
        _sim.BookOfAccounts = result.newBookOfAccounts;
        _sim.TaxLedger = result.newLedger;
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Meeting RMD took {stopwatch.ElapsedMilliseconds} ms");
#endif 
        
        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcileRmd || !_isReconcilingTime) return;
        _reconciliationLedger.AddMessages(result.messages);
        _reconciliationLedger.AddFullReconLine(_sim, "RMD requirements met");
    }
    
    private void NormalizeDates(PgPerson simPgPerson, McModel simSimParameters)
    {
        var (newPerson, newModel) = Simulation.NormalizeDates(_sim.PgPerson, _sim.SimParameters);
        _sim.PgPerson = newPerson;
        _sim.SimParameters = newModel;
    }

    private void PayDownLoans()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        if (_sim.PgPerson.IsBankrupt)
        {
            return;
        }

        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcileLoanPaydown && _isReconcilingTime)
            _reconciliationLedger.AddFullReconLine(_sim, "Paying down loans");


        var paymentResult = AccountDebtPayment.PayDownLoans(
            _sim.BookOfAccounts, _sim.CurrentDateInSim, _sim.TaxLedger, _sim.LifetimeSpend);
        _sim.BookOfAccounts = paymentResult.newBookOfAccounts;
        _sim.TaxLedger = paymentResult.newLedger;
        _sim.LifetimeSpend = paymentResult.newSpend;
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Paying down loans took {stopwatch.ElapsedMilliseconds} ms");
#endif 
        
        if (paymentResult.isSuccessful == false)
        {
            DeclareBankruptcy();
            return;
        }

        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcileLoanPaydown || !_isReconcilingTime) return;
        _reconciliationLedger.AddMessages(paymentResult.messages);
        _reconciliationLedger.AddFullReconLine(_sim, "Pay down debt completed");
    }

    private void PayForStuff()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        if (_sim.PgPerson.IsBankrupt) return;
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcilePayingForStuff && _isReconcilingTime)
            _reconciliationLedger.AddFullReconLine(_sim, "Time to spend the money");

        var results = Simulation.PayForStuff(_sim.SimParameters, _sim.PgPerson,
            _sim.CurrentDateInSim, _sim.RecessionStats, _sim.TaxLedger, _sim.LifetimeSpend, _sim.BookOfAccounts);
        _sim.BookOfAccounts = results.accounts;
        _sim.TaxLedger = results.ledger;
        _sim.LifetimeSpend = results.spend;
        if (results.isSuccessful == false) DeclareBankruptcy();
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Paying for stuff took {stopwatch.ElapsedMilliseconds} ms");
#endif 
        
        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcilePayingForStuff || !_isReconcilingTime)
            return;
        _reconciliationLedger.AddMessages(results.messages);
        _reconciliationLedger.AddFullReconLine(_sim, "Monthly spend spent");
    }

    private void PayTax()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        if (_sim.PgPerson.IsBankrupt) return;

        var taxYear = _sim.CurrentDateInSim.Year - 1;
        var newTaxLedger = _sim.TaxLedger;

        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcileTaxCalcs && _isReconcilingTime)
            _reconciliationLedger.AddFullReconLine(_sim, $"Paying taxes for tax year {taxYear}");

        var taxResult = Simulation.PayTaxForYear(_sim.PgPerson, _sim.CurrentDateInSim,
            _sim.TaxLedger, _sim.LifetimeSpend, _sim.BookOfAccounts, _sim.CurrentDateInSim.Year - 1);
        _sim.BookOfAccounts = taxResult.accounts;
        _sim.TaxLedger = taxResult.ledger;
        _sim.LifetimeSpend = taxResult.spend;
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Paying tax took {stopwatch.ElapsedMilliseconds} ms");
#endif 

        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcileTaxCalcs || !_isReconcilingTime) return;
        _reconciliationLedger.AddMessages(taxResult.messages);
        _reconciliationLedger.AddFullReconLine(_sim, "Paid taxes");
    }

    private void ProcessPayday()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcilePayDay && _isReconcilingTime)
            _reconciliationLedger.AddFullReconLine(_sim, "processing payday");

        var results = Simulation.ProcessPayday(_sim.PgPerson, _sim.CurrentDateInSim,
            _sim.BookOfAccounts, _sim.TaxLedger, _sim.LifetimeSpend, _sim.SimParameters, _sim.CurrentPrices);
        _sim.BookOfAccounts = results.accounts;
        _sim.TaxLedger = results.ledger;
        _sim.LifetimeSpend = results.spend;
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Processing payday took {stopwatch.ElapsedMilliseconds} ms");
#endif
        
        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcilePayDay || !_isReconcilingTime) return;
        _reconciliationLedger.AddMessages(results.messages);
        _reconciliationLedger.AddFullReconLine(_sim, "processed payday");
    }

    private void RebalancePortfolio()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        _sim.Log.Debug("Rebalancing portfolio");
        if (_sim.PgPerson.IsBankrupt) return;
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcileRebalancing && _isReconcilingTime) 
            _reconciliationLedger.AddFullReconLine(_sim, "Rebalancing portfolio");
        
        var results = Rebalance.RebalancePortfolio(
            _sim.CurrentDateInSim, _sim.BookOfAccounts, _sim.RecessionStats, _sim.CurrentPrices, _sim.SimParameters,
            _sim.TaxLedger, _sim.PgPerson);
        _sim.BookOfAccounts = results.newBookOfAccounts;
        _sim.TaxLedger = results.newLedger;
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Rebalancing took {stopwatch.ElapsedMilliseconds} ms");
#endif 
        
        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcileRebalancing || !_isReconcilingTime) return;
        _reconciliationLedger.AddMessages(results.messages);
        _reconciliationLedger.AddFullReconLine(_sim, "Rebalanced portfolio");
    }

    private void RecordFunAndAnxiety()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcilePayDay && _isReconcilingTime)
            _reconciliationLedger.AddFullReconLine(_sim, "Recording fun and anxiety");

        var results = Simulation.RecordFunAndAnxiety(_sim.SimParameters, _sim.PgPerson, 
            _sim.CurrentDateInSim, _sim.RecessionStats, _sim.LifetimeSpend);
        _sim.LifetimeSpend = results.spend;
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Processing fun and anxiety took {stopwatch.ElapsedMilliseconds} ms");
#endif
        
        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcilePayDay || !_isReconcilingTime) return;
        _reconciliationLedger.AddMessages(results.messages);
        _reconciliationLedger.AddFullReconLine(_sim, "Done recording fund and anxiety");
    }
    private void SetGrowthAndPrices()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        _sim.CurrentPrices = Simulation.SetNewPrices(
            _sim.CurrentPrices, _hypotheticalPrices, _sim.CurrentDateInSim);
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcilePricingGrowth && _isReconcilingTime)
        {
            _reconciliationLedger.AddMessageLine(new ReconciliationMessage(
                _sim.CurrentDateInSim, _sim.CurrentPrices.CurrentLongTermGrowthRate,
                "Updated prices with new long term growth rate"));
        }
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Setting growth and prices took {stopwatch.ElapsedMilliseconds} ms");
#endif 
        
    }
    
    private void SetReconClutch()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        _isReconcilingTime = Simulation.IsReconciliationPeriod(_sim.CurrentDateInSim);
         
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Setting the recon clutch took {stopwatch.ElapsedMilliseconds} ms");
#endif 
        
    }
    
    private void UpdateRecessionStats()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        // do our recession checking every month, regardless of whether
        // it's time to move money around. this gives us a finer grain for
        // determining down years
        _sim.RecessionStats = Recession.CalculateRecessionStats(
            _sim.RecessionStats, _sim.CurrentPrices, _sim.SimParameters, _sim.BookOfAccounts,
            _sim.CurrentDateInSim);
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Updating recessing stats took {stopwatch.ElapsedMilliseconds} ms");
#endif 
        
    }
    
    #endregion
}
