using Lib.DataTypes.MonteCarlo;

namespace Lib.Tests;

public static class TestDataManager
{
    /// <summary>
    /// creates a book of accounts with empty positions
    /// </summary>
    internal static BookOfAccounts CreateTestBookOfAccounts()
    {
        var cash = new McInvestmentAccount
        {
            Id = Guid.NewGuid(), Name = "test cash account", AccountType = McInvestmentAccountType.CASH, Positions = []
        };
        var roth401K = new McInvestmentAccount()
        {
            Id = Guid.Empty, AccountType = McInvestmentAccountType.ROTH_401_K, Name = "test Roth 401k", Positions = []
        };
        var rothIra = new McInvestmentAccount()
        {
            Id = Guid.Empty, AccountType = McInvestmentAccountType.ROTH_IRA, Name = "test Roth IRA", Positions = []
        };
        var traditional401K = new McInvestmentAccount()
        {
            Id = Guid.Empty, AccountType = McInvestmentAccountType.TRADITIONAL_401_K, Name = "test traditional 401k", Positions = []
        };
        var traditionalIra = new McInvestmentAccount()
        {
            Id = Guid.Empty, AccountType = McInvestmentAccountType.TRADITIONAL_IRA, Name = "test traditional IRA", Positions = []
        };
        var taxableBrokerage = new McInvestmentAccount()
        {
            Id = Guid.Empty, AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE, Name = "test taxable brokerage", Positions = []
        };
        var hsa = new McInvestmentAccount()
        {
            Id = Guid.Empty, AccountType = McInvestmentAccountType.HSA, Name = "test taxable brokerage", Positions = []
        };

        return new BookOfAccounts(
            roth401K,
            rothIra, 
            traditional401K, 
            traditionalIra,
            taxableBrokerage,
            hsa,
            cash,
            new List<McInvestmentAccount>(),
            new List<McDebtAccount>()
        );
    }
}