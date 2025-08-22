﻿using System.Globalization;
using System.Text;
using Lib;
using Lib.DataTypes;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;

namespace PersonalFinance
{
    internal class PresentationBuilder
    {
        private const string OutputDirectory = "/media/dan/fdrive/codeprojects/PersonalFinance/OutputFiles/";
        private List<Position> _wealthPositions = [];
        private List<Position> _cashPositions = [];
        private List<Position> _debtPositions = [];
        private List<BudgetPosition> _budgetPositions = [];
        private List<PgCategory> _budgetCategories = [];
        private  List<Position> _currentWealthPositions = [];
        private List<Position> _currentCashPositions = [];
        private List<Position> _currentDebtPositions = [];
        private  List<(string MonthAbbreviation, DateTime PositionDate)> _months = [];
        private  List<string> _taxBuckets = [];
        private  List<string> _accounts = [];
        private List<string> _accountGroups = [];
        private List<string> _stockTypeIndividualVsIndex = [];
        private List<string> _symbols = [];
        private List<AreaChart> _areaCharts = [];
        private List<MonteCarloResultsChart> _monteCarloResultsCharts = [];
        private SingleModelRunResult? _singleModelRunResult;

        private DateTime? _effectiveDate;

        private const string AccountingFormat = "#,##0.00;(#,##0.00);--";

        internal void BuildPresentation()
        {
            PullAndPopulateData();
            DefineAreaCharts();
            DefineMonteCarloCharts();

            PresentationHtml pHtml = new PresentationHtml(this);
            
            string html = pHtml.GetHTML();
            string timestamp = $".{DateTime.Now.ToString("yyyy.MM.dd.HH.mm.ss")}";
            //timestamp = "";
            string fullOutputPath = $"{OutputDirectory}PersonalFinanceBreakdown{timestamp}.html";
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
        public DateTime GetEffectiveDate()
        {
            if(_wealthPositions is null) throw new Exception("Wealth positions not populated");
            _effectiveDate ??= _wealthPositions.Max(x => x.PositionDate);
            return (DateTime)_effectiveDate;
        }
        public string GetChartsHead()
        {
            if(_areaCharts is null) throw new InvalidDataException($"{nameof(_areaCharts)} not defined");
            StringBuilder output = new StringBuilder();
            output.AppendLine("<script type=\"text/javascript\" src=\"https://www.gstatic.com/charts/loader.js\"></script>");
            output.AppendLine("<script type=\"text/javascript\">");
            output.AppendLine("google.charts.load('current', { 'packages':['corechart']});");
            output.AppendLine("google.charts.load('current', {'packages':['bar']});");
            // draw the charts when the page loads
            foreach (var c in _areaCharts.OrderBy(x => x.Ordinal))
            {
                output.AppendLine($"google.charts.setOnLoadCallback({c.JavascriptFunctionName});");
            }
            foreach (var c in _monteCarloResultsCharts.OrderBy(x => x.Ordinal))
            {
                output.AppendLine($"google.charts.setOnLoadCallback({c.JavascriptFunctionName});");
            }
            
            // populate the chart data
            foreach (var c in _areaCharts.OrderBy(x => x.Ordinal))
            {
                output.AppendLine(GetAreaChartFunction(c));
            }

            foreach (var c in _monteCarloResultsCharts.OrderBy(x => x.Ordinal))
            {
                output.AppendLine(GetMonteCarloResultChartFunction(c));
            }
            output.AppendLine("    </script>");
            return output.ToString();
        }
        public string GetCss()
        {
            string output = """
                <style type="text/css">
                    body { padding:25px; }
                    h1 { margin:25px; }
                    #myTab { margin-top:25px; }
                    .textSpace {
                        border:solid 3px black;
                        background:#ffffff;
                        margin-bottom:40px;
                        padding:40px;
                    }
                    .chartSpace {
                        margin-bottom:40px;
                        padding:40px;
                    }
                    .chartDescription {
                        font-size:20px;
                        width:60%;
                        margin-left:15%;
                    }
                    
                    .summaryTable tbody th { text-align:left; width:400px;}
                    .summaryTable tbody td { text-align:right; width:400px; }
                    .summaryTable tbody th.level0 { padding-left:0px; font-weight:bold; font-size:24px; }
                    .summaryTable tbody td.level0 { padding-right:0px; font-weight:bold; font-size:24px; }
                    .summaryTable tbody th.level1 { padding-left:25px; font-weight:bold; }
                    .summaryTable tbody td.level1 { padding-right:0px; font-weight:bold; }
                    .summaryTable tbody th.level2 { padding-left:50px; font-weight:normal; }
                    .summaryTable tbody td.level2 { padding-right:0px; font-weight:normal; }
                    .summaryTable tbody th.suml0 { border-top:solid 3px black; }
                    .summaryTable tbody td.suml0 { border-top:solid 3px black; }
                    .summaryTable tbody th.suml1 { border-top:solid 1px black; }
                    .summaryTable tbody td.suml1 { border-top:solid 1px black; }
                    .summaryTable tbody tr.ledgerline { border-bottom:solid 1px #cccccc; }
                    .summaryTable tbody th.monthHead { text-align:right; }
                    .tab-content>.tab-pane {
                        height: 1px;
                        overflow: hidden;
                        display: block;
                        visibility: hidden;
                    }
                    .tab-content>.active {
                        height: auto;
                        overflow: auto;
                        visibility: visible;
                    }

                </style>
                """;
            
            return output;
        }
        private List<string> GetChildrenCatIds(string catId)
        {
            var output = new List<string>();
            var children = _budgetCategories
                .Where(x => x.ParentId == catId)
                .Select(x => x.Id)
                .ToList();
            foreach (var child in children)
            {
                output.Add(child);
                output.AddRange(GetChildrenCatIds(child));
            }
            return output;
        }
        public (List<BudgetTableCell> cells, int nextRow) GetCellsForCat(
            string catId, int startingRow, int level, List<(string? MonthAbbreviation, DateTime PositionDate)> months)
        {
            List<BudgetTableCell> cells = new List<BudgetTableCell>();
            int nextRow = startingRow + 1;
            var descendants = GetChildrenCatIds(catId);
            var cat = _budgetPositions.Where(x => x.CategoryId == catId).FirstOrDefault();
            if (cat == null) return (new List<BudgetTableCell>(), nextRow);
            
            // add a row for this cat
            for (int currentColumn = 0; currentColumn <= months.Count + 1; currentColumn++)
            {
                var monthAbbreviation = "";
                if (currentColumn > 0 && currentColumn <= months.Count) 
                    monthAbbreviation =  months[currentColumn - 1].MonthAbbreviation;
                if (currentColumn == 0)
                {
                    cells.Add(new BudgetTableCell()
                    {
                        Category = catId,
                        Column = currentColumn,
                        Row = startingRow,
                        Label = cat.CategoryName,
                        Value = 0M,
                        CssClass = $"level{level}"
                    });
                }
                else if (currentColumn > 0 && currentColumn <= months.Count)
                {
                    // total for this month; this cat and all children
                    var query = from bp in _budgetPositions
                                where
                                    bp.MonthAbbreviation == monthAbbreviation &&
                                    (
                                        (bp.CategoryId is not null && descendants.Contains(bp.CategoryId)) ||
                                        bp.CategoryId == catId
                                    )
                                select bp;

                    var thisTotal = query.Sum(x => x.SumTotal);
                    cells.Add(new BudgetTableCell()
                    {
                        Category = catId,
                        Column = currentColumn,
                        Row = startingRow,
                        Label = "",
                        Value = thisTotal,
                        CssClass = $"level{level}"
                    });
                }
                else if (currentColumn == months.Count + 1)
                {
                    // total for all months; this cat and all children
                    var query = from bp in _budgetPositions
                                where (bp.CategoryId is not null && descendants.Contains(bp.CategoryId)) ||
                                      bp.CategoryId == catId
                                select bp;

                    var thisTotal = query.Sum(x => x.SumTotal);
                    cells.Add(new BudgetTableCell()
                    {
                        Category = catId,
                        Column = currentColumn,
                        Row = startingRow,
                        Label = "",
                        Value = thisTotal,
                        CssClass = $"level{level}"
                    });
                }
            }
            // add rows for all children
            var children = _budgetCategories.Where(x => x.ParentId == catId).ToList();
            foreach (var child in children)
            {
                var recursiveResult = GetCellsForCat(child.Id, nextRow, level + 1, months);
                nextRow = recursiveResult.nextRow;
                cells.AddRange(recursiveResult.cells);
            }
            return (cells, nextRow);
        }
        public string GetBudgetSummary()
        {
            var months = _budgetPositions
                .Select(p => (p.MonthAbbreviation, p.PositionDate))
                .Distinct()
                .OrderByDescending(p => p.PositionDate)
                .ToList();

            

            var catsSansParents = _budgetCategories
                .Where(x => string.IsNullOrEmpty(x.ParentId))
                .OrderBy(x => x.OrdinalWithinParent)
                .ToList();

            
            List<BudgetTableCell> cells = new List<BudgetTableCell>();
            int currentRow = 1;
            foreach (var cPrime in catsSansParents)
            {
                var getCellsResult = GetCellsForCat(cPrime.Id, currentRow, 0, months);
                cells.AddRange(getCellsResult.cells);
                currentRow = getCellsResult.nextRow;
            }

            StringBuilder tableBody = new StringBuilder();
            // months header
            tableBody.AppendLine("<tr class=\"ledgerline\">");
            tableBody.AppendLine("<th class=\"monthHead\"></th>");
            foreach(var m in months)
            {
                tableBody.AppendLine($"<th class=\"monthHead\">{m.MonthAbbreviation}</th>");
            }
            tableBody.AppendLine("<th class=\"monthHead\">Total</th>");
            tableBody.AppendLine("</tr>");

            // individual rows
            var rowNums = cells
                .OrderBy(x => x.Row)
                .Select(x => x.Row).Distinct();
            foreach (var rowNum in rowNums)
            {
                tableBody.AppendLine("<tr class=\"ledgerline\">");
                for (int currentColumn = 0; currentColumn <= months.Count + 1; currentColumn++)
                {
                    var cell = cells.FirstOrDefault(x => x.Row == rowNum && x.Column == currentColumn);
                    string label = cell.Label;
                    if (currentColumn == 0)
                    {
                        tableBody.AppendLine($"<th class=\"{cell.CssClass}\">{cell.Label}</th>");
                    }
                    if (currentColumn > 0 && currentColumn < months.Count + 1)
                    {
                        tableBody.AppendLine($"<td class=\"{cell.CssClass}\">{cell.Value.ToString(AccountingFormat)}</td>");
                    }
                    if (currentColumn == months.Count + 1)
                    {
                        tableBody.AppendLine($"<td class=\"{cell.CssClass}\">{cell.Value.ToString(AccountingFormat)}</td>");
                    }
                }
                tableBody.AppendLine("</tr>");
            }


            StringBuilder output = new StringBuilder();
            output.AppendLine($"""
                    <table class="summaryTable">
                {tableBody.ToString()}
                
                    </table>
                """);

            return output.ToString();
        }
        public string GetFinancialSummary()
        {

            decimal mortgageDebt = _currentDebtPositions
                .Where(x => x.AccountGroup == "Home loan")
                .Sum(x => x.ValueAtTime);
            decimal totalDebt = _currentDebtPositions.Sum(x => x.ValueAtTime);
            decimal homeEquity = _currentWealthPositions
                .Where(x => x.AccountGroup == "Home Equity")
                .Sum(x => x.ValueAtTime);
            decimal totalWealthPositions = _currentWealthPositions
                .Where(x => x.AccountGroup != "Home Equity")
                .Sum(x => x.ValueAtTime);
            decimal homeValue = homeEquity;// + mortgageDebt; no longer need to add the mortgage debt
            decimal totalCash = _currentCashPositions.Sum(x => x.ValueAtTime);
            decimal totalNetWorth = totalWealthPositions + homeValue - totalDebt + totalCash;

            var investmentAssetsSummary = GetInvestmentAssestsSummary(totalWealthPositions);
            var cashAssetsSummary = GetCashAssestsSummary(totalCash);
            var propertyAssetsSummary = GetPropertyAssestsSummary(homeValue);
            var debtLiabilitiesSummary = GetDebtLiabilitiesSummary(totalDebt);


            StringBuilder output = new StringBuilder();
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
                            <td class="level0 suml0">$&nbsp;&nbsp;&nbsp;&nbsp;{totalNetWorth.ToString(AccountingFormat)}</td>
                        </tr>
                    </table>
                """);

            return output.ToString();
        }
        public string GetCharts()
        {
            StringBuilder output = new StringBuilder();

            foreach (var c in _areaCharts.OrderBy(x => x.Ordinal))
            {
                output.AppendLine("    <div class=\"chartSpace\">");
                output.AppendLine($"    <div id=\"{c.JavascriptId}\" class=\"gchart\" ></div>");
                output.AppendLine($"    <p class=\"chartDescription\">{c.Description}</p>");
                output.AppendLine("    </div>");
            }
            foreach (var c in _monteCarloResultsCharts.OrderBy(x => x.Ordinal))
            {
                output.AppendLine("    <div class=\"chartSpace\">");
                output.AppendLine($"    <div id=\"{c.JavascriptId}\" class=\"gchart\" ></div>");
                output.AppendLine($"    <p class=\"chartDescription\">{c.Description}</p>");
                output.AppendLine("    </div>");
            }
            return output.ToString();
        }
        private void PullAndPopulateData()
        {
            // populate from and to dates  to pull budget data
            var firstDayThisMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var to = firstDayThisMonth.AddSeconds(-1);
            var from = firstDayThisMonth.AddMonths(-12);
            
            var context = new PgContext();
            _wealthPositions = PostgresDal.GetWealthPositions();
            _cashPositions = PostgresDal.GetCashPositions();
            _debtPositions = PostgresDal.GetDebtPositions();
            _budgetPositions = PostgresDal.GetBudgetPositions(from, to);
            //_budgetCategories = PostgresDAL.GetCategories();
            _budgetCategories = context.PgCategories
                .Where(x => x.ShowInReport)
                .OrderBy(x => x.OrdinalWithinParent)
                .ToList();
            

            var effectiveDate = GetEffectiveDate();

            _currentWealthPositions = _wealthPositions
                .Where(x => x.PositionDate == effectiveDate)
                .ToList();
            _currentDebtPositions = _debtPositions
                .Where(x => x.PositionDate == effectiveDate)
                .ToList();
            _currentCashPositions = _cashPositions
                .Where(x => x.PositionDate == effectiveDate)
                .ToList();

            // get distinct month values
            _months = _wealthPositions
                .Select(p => (p.MonthAbbreviation, p.PositionDate))
                .Distinct()
                .OrderBy(p => p.PositionDate)
                .ToList();

            // get distinct tax buckets
            _taxBuckets = _wealthPositions.Select(p => p.TaxBucket).Distinct().ToList();

            // get distinct accounts
            _accounts = _wealthPositions
                .Select(p => (p.AccountId, p.AccountName))
                .Distinct()
                .OrderBy(p => p.AccountId)
                .Select(p => p.AccountName)
                .ToList();

            // get distinct account groups
            _accountGroups = _wealthPositions.Select(p => p.AccountGroup).Distinct().ToList();

            // get distinct Symbols
            //_symbols = _wealthPositions.Select(p => p.Symbol).Distinct().ToList();
            var maxMonth = _months.Max(x => x.PositionDate);
            _symbols = _wealthPositions
                .Where(x => x.PositionDate == maxMonth && x.ValueAtTime > 0)
                .Select(x => x.Symbol)
                .Distinct()
                .ToList();
            _symbols.Add("No longer held");

            _stockTypeIndividualVsIndex = [];
            _stockTypeIndividualVsIndex.Add("Individual stock");
            _stockTypeIndividualVsIndex.Add("Index");

            _singleModelRunResult = PopulateMonteCarloData();
        }

        private SingleModelRunResult PopulateMonteCarloData()
        {
            string logDir = ConfigManager.ReadStringSetting("LogDir");
            string timeSuffix = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
            string logFilePath = $"{logDir}MonteCarloLog{timeSuffix}.txt";
            var logger = new Logger(
                Lib.StaticConfig.MonteCarloConfig.LogLevel,
                logFilePath
            );

            logger.Info("Pulling person from the database");
            var danId = ConfigManager.ReadStringSetting("DanId");
            Guid danIdGuid = Guid.Parse(danId);
            var dan = Person.GetPersonById(danIdGuid);
            var investmentAccounts = AccountDbRead.FetchDbInvestmentAccountsByPersonId(danIdGuid);
            var debtAccounts = AccountDbRead.FetchDbDebtAccountsByPersonId(danIdGuid);


            logger.Info("Pulling historical pricing data");
            decimal[] sAndP500HistoricalTrends = Pricing.FetchSAndP500HistoricalTrends();
            
            logger.Info("Running in single model mode");
    
            logger.Info("Pulling model champion from the database");
            McModel champion = DataStage.GetModelChampion(dan);
    
            // over-write the start and end dates from the DB champion model to use what's in the app config
            champion.SimStartDate = MonteCarloConfig.MonteCarloSimStartDate;
            champion.SimEndDate = MonteCarloConfig.MonteCarloSimEndDate;
    
            logger.Info(logger.FormatBarSeparator('*'));
            logger.Info(logger.FormatHeading("Beginning Monte Carlo single model session run"));
            logger.Info(logger.FormatBarSeparator('*'));
            var results = SimulationTrigger.RunSingleModelSession(
                logger, champion, dan, investmentAccounts, debtAccounts, sAndP500HistoricalTrends);
            logger.Info("Single model simulation of all lives completed");
            return results;
        }

        private void DefineAreaCharts()
        {
            Func<Position, string, bool> taxBucketCatMatch = (p, cat) =>
                (p.TaxBucket == cat);
            Func<Position, string, bool> accountCatMatch = (p, cat) =>
                (p.AccountName == cat);
            Func<Position, string, bool> accountGroupCatMatch = (p, cat) =>
                (p.AccountGroup == cat);
            Func<Position, string, bool> indexVsIndividualCatMatch = (p, cat) => (
                    (cat == "Index")
                        ? IsPositionIndex(p)
                        : (IsPositionStock(p) && !IsPositionIndex(p))
                );
            Func<Position, string, bool> symbolCatMatch = (p, cat) =>
            {
                if (cat != "No longer held") return (p.Symbol == cat);
                return !_symbols.Contains(p.Symbol);
            };
                

            _areaCharts = new List<AreaChart>();
            _areaCharts.Add(new AreaChart()
            {
                Ordinal = 0,
                JavascriptId = "wealth_by_tax_buckets_chart_div",
                JavascriptFunctionName = "drawWealthByTaxBucketsChart",
                Title = "Wealth by tax types",
                VAxisTitle = "Wealth in USD",
                Description = "The \"Wealth by tax types\" chart above shows " +
                "future tax implications for investment assets. Whether we will" +
                " have to pay tax on the total amount, just the growth, or none" +
                " of it.",
                FuncCatMatch = taxBucketCatMatch,
                Categories = _taxBuckets
            });
            _areaCharts.Add(new AreaChart()
            {
                Ordinal = 20,
                JavascriptId = "wealth_by_accounts_chart_div",
                JavascriptFunctionName = "drawWealthByAccountsChart",
                Title = "Wealth by accounts",
                VAxisTitle = "Wealth in USD",
                Description = "The \"Wealth by accounts\" chart above is a more" +
                " fine-grained version of the \"Wealth by tax types\" chart, " +
                "showing all the individual accounts that roll up into the " +
                "account families.",
                FuncCatMatch = accountCatMatch,
                Categories = _accounts

            });
            _areaCharts.Add(new AreaChart()
            {
                Ordinal = 10,
                JavascriptId = "wealth_by_account_groups_chart_div",
                JavascriptFunctionName = "drawWealthByAccountGroupsChart",
                Title = "Wealth by accounts groups",
                VAxisTitle = "Wealth in USD",
                Description = "The \"Wealth by account groups\" chart above " +
                "shows our total wealth by account families. Different account " +
                "families can contain multiple specific accounts.",
                FuncCatMatch = accountGroupCatMatch,
                Categories = _accountGroups
            });
            _areaCharts.Add(new AreaChart()
            {
                Ordinal = 30,
                JavascriptId = "index_vs_individual_chart_div",
                JavascriptFunctionName = "drawIndexVsIndividualChart",
                Title = "Index funds vs individual stocks",
                VAxisTitle = "Wealth in USD",
                Description = "The \"Index funds vs individual stocks\" chart " +
                "above shows the split between individual stock purchases " +
                "(buying N shares of Microsoft) versus purchases of stock " +
                "indexes (mutual funds, EFTs, and other investment vehicles " +
                "that buy a broad range of investments).",
                FuncCatMatch = indexVsIndividualCatMatch,
                Categories = _stockTypeIndividualVsIndex
            });

            _areaCharts.Add(new AreaChart()
            {
                Ordinal = 40,
                JavascriptId = "wealth_by_symbol_chart_div",
                JavascriptFunctionName = "drawWealthBySymbolChart",
                Title = "Wealth by investment vehicle",
                VAxisTitle = "Wealth in USD",
                Description = "The \"Wealth by investment vehicle\" chart shows " +
                "how our total wealth is divided into different forms of " +
                "investment (stock, bonds, real estate). Mortgage debt is " +
                "factored into home equity. Other debt is not included. Note, " +
                "the VanXXX represents an institutional fund that I couldn't " +
                "find the ticker symbol for.",
                FuncCatMatch = symbolCatMatch,
                Categories = _symbols
            });
        } 
        private void DefineMonteCarloCharts()
        {
            _monteCarloResultsCharts = new List<MonteCarloResultsChart>();
            
            
            _monteCarloResultsCharts.Add(new MonteCarloResultsChart()
            {
                Ordinal = 10,
                JavascriptId = "mc_fun_points_chart_div",
                JavascriptFunctionName = "drawMcFunPointsChart",
                Title = "Monte Carlo Fun Points",
                VAxisTitle = "Fun Points!",
                Description = "It's the funzies, y'all. Get funzies when you spend money you don't have to spend." +
                              " Lose funzies when you are anxious about your money or when you have to work to " +
                              "support your lifestyle.",
                StatLinesAtTime = _singleModelRunResult?.TotalFunPointsOverTime,
                BankruptcyRatesOverTime = null
            });
            _monteCarloResultsCharts.Add(new MonteCarloResultsChart()
            {
                Ordinal = 20,
                JavascriptId = "mc_net_worth_chart_div",
                JavascriptFunctionName = "drawMcNetWorthChart",
                Title = "Monte Carlo Net Worth",
                VAxisTitle = "Total net worth (USD)",
                Description = "Measures your projected total net worth over time",
                StatLinesAtTime = _singleModelRunResult?.NetWorthStatsOverTime,
                BankruptcyRatesOverTime = null
            });
            
            _monteCarloResultsCharts.Add(new MonteCarloResultsChart()
            {
                Ordinal = 30,
                JavascriptId = "mc_spend_chart_div",
                JavascriptFunctionName = "drawMcSpendChart",
                Title = "Monte Carlo Spend",
                VAxisTitle = "Total spend (USD)",
                Description = "Measures your projected total spend over time. Spend includes money you had to " +
                              "spend, like groceries, mortgages, etc. It also includes any fun spending." +
                              " It does not include money you spent to buy investment positions.",
                StatLinesAtTime = _singleModelRunResult?.TotalSpendOverTime,
                BankruptcyRatesOverTime = null
            });
            _monteCarloResultsCharts.Add(new MonteCarloResultsChart()
            {
                Ordinal = 40,
                JavascriptId = "mc_tax_chart_div",
                JavascriptFunctionName = "drawMcTaxChart",
                Title = "Monte Carlo Tax",
                VAxisTitle = "Total tax payment (USD)",
                Description = "Measures your total lifetime tax payment (starting at $0 at the simulation start date)",
                StatLinesAtTime = _singleModelRunResult?.TotalTaxOverTime,
                BankruptcyRatesOverTime = null
            });
            _monteCarloResultsCharts.Add(new MonteCarloResultsChart()
            {
                Ordinal = 50,
                JavascriptId = "mc_bankruptcy_chart_div",
                JavascriptFunctionName = "drawMcBankruptcyChart",
                Title = "Monte Carlo Bankruptcy Rates",
                VAxisTitle = "Bankruptcy percentage",
                Description = "The percentage of simulated lives in bankruptcy at each point in time in the simulator",
                StatLinesAtTime = null,
                BankruptcyRatesOverTime = _singleModelRunResult?.BankruptcyRateOverTime
            });
            _monteCarloResultsCharts.Add(new MonteCarloResultsChart()
            {
                Ordinal = 60,
                JavascriptId = "mc_funbyyear_chart_div",
                JavascriptFunctionName = "drawFunByYearChart",
                Title = "Monte Carlo Fun Points by Year",
                VAxisTitle = "Fun Points",
                Description = "The amount of fun points logged each year",
                StatLinesAtTime = _singleModelRunResult?.FunPointsByYear,
                BankruptcyRatesOverTime = null,
                IsBar = true,
            });
            _monteCarloResultsCharts.Add(new MonteCarloResultsChart()
            {
                Ordinal = 70,
                JavascriptId = "mc_spendbyyear_chart_div",
                JavascriptFunctionName = "drawSpendByYearChart",
                Title = "Monte Carlo spend by Year",
                VAxisTitle = "Money spent",
                Description = "The amount of money you spent (required and fun) each year",
                StatLinesAtTime = _singleModelRunResult?.SpendByYear,
                BankruptcyRatesOverTime = null,
                IsBar = true,
            });
            _monteCarloResultsCharts.Add(new MonteCarloResultsChart()
            {
                Ordinal = 80,
                JavascriptId = "mc_taxbyyear_chart_div",
                JavascriptFunctionName = "drawTaxByYearChart",
                Title = "Monte Carlo tax by Year",
                VAxisTitle = "Tax paid",
                Description = "The amount of money you paid in tax each year",
                StatLinesAtTime = _singleModelRunResult?.TaxByYear,
                BankruptcyRatesOverTime = null,
                IsBar = true,
            });
            _monteCarloResultsCharts.Add(new MonteCarloResultsChart()
            {
                Ordinal = 90,
                JavascriptId = "mc_healthspendbyyear_chart_div",
                JavascriptFunctionName = "drawHealthSpendByYearChart",
                Title = "Monte Carlo health spend by year",
                VAxisTitle = "Health spend",
                Description = "The amount of money you paid in health care each year",
                StatLinesAtTime = _singleModelRunResult?.HealthSpendByYear,
                BankruptcyRatesOverTime = null,
                IsBar = true,
            });
            _monteCarloResultsCharts.Add(new MonteCarloResultsChart()
            {
                Ordinal = 100,
                JavascriptId = "mc_iradistrbyyear_chart_div",
                JavascriptFunctionName = "drawIraDistByYearChart",
                Title = "Monte Carlo IRA distributions by Year",
                VAxisTitle = "IRA distributions",
                Description = "The amount of money you pulled from tax-deferred accounts each year",
                StatLinesAtTime = _singleModelRunResult?.IraDistributionsByYear,
                BankruptcyRatesOverTime = null,
                IsBar = true,
            });
            _monteCarloResultsCharts.Add(new MonteCarloResultsChart()
            {
                Ordinal = 110,
                JavascriptId = "mc_capgainsbyyear_chart_div",
                JavascriptFunctionName = "drawCapGainsByYearChart",
                Title = "Monte Carlo capital gains by Year",
                VAxisTitle = "Capital gains",
                Description = "The amount of money you received as capital gains each year",
                StatLinesAtTime = _singleModelRunResult?.CapitalGainsByYear,
                BankruptcyRatesOverTime = null,
                IsBar = true,
            });
            _monteCarloResultsCharts.Add(new MonteCarloResultsChart()
            {
                Ordinal = 120,
                JavascriptId = "mc_taxfreebyyear_chart_div",
                JavascriptFunctionName = "drawTaxFreeByYearChart",
                Title = "Monte Carlo tax free withdrawals by Year",
                VAxisTitle = "Tax free withdrawals",
                Description = "The amount of money you pulled from tax-free accounts each year",
                StatLinesAtTime = _singleModelRunResult?.TaxFreeWithdrawalsByYear,
                BankruptcyRatesOverTime = null,
                IsBar = true,
            });
            _monteCarloResultsCharts.Add(new MonteCarloResultsChart()
            {
                Ordinal = 130,
                JavascriptId = "mc_funspendbyyear_chart_div",
                JavascriptFunctionName = "drawFundSpendByYearChart",
                Title = "Monte Carlo fun spend by Year",
                VAxisTitle = "Fun spend",
                Description = "The amount of money you spent to have fun each year",
                StatLinesAtTime = _singleModelRunResult?.FunSpendByYear,
                BankruptcyRatesOverTime = null,
                IsBar = true,
            });
            _monteCarloResultsCharts.Add(new MonteCarloResultsChart()
            {
                Ordinal = 130,
                JavascriptId = "mc_requiredspendbyyear_chart_div",
                JavascriptFunctionName = "drawRequiredSpendByYearChart",
                Title = "Monte Carlo required spend by Year",
                VAxisTitle = "Required spend",
                Description = "The amount of money you spent each year that you had no choice over. Includes bills and health care costs, but does not include debt payback",
                StatLinesAtTime = _singleModelRunResult?.NotFunSpendByYear,
                BankruptcyRatesOverTime = null,
                IsBar = true,
            });
        } 
        private string GetInvestmentAssestsSummary(decimal totalWealthPositions)
        {
            var wealthGroups = from p in _currentWealthPositions.Where(x => x.AccountGroup != "Home Equity")
                               group p by p.AccountGroup into accountGroup
                               select accountGroup;
            StringBuilder wealthGroupsRows = new StringBuilder();
            foreach (var g in wealthGroups)
            {
                decimal totalValue = g.Sum(x => x.ValueAtTime);
                wealthGroupsRows.AppendLine("<tr>");
                wealthGroupsRows.AppendLine($"<th class=\"level2\">{g.Key}:</th>");
                wealthGroupsRows.AppendLine($"<td class=\"level2\">{totalValue.ToString(AccountingFormat)}</td>");
                wealthGroupsRows.AppendLine("</tr>");
            }

            StringBuilder output = new StringBuilder();
            output.AppendLine($"""
                        <tr>
                            <th class="level1">Investment assets:</th>
                            <td class="level1"></td>
                        </tr>
                            {wealthGroupsRows}
                        <tr>
                            <th class="level1 suml1">Total investments:</th>
                            <td class="level1 suml1">{totalWealthPositions.ToString(AccountingFormat)}</td>
                        </tr>
                """);

            return output.ToString();

        }
        private string GetCashAssestsSummary(decimal totalCash)
        {
            StringBuilder rows = new StringBuilder();
            foreach (var r in _currentCashPositions)
            {
                rows.AppendLine("<tr>");
                rows.AppendLine($"<th class=\"level2\">{r.AccountName}:</th>");
                rows.AppendLine($"<td class=\"level2\">{r.ValueAtTime.ToString(AccountingFormat)}</td>");
                rows.AppendLine("</tr>");
            }

            StringBuilder output = new StringBuilder();
            output.AppendLine($"""
                        <tr>
                            <th class="level1">Cash assets:</td>
                            <td class="level1"></td>
                        </tr>
                            {rows}
                        <tr>
                            <th class="level1 suml1">Total cash:</th>
                            <td class="level1 suml1">{totalCash.ToString(AccountingFormat)}</td>
                        </tr>
                """);

            return output.ToString();

        }
        private string GetPropertyAssestsSummary(decimal totalProperty)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine($"""
                        <tr>
                            <th class="level1">Property assets:</td>
                            <td class="level1"></td>
                        </tr>
                        <tr>
                            <th class="level2">Value of house on Logan Circle</th>
                            <td class="level2">{totalProperty.ToString(AccountingFormat)}</td>
                        </tr>
                        <tr>
                            <th class="level1 suml1">Total property:</th>
                            <td class="level1 suml1">{totalProperty.ToString(AccountingFormat)}</td>
                        </tr>
                """);

            return output.ToString();

        }
        private string GetDebtLiabilitiesSummary(decimal totalDebt)
        {
            StringBuilder rows = new StringBuilder();
            foreach (var r in _currentDebtPositions)
            {
                rows.AppendLine("<tr>");
                rows.AppendLine($"<th class=\"level2\">{r.AccountName}:</th>");
                rows.AppendLine($"<td class=\"level2\">{(r.ValueAtTime * -1.0M).ToString(AccountingFormat)}</td>");
                rows.AppendLine("</tr>");
            }

            StringBuilder output = new StringBuilder();
            output.AppendLine($"""
                        <tr>
                            <th class="level1">Debt liabilities:</td>
                            <td class="level1"></td>
                        </tr>
                            {rows}
                        <tr>
                            <th class="level1 suml1">Total debt:</th>
                            <td class="level1 suml1">{(totalDebt * -1M).ToString(AccountingFormat)}</td>
                        </tr>
                """);

            return output.ToString();

        }       
        private string GetAreaChartFunction(AreaChart c)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine($"      function {c.JavascriptFunctionName}() {"{"}");
            output.AppendLine("        var data = new google.visualization.DataTable();");
            output.AppendLine("        data.addColumn('string', 'Month');");
            foreach (var cat in c.Categories)
            {
                var sanitized = cat.Replace("'", "\\'");
                output.AppendLine($"        data.addColumn('number', '{sanitized}');");
            }
            output.AppendLine("        data.addRows([");



            // iterate through months and calculate the total wealth by category
            foreach (var month in _months)
            {
                List<string> values = new List<string>();
                foreach(var cat in c.Categories)
                {
                    var sum = _wealthPositions
                        .Where(p => p.MonthAbbreviation == month.MonthAbbreviation
                            && c.FuncCatMatch is not null && c.FuncCatMatch(p, cat))
                        .Sum(x => x.ValueAtTime);                        

                    var value = Math.Round(sum, 0).ToString(CultureInfo.CurrentCulture);
                    values.Add(value.ToString());
                }
                output.AppendLine($"          ['{month.MonthAbbreviation}', {String.Join(",", values)}],");

            }

            output.AppendLine("        ]);");
            output.AppendLine("");
            output.AppendLine($"        var options = {"{"}title:'{c.Title}',");
            output.AppendLine("                       hAxis:{title: 'Month',  titleTextStyle: {color: '#333'}},");
            output.AppendLine($"                       vAxis: {"{"}title: '{c.VAxisTitle}', minValue: 0{"}"}, ");
            output.AppendLine("                       isStacked:true,");
            output.AppendLine("                       width:1500,");
            output.AppendLine("                       height:500,");
            output.AppendLine("                       explorer: { actions: ['dragToZoom', 'rightClickToReset']	},");
            output.AppendLine("                       focusTarget: 'category'");
            output.AppendLine("                       };");
            output.AppendLine("");
            output.AppendLine($"        var chart = new google.visualization.AreaChart(document.getElementById('{c.JavascriptId}'));");
            output.AppendLine("        chart.draw(data, options);");
            output.AppendLine("      }");


            return output.ToString();
        }
        private string GetMonteCarloResultChartFunction(MonteCarloResultsChart c)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine($"      function {c.JavascriptFunctionName}() {"{"}");
            if (!c.IsBar)
            {
                output.AppendLine("        var data = new google.visualization.DataTable();");
                output.AppendLine("        data.addColumn('string', 'Month');");
                if (c.StatLinesAtTime is not null)
                    output.AppendLine($"        data.addColumn('number', '10 percentile');");
                if (c.StatLinesAtTime is not null)
                    output.AppendLine($"        data.addColumn('number', '25 percentile');");
                if (c.StatLinesAtTime is not null)
                    output.AppendLine($"        data.addColumn('number', '50 percentile');");
                if (c.StatLinesAtTime is not null)
                    output.AppendLine($"        data.addColumn('number', '75 percentile');");
                if (c.StatLinesAtTime is not null)
                    output.AppendLine($"        data.addColumn('number', '90 percentile');");
                if (c.BankruptcyRatesOverTime is not null)
                    output.AppendLine($"        data.addColumn('number', 'All');");
                output.AppendLine("        data.addRows([");
            }

            else
            {
                output.AppendLine("        var data = new google.visualization.arrayToDataTable([");
                output.AppendLine("          ['Year', '10 percentile', '25 percentile','50 percentile'," +
                                  "'75 percentile','90 percentile'],");
            }


            // iterate through months and calculate the total wealth by category
            if (c.StatLinesAtTime is not null)
            {
                foreach (var statLine in c.StatLinesAtTime)
                {
                    var dateText = statLine.Date.ToDateTimeUnspecified().ToString("MMM-yyyy");
                    if (c.IsBar) dateText = statLine.Date.ToDateTimeUnspecified().ToString("yyyy");
                    var percentile10 = Math.Round(statLine.Percentile10, 0).ToString();
                    var percentile25 = Math.Round(statLine.Percentile25, 0).ToString();
                    var percentile50 = Math.Round(statLine.Percentile50, 0).ToString();
                    var percentile75 = Math.Round(statLine.Percentile75, 0).ToString();
                    var percentile90 = Math.Round(statLine.Percentile90, 0).ToString();
                    output.AppendLine($"          ['{dateText}', {percentile10}, {percentile25}," +
                                      $" {percentile50}, {percentile75}, {percentile90}],");

                }
            }
            if (c.BankruptcyRatesOverTime is not null)
            {
                foreach (var statLine in c.BankruptcyRatesOverTime)
                {
                    var dateText = statLine.Date.ToDateTimeUnspecified().ToString("MMM-yyyy");
                    var rate = Math.Round(100 * statLine.BankruptcyRate, 0).ToString();
                    output.AppendLine($"          ['{dateText}', {rate}],");

                }
            }

            output.AppendLine("        ]);");
            output.AppendLine("");
            if (!c.IsBar)
            {
                output.AppendLine($"        var options = {"{"}title:'{c.Title}',");
                output.AppendLine("                       hAxis:{title: 'Month',  titleTextStyle: {color: '#333'}},");
                output.AppendLine($"                       vAxis: {"{"}title: '{c.VAxisTitle}', minValue: 0{"}"}, ");
                output.AppendLine("                       isStacked:false,");
                output.AppendLine("                       width:1500,");
                output.AppendLine("                       height:500,");
                output.AppendLine("                       explorer: { actions: ['dragToZoom', 'rightClickToReset']	},");
                output.AppendLine("                       focusTarget: 'category'");
                output.AppendLine("                       };");
                output.AppendLine("");
                output.AppendLine(
                        $"        var chart = new google.visualization.LineChart(document.getElementById('{c.JavascriptId}'));");
                output.AppendLine("        chart.draw(data, options);");
            }
            else
            {
                output.AppendLine("        var options = { width:1500, height:500, chart: { title:'" +
                                  c.Title +
                                  "' } };");
                output.AppendLine("        var chart = new google.charts.Bar(document.getElementById('" +
                                  c.JavascriptId +
                                  "'));");
                output.AppendLine("        chart.draw(data, google.charts.Bar.convertOptions(options));");
            }
            output.AppendLine("      }");


            return output.ToString();
        }
        private bool IsPositionStock(Position p)
        {
            if (p.FundType1 == "Stock") return true;
            if (p.FundType2 == "Stock") return true;
            if (p.FundType3 == "Stock") return true;
            if (p.FundType4 == "Stock") return true;
            if (p.FundType5 == "Stock") return true;
            return false;
        }
        private bool IsPositionIndex(Position p)
        {
            if (p.FundType1 == "Index") return true;
            if (p.FundType2 == "Index") return true;
            if (p.FundType3 == "Index") return true;
            if (p.FundType4 == "Index") return true;
            if (p.FundType5 == "Index") return true;
            return false;
        }

    }
}
