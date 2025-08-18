using Lib.DataTypes.MonteCarlo;

namespace PersonalFinance;

public class MonteCarloResultsChart
{
    public int Ordinal { get; init; }
    public string? JavascriptId { get; init; }
    public string? JavascriptFunctionName { get; init; }
    public string? Title { get; init; }
    public string? VAxisTitle { get; init; }
    public string? Description { get; init; }
    public SingleModelRunResultStatLineAtTime[]? StatLinesAtTime { get; init; }
    public SingleModelRunResultBankruptcyRateAtTime[]? BankruptcyRatesOverTime { get; init; }
    public bool IsBar { get; init; } = false;
}