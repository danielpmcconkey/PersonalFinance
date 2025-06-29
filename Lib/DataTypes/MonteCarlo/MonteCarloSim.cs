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
    public required  Logger Log { get; set; }
    public required McModel SimParameters { get; set; }
    public required BookOfAccounts BookOfAccounts { get; set; }
    public required McPerson Person { get; set; }
    public required  LocalDateTime CurrentDateInSim { get; set; }
    public CurrentPrices CurrentPrices { get; set; } = new();
    public RecessionStats RecessionStats { get; set; } = new();
    public TaxLedger TaxLedger { get; set; } = new();
    public LifetimeSpend LifetimeSpend { get; set; } = new();
}