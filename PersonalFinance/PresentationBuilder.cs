﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lib;
using Lib.DataTypes;

namespace PersonalFinance
{
    internal class PresentationBuilder
    {
        internal string _outputDir = "/media/dan/fdrive/codeprojects/PersonalFinance/OutputFiles/";

        List<Position>? _wealthPositions;
        List<Position>? _cashPositions;
        List<Position>? _debtPositions;
        List<BudgetPosition>? _budgetPositions;
        List<PgCategory> _budgetCategories = new List<PgCategory>();

        List<Position>? _currentWealthPositions;
        List<Position>? _currentCashPositions;
        List<Position>? _currentDebtPositions;

        List<(string? MonthAbbreviation, DateTime PositionDate)>? _months;
        List<string?>? _taxBuckets;
        List<string?>? _accounts;
        List<string?>? _accountGroups;
        List<string?>? _stockTypeIndividualVsIndex;
        List<string?>? _symbols;

        List<AreaChart>? _areaCharts;

        private DateTime? _effectiveDate;

        const string accountingFormat = "#,##0.00;(#,##0.00);--";

        internal void BuildPresentation()
        {
            PullAndPopulateData();
            DefineAreaCharts();

            PresentationHtml pHtml = new PresentationHtml(this);
            
            string html = pHtml.GetHTML();
            string timestamp = $".{DateTime.Now.ToString("yyyy.MM.dd.HH.mm.ss")}";
            //timestamp = "";
            string fullOutputPath = $"{_outputDir}PersonalFinanceBreakdown{timestamp}.html";
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
            if (_effectiveDate == null)
                _effectiveDate = _wealthPositions.Max(x => x.PositionDate);
            return (DateTime)_effectiveDate;
        }
        public string GetChartsHead()
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("<script type=\"text/javascript\" src=\"https://www.gstatic.com/charts/loader.js\"></script>");
            output.AppendLine("<script type=\"text/javascript\">");
            output.AppendLine("google.charts.load('current', { 'packages':['corechart']});");
            // draw the charts when the page loads
            foreach (var c in _areaCharts.OrderBy(x => x.Ordinal))
            {
                output.AppendLine($"google.charts.setOnLoadCallback({c.JavascriptFunctionName});");
            }
            // populate the chart data
            foreach (var c in _areaCharts.OrderBy(x => x.Ordinal))
            {
                output.AppendLine(GetAreaChartFunction(c));
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
            List<string> output = new List<string>();
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
                                        descendants.Contains(bp.CategoryId) ||
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
                                where  descendants.Contains(bp.CategoryId) || bp.CategoryId == catId
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
            foreach (var c_prime in catsSansParents)
            {
                var getCellsResult = GetCellsForCat(c_prime.Id, currentRow, 0, months);
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
                    var cell = cells.Where(x => x.Row == rowNum && x.Column == currentColumn).FirstOrDefault();
                    string label = cell.Label;
                    if (currentColumn == 0)
                    {
                        tableBody.AppendLine($"<th class=\"{cell.CssClass}\">{cell.Label}</th>");
                    }
                    if (currentColumn > 0 && currentColumn < months.Count + 1)
                    {
                        tableBody.AppendLine($"<td class=\"{cell.CssClass}\">{cell.Value.ToString(accountingFormat)}</td>");
                    }
                    if (currentColumn == months.Count + 1)
                    {
                        tableBody.AppendLine($"<td class=\"{cell.CssClass}\">{cell.Value.ToString(accountingFormat)}</td>");
                    }
                }
                tableBody.AppendLine("</tr>");
            }

            //for (int currentRow = 0; currentRow <= maxRow; currentRow++)
            //{
            //    tableBody.AppendLine("<tr>");
            //    for (int currentColumn = 0; currentColumn <= months.Count; currentColumn++)
            //    {
            //        var cell = cells.Where(x => x.row == currentRow && x.column == currentColumn).FirstOrDefault();
            //        tableBody.AppendLine(cell.value);
            //    }
            //    tableBody.AppendLine("</tr>");
            //}


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
                            <td class="level0 suml0">$&nbsp;&nbsp;&nbsp;&nbsp;{totalNetWorth.ToString(accountingFormat)}</td>
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
            return output.ToString();
        }
        private void PullAndPopulateData()
        {
            // populate from and to dates  to pull budget data
            var firstDayThisMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var to = firstDayThisMonth.AddSeconds(-1);
            var from = firstDayThisMonth.AddMonths(-12);
            
            var context = new PgContext();
            _wealthPositions = PostgresDAL.GetWealthPositions();
            _cashPositions = PostgresDAL.GetCashPositions();
            _debtPositions = PostgresDAL.GetDebtPositions();
            _budgetPositions = PostgresDAL.GetBudgetPositions(from, to);
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
            _symbols = _wealthPositions.Select(p => p.Symbol).Distinct().ToList();

            _stockTypeIndividualVsIndex = new List<string?>();
            _stockTypeIndividualVsIndex.Add("Individual stock");
            _stockTypeIndividualVsIndex.Add("Index");
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
                (p.Symbol == cat);

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
                Func_CatMatch = taxBucketCatMatch,
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
                Func_CatMatch = accountCatMatch,
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
                Func_CatMatch = accountGroupCatMatch,
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
                Func_CatMatch = indexVsIndividualCatMatch,
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
                Func_CatMatch = symbolCatMatch,
                Categories = _symbols
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
                wealthGroupsRows.AppendLine($"<td class=\"level2\">{totalValue.ToString(accountingFormat)}</td>");
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
                            <td class="level1 suml1">{totalWealthPositions.ToString(accountingFormat)}</td>
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
                rows.AppendLine($"<td class=\"level2\">{r.ValueAtTime.ToString(accountingFormat)}</td>");
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
                            <td class="level1 suml1">{totalCash.ToString(accountingFormat)}</td>
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
                            <td class="level2">{totalProperty.ToString(accountingFormat)}</td>
                        </tr>
                        <tr>
                            <th class="level1 suml1">Total property:</th>
                            <td class="level1 suml1">{totalProperty.ToString(accountingFormat)}</td>
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
                rows.AppendLine($"<td class=\"level2\">{(r.ValueAtTime * -1.0M).ToString(accountingFormat)}</td>");
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
                            <td class="level1 suml1">{(totalDebt * -1M).ToString(accountingFormat)}</td>
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
                            && c.Func_CatMatch(p, cat))
                        .Sum(x => x.ValueAtTime);                        

                    var value = Math.Round(sum, 0).ToString();// c.Func_GetValueForMonthAndCategory(month.MonthAbbreviation, cat);
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
