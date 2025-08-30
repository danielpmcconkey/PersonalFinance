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
            .ThenInclude(f => f.InvestmentType)
            .Include(x =>x.InvestmentAccounts)
            .ThenInclude(x => x.Positions)
            .ThenInclude(p => p.Fund)
            .ThenInclude(f => f.Size)
            .Include(x =>x.InvestmentAccounts)
            .ThenInclude(x => x.Positions)
            .ThenInclude(p => p.Fund)
            .ThenInclude(f => f.IndexOrIndividual)
            .Include(x =>x.InvestmentAccounts)
            .ThenInclude(x => x.Positions)
            .ThenInclude(p => p.Fund)
            .ThenInclude(f => f.Sector)
            .Include(x =>x.InvestmentAccounts)
            .ThenInclude(x => x.Positions)
            .ThenInclude(p => p.Fund)
            .ThenInclude(f => f.Region)
            .Include(x =>x.InvestmentAccounts)
            .ThenInclude(x => x.Positions)
            .ThenInclude(p => p.Fund)
            .ThenInclude(f => f.Objective)
            .ToList();
    }

}