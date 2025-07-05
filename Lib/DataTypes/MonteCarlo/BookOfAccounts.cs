namespace Lib.DataTypes.MonteCarlo;

public struct BookOfAccounts
{
    public McInvestmentAccount Roth401K { get; set; }
    public McInvestmentAccount RothIra { get; set; }
    public McInvestmentAccount Traditional401K { get; set; }
    public McInvestmentAccount TraditionalIra { get; set; }
    public McInvestmentAccount Brokerage { get; set; }
    public McInvestmentAccount Hsa { get; set; }
    public McInvestmentAccount Cash { get; set; }
    public List<McInvestmentAccount> InvestmentAccounts { get; set; }
    public List<McDebtAccount> DebtAccounts { get; set; }
}