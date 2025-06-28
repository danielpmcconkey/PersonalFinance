using NodaTime;
using Lib.MonteCarlo;

namespace Lib.DataTypes.MonteCarlo;

/// <summary>
/// This is meant to encapsulate all data needed by the sim and persisted throughout the sim. Each sim run should have
/// one of these 
/// </summary>
public class MonteCarloSim
{
    /// <summary>
    /// currently, each sim run has its own logger. Todo: make logger more of an asynch app telemetry tool
    /// </summary>
    public Logger Log { get; set; }
    public McModel SimParameters { get; set; }
    public BookOfAccounts BookOfAccounts { get; set; } = new();
    public CurrentPrices CurrentPrices { get; set; } = new();
    public McPerson Person { get; set; }
    public LocalDateTime CurrentDateInSim { get; set; }
    public RecessionStats RecessionStats { get; set; } = new();
    public TaxLedger TaxLedger { get; set; } = new();
    public LifetimeSpend LifetimeSpend { get; set; } = new();

    public MonteCarloSim(
        Logger log,
        McModel simParameters,
        McPerson person)
    {
        Log = log;
        SimParameters = simParameters;
        Person = person;
        CurrentDateInSim = StaticConfig.MonteCarloConfig.MonteCarloSimStartDate;
    }
}