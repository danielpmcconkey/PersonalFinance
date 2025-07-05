using Lib.DataTypes.MonteCarlo;

namespace Lib.MonteCarlo.StaticFunctions;

public static class AccountCopy
{
    
    #region account copy functions

    public static BookOfAccounts CopyBookOfAccounts(BookOfAccounts bookOfAccounts)
    {
        var investments = CopyInvestmentAccounts(bookOfAccounts.InvestmentAccounts);
        var debt = CopyDebtAccounts(bookOfAccounts.DebtAccounts);
        return Account.CreateBookOfAccounts(investments, debt);
    }
    public static McDebtAccount CopyDebtAccount(McDebtAccount account)
    {
        var newAccount = new McDebtAccount()
        {
            Id = account.Id,
            Name = account.Name,
            Positions = CopyDebtPositions(account.Positions),
        };
        return newAccount;
    }
    /// <summary>
    /// Used to create a new object with the same characteristics as the original so we don't have to worry about one
    /// sim run updating another's stats
    /// </summary>
    public static List<McDebtAccount> CopyDebtAccounts(List<McDebtAccount> oldAccounts)
    {
        if (oldAccounts.Count == 0) return oldAccounts;
        
        List<McDebtAccount> newAccounts = [];
        foreach (McDebtAccount a in oldAccounts)
        {
            newAccounts.Add(CopyDebtAccount(a));
        }
        return newAccounts;
    }

    public static McDebtPosition CopyDebtPosition(McDebtPosition p)
    {
        return new McDebtPosition()
        {
            Id = p.Id,
            IsOpen = p.IsOpen,
            Name = p.Name,
            Entry = p.Entry,
            AnnualPercentageRate = p.AnnualPercentageRate,
            MonthlyPayment = p.MonthlyPayment,
            CurrentBalance = p.CurrentBalance,
        };
    }
    
    // <summary>
    /// Creates a copy of debt positions with new IDs while preserving all other properties
    /// </summary>
    /// <param name="positions">Original debt positions to copy</param>
    /// <returns>A new list containing copies of the original positions</returns>
    public static List<McDebtPosition> CopyDebtPositions(List<McDebtPosition> positions)
    {
        if (positions is null) throw new ArgumentNullException("positions is null");
        List<McDebtPosition> newList = [];
        foreach (McDebtPosition p in positions)
        {
            newList.Add(CopyDebtPosition(p));
        }
        return newList;
    }

    public static McInvestmentAccount CopyInvestmentAccount(McInvestmentAccount a)
    {
        var newAccount = new McInvestmentAccount()
        {
            Id = a.Id,
            Name = a.Name,
            AccountType = a.AccountType,
            Positions = CopyInvestmentPositions(a.Positions),
        };
        return newAccount;
    }

    /// <summary>
    /// Used to create a new object with the same characteristics as the original so we don't have to worry about one
    /// sim run updating another's stats
    /// </summary>
    public static List<McInvestmentAccount> CopyInvestmentAccounts(List<McInvestmentAccount> oldAccounts)
    {
        if (oldAccounts.Count == 0) return oldAccounts;
        
        List<McInvestmentAccount> newAccounts = [];
        newAccounts.AddRange(oldAccounts.Select(CopyInvestmentAccount));
        return newAccounts;
    }

    public static McInvestmentPosition CopyInvestmentPosition(McInvestmentPosition p)
    {
        return new McInvestmentPosition()
        {
            Id = p.Id,
            IsOpen = p.IsOpen,
            Name = p.Name,
            Entry = p.Entry,
            InvestmentPositionType = p.InvestmentPositionType,
            InitialCost = p.InitialCost,
            Quantity = p.Quantity,
            Price = p.Price,
        };
    }
    // <summary>
    /// Creates a copy of investment positions with new IDs while preserving all other properties
    /// </summary>
    /// <param name="positions">Original investment positions to copy</param>
    /// <returns>A new list containing copies of the original positions</returns>
    public static List<McInvestmentPosition> CopyInvestmentPositions(List<McInvestmentPosition> positions)
    {
        if (positions is null) throw new ArgumentNullException("positions is null");
        
        List<McInvestmentPosition> newList = [];
        foreach (McInvestmentPosition p in positions)
        {
            newList.Add(CopyInvestmentPosition(p));
        }
        return newList;
    }

    #endregion account copy functions
}