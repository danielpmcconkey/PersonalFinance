using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Investment
{
    
    public static McInvestmentPositionType GetInvestmentPositionType(PgFundType objectiveFundType)
    {
        var posType = objectiveFundType.Name switch
        {
            "Growth" => McInvestmentPositionType.LONG_TERM,
            "A place to live" or "Diversification" or "Target date" or "Safety" or "Dividend" => McInvestmentPositionType.MID_TERM,
            _ => McInvestmentPositionType.SHORT_TERM
        };
        return posType;
    }
    
    /// <summary>
    /// gets all positions that match the account and position types and are also ready to sell, meaning they're at
    /// least a year old and won't invoke short-term capital gains taxes
    /// </summary>
    public static List<McInvestmentPosition> GetInvestmentPositionsToSellByAccountTypeAndPositionType( 
        List<McInvestmentAccount> investmentAccounts, McInvestmentAccountType accountType,
        McInvestmentPositionType mcInvestmentPositionType, LocalDateTime currentDate)
    {
        var positions = investmentAccounts
            .Where(x => x.AccountType == accountType)
            .SelectMany(x => x.Positions.Where(y =>
                {
                    if (!y.IsOpen) return false;
                    if (y.Entry > currentDate.PlusYears(-1)) return false;
                    if (y.InvestmentPositionType != mcInvestmentPositionType) return false;
                    return true;
                }
            ))
            .ToList();

        
        return positions;
    }
    
    /// <summary>
    /// note this only does the investment. it assumes that you withdrew the cash external to this method. The reason is
    /// that we also use this to invest things like 401K contributions that come from outside of our cash account
    /// </summary>
    public static (BookOfAccounts accounts, List<ReconciliationMessage> messages) InvestFunds(
        BookOfAccounts accounts, LocalDateTime currentDate, decimal dollarAmount, 
        McInvestmentPositionType mcInvestmentPositionType, McInvestmentAccountType accountType, CurrentPrices prices)
    {
        if (dollarAmount <= 0) return (accounts, []);
        if (accounts.Cash is null) throw new InvalidDataException("Cash account is null");
        if (accounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        if (accounts.DebtAccounts is null) throw new InvalidDataException("DebtAccounts is null");
        if (accounts.Roth401K is null) throw new InvalidDataException("Roth401k is null");
        if (accounts.RothIra is null) throw new InvalidDataException("RothIra is null");
        if (accounts.Traditional401K is null) throw new InvalidDataException("Traditional401k is null");
        if (accounts.TraditionalIra is null) throw new InvalidDataException("TraditionalIra is null");
        if (accounts.Brokerage is null) throw new InvalidDataException("Brokerage account is null");
        if (accounts.Hsa is null) throw new InvalidDataException("Hsa account is null");
        
        (BookOfAccounts accounts, List<ReconciliationMessage> messages) results = (
            AccountCopy.CopyBookOfAccounts(accounts), []);
        
        // figure out the correct account pointer
        McInvestmentAccount GetAccount() =>
            accountType switch
            {
                McInvestmentAccountType.CASH => results.accounts.Cash,
                McInvestmentAccountType.HSA => results.accounts.Hsa,
                McInvestmentAccountType.ROTH_401_K => results.accounts.Roth401K,
                McInvestmentAccountType.ROTH_IRA => results.accounts.RothIra,
                McInvestmentAccountType.TAXABLE_BROKERAGE => results.accounts.Brokerage,
                McInvestmentAccountType.TRADITIONAL_IRA => results.accounts.TraditionalIra,
                McInvestmentAccountType.TRADITIONAL_401_K => results.accounts.Traditional401K,
                _ => throw new InvalidDataException(),
            };
        

        var roundedDollarAmount = Math.Round(dollarAmount, 2);

        decimal getPrice() =>
        mcInvestmentPositionType switch
        {
            McInvestmentPositionType.SHORT_TERM => prices.CurrentShortTermInvestmentPrice,
            McInvestmentPositionType.MID_TERM => prices.CurrentMidTermInvestmentPrice,
            McInvestmentPositionType.LONG_TERM => prices.CurrentLongTermInvestmentPrice,
            _ => throw new InvalidDataException(),
        };
        decimal price = getPrice();
        decimal quantity = Math.Round(roundedDollarAmount / price, 4);
        var account = GetAccount();
        account.Positions.Add(new McInvestmentPosition()
        {
            Id = Guid.NewGuid(),
            Entry = currentDate,
            InitialCost = dollarAmount,
            InvestmentPositionType = mcInvestmentPositionType,
            IsOpen = true,
            Name = "automated investment",
            Price = price,
            Quantity = quantity
        });
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.Add(new ReconciliationMessage(currentDate, dollarAmount,
            $"Investment in account {account.Name}, type {mcInvestmentPositionType}"));
        return results;
    }
    
    /// <summary>
    /// Initially, we pull positions from the database using real-world pries. But that causes issues with rounding and
    /// such every time we accrue interest. Too many little positions and rounding adds up over time. So, with this
    /// function, we set all postions to the long-term, mid-term, or short-term costs and recalculate the quantity
    /// accordingly, such that the value of the position is the same, but it's now in simpler terms  
    /// </summary>
    public static BookOfAccounts NormalizeInvestmentPositions(BookOfAccounts bookOfAccounts, CurrentPrices prices)
    {
        if(bookOfAccounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        
        var result = AccountCopy.CopyBookOfAccounts(bookOfAccounts);
        
        var relevantAccounts =
            result.InvestmentAccounts.Where(x =>
                x.AccountType is not McInvestmentAccountType.PRIMARY_RESIDENCE 
                && x.AccountType is not McInvestmentAccountType.CASH);
        foreach (var a in relevantAccounts)
        {
            foreach (var p in a.Positions)
            {
                var totalValue = p.CurrentValue;

                var newPrice = p.InvestmentPositionType switch
                {
                    McInvestmentPositionType.MID_TERM => (decimal)prices.CurrentMidTermInvestmentPrice,
                    McInvestmentPositionType.SHORT_TERM => (decimal)prices.CurrentShortTermInvestmentPrice,
                    _ => (decimal)prices.CurrentLongTermInvestmentPrice
                };

                var newQuantity = (totalValue / newPrice);
                p.Quantity = newQuantity;
                p.Price = newPrice;
            }
        }
        return result;
    }
    
    
    
    


}