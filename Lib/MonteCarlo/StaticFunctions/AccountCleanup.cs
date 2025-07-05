using Lib.DataTypes.MonteCarlo;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class AccountCleanup
{
    #region account cleanup functions

    /// <summary>
    /// removes closed positions and splits up large investment positions
    /// </summary>
    public static BookOfAccounts CleanUpAccounts(LocalDateTime currentDate, BookOfAccounts bookOfAccounts, CurrentPrices prices)
    {
        var cleanBook = RemoveClosedPositions(bookOfAccounts);
        cleanBook = SplitLargePositions(cleanBook, prices);
        return cleanBook;
    }
    public static BookOfAccounts RemoveClosedPositions(BookOfAccounts bookOfAccounts)
    {
        if (bookOfAccounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        if (bookOfAccounts.DebtAccounts is null) throw new InvalidDataException("DebtAccounts is null");
        
        var cleanBook = new  BookOfAccounts
        {
            Roth401K = bookOfAccounts.Roth401K,
            RothIra = bookOfAccounts.RothIra,
            Traditional401K = bookOfAccounts.Traditional401K,
            TraditionalIra = bookOfAccounts.TraditionalIra,
            Brokerage = bookOfAccounts.Brokerage,
            Hsa = bookOfAccounts.Hsa,
            Cash = bookOfAccounts.Cash,
            InvestmentAccounts = [],
            DebtAccounts = []
        };

        foreach (var account in bookOfAccounts.InvestmentAccounts)
        {
            cleanBook.InvestmentAccounts.Add(new McInvestmentAccount() {
                    Id = account.Id,
                    Name = account.Name,
                    AccountType = account.AccountType,
                    Positions = account.Positions.Where(a => a.IsOpen).ToList()
                }
            );
        }
        foreach (var account in bookOfAccounts.DebtAccounts)
        {
            cleanBook.DebtAccounts.Add(new McDebtAccount() {
                    Id = account.Id,
                    Name = account.Name,
                    Positions = account.Positions.Where(a => a.IsOpen).ToList()
                }
            );
        }
        return cleanBook;
    }
    
    /// <summary>
    /// break up large positions into byte-sized chunks. this makes it
    /// easier to sell them. if we keep the sizes small, we don't have to
    /// worry about selling partial holdings. there is no tax implication
    /// as we're just making it easier to do math later on
    /// </summary>
    public static BookOfAccounts SplitLargePositions(BookOfAccounts bookOfAccounts, CurrentPrices prices)
    {
        if (bookOfAccounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        var maxPositionValue = StaticConfig.InvestmentConfig.MonteCarloSimMaxPositionValue;
        
        var cleanBook = new  BookOfAccounts
        {
            Roth401K = bookOfAccounts.Roth401K,
            RothIra = bookOfAccounts.RothIra,
            Traditional401K = bookOfAccounts.Traditional401K,
            TraditionalIra = bookOfAccounts.TraditionalIra,
            Brokerage = bookOfAccounts.Brokerage,
            Hsa = bookOfAccounts.Hsa,
            Cash = bookOfAccounts.Cash,
            InvestmentAccounts = [],
            DebtAccounts = bookOfAccounts.DebtAccounts
        };
        
        
        foreach (var account in bookOfAccounts.InvestmentAccounts)
        {
            cleanBook.InvestmentAccounts.Add(account);
            if(account.AccountType == McInvestmentAccountType.PRIMARY_RESIDENCE) continue;
            if(account.AccountType == McInvestmentAccountType.CASH) continue;
            
            List<McInvestmentPosition> newPositions = []; 
            
            var oldPositions = 
                account
                    .Positions
                    .Where(x => x.IsOpen && x is McInvestmentPosition)
                    .ToList();

            foreach (var oldPosition in oldPositions)
            {
                /*
                 * if a position is greater than the max, just divide it in 2. At the start of the sim,
                 * we'll have some positions that are way bigger, but they'll be broken up into manageable
                 * chunks by the time we start selling
                 */
                
                if (oldPosition.CurrentValue <= maxPositionValue)
                {
                    newPositions.Add(oldPosition);
                    continue;
                }
                newPositions.AddRange(SplitPositionInHalf(oldPosition));
            }
            account.Positions = newPositions;
            
        }
        return cleanBook;
    }
    
    
    public static List<McInvestmentPosition> SplitPositionInHalf(McInvestmentPosition oldPosition)
    {   
        // the position is too big. split it in 2
        var originalQuantity = oldPosition.Quantity;
        var quantity1 = Math.Round(originalQuantity / 2, 4);
        var quantity2 = originalQuantity - quantity1;
        var originalPrice = oldPosition.Price;
        var originalCost = oldPosition.InitialCost;
        var cost1 = Math.Round(originalCost / 2, 4);
        var cost2 = originalCost - cost1;

        return [
            new McInvestmentPosition()
            {
                Id = Guid.NewGuid(),
                Entry = oldPosition.Entry,
                InitialCost = cost1,
                InvestmentPositionType = oldPosition.InvestmentPositionType,
                IsOpen = oldPosition.IsOpen,
                Name = "automated investment",
                Price = oldPosition.Price,
                Quantity = quantity1
            },
            new McInvestmentPosition()
            {
                Id = Guid.NewGuid(),
                Entry = oldPosition.Entry,
                InitialCost = cost2,
                InvestmentPositionType = oldPosition.InvestmentPositionType,
                IsOpen = oldPosition.IsOpen,
                Name = "automated investment",
                Price = oldPosition.Price,
                Quantity = quantity2
            }];
    }


    #endregion account cleanup functions
}