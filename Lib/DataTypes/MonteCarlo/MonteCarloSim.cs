using NodaTime;
using Lib.MonteCarlo;

namespace Lib.DataTypes.MonteCarlo;

/// <summary>
/// This is meant to encapsulate all data needed by the sim and persisted throughout the sim. Each sim run should have
/// one of these 
/// </summary>
public struct MonteCarloSim
{
    public MonteCarloSim(
        Logger logger, McModel simParams, BookOfAccounts bookOfAccounts, McPerson person,
        LocalDateTime currentDateInSim, CurrentPrices currentPrices, RecessionStats recessionStats,
        TaxLedger taxLedger, LifetimeSpend lifetimeSpend)
    {
        Log = logger;
        SimParameters = simParams;
        BookOfAccounts = bookOfAccounts;
        Person = person;
        CurrentDateInSim = currentDateInSim;
        CurrentPrices = currentPrices;
        RecessionStats = recessionStats;
        TaxLedger = taxLedger;
        LifetimeSpend = lifetimeSpend;
    }

    /// <summary>
    /// currently, each sim run has its own logger. Todo: make logger more of an asynch app telemetry tool
    /// </summary>
    public required  Logger Log { get; set; }

    public required McModel SimParameters { get; set; }
    public required BookOfAccounts BookOfAccounts { get; set; }
    public required McPerson Person { get; set; }
    public required  LocalDateTime CurrentDateInSim { get; set; }
    public CurrentPrices CurrentPrices { get; set; }
    public RecessionStats RecessionStats { get; set; }
    public TaxLedger TaxLedger { get; set; }
    public LifetimeSpend LifetimeSpend { get; set; }
}