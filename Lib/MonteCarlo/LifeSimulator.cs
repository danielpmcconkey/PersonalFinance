//#define PERFORMANCEPROFILING
using System.Text;
using Lib.DataTypes.MonteCarlo;
using NodaTime;
using System.Collections.Concurrent;
using System.Diagnostics;
using Lib.DataTypes.Postgres;
using Lib.MonteCarlo.StaticFunctions;
using Lib.MonteCarlo.TaxForms.Federal;
using Lib.StaticConfig;
using Model = Lib.DataTypes.MonteCarlo.Model;

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
    private SimData _simData;

    private ReconciliationLedger _reconciliationLedger = new();
    private bool _isReconcilingTime = false;
    
    /// <summary>
    /// which run within the grander model run this is
    /// </summary>
    private readonly int _lifeNum;

    public LifeSimulator(
        Logger logger, Model model, PgPerson person, List<McInvestmentAccount> investmentAccounts,
        List<McDebtAccount> debtAccounts, Dictionary<LocalDateTime, Decimal> hypotheticalPrices, int lifeNum)
    {
#if PERFORMANCEPROFILING
        // set up a run that will last
        model.DesiredMonthlySpendPostRetirement = 500m;
        model.DesiredMonthlySpendPreRetirement = 500m;
        model.NumMonthsCashOnHand = 12;
        model.NumMonthsMidBucketOnHand = 12;
        model.AusterityRatio = 0.5m;
        model.ExtremeAusterityRatio = 0.5m;
        model.ExtremeAusterityNetWorthTrigger = 1000000m;
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
            model.SocialSecurityStart);
        var copiedPerson = Person.CopyPerson(person, false);
        copiedPerson.AnnualSocialSecurityWage =
            monthlySocialSecurityWage * 12; 
        copiedPerson.Annual401KPreTax = copiedPerson.Annual401KContribution * model.Percent401KTraditional;
        copiedPerson.Annual401KPostTax = Math.Max(
            0, copiedPerson.Annual401KContribution - copiedPerson.Annual401KPreTax);
        copiedPerson.IsBankrupt = false;
        copiedPerson.IsRetired = false;
        var ledger = new TaxLedger();
        ledger.SocialSecurityWageMonthly = monthlySocialSecurityWage;
        ledger.SocialSecurityElectionStartDate = model.SocialSecurityStart;
        // add the pre-sim income and tax withholding to make the first year's tax filing more realistic
        ledger.W2Income.Add(
            (model.SimStartDate, copiedPerson.ThisYearsIncomePreSimStart));
        ledger.FederalWithholdings.Add(
            (model.SimStartDate, copiedPerson.ThisYearsFederalTaxWithholdingPreSimStart));
        ledger.StateWithholdings.Add(
            (model.SimStartDate, copiedPerson.ThisYearsStateTaxWithholdingPreSimStart));
        
        _lifeNum = lifeNum;

        // set up the sim struct to be used to keep track of all the sim data
        _simData = new SimData()
        {
            Log = logger,
            Model = model,
            BookOfAccounts = accounts,
            PgPerson = copiedPerson,
            CurrentDateInSim = MonteCarloConfig.MonteCarloSimStartDate,
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
            _simData.Log.Debug($"Beginning lifetime {_lifeNum}.");
            NormalizeDates(_simData.PgPerson, _simData.Model);
            
            
            while (_simData.CurrentDateInSim <= MonteCarloConfig.MonteCarloSimEndDate)
            {
                SetReconClutch();
                
                if (MonteCarloConfig.DebugMode)
                {
                    _reconciliationLedger.AddFullReconLine(
                        _simData, $"Starting new month: {_simData.CurrentDateInSim}");
                    _simData.Log.Debug($"Starting new month: {_simData.CurrentDateInSim}");
                }

                if (!_simData.PgPerson.IsBankrupt)
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

                    if (_simData.CurrentDateInSim.Month == 1)
                    {
                        CleanUpAccounts();
                        PayTax();
                    }

                    if (_simData.CurrentDateInSim.Month == 12)
                    {
                        MeetRmdRequirements();
                    }
                    
                    InvestExcessCash();
                }
                
                RecordFunAndAnxiety();

                CreateMonthEndReport();
                
                if (MonteCarloConfig.DebugMode && _simData.CurrentDateInSim.Month == 12)
                    ReconcileEoy();
                
                _simData.CurrentDateInSim = _simData.CurrentDateInSim.PlusMonths(1);
            }

            _simData.Log.Debug($"Done with lifetime {_lifeNum}.");
            if (!MonteCarloConfig.DebugMode) return _snapshots;
            
            _simData.Log.Debug("Writing reconciliation data to spreadsheet");
            _reconciliationLedger.ExportToSpreadsheet();
            _simData.Log.Debug(_simData.Log.FormatHeading("End of simulated lifetime"));
            return _snapshots;
        }
        catch (Exception e)
        {
            _simData.Log.Error(_simData.Log.FormatHeading($"Error in Run({_lifeNum})"));
            _simData.Log.Error($"Model ID: {_simData.Model.Id}");
            _simData.Log.Error($"Life num: {_lifeNum}");
            _simData.Log.Error(e.ToString());
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
        if (_simData.PgPerson.IsBankrupt) return;
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcileInterestAccrual && _isReconcilingTime)
        {
            _reconciliationLedger.AddFullReconLine(_simData, "Accruing interest");
        }

        var result = AccountInterestAccrual.AccrueInterest(
            _simData.CurrentDateInSim, _simData.BookOfAccounts, _simData.CurrentPrices, _simData.LifetimeSpend);
        _simData.BookOfAccounts = result.newAccounts;
        _simData.LifetimeSpend = result.newSpend;
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Accruing interest took {stopwatch.ElapsedMilliseconds} ms");
#endif 

        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcileInterestAccrual || !_isReconcilingTime) return;

        _reconciliationLedger.AddMessages(result.messages);
        _reconciliationLedger.AddFullReconLine(_simData, "Accrued interest");
    }

    private void CheckForRetirement()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        // if already retired, no need to check again
        if (_simData.PgPerson.IsRetired) return;

        // if not retired, check if we're retiring
        var result = Simulation.SetIsRetiredFlagIfNeeded(
            _simData.CurrentDateInSim, _simData.PgPerson, _simData.Model);

        // if result is still false, just return
        if (!result.isRetired) return;

        // retirement day; update the person object and log the event
        _simData.PgPerson = result.person;
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Checking for retirement took {stopwatch.ElapsedMilliseconds} ms");
#endif 
        
        if (!MonteCarloConfig.DebugMode || !_isReconcilingTime) return;
        _reconciliationLedger.AddFullReconLine(_simData, "Retirement day!");
    }

    private void CleanUpAccounts()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        if (_simData.PgPerson.IsBankrupt) return;
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcileInterestAccrual && _isReconcilingTime)
        {
            _reconciliationLedger.AddFullReconLine(_simData, "Cleaning up accounts");
        }

        _simData.BookOfAccounts =
            AccountCleanup.CleanUpAccounts(_simData.CurrentDateInSim, _simData.BookOfAccounts, _simData.CurrentPrices);
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Cleaning up accounts took {stopwatch.ElapsedMilliseconds} ms");
#endif 

        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcileAccountCleanUp || !_isReconcilingTime)
            return;

        _reconciliationLedger.AddFullReconLine(_simData, "Cleaned up accounts");
    }

    private void CreateMonthEndReport()
    {
        if (MonteCarloConfig.ModelTrainingMode && 
            _simData.CurrentDateInSim < StaticConfig.MonteCarloConfig.MonteCarloSimEndDate) 
            return; // only log the intra-sim metrics when in single run mode
        
        var snapshot = Simulation.CreateSimSnapshot(_simData);
        if (snapshot.NetWorth <= 0 || _simData.PgPerson.IsBankrupt)
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
        _simData.PgPerson.IsBankrupt = true;
        // zero out everything to make reporting easier
        List<McInvestmentAccount> emptyInvestAccounts = [];
        List<McDebtAccount> emptyDebtAccounts = [];
        _simData.BookOfAccounts = Account.CreateBookOfAccounts(emptyInvestAccounts, emptyDebtAccounts);
        if (MonteCarloConfig.DebugMode)
        {
            _reconciliationLedger.AddFullReconLine(_simData, "Bankruptcy declared");
            _simData.Log.Debug("Bankruptcy declared");
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
        _simData.Log.Debug("Investing excess cash");
        if (_simData.PgPerson.IsBankrupt) return;
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcileRebalancing && _isReconcilingTime) 
            _reconciliationLedger.AddFullReconLine(_simData, "Investing excess cash");
        
        var results = _simData.Model.WithdrawalStrategy.InvestExcessCash(
            _simData.CurrentDateInSim, _simData.BookOfAccounts, _simData.CurrentPrices, _simData.Model,
            _simData.PgPerson);
        _simData.BookOfAccounts = results.accounts;
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Investing excess cash took {stopwatch.ElapsedMilliseconds} ms");
#endif 
        
        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcileRebalancing || !_isReconcilingTime) return;
        _reconciliationLedger.AddMessages(results.messages);
        _reconciliationLedger.AddFullReconLine(_simData, "Invested excess cash");
    }

    private void MeetRmdRequirements()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        if (_simData.PgPerson.IsBankrupt) return;
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcileRmd && _isReconcilingTime)
            _reconciliationLedger.AddFullReconLine(_simData, "Meeting RMD requirements");

        var age = _simData.CurrentDateInSim.Year - _simData.PgPerson.BirthDate.Year;
        var result = Tax.MeetRmdRequirements(
            _simData.TaxLedger, _simData.CurrentDateInSim, _simData.BookOfAccounts, age, _simData.Model);
        _simData.BookOfAccounts = result.newBookOfAccounts;
        _simData.TaxLedger = result.newLedger;
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Meeting RMD took {stopwatch.ElapsedMilliseconds} ms");
#endif 
        
        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcileRmd || !_isReconcilingTime) return;
        _reconciliationLedger.AddMessages(result.messages);
        _reconciliationLedger.AddFullReconLine(_simData, "RMD requirements met");
    }
    
    private void NormalizeDates(PgPerson simPgPerson, Model simSimParameters)
    {
        var (newPerson, newModel) = Simulation.NormalizeDates(_simData.PgPerson, _simData.Model);
        _simData.PgPerson = newPerson;
        _simData.Model = newModel;
    }

    private void PayDownLoans()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        if (_simData.PgPerson.IsBankrupt)
        {
            return;
        }

        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcileLoanPaydown && _isReconcilingTime)
            _reconciliationLedger.AddFullReconLine(_simData, "Paying down loans");


        var paymentResult = AccountDebtPayment.PayDownLoans(
            _simData.BookOfAccounts, _simData.CurrentDateInSim, _simData.TaxLedger, _simData.LifetimeSpend,
            _simData.Model);
        _simData.BookOfAccounts = paymentResult.newBookOfAccounts;
        _simData.TaxLedger = paymentResult.newLedger;
        _simData.LifetimeSpend = paymentResult.newSpend;
        
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
        _reconciliationLedger.AddFullReconLine(_simData, "Pay down debt completed");
    }

    private void PayForStuff()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        if (_simData.PgPerson.IsBankrupt) return;
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcilePayingForStuff && _isReconcilingTime)
            _reconciliationLedger.AddFullReconLine(_simData, "Time to spend the money");

        var results = Simulation.PayForStuff(_simData.Model, _simData.PgPerson,
            _simData.CurrentDateInSim, _simData.RecessionStats, _simData.TaxLedger, _simData.LifetimeSpend, _simData.BookOfAccounts);
        _simData.BookOfAccounts = results.accounts;
        _simData.TaxLedger = results.ledger;
        _simData.LifetimeSpend = results.spend;
        if (results.isSuccessful == false) DeclareBankruptcy();
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Paying for stuff took {stopwatch.ElapsedMilliseconds} ms");
#endif 
        
        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcilePayingForStuff || !_isReconcilingTime)
            return;
        _reconciliationLedger.AddMessages(results.messages);
        _reconciliationLedger.AddFullReconLine(_simData, "Monthly spend spent");
    }

    private void PayTax()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        if (_simData.PgPerson.IsBankrupt) return;

        var taxYear = _simData.CurrentDateInSim.Year - 1;
        var newTaxLedger = _simData.TaxLedger;

        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcileTaxCalcs && _isReconcilingTime)
            _reconciliationLedger.AddFullReconLine(_simData, $"Paying taxes for tax year {taxYear}");

        var taxResult = Simulation.PayTaxForYear(
            _simData.PgPerson, _simData.CurrentDateInSim, _simData.TaxLedger, _simData.LifetimeSpend,
            _simData.BookOfAccounts, _simData.CurrentDateInSim.Year - 1, _simData.Model);
        _simData.BookOfAccounts = taxResult.accounts;
        _simData.TaxLedger = taxResult.ledger;
        _simData.LifetimeSpend = taxResult.spend;
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Paying tax took {stopwatch.ElapsedMilliseconds} ms");
#endif 

        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcileTaxCalcs || !_isReconcilingTime) return;
        _reconciliationLedger.AddMessages(taxResult.messages);
        _reconciliationLedger.AddFullReconLine(_simData, "Paid taxes");
    }

    private void ProcessPayday()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcilePayDay && _isReconcilingTime)
            _reconciliationLedger.AddFullReconLine(_simData, "processing payday");

        var results = Simulation.ProcessPayday(_simData.PgPerson, _simData.CurrentDateInSim,
            _simData.BookOfAccounts, _simData.TaxLedger, _simData.LifetimeSpend, _simData.Model, _simData.CurrentPrices);
        _simData.BookOfAccounts = results.accounts;
        _simData.TaxLedger = results.ledger;
        _simData.LifetimeSpend = results.spend;
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Processing payday took {stopwatch.ElapsedMilliseconds} ms");
#endif
        
        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcilePayDay || !_isReconcilingTime) return;
        _reconciliationLedger.AddMessages(results.messages);
        _reconciliationLedger.AddFullReconLine(_simData, "processed payday");
    }

    private void RebalancePortfolio()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        _simData.Log.Debug("Rebalancing portfolio");
        if (_simData.PgPerson.IsBankrupt) return;
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcileRebalancing && _isReconcilingTime) 
            _reconciliationLedger.AddFullReconLine(_simData, "Rebalancing portfolio");
        
        var results = _simData.Model.WithdrawalStrategy.RebalancePortfolio(
            _simData.CurrentDateInSim, _simData.BookOfAccounts, _simData.RecessionStats, _simData.CurrentPrices,
            _simData.Model, _simData.TaxLedger, _simData.PgPerson);
        _simData.BookOfAccounts = results.accounts;
        _simData.TaxLedger = results.ledger;
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Rebalancing took {stopwatch.ElapsedMilliseconds} ms");
#endif 
        
        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcileRebalancing || !_isReconcilingTime) return;
        _reconciliationLedger.AddMessages(results.messages);
        _reconciliationLedger.AddFullReconLine(_simData, "Rebalanced portfolio");
    }
    
    private void ReconcileEoy()
    {
        if (!MonteCarloConfig.DebugMode) return;
        if (!MonteCarloConfig.ShouldReconcileEOYRecap) return;
        _reconciliationLedger.AddFullReconLine(_simData, "EOY recap");
    }

    private void RecordFunAndAnxiety()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcilePayDay && _isReconcilingTime)
            _reconciliationLedger.AddFullReconLine(_simData, "Recording fun and anxiety");

        var results = Simulation.RecordFunAndAnxiety(_simData.Model, _simData.PgPerson, 
            _simData.CurrentDateInSim, _simData.RecessionStats, _simData.LifetimeSpend, _simData.BookOfAccounts);
        _simData.LifetimeSpend = results.spend;
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Processing fun and anxiety took {stopwatch.ElapsedMilliseconds} ms");
#endif
        
        if (!MonteCarloConfig.DebugMode || !MonteCarloConfig.ShouldReconcilePayDay || !_isReconcilingTime) return;
        _reconciliationLedger.AddMessages(results.messages);
        _reconciliationLedger.AddFullReconLine(_simData, "Done recording fund and anxiety");
    }
    private void SetGrowthAndPrices()
    {
#if PERFORMANCEPROFILING
        Stopwatch stopwatch = new();
        stopwatch.Start();
#endif 
        _simData.CurrentPrices = Simulation.SetNewPrices(
            _simData.CurrentPrices, _hypotheticalPrices, _simData.CurrentDateInSim);
        if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcilePricingGrowth && _isReconcilingTime)
        {
            _reconciliationLedger.AddMessageLine(new ReconciliationMessage(
                _simData.CurrentDateInSim, _simData.CurrentPrices.CurrentLongTermGrowthRate,
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
        _isReconcilingTime = Simulation.IsReconciliationPeriod(_simData.CurrentDateInSim);
         
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
        _simData.RecessionStats = Recession.CalculateRecessionStats(
            _simData.RecessionStats, _simData.CurrentPrices, _simData.Model, _simData.BookOfAccounts,
            _simData.CurrentDateInSim);
        
#if PERFORMANCEPROFILING
        stopwatch.Stop();
        _sim.Log.Debug($"Updating recessing stats took {stopwatch.ElapsedMilliseconds} ms");
#endif 
        
    }
    
    #endregion
}
