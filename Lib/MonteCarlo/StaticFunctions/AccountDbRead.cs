using Lib.DataTypes.MonteCarlo;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class AccountDbRead
{
    #region DB read functions

    public static decimal FetchDbCashTotalByPersonId(Guid personId)
    {
        // todo: change get cash method to actually use the person ID
        
        using var context = new PgContext();
        var currentCash = 0M;
        
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
            var positionAtMaxDate = 
                context
                    .PgCashPositions
                    .Where(x => 
                        x.PositionDate == maxDate.maxdate && 
                        x.CashAccountId == maxDate.Key)
                    .OrderByDescending(x => x.CurrentBalance)
                    .FirstOrDefault()
                ??  throw new InvalidDataException();
            if (positionAtMaxDate.CurrentBalance >= 0)
            {
                currentCash += positionAtMaxDate.CurrentBalance;
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
                accountPg.AnnualPercentageRate,
                accountPg.MonthlyPayment
                )
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
        foreach (var pgInvestmentAccount in accountsPg)
        {
            var positions = FetchDbOpenInvestmentPositionsByAccountId(pgInvestmentAccount.Id);
            if(!positions.Any()) continue;
            accounts.Add(new McInvestmentAccount()
            {
                Id = Guid.NewGuid(), 
                Name = pgInvestmentAccount.Name, 
                AccountType = Account.GetAccountType(
                    pgInvestmentAccount.TaxBucketId,
                    pgInvestmentAccount.InvestmentAccountGroupId),
                Positions = positions,
            });
        }
        
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
                Price  = 1.0M,
            }],
        });
        return accounts;
    }
    public static List<McDebtPosition> FetchDbOpenDebtPositionsByAccountId(int accountId, decimal apr, decimal payment)
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
            CurrentBalance = latestPosition.CurrentBalance,
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
                    InvestmentPositionType = Investment.GetInvestmentPositionType(maxDate.Key),
                    InitialCost = positionAtMaxDate.CostBasis,
                    Quantity = positionAtMaxDate.TotalQuantity,
                    Price = positionAtMaxDate.Price,
                });
            }
        }
        
        return positions;
    }

    #endregion DB read functions
}