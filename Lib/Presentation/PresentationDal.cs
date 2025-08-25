using Lib.DataTypes.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Lib.Presentation;

public static class PresentationDal
{
    

    public static List<PgCashAccount> FetchCashAccountsAndPositions()
    {
        using var context = new PgContext();
        return context.PgCashAccounts
            .Include(x => x.Positions)
            .ToList();
    }

    public static List<PgDebtAccount> FetchDebtAccountsAndPositions()
    {
        using var context = new PgContext();
        return context.PgDebtAccounts
            .Include(x => x.Positions)
            .ToList();
    }
    
    public static List<PgInvestmentAccountGroup> FetchInvestAccountGroupsAndChildData()
    {
        using var context = new PgContext();
        return context.PgInvestmentAccountGroups
            .Include(x =>x.InvestmentAccounts)
                .ThenInclude(x => x.TaxBucket)
            .Include(x =>x.InvestmentAccounts)
            .ThenInclude(x => x.Positions)
            .ThenInclude(p => p.Fund)
            .ThenInclude(f => f.FundType1)
            .Include(x =>x.InvestmentAccounts)
            .ThenInclude(x => x.Positions)
            .ThenInclude(p => p.Fund)
            .ThenInclude(f => f.FundType2)
            .Include(x =>x.InvestmentAccounts)
            .ThenInclude(x => x.Positions)
            .ThenInclude(p => p.Fund)
            .ThenInclude(f => f.FundType3)
            .Include(x =>x.InvestmentAccounts)
            .ThenInclude(x => x.Positions)
            .ThenInclude(p => p.Fund)
            .ThenInclude(f => f.FundType4)
            .Include(x =>x.InvestmentAccounts)
            .ThenInclude(x => x.Positions)
            .ThenInclude(p => p.Fund)
            .ThenInclude(f => f.FundType5)
            .ToList();
    }

}