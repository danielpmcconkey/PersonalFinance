using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
using Lib.DataTypes.Presentation;
using Lib.StaticConfig;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NodaTime;

namespace Lib.Presentation;

public static class ChartData
{
    public static List<PresInvestmentPosition> GetEndOfMonthPositionsBySymbolAndAccount(
        List<PgInvestmentAccountGroup> accountGroups)
    {
        List<PresInvestmentPosition> results = [];
        Dictionary<string, PgFund> funds = [];
        foreach (var ag in accountGroups)
        {
            foreach (var a in ag.InvestmentAccounts)
            {
                var positions = GetEndOfMonthPositionsBySymbol(a);
                foreach (var (monthEnd, symbol, value, isCurrent) in positions)
                {
                    funds.TryGetValue(symbol, out var fund);
                    if (fund is null)
                    {
                        fund = accountGroups
                            .SelectMany(x => x.InvestmentAccounts)
                            .SelectMany(x =>x.Positions)
                            .First(x => x.Symbol == symbol).Fund;
                        funds.Add(symbol, fund);
                    }

                    var taxBucket = string.Empty;
                    if(a.TaxBucket != null) taxBucket = a.TaxBucket.Name;
                    
                    results.Add(new PresInvestmentPosition()
                    {
                        AccountGroupName = ag.Name,
                        AccountName = a.Name,
                        MonthEnd = monthEnd,
                        Symbol = symbol,
                        Value = value,
                        IsCurrent = isCurrent,
                        InvestmentType = fund.InvestmentType.Name,
                        Size = fund.Size.Name,
                        IndexOrIndividual = fund.IndexOrIndividual.Name,
                        Sector = fund.Sector.Name,
                        Region = fund.Region.Name,
                        Objective = fund.Objective.Name,
                        TaxBucketName = taxBucket,
                    });
                }
            }
        }
        return results;
    }

    /// <summary>
    /// This method was written by Claude.
    /// For the given investment account, returns a list of (MonthEnd, Symbol, Value) tuples where:
    ///     MonthEnd is the last day of each month between the earliest and latest PositionDate in the account.
    ///     For each symbol and month, the Value is taken from the most recent position whose PositionDate is less than
    ///         or equal to MonthEnd.
    ///     Values are carried forward until a new position appears for that symbol.
    ///     Symbols without any position on or before MonthEnd are skipped for that month.
    /// The result is ordered by MonthEnd ascending, then Symbol ascending.
    /// </summary>
    public static List<(LocalDate MonthEnd, string Symbol, decimal Value, bool IsCurrent)> 
        GetEndOfMonthPositionsBySymbol(PgInvestmentAccount account)
    {
        var results = new List<(LocalDate MonthEnd, string Symbol, decimal Value, bool IsCurrent)>();
        if (account?.Positions == null || account.Positions.Count == 0)
            return results;

        // Project to what we need. Keep full LocalDateTime for stable "latest within the day" ordering,
        // but compare by Date when checking <= MonthEnd.
        var positions = account.Positions
            .Where(p => !string.IsNullOrWhiteSpace(p.Symbol))
            .Select(p => new
            {
                DateTime = p.PositionDate,       // LocalDateTime
                Date = p.PositionDate.Date,      // LocalDate (for month-end comparison)
                p.Symbol,
                Value = p.CurrentValue
            })
            .ToList();

        if (positions.Count == 0)
            return results;

        var minDate = positions.Min(p => p.Date);
        var maxDate = positions.Max(p => p.Date);

        // Generate all month-end dates in the observed range.
        var monthEnds = new List<LocalDate>();
        var monthCursor = new LocalDate(minDate.Year, minDate.Month, 1);
        var lastMonthStart = new LocalDate(maxDate.Year, maxDate.Month, 1);
        while (monthCursor <= lastMonthStart)
        {
            monthEnds.Add(monthCursor.With(DateAdjusters.EndOfMonth));
            monthCursor = monthCursor.PlusMonths(1);
        }

        // Group positions by symbol and sort by full timestamp ascending
        // so that the most recent entry up to each month-end is picked.
        var bySymbol = positions
            .GroupBy(p => p.Symbol!)
            .ToDictionary(
                g => g.Key!,
                g => g.OrderBy(p => p.DateTime).ToList()
            );

        foreach (var (symbol, list) in bySymbol)
        {
            int idx = 0;
            decimal? currentValue = null;
            // todo: make a UT that checks that iscurrent is being assigned correctly
            var isCurrent = list.OrderByDescending(x => x.DateTime).First().Value > 0 ? true : false;
            
            foreach (var monthEnd in monthEnds)
            {
                // Advance through positions for this symbol up to the month-end date.
                while (idx < list.Count && list[idx].Date <= monthEnd)
                {
                    currentValue = list[idx].Value;
                    idx++;
                }

                if (currentValue.HasValue)
                {
                    results.Add((monthEnd, symbol, currentValue.Value, isCurrent));
                }
            }
        }

        results.Sort((a, b) =>
        {
            var cmp = a.MonthEnd.CompareTo(b.MonthEnd);
            return cmp != 0 ? cmp : string.CompareOrdinal(a.Symbol, b.Symbol);
        });

        return results;
    }

    public static List<(string MonthAbbreviation, LocalDate MonthEnd)> GetInvestmentMonthEnds(
        List<PresInvestmentPosition> positions)
    {
        return positions
            .GroupBy(p => p.MonthEnd)
            .Select(g => (
                $"{PresentationConfig.MothAbbreviations[g.Key.Month]}, {g.Key.Year}",
                g.Key))
            .ToList();
    }

    public static List<PresChart> BuildCharts(List<(string MonthAbbreviation, LocalDate MonthEnd)> allMonths,
        List<PresInvestmentPosition> monthEndInvestmentPositions, SingleModelRunResult mcResults)
    {
        var charts = new List<PresChart>();
        charts.Add(new PresChart()
        {
            Ordinal = 10,
            ChartDataGrid = BuildPositionsDataGrid(allMonths, monthEndInvestmentPositions, x => x.TaxBucketName),
            Description = "The \"Assets by tax types\" chart above shows future tax implications for investment assets " +
                          "and how they've grown over time. Whether we will have to pay tax on the total amount, just" +
                          " the growth, or none of it.",
            Title = "Assets by tax types",
            VAxisTitle = "USD",
            PresChartType = PresChartType.Area
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 20,
            ChartDataGrid = BuildPositionsDataGrid(allMonths, monthEndInvestmentPositions,
                x => x.Symbol, true),
            Title = "Assets by investment vehicle",
            VAxisTitle = "USD",
            Description = "The \"Assets by investment vehicle\" chart shows " +
                          "how our total wealth is divided into different forms of " +
                          "investment (stock, bonds, real estate). Mortgage debt is " +
                          "factored into home equity. Other debt is not included. Note, " +
                          "the VanXXX represents an institutional fund that I couldn't " +
                          "find the ticker symbol for.",
            PresChartType = PresChartType.Area
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 30,
            ChartDataGrid = BuildCurrentWealthByTypeDataGrid(monthEndInvestmentPositions, 
                x => x.AccountGroupName),
            Title = "Assets by account group",
            VAxisTitle = "USD",
            Description = "The \"Assets by account group\" chart above shows our total wealth by account families." +
                          " Different account families can contain multiple specific accounts.",
            PresChartType = PresChartType.Pie
        });
        
        charts.Add(new PresChart()
            {
                Ordinal = 40,
                ChartDataGrid = BuildCurrentWealthByTypeDataGrid(monthEndInvestmentPositions, 
                    x => x.AccountName),
                Title = "Assets by account",
                VAxisTitle = "USD",
                Description = "The \"Assets by account\" chart above is a more fine-grained version of the \"Assets by " +
                              "account group\" chart, showing all the individual accounts that roll up into the account " +
                              "families.",
                PresChartType = PresChartType.Pie
            });

        charts.Add(new PresChart()
        {
            Ordinal = 50,
            ChartDataGrid = BuildCurrentWealthByTypeDataGrid(monthEndInvestmentPositions,
                x => x.InvestmentType),
            Title = "Assets by core investment types",
            VAxisTitle = "USD",
            Description = "The \"Assets by core investment type\" chart above shows the split between equities, bonds, " +
                          "real estate, target date funds, etc.",
            PresChartType = PresChartType.Pie
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 60,
            ChartDataGrid = BuildCurrentWealthByTypeDataGrid(monthEndInvestmentPositions,
                x => x.Size),
            Title = "Assets by market capitalization size",
            VAxisTitle = "USD",
            Description = "The \"Cap size type\" chart above shows the split between small cap, mid cap and large cap " +
                          "funds that a particular investment either represents (individual stocks) or targets index " +
                          "funds.",
            PresChartType = PresChartType.Pie
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 70,
            ChartDataGrid = BuildCurrentWealthByTypeDataGrid(monthEndInvestmentPositions,
                x => x.IndexOrIndividual),
            Title = "Assets by type (indexes or individual stocks)",
            VAxisTitle = "USD",
            Description = "The \"Assets by type (indexes or individual stocks)\" chart above shows the split between " +
                          "individual stock purchases (buying N shares of Microsoft) versus purchases of stock " +
                          "indexes (mutual funds, ETFs, and other investment vehicles that buy a broad range of " +
                          "investments). N/A here represents wealth that is not held in the stock market (such as " +
                          "real estate, bonds, etc.)",
            PresChartType = PresChartType.Pie
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 80,
            ChartDataGrid = BuildCurrentWealthByTypeDataGrid(monthEndInvestmentPositions,
                x => x.Sector),
            Title = "Assets by sector",
            VAxisTitle = "USD",
            Description = "The \"Assets by sector\" chart above shows the which industry the underlying investment is " +
                          "in or, in the case of index funds, if there's a specific industry the fund is targeting. ",
            PresChartType = PresChartType.Pie
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 90,
            ChartDataGrid = BuildCurrentWealthByTypeDataGrid(monthEndInvestmentPositions,
                x => x.Region),
            Title = "Assets by region",
            VAxisTitle = "USD",
            Description = "The \"Assets by region\" chart above shows the which geographic region the underlying " +
                          "investment is in or, in the case of index funds, if there's a specific region the fund is " +
                          "targeting. ",
            PresChartType = PresChartType.Pie
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 100,
            ChartDataGrid = BuildCurrentWealthByTypeDataGrid(monthEndInvestmentPositions,
                x => x.Objective),
            Title = "Assets by objective",
            VAxisTitle = "USD",
            Description = "The \"Assets by objective\" chart above shows what I hope to get out of the investment.",
            PresChartType = PresChartType.Pie
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 110,
            ChartDataGrid = BuildMcDataGrid(mcResults.NetWorthStatsOverTime),
            Description = "Measures your projected total net worth over time in the Monte Carlo simulation",
            Title = "Monte Carlo net worth over time",
            VAxisTitle = "Total net worth (USD)",
            PresChartType = PresChartType.Line
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 120,
            //ChartDataGrid = BuildMcDataGrid(mcResults.FunPointsByYear),
            ChartDataGrid = BuildMcDataGrid(mcResults.TotalFunPointsOverTime),
            Description = "It's the funzies, y'all. Get funzies when you spend money you don't have to spend. " +
                          "Lose funzies when you are anxious about your money or when you have to work to " +
                          "support your lifestyle.",
            Title = "Monte Carlo fun points over time",
            VAxisTitle = "Fun Points!",
            PresChartType = PresChartType.Line
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 130,
            ChartDataGrid = BuildMcDataGrid(mcResults.FunPointsByYear),
            Description = "This shows fun point, but specifically how many you earn / lose each year in the Monte Carlo " +
                          "simulations",
            Title = "Monte Carlo fun points year over year",
            VAxisTitle = "Fun Points!",
            PresChartType = PresChartType.Bar
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 140,
            ChartDataGrid = BuildMcDataGrid(mcResults.TotalSpendOverTime),
            Description = "Measures your projected total spend over time. Spend includes money you had to " +
                          "spend, like groceries, mortgages, etc. It also includes any fun spending. " +
                          "It does not include money you spent to buy investment positions.",
            Title = "Monte Carlo spend by Year",
            VAxisTitle = "Total spend (USD)",
            PresChartType = PresChartType.Line
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 150,
            ChartDataGrid = BuildMcDataGrid(mcResults.SpendByYear),
            Description = "The amount of money you spent (required and fun) each year",
            Title = "Monte Carlo spend year over year",
            VAxisTitle = "Money spent",
            PresChartType = PresChartType.Bar
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 160,
            ChartDataGrid = BuildMcDataGrid(mcResults.TotalTaxOverTime),
            Description = "Measures your total lifetime tax payment (starting at $0 at the simulation start date)",
            Title = "Monte Carlo Tax",
            VAxisTitle = "Total tax payment (USD)",
            PresChartType = PresChartType.Line
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 170,
            ChartDataGrid = BuildMcDataGrid(mcResults.TaxByYear),
            Title = "Monte Carlo tax by Year",
            VAxisTitle = "Tax paid",
            Description = "The amount of money you paid in tax each year",
            PresChartType = PresChartType.Bar
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 180,
            ChartDataGrid = BuildMcDataGrid(mcResults.HealthSpendByYear),
            Title = "Monte Carlo health spend by year",
            VAxisTitle = "Health spend",
            Description = "The amount of money you paid in health care each year",
            PresChartType = PresChartType.Bar
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 190,
            ChartDataGrid = BuildMcDataGrid(mcResults.IraDistributionsByYear),
            Title = "Monte Carlo IRA distributions by Year",
            VAxisTitle = "IRA distributions",
            Description = "The amount of money you pulled from tax-deferred accounts each year",
            PresChartType = PresChartType.Bar
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 200,
            ChartDataGrid = BuildMcDataGrid(mcResults.CapitalGainsByYear),
            Title = "Monte Carlo capital gains by Year",
            VAxisTitle = "Capital gains",
            Description = "The amount of money you received as capital gains each year",
            PresChartType = PresChartType.Bar
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 210,
            ChartDataGrid = BuildMcDataGrid(mcResults.TaxFreeWithdrawalsByYear),
            Title = "Monte Carlo tax free withdrawals by Year",
            VAxisTitle = "Tax free withdrawals",
            Description = "The amount of money you pulled from tax-free accounts each year",
            PresChartType = PresChartType.Bar
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 220,
            ChartDataGrid = BuildMcDataGrid(mcResults.FunSpendByYear),
            Title = "Monte Carlo fun spend by Year",
            VAxisTitle = "Fun spend",
            Description = "The amount of money you spent to have fun each year",
            PresChartType = PresChartType.Bar
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 230,
            ChartDataGrid = BuildMcDataGrid(mcResults.NotFunSpendByYear),
            Title = "Monte Carlo required spend by Year",
            VAxisTitle = "Required spend",
            Description = "The amount of money you spent each year that you had no choice over. Includes bills and" +
                          " health care costs, but does not include debt payback",
            PresChartType = PresChartType.Bar
        });
        
        charts.Add(new PresChart()
        {
            Ordinal = 240,
            ChartDataGrid = BuildMcBankruptcyDataGrid(mcResults.BankruptcyRateOverTime),
            Title = "Monte Carlo Bankruptcy Rates",
            VAxisTitle = "Bankruptcy percentage",
            Description = "The percentage of simulated lives in bankruptcy at each point in time in the simulator",
            PresChartType = PresChartType.Line
        });
        
        
        
        
        return charts;
    }

    private static ChartDataGrid BuildMcDataGrid(SingleModelRunResultStatLineAtTime[] data)
    {
        var columnNames = new string[]
        {
            "Month",
            "10 percentile",
            "25 percentile",
            "50 percentile",
            "75 percentile",
            "90 percentile"
        };
        string[][] rows = new string[data.Length][];
        for (var i = 0; i < data.Length; i++)
        {
            rows[i] = [
                data[i].Date.Year.ToString(),
                Math.Round(data[i].Percentile10, 0).ToString(CultureInfo.CurrentCulture),
                Math.Round(data[i].Percentile25, 0).ToString(CultureInfo.CurrentCulture),
                Math.Round(data[i].Percentile50, 0).ToString(CultureInfo.CurrentCulture),
                Math.Round(data[i].Percentile75, 0).ToString(CultureInfo.CurrentCulture),
                Math.Round(data[i].Percentile90, 0).ToString(CultureInfo.CurrentCulture)
            ];
        }

        var grid = new ChartDataGrid()
        {
            ColumnNames = columnNames,
            Data = rows.ToArray()
        };
        return grid;
    }
    
    private static ChartDataGrid BuildMcBankruptcyDataGrid(SingleModelRunResultBankruptcyRateAtTime[] data)
    {
        var columnNames = new[]
        {
            "Month", "All",
        };
        string[][] rows = new string[data.Length][];
        for (var i = 0; i < data.Length; i++)
        {
            var thisMonth = data[i].Date;
            rows[i] = [
                $"{PresentationConfig.MothAbbreviations[thisMonth.Month]}, {thisMonth.Year.ToString()}",
                Math.Round(100 * data[i].BankruptcyRate, 0).ToString(CultureInfo.CurrentCulture),
            ];
        }

        var grid = new ChartDataGrid()
        {
            ColumnNames = columnNames,
            Data = rows.ToArray()
        };
        return grid;
    }
    private static ChartDataGrid BuildPositionsDataGrid(
        List<(string MonthAbbreviation, LocalDate MonthEnd)> allMonths,
        List<PresInvestmentPosition> monthEndInvestmentPositions,
        Func<PresInvestmentPosition, string> categorySelector,
        bool groupNonCurrent = false)
    {
        const string nonCurrentLabel = "No longer owned";
        var categories = monthEndInvestmentPositions
            .Where(x => groupNonCurrent == false || x.IsCurrent)
            .Select(categorySelector)
            .Distinct()
            .ToList();
        if (groupNonCurrent) categories.Add(nonCurrentLabel);
        
        var columnNames = new string[categories.Count + 1];
        columnNames[0] = "Month";
        for (var i = 0; i < categories.Count; i++)
        {
            var catName = categories[i].ToString()?.Replace('\'', '\\') ?? string.Empty;
            columnNames[i + 1] = catName;
        }

        var rows = new List<string[]>();
        foreach (var month in allMonths)
        {
            var row = new string[columnNames.Length];
            row[0] = month.MonthAbbreviation;
            for (int i = 0; i < categories.Count; i++)
            {
                var category = categories[i];
                var sumAtCat = 0m;
                if (groupNonCurrent == false || !category.Equals(nonCurrentLabel))
                {
                    sumAtCat = monthEndInvestmentPositions
                        .Where(x =>
                            categorySelector(x)?.Equals(category) == true && x.MonthEnd == month.MonthEnd)
                        .Sum(x => x.Value);
                }
                else
                {
                    sumAtCat = monthEndInvestmentPositions
                        .Where(x =>
                            !x.IsCurrent && x.MonthEnd == month.MonthEnd)
                        .Sum(x => x.Value);
                }

                row[i + 1] = Math.Round(sumAtCat, 0).ToString(CultureInfo.CurrentCulture);
            }
            rows.Add(row);
        }

        return new ChartDataGrid
        {
            ColumnNames = columnNames,
            Data = rows.ToArray()
        };
    }
    
    private static ChartDataGrid BuildCurrentWealthByTypeDataGrid(
        List<PresInvestmentPosition> monthEndInvestmentPositions,
        Func<PresInvestmentPosition, string> categorySelector)
    {
        var currentPositions = monthEndInvestmentPositions
            .GroupBy(p => new { p.AccountGroupName, p.AccountName, p.Symbol})
            .Select(x => (
                    x.Key,
                    x.OrderByDescending(y => y.MonthEnd).First()
                )
            )
            .Where(x => x.Item2.Value > 0)
            .Select(x=> x.Item2)
            .ToList();
        
        var categories = currentPositions
            .Select(categorySelector)
            .Distinct()
            .ToList();
        
        string[] columnNames = ["Fund type", "Total wealth at this type"];
        string[][] rows = new string[categories.Count][];
       
        for (var i = 0; i < categories.Count; i++)
        {
            var category = categories[i];
            
            var sumAtCat = currentPositions
                .Where(x =>
                    categorySelector(x)?.Equals(category) == true)
                .Sum(x => x.Value);
            rows[i] = [
                category.Replace('\'', '\\'), 
                Math.Round(sumAtCat,0).ToString(CultureInfo.CurrentCulture)];
        }

        return new ChartDataGrid
        {
            ColumnNames = columnNames,
            Data = rows.ToArray()
        };
    }
}