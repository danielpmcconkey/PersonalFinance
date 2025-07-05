using Lib.DataTypes.MonteCarlo;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Investment
{
    
    public static McInvestmentPositionType GetInvestmentPositionType(string symbol)
    {
        // todo: change GetInvestmentPositionType to read type from the DB
        if (symbol == "SCHD") return McInvestmentPositionType.MID_TERM;
        return McInvestmentPositionType.LONG_TERM;
    }
    
    
    public static List<McInvestmentPosition> GetInvestmentPositionsByAccountTypeAndPositionType( 
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
    
    public static void InvestFunds(BookOfAccounts accounts, LocalDateTime currentDate, decimal dollarAmount, 
            McInvestmentPositionType mcInvestmentPositionType, McInvestmentAccountType accountType, CurrentPrices prices)
        {
            if (dollarAmount <= 0) return;
            if (accounts.Cash is null) throw new InvalidDataException("Cash account is null");
            if (accounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
            if (accounts.DebtAccounts is null) throw new InvalidDataException("DebtAccounts is null");
            if (accounts.Roth401K is null) throw new InvalidDataException("Roth401k is null");
            if (accounts.RothIra is null) throw new InvalidDataException("RothIra is null");
            if (accounts.Traditional401K is null) throw new InvalidDataException("Traditional401k is null");
            if (accounts.TraditionalIra is null) throw new InvalidDataException("TraditionalIra is null");
            if (accounts.Brokerage is null) throw new InvalidDataException("Brokerage account is null");
            if (accounts.Hsa is null) throw new InvalidDataException("Hsa account is null");
            
            // figure out the correct account pointer
            McInvestmentAccount GetAccount() =>
                accountType switch
                {
                    McInvestmentAccountType.CASH => accounts.Cash,
                    McInvestmentAccountType.HSA => accounts.Hsa,
                    McInvestmentAccountType.ROTH_401_K => accounts.Roth401K,
                    McInvestmentAccountType.ROTH_IRA => accounts.RothIra,
                    McInvestmentAccountType.TAXABLE_BROKERAGE => accounts.Brokerage,
                    McInvestmentAccountType.TRADITIONAL_IRA => accounts.TraditionalIra,
                    McInvestmentAccountType.TRADITIONAL_401_K => accounts.Traditional401K,
                    _ => throw new NotImplementedException(),
                };
            

            var roundedDollarAmount = Math.Round(dollarAmount, 2);

            decimal getPrice() =>
            mcInvestmentPositionType switch
            {
                McInvestmentPositionType.SHORT_TERM => prices.CurrentShortTermInvestmentPrice,
                McInvestmentPositionType.MID_TERM => prices.CurrentMidTermInvestmentPrice,
                McInvestmentPositionType.LONG_TERM => prices.CurrentLongTermInvestmentPrice,
                _ => throw new NotImplementedException(),
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
            if (StaticConfig.MonteCarloConfig.DebugMode == true)
            {
                Reconciliation.AddMessageLine(
                    currentDate, dollarAmount, $"Investment in account {account.Name}, type {mcInvestmentPositionType}");
            }
        }
    
    /// <summary>
    /// Initially, we pull positions from the database using real-world pries. But that causes issues with rounding and
    /// such every time we accrue interest. Too many little positions and rounding adds up over time. So, with this
    /// function, we set all postions to the decimal-term, mid-term, or short-term costs and recalculate the quantity
    /// accordingly, such that the value of the position is the same, but it's now in simpler terms  
    /// </summary>
    public static void NormalizeInvestmentPositions(BookOfAccounts bookOfAccounts, CurrentPrices prices)
    {
        if(bookOfAccounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        
        var relevantAccounts =
            bookOfAccounts.InvestmentAccounts.Where(x =>
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
    }
    
    
    
    


}