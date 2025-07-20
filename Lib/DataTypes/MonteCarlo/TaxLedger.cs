using Lib.StaticConfig;
using NodaTime;

namespace Lib.DataTypes.MonteCarlo;

public struct TaxLedger
{
    public TaxLedger()
    {
    }
    public List<(LocalDateTime earnedDate, decimal amount)> SocialSecurityIncome { get; set; } = [];
    public List<(LocalDateTime earnedDate, decimal amount)> W2Income { get; set; } = [];
    public List<(LocalDateTime earnedDate, decimal amount)> TaxableIraDistribution { get; set; } = [];
    public List<(LocalDateTime earnedDate, decimal amount)> TaxableInterestReceived { get; set; } = []; // todo: record taxable interest received
    public List<(LocalDateTime earnedDate, decimal amount)> TaxFreeInterestPaid { get; set; } = [];
    public List<(LocalDateTime earnedDate, decimal amount)> QualifiedDividendsReceived { get; set; } = [];
    public List<(LocalDateTime earnedDate, decimal amount)> DividendsReceived { get; set; } = []; // includes ordinary and qualified
    public List<(LocalDateTime earnedDate, decimal amount)> FederalWithholdings { get; set; } = [];
    public List<(LocalDateTime earnedDate, decimal amount)> StateWithholdings { get; set; } = [];
    public List<(LocalDateTime earnedDate, decimal amount)> LongTermCapitalGains { get; set; } = [];
    public List<(LocalDateTime earnedDate, decimal amount)> ShortTermCapitalGains { get; set; } = []; // todo: manage long term vs short term gains
    public decimal TotalTaxPaid { get; set; } = 0; // lifetime total
}