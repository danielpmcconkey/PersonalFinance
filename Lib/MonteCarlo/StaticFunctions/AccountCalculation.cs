using Lib.DataTypes.MonteCarlo;

namespace Lib.MonteCarlo.StaticFunctions;

public static class AccountCalculation
{
    
    #region Calculation functions
    public static decimal CalculateCashBalance(BookOfAccounts accounts)
    {
        if (accounts.Cash is null) throw new InvalidDataException("Cash is null");
        if (accounts.Cash.Positions is null) throw new InvalidDataException("Cash.Positions is null");
        
        return accounts.Cash.Positions.Sum(x => {
            if (!x.IsOpen) return 0;
            var ip = (McInvestmentPosition)x;
            return ip.CurrentValue;
        });
    }
    public static decimal CalculateInvestmentAccountTotalValue(McInvestmentAccount account)
    {
        if (account.Positions is null) throw new InvalidDataException("Positions is null");
        
        return account.Positions.Sum(x => 
        {
            if (x.IsOpen)
            {
                return x.CurrentValue;
            }
            return 0;
        });
    }
    public static decimal CalculateLongBucketTotalBalance(BookOfAccounts accounts)
    {
        return CalculateTotalBalanceByBucketType(accounts, McInvestmentPositionType.LONG_TERM);
    }
    public static decimal CalculateMidBucketTotalBalance(BookOfAccounts accounts)
    {
        return CalculateTotalBalanceByBucketType(accounts, McInvestmentPositionType.MID_TERM);
    }
    public static decimal CalculateShortBucketTotalBalance(BookOfAccounts accounts)
    {
        return CalculateTotalBalanceByBucketType(accounts, McInvestmentPositionType.SHORT_TERM);
    }

    public static decimal CalculateNetWorth(BookOfAccounts accounts)
    {
        if (accounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        if (accounts.DebtAccounts is null) throw new InvalidDataException("DebtAccounts is null");
        
        var totalAssets = 0M;
        var totalLiabilities = 0M;
        foreach (var account in accounts.InvestmentAccounts)
        {
            if (account.AccountType is not McInvestmentAccountType.PRIMARY_RESIDENCE)
            {
                totalAssets += account.Positions.Where(x => x.IsOpen).Sum(x =>
                {
                    McInvestmentPosition ip = (McInvestmentPosition)x;
                    return ip.CurrentValue;
                });
            }
        }

        foreach (var account in accounts.DebtAccounts)
        {
            totalLiabilities += account.Positions.Where(x => x.IsOpen).Sum(x =>
            {
                McDebtPosition dp = (McDebtPosition)x;
                return dp.CurrentBalance;
            });
        }

        return totalAssets - totalLiabilities;
    }

    public static decimal CalculateTotalBalanceByBucketType(BookOfAccounts bookOfAccounts, McInvestmentPositionType bucketType)
    {
        if (bookOfAccounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        
        var totalBalance = 0M;
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
    public static decimal CalculateDebtTotal(BookOfAccounts accounts)
    {
        if (accounts.DebtAccounts is null) throw new InvalidDataException("DebtAccounts is null");
        
        decimal total = 0L;
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

    
}