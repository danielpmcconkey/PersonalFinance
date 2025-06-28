using Lib.DataTypes.MonteCarlo;
using Lib.Utils;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Account
{
    #region DB read functions

    public static long FetchDbCashTotalByPersonId(Guid personId)
    {
        // todo: change get cash method to actually use the person ID
        
        using var context = new PgContext();
        long currentCash = 0L;
        
        // get the max date by symbol
        var maxDateByAccount = (
            from p in context.PgCashPositions
            group p by p.CashAccountId
            into g
            select new { g.Key, maxdate = g.Max(x => x.PositionDate) }
        ).ToList();
        // get any positions at max date
        foreach (var maxDate in maxDateByAccount)
        {
            var positionAtMaxDate = context.PgCashPositions
                                        .Where(x => 
                                            x.PositionDate == maxDate.maxdate && 
                                            x.CashAccountId == maxDate.Key)
                                        .OrderByDescending(x => x.CurrentBalance)
                                        .FirstOrDefault()
                                    ??  throw new InvalidDataException();
            if (positionAtMaxDate.CurrentBalance >= 0)
            {
                currentCash += CurrencyConverter.ConvertFromCurrency(positionAtMaxDate.CurrentBalance);
            }
        }
        
        return currentCash;
    }
    
    public static List<McDebtAccount> FetchDbDebtAccountsByPersonId(Guid personId)
    {
        // todo: change this method to use the person Id in the DB pull
        
        List<McDebtAccount> accounts = [];
        using var context = new PgContext();
        var accountsPg = (
            context.PgDebtAccounts
            ?? throw new InvalidDataException()
        ).ToList();
        
        accounts.AddRange(
            from accountPg in accountsPg 
            let positionsPg = FetchDbOpenDebtPositionsByAccountId(
                accountPg.Id, 
                CurrencyConverter.ConvertFromCurrency(accountPg.AnnualPercentageRate),
                CurrencyConverter.ConvertFromCurrency(accountPg.MonthlyPayment))
            where positionsPg.Any() select new McDebtAccount()
            {
                Id = Guid.NewGuid(), Name = accountPg.Name, Positions = positionsPg,
            });

        return accounts;
    }
    
    public static List<McInvestmentAccount> FetchDbInvestmentAccountsByPersonId(Guid personId)
    {
        // todo: change this method to use the person Id in the DB pull
        
        List<McInvestmentAccount> accounts = [];
        using var context = new PgContext();
        var accountsPg = context.PgInvestmentAccounts
            ?? throw new InvalidDataException();
        accounts.AddRange(
            from accountPg in accountsPg
            let positionsPg = FetchDbOpenInvestmentPositionsByAccountId(accountPg.Id)
            where positionsPg.Any()
        select new McInvestmentAccount()
        {
            Id = Guid.NewGuid(), 
            Name = accountPg.Name, 
            AccountType = GetAccountType(
                accountPg.TaxBucketId,
                accountPg.InvestmentAccountGroupId),
            Positions = positionsPg,
        });
        // the Monte Carlo sim considers cash accounts as investment
        // positions with a type of CASH so add any cash here 
        var currentCash = FetchDbCashTotalByPersonId(personId);
        accounts.Add(new McInvestmentAccount()
        {
            Id = Guid.NewGuid(),
            Name = "Cash",
            AccountType = McInvestmentAccountType.CASH,
            Positions = [new McInvestmentPosition()
            {
                Id = Guid.NewGuid(),
                IsOpen  = true,
                Name = "cash",
                Entry = new LocalDateTime(2025, 2, 1, 0, 0), // entry date doesn't matter here
                InvestmentPositionType = McInvestmentPositionType.SHORT_TERM,
                InitialCost = currentCash,
                Quantity  = currentCash,
                Price  = 1L,
            }],
        });
        return accounts;
    }
    public static List<McDebtPosition> FetchDbOpenDebtPositionsByAccountId(int accountId, long apr, long payment)
    {
        List<McDebtPosition> positions = [];
        using var context = new PgContext();
        
        var latestPosition = context.PgDebtPositions
                                 .Where(x => x.DebtAccountId == accountId)
                                 .OrderByDescending(x => x.PositionDate)
                                 .FirstOrDefault()
                             ??  throw new InvalidDataException();
        if (latestPosition.CurrentBalance <= 0) return positions;
        
            
        positions.Add(new McDebtPosition()
        {
            Id = Guid.NewGuid(),
            IsOpen = true,
            Name = $"Position {latestPosition.CurrentBalance}",
            Entry = latestPosition.PositionDate,
            CurrentBalance = CurrencyConverter.ConvertFromCurrency(latestPosition.CurrentBalance),
            MonthlyPayment = payment,
            AnnualPercentageRate = apr
        });
            
        
        
        return positions;
    }
    public static List<McInvestmentPosition> FetchDbOpenInvestmentPositionsByAccountId(int accountId)
    {
        List<McInvestmentPosition> positions = [];
        using var context = new PgContext();
        
        // get the max date by symbol
        var maxDateBySymbol = (
                from p in context.PgPositions
                where p.InvestmentAccountId == accountId
                group p by p.Symbol
                into g
                select new { g.Key, maxdate = g.Max(x => x.PositionDate) })
            .ToList();
        foreach (var maxDate in maxDateBySymbol)
        {
            // order by totalQuantity and pick the first so that anytime we have a
            // positive quantity and a zero quantity, we'll know we picked the right
            // one (the zero means we closed it out)
            var positionAtMaxDate =
                context.PgPositions
                    .Where(x =>
                        x.PositionDate == maxDate.maxdate &&
                        x.InvestmentAccountId == accountId &&
                        x.Symbol == maxDate.Key)
                    .OrderBy(x => x.TotalQuantity)
                    .FirstOrDefault()
                ?? throw new InvalidDataException();
            if (positionAtMaxDate.TotalQuantity > 0)
            {
                positions.Add(new McInvestmentPosition()
                {
                    Id = Guid.NewGuid(),
                    IsOpen = true,
                    Name = $"Position {maxDate.Key}",
                    Entry = positionAtMaxDate.PositionDate,
                    InvestmentPositionType = GetInvestmentPositionType(maxDate.Key),
                    InitialCost = CurrencyConverter.ConvertFromCurrency(positionAtMaxDate.CostBasis),
                    Quantity = CurrencyConverter.ConvertFromCurrency(positionAtMaxDate.TotalQuantity),
                    Price = CurrencyConverter.ConvertFromCurrency(positionAtMaxDate.Price),
                });
            }
        }
        
        return positions;
    }

    #endregion DB read functions

    #region Calculation functions
    public static long CalculateCashBalance(BookOfAccounts accounts)
    {
        return accounts.Cash.Positions.Sum(x => {
            if (!x.IsOpen) return 0;
            var ip = (McInvestmentPosition)x;
            return ip.CurrentValue;
        });
    }
    public static long CalculateInvestmentAccountTotalValue(McInvestmentAccount account)
    {
        return account.Positions.Sum(x => {
            if (x.IsOpen && x is McInvestmentPosition)
            {
                var ip = x as McInvestmentPosition;
                if (ip is null) return 0;
                return ip.CurrentValue;
            }
            return 0;
        });
    }
    public static long CalculateLongBucketTotalBalance(BookOfAccounts accounts)
    {
        return CalculateTotalBalanceByBucketType(accounts, McInvestmentPositionType.LONG_TERM);
    }
    public static long CalculateMidBucketTotalBalance(BookOfAccounts accounts)
    {
        return CalculateTotalBalanceByBucketType(accounts, McInvestmentPositionType.MID_TERM);
    }
    public static long CalculateShortBucketTotalBalance(BookOfAccounts accounts)
    {
        return CalculateTotalBalanceByBucketType(accounts, McInvestmentPositionType.SHORT_TERM);
    }
    public static long CalculateNetWorth(BookOfAccounts accounts)
        {
            var totalAssets = 0L;
            var totalLiabilities = 0L;
            foreach (var account in accounts.InvestmentAccounts)
            {
                if (account.AccountType is not McInvestmentAccountType.PRIMARY_RESIDENCE)
                {
                    totalAssets += account.Positions.Where(x => x.IsOpen).Sum(x => {
                        McInvestmentPosition ip = (McInvestmentPosition)x;
                        return ip.CurrentValue;
                    });
                }
            }
            foreach (var account in accounts.DebtAccounts)
            {
                totalLiabilities += account.Positions.Where(x => x.IsOpen).Sum(x => {
                    McDebtPosition dp = (McDebtPosition)x;
                    return dp.CurrentBalance;
                });
            }
            return totalAssets - totalLiabilities;
        }
    public static long CalculateTotalBalanceByBucketType(BookOfAccounts bookOfAccounts, McInvestmentPositionType bucketType)
    {
        var totalBalance = 0L;
        var accounts = bookOfAccounts.InvestmentAccounts.Where(x => {
            if (x.AccountType == McInvestmentAccountType.PRIMARY_RESIDENCE) return false;
            if (x.AccountType == McInvestmentAccountType.CASH) return false;
            return true;
        });
        foreach (var account in accounts)
        {
            var totalValueBegin = CalculateInvestmentAccountTotalValue(account);
            totalBalance += account.Positions
                .Where(x => x.IsOpen && x is McInvestmentPosition)
                .Sum(x => {
                    var ip = x as McInvestmentPosition;
                    if (ip is null) return 0;
                    if (ip.InvestmentPositionType != bucketType) return 0;
                    return ip.CurrentValue;
                });
        }
        return totalBalance;
    }
    public static long CalculateDebtTotal(BookOfAccounts accounts)
    {
        long total = 0L;
        foreach (var a in accounts.DebtAccounts)
        {
            var positions = a.Positions
                .Where(x => x.IsOpen)
                .ToList();
            foreach (var p in positions)
            {
                total += p.CurrentBalance;
            }
        }
        return total;
    }
    

    #endregion Calculation functions
    
    
    
    
    public static BookOfAccounts CreateBookOfAccounts(
        List<McInvestmentAccount> investmentAccounts, List<McDebtAccount> debtAccounts)
    {
        Func<McInvestmentAccountType, McInvestmentAccount> getOrCreateAccount = (McInvestmentAccountType accountType) =>
        {
            var firstAccount = investmentAccounts
                .FirstOrDefault(x => x.AccountType == accountType);
            if (firstAccount is null)
            {
                McInvestmentAccount defaultAccount = new()
                {
                    Id = Guid.NewGuid(),
                    AccountType = accountType,
                    Name = $"default {accountType.ToString()} account",
                    Positions = []
                };
                investmentAccounts.Add(defaultAccount);
                return defaultAccount;
            }
            return firstAccount;
        };
        var book = new BookOfAccounts(
            getOrCreateAccount(McInvestmentAccountType.ROTH_401_K), 
            getOrCreateAccount(McInvestmentAccountType.ROTH_IRA),
            getOrCreateAccount(McInvestmentAccountType.TRADITIONAL_401_K),
            getOrCreateAccount(McInvestmentAccountType.TRADITIONAL_IRA),
            getOrCreateAccount(McInvestmentAccountType.TAXABLE_BROKERAGE),
            getOrCreateAccount(McInvestmentAccountType.HSA),
            getOrCreateAccount(McInvestmentAccountType.CASH),
            investmentAccounts,
            debtAccounts
            );
        return book;
    }
    
    
    
    
    public static McInvestmentPositionType GetInvestmentPositionType(string symbol)
    {
        // todo: change GetInvestmentPositionType to read type from the DB
        if (symbol == "SCHD") return McInvestmentPositionType.MID_TERM;
        return McInvestmentPositionType.LONG_TERM;
    }
    public static McInvestmentAccountType GetAccountType(int taxBucket, int accountGroup)
    {
        // tax buckets
        // 1	"Tax deferred"
        // 2	"Tax free HSA"
        // 3	"Tax free Roth"
        // 4	"Tax on capital gains"

        // account groups
        // 1	"Dan's 401(k)"
        // 2	"Dan's IRAs"
        // 3	"Jodi's IRAs"
        // 4	"Brokerage Account"
        // 5	"Home Equity"
        // 6	"Health Equity"

        // McInvestmentAccountType
        // TAXABLE_BROKERAGE = 0,
        // TRADITIONAL_401_K = 1,
        // ROTH_401_K = 2,
        // TRADITIONAL_IRA = 3,
        // ROTH_IRA = 4,
        // HSA = 5,
        // PRIMARY_RESIDENCE = 6,
        // CASH = 7,
        
        if (taxBucket == 2) return McInvestmentAccountType.HSA;
        if (taxBucket == 1 && accountGroup == 1) return McInvestmentAccountType.TRADITIONAL_401_K;
        if (taxBucket == 3 && accountGroup == 1) return McInvestmentAccountType.ROTH_401_K;
        if (taxBucket == 3 && accountGroup == 2) return McInvestmentAccountType.ROTH_IRA;
        if (taxBucket == 1 && accountGroup == 2) return McInvestmentAccountType.TRADITIONAL_IRA;
        if (taxBucket == 4 && accountGroup == 4) return McInvestmentAccountType.TAXABLE_BROKERAGE;
        if (taxBucket == 4 && accountGroup == 5) return McInvestmentAccountType.PRIMARY_RESIDENCE;
        if (taxBucket == 1 && accountGroup == 3) return McInvestmentAccountType.TRADITIONAL_IRA;
        return McInvestmentAccountType.CASH;
    }
    
    
    public static NetWorthMeasurement CreateNetWorthMeasurement(MonteCarloSim sim)
        {
            var totalAssets = 0L;
            var totalLiabilities = 0L;
            foreach (var account in sim.BookOfAccounts.InvestmentAccounts)
            {
                if (account.AccountType is not McInvestmentAccountType.PRIMARY_RESIDENCE)
                {
                    totalAssets += account.Positions.Where(x => x.IsOpen).Sum(x => {
                        McInvestmentPosition ip = (McInvestmentPosition)x;
                        return ip.CurrentValue;
                    });
                }
            }
            foreach (var account in sim.BookOfAccounts.DebtAccounts)
            {
                totalLiabilities += account.Positions.Where(x => x.IsOpen).Sum(x => {
                    McDebtPosition dp = (McDebtPosition)x;
                    return dp.CurrentBalance;
                });
            }
            NetWorthMeasurement measurement = new NetWorthMeasurement()
            {
                MeasuredDate = sim.CurrentDateInSim,
                TotalAssets = totalAssets,
                TotalLiabilities = totalLiabilities,
                TotalCash = CalculateCashBalance(sim.BookOfAccounts),
                TotalMidTermInvestments = CalculateMidBucketTotalBalance(sim.BookOfAccounts),
                TotalLongTermInvestments = CalculateLongBucketTotalBalance(sim.BookOfAccounts),
                TotalSpend = 0,
                TotalTax = sim.TaxLedger.TotalTaxPaid,
            };

            // see if we're in extreme austerity measures based on total net worth
            if (measurement.NetWorth <= sim.SimParameters.ExtremeAusterityNetWorthTrigger)
            {

                sim.RecessionStats.AreWeInExtremeAusterityMeasures = true;
                // set the end date to now. if we stay below the line, the date
                // will keep going up with it
                sim.RecessionStats.LastExtremeAusterityMeasureEnd = sim.CurrentDateInSim;
            }
            else
            {
                // has it been within 12 months that we were in an extreme measure?
                if (sim.RecessionStats.LastExtremeAusterityMeasureEnd < sim.CurrentDateInSim.PlusYears(-1))
                {

                    sim.RecessionStats.AreWeInExtremeAusterityMeasures = false;
                }
            }
            return measurement;
        }
}