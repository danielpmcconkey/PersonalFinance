using System.Globalization;
using System.Text;
using Lib;
using Lib.DataTypes;
using Lib.DataTypes.Presentation;
using Lib.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.Presentation;
using Lib.StaticConfig;
using Model = Lib.DataTypes.MonteCarlo.Model;

namespace PersonalFinance;

internal class PresentationBuilder
{
    private readonly List<PresChart> _charts;
    private readonly string _formattedFinancialSummary;

    internal PresentationBuilder()
    {
        var investmentAccountGroups = PresentationDal.FetchInvestAccountGroupsAndChildData();
        var debtAccounts = PresentationDal.FetchDebtAccountsAndPositions();
        var cashAccounts = PresentationDal.FetchCashAccountsAndPositions();
        var monthEndInvestmentPositions = ChartData.GetEndOfMonthPositionsBySymbolAndAccount(investmentAccountGroups);
        var singleModelRunResult = MonteCarloFunctions.RunMonteCarlo();
        var months = ChartData.GetInvestmentMonthEnds(monthEndInvestmentPositions);
        _charts = ChartData.BuildCharts(months, monthEndInvestmentPositions, singleModelRunResult);
        
        _formattedFinancialSummary = NetWorth.CreateFormattedFinancialSummary(
            investmentAccountGroups, debtAccounts, cashAccounts);
    }

    internal void BuildPresentation()
    {
         
        var html = Html.CreateOverallHtml(_formattedFinancialSummary, _charts);
        var timestamp = $".{DateTime.Now:yyyy.MM.dd.HH.mm.ss}";
        var fullOutputPath = $"{PresentationConfig.PresentationOutputDir}PersonalFinanceBreakdown{timestamp}.html";
        try
        {
            File.WriteAllText(fullOutputPath, html);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}
