using System.Text;
using Lib.DataTypes.Postgres;
using Lib.StaticConfig;

namespace Lib.DataTypes.Presentation;

public static class NetWorth
{
    public static string CreateFormattedFinancialSummary(
        List<PgInvestmentAccountGroup> investmentAccountGroups, List<PgDebtAccount> debtAccounts, 
        List<PgCashAccount> cashAccounts)
    {
        var totalDebt = debtAccounts
            .Sum(GetSumOfLatestPositionsForDebtAccount);

        var realEstateValue = investmentAccountGroups
            .Where(x => x.Name == PresentationConfig.HomeInvestementAccountName)
            .SelectMany(x => x.InvestmentAccounts)
            .Sum(GetSumOfLatestPositionsForInvestmentAccount);
        
        var totalEquities = investmentAccountGroups
            .Where(x => x.Name != PresentationConfig.HomeInvestementAccountName)
            .SelectMany(x => x.InvestmentAccounts)
            .Sum(GetSumOfLatestPositionsForInvestmentAccount);

        var totalCash = cashAccounts
            .Sum(GetSumOfLatestPositionsForCashAccount);
        
        var totalNetWorth = totalEquities + realEstateValue - totalDebt + totalCash;
        var investmentAssetsSummary = CreateFormattedInvestmentAssetsSummary(
            investmentAccountGroups, totalEquities);
        var cashAssetsSummary = CreateFormattedCashAssetsSummary(cashAccounts, totalCash);
        var propertyAssetsSummary = CreateFormattedPropertyAssetsSummary(realEstateValue);
        var debtLiabilitiesSummary = CreateFormattedDebtLiabilitiesSummary(debtAccounts, totalDebt);
        
        var output = new StringBuilder();
        output.AppendLine($"""
                <table class="summaryTable">
                    <tr>
                        <th class="level0">Net worth:</th>
                        <td class="level0"></td>
                    </tr>
            {investmentAssetsSummary}
            {cashAssetsSummary}
            {propertyAssetsSummary}
            {debtLiabilitiesSummary}
                    <tr>
                        <th class="level0 suml0">Total net worth:</th>
                        <td class="level0 suml0">$&nbsp;&nbsp;&nbsp;&nbsp;{totalNetWorth.ToString(PresentationConfig.AccountingFormat)}</td>
                    </tr>
                </table>
            """);

        return output.ToString();
    }

    private static decimal GetSumOfLatestPositionsForCashAccount(
        PgCashAccount account)
    {
        return account.Positions
            .OrderByDescending(x => x.PositionDate)
            .FirstOrDefault()?.CurrentBalance ?? 0;
    }
    
    private static decimal GetSumOfLatestPositionsForDebtAccount(
        PgDebtAccount account)
    {
        return account.Positions
            .OrderByDescending(x => x.PositionDate)
            .FirstOrDefault()?.CurrentBalance ?? 0;
    }

    private static decimal GetSumOfLatestPositionsForInvestmentAccount(PgInvestmentAccount account)
    {
        return GetLatestPositionsForInvestmentAccount(account)
            .Sum(x => x.latestPosition?.CurrentValue ?? 0);
    }

    private static List<(string symbol, PgPosition latestPosition)> GetLatestPositionsForInvestmentAccount(
        PgInvestmentAccount account)
    {
        var list = account.Positions
            .GroupBy(p => p.Symbol)
            .Select(x => (
                    x.Key,
                    x.OrderByDescending(y => y.PositionDate).First()
                )
            ).ToList();
        return list;
    }
    
    private static string CreateFormattedInvestmentAssetsSummary(
        List<PgInvestmentAccountGroup> investmentAccountGroups, decimal totalEquities)
    {
        List<(string groupName, decimal totalValue)> wealthGroups = investmentAccountGroups
            .Where(x => x.Name != PresentationConfig.HomeInvestementAccountName)
            .Select(x => (
                x.Name, 
                x.InvestmentAccounts.Select(GetSumOfLatestPositionsForInvestmentAccount).Sum())
            )
            .ToList();
            
           
         
        var wealthGroupsRows = new StringBuilder();
        foreach (var g in wealthGroups)
        {
            wealthGroupsRows.AppendLine("<tr>");
            wealthGroupsRows.AppendLine($"<th class=\"level2\">{g.groupName}:</th>");
            wealthGroupsRows.AppendLine($"<td class=\"level2\">{g.totalValue.ToString(PresentationConfig.AccountingFormat)}</td>");
            wealthGroupsRows.AppendLine("</tr>");
        }

        var output = new StringBuilder();
        output.AppendLine($"""
                                   <tr>
                                       <th class="level1">Investment assets:</th>
                                       <td class="level1"></td>
                                   </tr>
                                       {wealthGroupsRows}
                                   <tr>
                                       <th class="level1 suml1">Total investments:</th>
                                       <td class="level1 suml1">{totalEquities.ToString(PresentationConfig.AccountingFormat)}</td>
                                   </tr>
                           """);

        return output.ToString();
    }
    
    private static string CreateFormattedCashAssetsSummary(List<PgCashAccount> cashAccounts, decimal totalCash)
    {
        var rows = new StringBuilder();
        foreach (var r in cashAccounts)
        {
            rows.AppendLine("<tr>");
            rows.AppendLine($"<th class=\"level2\">{r.Name}:</th>");
            rows.AppendLine($"<td class=\"level2\">{GetSumOfLatestPositionsForCashAccount(r).ToString(PresentationConfig.AccountingFormat)}</td>");
            rows.AppendLine("</tr>");
        }

        var output = new StringBuilder();
        output.AppendLine($"""
                    <tr>
                        <th class="level1">Cash assets:</td>
                        <td class="level1"></td>
                    </tr>
                        {rows}
                    <tr>
                        <th class="level1 suml1">Total cash:</th>
                        <td class="level1 suml1">{totalCash.ToString(PresentationConfig.AccountingFormat)}</td>
                    </tr>
            """);

        return output.ToString();

    }
    private static string CreateFormattedPropertyAssetsSummary(decimal totalProperty)
    {
        var output = new StringBuilder();
        output.AppendLine($"""
                    <tr>
                        <th class="level1">Property assets:</td>
                        <td class="level1"></td>
                    </tr>
                    <tr>
                        <th class="level2">Value of house on Logan Circle</th>
                        <td class="level2">{totalProperty.ToString(PresentationConfig.AccountingFormat)}</td>
                    </tr>
                    <tr>
                        <th class="level1 suml1">Total property:</th>
                        <td class="level1 suml1">{totalProperty.ToString(PresentationConfig.AccountingFormat)}</td>
                    </tr>
            """);

        return output.ToString();

    }
    private static string CreateFormattedDebtLiabilitiesSummary(List<PgDebtAccount> debtAccounts, decimal totalDebt)
    {
        var rows = new StringBuilder();
        foreach (var r in debtAccounts)
        {
            var balance = GetSumOfLatestPositionsForDebtAccount(r);
            rows.AppendLine("<tr>");
            rows.AppendLine($"<th class=\"level2\">{r.Name}:</th>");
            rows.AppendLine($"<td class=\"level2\">{(balance * -1.0M).ToString(PresentationConfig.AccountingFormat)}</td>");
            rows.AppendLine("</tr>");
        }

        var output = new StringBuilder();
        output.AppendLine($"""
                    <tr>
                        <th class="level1">Debt liabilities:</td>
                        <td class="level1"></td>
                    </tr>
                        {rows}
                    <tr>
                        <th class="level1 suml1">Total debt:</th>
                        <td class="level1 suml1">{(totalDebt * -1M).ToString(PresentationConfig.AccountingFormat)}</td>
                    </tr>
            """);

        return output.ToString();

    }  
}