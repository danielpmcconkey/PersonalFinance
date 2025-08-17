using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using Lib.DataTypes;
using Lib;

namespace PersonalFinance
{
    internal class TreeBuilder
    {
        private List<PgCategory> _categories = [];
        private readonly List<PgCategory> _categoryTree = [];
        private List<PgTransaction> _transactions = [];
        private readonly StringBuilder _output = new StringBuilder();
        private decimal _globalSize = 0;
        private List<(decimal, decimal, int)> _colorRanges = [];
        private const string OutputPath = @"C:\Users\Dan\Documents\household budget queries\2023_household_spend_sankey.html";

        internal void Build()
        {
            var context = new PgContext();

            //_categories = PostgresDAL.GetCategories();
            _categories = context.PgCategories
                .Where(x => x.ShowInReport)
                .OrderBy(x => x.OrdinalWithinParent)
                .ToList();
            //_transactions = PostgresDAL.GetTransactions();
            _transactions = context.PgTransactions.ToList();

            var topLevelCats = _categories
                .Where(c => string.IsNullOrEmpty(c.ParentId));
            foreach (var cat in topLevelCats)
            {
                _categoryTree.Add(cat);
                AddChildrenAndTransToCat(cat);
                _globalSize = Math.Max(Math.Abs(cat.TransactionTotal), _globalSize);
            }

            AddHeaderToOutput();
            foreach (var cat in _categoryTree)
            {
                AddCatToOutput(cat, null);
            }
            AddFooterToOutput();

            var finalOutput = _output.ToString();

            System.IO.File.WriteAllText(OutputPath, finalOutput);
        }
        internal void AddCatToOutput(PgCategory cat, PgCategory? parent)
        {
            string format = "C2";
            int amplitude = NormalizeValue(cat.TransactionTotal);
            int color = NormalizeColor(cat.TransactionTotal);



            decimal displayTotal = Math.Abs(cat.TransactionTotal);
            var catLabel = $"{cat.DisplayName}: {displayTotal.ToString(format)}";
            var parentLabel = "Total Finances";

            if (parent != null)
            {
                decimal displayTotalParent = Math.Abs(parent.TransactionTotal);
                parentLabel = $"{parent.DisplayName}: {displayTotalParent.ToString(format)}";
            }

            _output.AppendLine($"['{catLabel}','{parentLabel}',{amplitude},{color}],");

            AddTransactionsToOutput(cat, catLabel);

            if (cat.ChildCategories != null)
            {
                foreach (var child in cat.ChildCategories)
                {
                    AddCatToOutput(child, cat);
                }
            }
        }
        internal void AddChildrenAndTransToCat(PgCategory cat)
        {
            // any transactions?
            var cat_transactions = _transactions
                .Where(t => t.CategoryId == cat.Id);
            if (cat_transactions.Any())
            {
                cat.Transactions = cat_transactions.ToList();
                cat.TransactionTotal += cat_transactions.Sum(x => x.Amount);
            }

            // any children?
            var cat_children = _categories.Where(c => c.ParentId == cat.Id);
            if (cat_children.Any())
            {
                foreach (var child in cat_children)
                {
                    if (cat.ChildCategories == null)
                        cat.ChildCategories = new List<PgCategory>();
                    cat.ChildCategories.Add(child);
                    AddChildrenAndTransToCat(child);
                    cat.TransactionTotal += child.TransactionTotal;
                }
            }
        }
        internal void AddFooterToOutput()
        {
            _output.AppendLine("		]);");
            _output.AppendLine("        tree = new google.visualization.TreeMap(document.getElementById('chart_div'));");
            _output.AppendLine("        tree.draw(data, {");
            _output.AppendLine("          minColor: '#009688',");
            _output.AppendLine("          midColor: '#f7f7f7',");
            _output.AppendLine("          maxColor: '#ee8100',");
            _output.AppendLine("          headerHeight: 15,");
            _output.AppendLine("          fontColor: 'black',");
            _output.AppendLine("         showScale: true");
            _output.AppendLine("       });");
            _output.AppendLine("     }");
            _output.AppendLine("    </script>");
            _output.AppendLine("  </head>");
            _output.AppendLine("  <body>");
            _output.AppendLine("    <div id=\"chart_div\" style=\"width: 1900px; height: 900px;\"></div>");
            _output.AppendLine("  </body>");
            _output.AppendLine("</html>");
        }
        internal void AddHeaderToOutput()
        {
            _output.AppendLine("<html>");
            _output.AppendLine("<head>");
            _output.AppendLine("<script type=\"text/javascript\" src=\"https://www.gstatic.com/charts/loader.js\"></script>");
            _output.AppendLine("<script type=\"text/javascript\">");
            _output.AppendLine("google.charts.load('current', { 'packages':['treemap']});");
            _output.AppendLine("google.charts.setOnLoadCallback(drawChart);");
            _output.AppendLine("function drawChart() {");
            _output.AppendLine("var data = google.visualization.arrayToDataTable([");
            _output.AppendLine("['Label', 'Parent', 'Amount (size)', 'Amount ratio (color)'],");
            _output.AppendLine($"['Total Finances', null, 200, 200],");
        }
        internal void AddTransactionsToOutput(PgCategory cat, string parentLabel)
        {
            if (cat.Transactions == null || cat.Transactions.Count == 0) return;
            var groupByDescription =
                from t in cat.Transactions
                group t by t.Description into newGroup
                orderby newGroup.Key
                select newGroup;
            if (groupByDescription.Count() == 1) return; // no sense

            foreach (var g in groupByDescription)
            {
                string name = g.Key.Replace("'","*");
                decimal amount = g.Sum(t => t.Amount);
                string label = $"{name}: {amount.ToString("C2")}";
                int amplitude = NormalizeValue(amount);
                int color = NormalizeColor(amount);
                _output.AppendLine($"['{label}','{parentLabel}',{amplitude},{color}],");
            }
        }
        internal int NormalizeColor(decimal x)
        {
            if (x == 0) return 0;
            if (_colorRanges == null)
            {
                _colorRanges = new List<(decimal, decimal, int)>();
                decimal lastValue = _globalSize;
                for (int i = 10; i >= 0; i--)
                {
                    decimal current = lastValue;
                    decimal half = (i == 0) ? 0M: lastValue / 2;
                    _colorRanges.Add((current, half, i));
                    lastValue = half;
                }
                lastValue = _globalSize * -1;
                for (int i = -10; i <= 0; i++)
                {
                    decimal current = lastValue;
                    decimal half = (i == 0) ? 0M : lastValue / 2;
                    _colorRanges.Add((half, current, i));
                    lastValue = half;
                }
            }

            int colorValue = _colorRanges.Where(y => x <= y.Item1 && x > y.Item2).FirstOrDefault().Item3;
            return colorValue;
        }
        internal int NormalizeValue(decimal x)
        {
            int min = 5;
            int max = 100;

            decimal percentOfTotal = x / _globalSize;
            percentOfTotal = Math.Abs(percentOfTotal);

            int outVal = (int)Math.Round(percentOfTotal * 100, 0);
            if (outVal < min) return min;
            if (outVal > max) return max;
            return outVal;
        }
    }
}
