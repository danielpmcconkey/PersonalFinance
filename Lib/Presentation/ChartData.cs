using System.Collections.Generic;
using System.Linq;
using Lib.DataTypes.Postgres;
using Lib.DataTypes.Presentation;
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
                    results.Add(new PresInvestmentPosition()
                    {
                        AccountGroupName = ag.Name,
                        AccountName = a.Name,
                        MonthEnd = monthEnd,
                        Symbol = symbol,
                        Value = value,
                        IsCurrent = isCurrent,
                        FundType1 = fund.FundType1?.Name,
                        FundType2 = fund.FundType2?.Name,
                        FundType3 = fund.FundType3?.Name,
                        FundType4 = fund.FundType4?.Name,
                        FundType5 = fund.FundType5?.Name,
                        TaxBucketName = a.TaxBucket?.Name ?? string.Empty,
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
    public static List<(LocalDate MonthEnd, string Symbol, decimal Value, bool IsCurrent)> GetEndOfMonthPositionsBySymbol(
        PgInvestmentAccount account)
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
                $"{g.Key.Month.ToString().Substring(0, 3)}, {g.Key.Year}",
                g.Key))
            .ToList();
    }
}