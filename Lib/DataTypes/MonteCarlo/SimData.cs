using Lib.DataTypes.Postgres;
using NodaTime;

namespace Lib.DataTypes.MonteCarlo;

/// <summary>
/// This is meant to encapsulate all data needed by the sim and persisted throughout the sim. Each sim run should have
/// one of these 
/// </summary>
public struct SimData
{
    public SimData(
        Logger logger, Model model, BookOfAccounts bookOfAccounts, PgPerson pgPerson,
        LocalDateTime currentDateInSim, CurrentPrices currentPrices, RecessionStats recessionStats,
        TaxLedger taxLedger, LifetimeSpend lifetimeSpend)
    {
        Log = logger;
        Model = model;
        BookOfAccounts = bookOfAccounts;
        PgPerson = pgPerson;
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

    public required Model Model { get; set; }
    public required BookOfAccounts BookOfAccounts { get; set; }
    public required PgPerson PgPerson { get; set; }
    public required  LocalDateTime CurrentDateInSim { get; set; }
    public CurrentPrices CurrentPrices { get; set; }
    public RecessionStats RecessionStats { get; set; }
    public TaxLedger TaxLedger { get; set; }
    public LifetimeSpend LifetimeSpend { get; set; }
}