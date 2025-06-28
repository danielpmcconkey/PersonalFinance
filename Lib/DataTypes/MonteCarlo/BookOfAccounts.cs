namespace Lib.DataTypes.MonteCarlo;

public class BookOfAccounts
{
    public McInvestmentAccount? Roth401k { get; set; }
    public McInvestmentAccount? RothIra { get; set; }
    public McInvestmentAccount? Traditional401k { get; set; }
    public McInvestmentAccount? TraditionalIra { get; set; }
    public McInvestmentAccount? Brokerage { get; set; }
    public McInvestmentAccount? Hsa { get; set; }
    public McInvestmentAccount? Cash { get; set; }
    public List<McInvestmentAccount>? InvestmentAccounts { get; set; }
    public List<McDebtAccount>? DebtAccounts { get; set; }

    public BookOfAccounts(
        McInvestmentAccount? roth401k = null,
        McInvestmentAccount? rothIra = null,
        McInvestmentAccount? traditional401k = null,
        McInvestmentAccount? traditionalIra = null,
        McInvestmentAccount? brokerage = null,
        McInvestmentAccount? hsa = null,
        McInvestmentAccount? cash = null,
        List<McInvestmentAccount>? investmentAccounts = null,
        List<McDebtAccount>? debtAccounts = null)
    {
        Roth401k = roth401k;
        RothIra = rothIra;
        Traditional401k = traditional401k;
        TraditionalIra = traditionalIra;
        Brokerage = brokerage;
        Hsa = hsa;
        Cash = cash;
        InvestmentAccounts = investmentAccounts;
        DebtAccounts = debtAccounts;
    }
}