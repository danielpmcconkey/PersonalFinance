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
            if (accounts.Roth401k is null) throw new InvalidDataException("Roth401k is null");
            if (accounts.RothIra is null) throw new InvalidDataException("RothIra is null");
            if (accounts.Traditional401k is null) throw new InvalidDataException("Traditional401k is null");
            if (accounts.TraditionalIra is null) throw new InvalidDataException("TraditionalIra is null");
            if (accounts.Brokerage is null) throw new InvalidDataException("Brokerage account is null");
            if (accounts.Hsa is null) throw new InvalidDataException("Hsa account is null");
            
            // figure out the correct account pointer
            McInvestmentAccount GetAccount() =>
                accountType switch
                {
                    McInvestmentAccountType.CASH => accounts.Cash,
                    McInvestmentAccountType.HSA => accounts.Hsa,
                    McInvestmentAccountType.ROTH_401_K => accounts.Roth401k,
                    McInvestmentAccountType.ROTH_IRA => accounts.RothIra,
                    McInvestmentAccountType.TAXABLE_BROKERAGE => accounts.Brokerage,
                    McInvestmentAccountType.TRADITIONAL_IRA => accounts.TraditionalIra,
                    McInvestmentAccountType.TRADITIONAL_401_K => accounts.Traditional401k,
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
                x.AccountType is not McInvestmentAccountType.PRIMARY_RESIDENCE);
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
            }
        }
    }
    
    
    public static BookOfAccounts RemoveClosedPositions(BookOfAccounts bookOfAccounts)
    {
        if (bookOfAccounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        if (bookOfAccounts.DebtAccounts is null) throw new InvalidDataException("DebtAccounts is null");
        
        var cleanBook = new  BookOfAccounts
        {
            Roth401k = bookOfAccounts.Roth401k,
            RothIra = bookOfAccounts.RothIra,
            Traditional401k = bookOfAccounts.Traditional401k,
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
    /// go through the accounts in order of sales preference and sell out
    /// of positions until you've met the amount needed. Also add taxable
    /// income to the tax sheet. Does not add the proceeds to the cash
    /// account because sometimes you just want to reinvest them into a
    /// different bucket
    /// </summary>
    /// <returns>the amount actually sold (may go under or over)</returns>
    public static decimal SellInvestment(BookOfAccounts bookOfAccounts, decimal amountNeededGlobal,
        McInvestmentPositionType mcInvestmentPositionType, LocalDateTime currentDate
        , TaxLedger taxLedger, bool isRmd = false)
    {
        if (bookOfAccounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        
        decimal amountSoldGlobal = 0M;
        var incomeRoom = Tax.CalculateIncomeRoom(taxLedger, currentDate.Year);



        // pull all relevant positions and sell until you've reached a
        // given amount or more. return what was actually sold
        Func<McInvestmentAccountType, decimal, decimal> sellToAmount = (McInvestmentAccountType accountType, decimal cap) =>
        {
            decimal amountSoldLocal = 0M; // this is the amount sold in just this internal amount
            var positions = GetInvestmentPositionsByAccountTypeAndPositionType(
                bookOfAccounts.InvestmentAccounts, accountType, mcInvestmentPositionType, currentDate);
            foreach (var p in positions)
            {
                if (amountSoldGlobal >= amountNeededGlobal) break;
                if (amountSoldLocal >= cap) break;
                

                // sell the whole thing; we should have split these
                // up into small enough pieces that that's okay
                Tax.LogInvestmentSale(taxLedger, currentDate, p, accountType);
                amountSoldGlobal += p.CurrentValue;
                amountSoldLocal += p.CurrentValue;
                p.Quantity = 0;
                p.IsOpen = false;
            }
            // add amount to RMD if qualified
            if (accountType is McInvestmentAccountType.TRADITIONAL_401_K ||
                accountType is McInvestmentAccountType.TRADITIONAL_IRA)
                Tax.AddRmdDistribution(taxLedger, currentDate, amountSoldLocal);

            return amountSoldLocal;
        };
        if(isRmd)
        {
            // this is a special circumstance just for EOY RMD requirements
            // meeting. It should have no concern for income room as, by
            // definition RMDs are a way for the IRS to get their money no
            // matter how well you've tax-advantaged your approach
            foreach (var accountType in StaticConfig.InvestmentConfig._salesOrderRmd)
            {
                if (amountSoldGlobal >= amountNeededGlobal) break;
                sellToAmount(accountType, amountNeededGlobal - amountSoldGlobal);
            }
        }
        else if (incomeRoom <= 0)
        {
            // no more income room. try to fill the order from Roth or HSA
            // accounts, then brokerage, then traditional accounts
            foreach (var accountType in StaticConfig.InvestmentConfig._salesOrderWithNoRoom)
            {
                if (amountSoldGlobal >= amountNeededGlobal) break;
                sellToAmount(accountType, amountNeededGlobal - amountSoldGlobal);
            }

        }
        else
        {
            // sell up to the incomeRoom amount using traditional accounts,
            // then, once the income room is reached, fulfill from
            // Roth / HSA, then brokerage, then traditional

            decimal amountSoldLocal = 0M; // just the amount sold in first pass (the salesOrderWithRoom flow)
            foreach (var accountType in StaticConfig.InvestmentConfig._salesOrderWithRoom)
            {
                if (amountSoldGlobal >= amountNeededGlobal) break;
                if (amountSoldLocal >= incomeRoom) break;

                /*
                 * scenario 1:
                 *    amountNeededGlobal = 35,000.00
                 *    amountSoldGlobal = 0
                 *    globalPendingSale = 35,000.00 (amountNeededGlobal - amountSoldGlobal)
                 *    incomeRoom = 15,000.00
                 *    amountSoldLocal = 0
                 *    incomeRoomLeft = 15,000.00 (incomeRoom - amountSoldLocal)
                 *    amountNeededLocal= 15,000.00 (Math.Min(globalPendingSale, incomeRoomLeft))
                 *    
                 * scenario 2:
                 *    amountNeededGlobal = 35,000.00
                 *    amountSoldGlobal = 5,000.00
                 *    globalPendingSale = 30,000.00 (amountNeededGlobal - amountSoldGlobal)
                 *    incomeRoom = 15,000.00
                 *    amountSoldLocal = 5,000.00
                 *    incomeRoomLeft = 10,000.00 (incomeRoom - amountSoldLocal)
                 *    amountNeededLocal= 10,000.00 (Math.Min(globalPendingSale, incomeRoomLeft))
                 *    
                 * scenario 3:
                 *    amountNeededGlobal = 12,000.00
                 *    amountSoldGlobal = 5,000.00
                 *    globalPendingSale = 7,000.00 (amountNeededGlobal - amountSoldGlobal)
                 *    incomeRoom = 15,000.00
                 *    amountSoldLocal = 5,000.00
                 *    incomeRoomLeft = 10,000.00 (incomeRoom - amountSoldLocal)
                 *    amountNeededLocal= 7,000.00 (Math.Min(globalPendingSale, incomeRoomLeft))
                 *    
                 * */
                decimal globalPendingSale = amountNeededGlobal - amountSoldGlobal;
                decimal incomeRoomLeft = incomeRoom - amountSoldLocal;
                decimal amountNeededLocal = Math.Min(globalPendingSale, incomeRoomLeft);
                amountSoldLocal += sellToAmount(accountType, amountNeededLocal);
            }
            if (amountSoldGlobal < amountNeededGlobal)
            {
                // still need more, but we've reached our income limit. Try
                // to fill with tax free sales
                foreach (var accountType in StaticConfig.InvestmentConfig._salesOrderWithNoRoom)
                {
                    if (amountSoldGlobal >= amountNeededGlobal) break;
                    sellToAmount(accountType, amountNeededGlobal - amountSoldGlobal);
                }
            }
        }

        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(null, amountSoldGlobal, $"Amount sold in investment accounts");
        }
        return amountSoldGlobal;
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
    
    /// <summary>
    /// break up large positions into byte-sized chunks. this makes it
    /// easier to sell them. if we keep the sizes small, we don't have to
    /// worry about selling partial holdings. there is no tax implication
    /// as we're just making it easier to do math later on
    /// </summary>
    public static void SplitLargePositions(BookOfAccounts bookOfAccounts, CurrentPrices prices)
    {
        if (bookOfAccounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        var maxPositionValue = StaticConfig.InvestmentConfig.MonteCarloSimMaxPositionValue;
        
        var accounts = bookOfAccounts.InvestmentAccounts.Where(x =>
        {
            if (x.AccountType == McInvestmentAccountType.PRIMARY_RESIDENCE) return false;
            if (x.AccountType == McInvestmentAccountType.CASH) return false;
            return true;
        });
        foreach (var account in accounts)
        {
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
    }


}