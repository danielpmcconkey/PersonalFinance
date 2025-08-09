// #define PERFORMANCEPROFILING
using System.Diagnostics;
using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class InvestmentSales
{
    public static (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)
        SellInvestmentsToDollarAmount(
            BookOfAccounts accounts, TaxLedger ledger, LocalDateTime currentDate, decimal amountToSell,
            (McInvestmentPositionType positionType, McInvestmentAccountType accountType)[]? typeOrder = null,
            LocalDateTime? minDateExclusive = null, LocalDateTime? maxDateInclusive = null)
    {
        (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger,
            List<ReconciliationMessage> messages) results = (
                0, 
                AccountCopy.CopyBookOfAccounts(accounts),
                Tax.CopyTaxLedger(ledger),
                []
            );
        var acceptableAccountTypes = typeOrder is null ? [] : typeOrder
            .Select(x => x.accountType)
            .Distinct()
            .ToArray();
        var acceptablePositionTypes = typeOrder is null ? [] : typeOrder
            .Select(x => x.positionType)
            .Distinct()
            .ToArray();
        
        // set up a dictionary that gives a numerical rank to each typeOrder entry
        Dictionary<(McInvestmentPositionType, McInvestmentAccountType), int> orderRank = [];
        for (int i = 0; typeOrder is not null && i < typeOrder.Length; i++) orderRank.Add(typeOrder[i], i);

        Func<McInvestmentPositionType, McInvestmentAccountType, int> rankPair = (positionType, accountType) =>
        {
            if (!orderRank.TryGetValue((positionType, accountType), out var rank)) return 6000;
            return rank;
        };
        
        // query the positions in exactly the order you want them
        var query = from account in results.accounts.InvestmentAccounts
                where account.AccountType != McInvestmentAccountType.CASH &&
                    account.AccountType != McInvestmentAccountType.PRIMARY_RESIDENCE &&
                    (typeOrder is null || acceptableAccountTypes.Contains(account.AccountType))
                from position in account.Positions
                where (
                    position.IsOpen && 
                    (minDateExclusive is null || position.Entry > minDateExclusive) && 
                    (maxDateInclusive is null || position.Entry <= maxDateInclusive)
                    && (typeOrder is null || acceptablePositionTypes.Contains(position.InvestmentPositionType)))
                orderby (rankPair(position.InvestmentPositionType, account.AccountType), position.Entry)
                select (account, position)
            ;
        
        
        var totalIraDistribution = 0m;
        var totalTaxableSold = 0m;
        var totalLongTermCapitalGains = 0m;
        var totalShortTermCapitalGains = 0m;
        var totalTaxFree = 0m;
            
        foreach (var (account, position) in query)
        {
            if (results.amountSold >= amountToSell) break;
            var amountStillNeeded = amountToSell - results.amountSold;
            var amountSoldThisPosition = (amountStillNeeded >= position.CurrentValue) ?
                position.CurrentValue :
                amountStillNeeded;
            var averageCost = position.InitialCost / position.CurrentValue;
            var costOfAmountSoldThisPosition = averageCost * amountSoldThisPosition;
                
            results.amountSold += amountSoldThisPosition;
            switch (account.AccountType)
            {
                case McInvestmentAccountType.ROTH_401_K:
                case McInvestmentAccountType.HSA:
                    totalTaxFree += amountSoldThisPosition;
                    break;
                case McInvestmentAccountType.TRADITIONAL_401_K:
                case McInvestmentAccountType.TRADITIONAL_IRA:
                    totalIraDistribution += amountSoldThisPosition;
                    break;
                case McInvestmentAccountType.TAXABLE_BROKERAGE:
                    var capitalGains = amountSoldThisPosition - costOfAmountSoldThisPosition;
                    if (position.Entry < currentDate.PlusYears(-1)) totalLongTermCapitalGains += capitalGains;
                    if (position.Entry >= currentDate.PlusYears(-1)) totalShortTermCapitalGains += capitalGains;
                    totalTaxableSold += amountSoldThisPosition;
                    break;
                default:
                    break;
            }

            if (amountStillNeeded >= position.CurrentValue)
            {
                // close the position
                position.Quantity = 0;
                position.IsOpen = false;
            }
            else
            {
                 // diminish the position (both quanity and initial cost)
                 var originalValue = position.CurrentValue;
                 var originalCost = position.InitialCost;
                 var averageCostBasis = originalCost / originalValue;
                 var newValue = originalValue - amountSoldThisPosition;
                 var newCostBasis = averageCostBasis * newValue;
                 position.Quantity = newValue / position.Price;
                 position.InitialCost = newCostBasis;
            }
        }
        // deposit the proceeds
        var depositResults = AccountCashManagement.DepositCash(
            results.accounts, results.amountSold, currentDate);
        results.accounts = depositResults.accounts;
        
        // record the IRA distributions
        var recordIraResults = Tax.RecordIraDistribution(results.ledger, currentDate, totalIraDistribution);
        results.ledger = recordIraResults.ledger;
        
        // record the Capital Gains
        var recordLongTermCapitalGainsResults = Tax.RecordLongTermCapitalGain(results.ledger, currentDate, totalLongTermCapitalGains);
        results.ledger = recordLongTermCapitalGainsResults.ledger;
        var recordShortTermCapitalGainsResults = Tax.RecordShortTermCapitalGain(results.ledger, currentDate, totalShortTermCapitalGains);
        results.ledger = recordShortTermCapitalGainsResults.ledger;
        
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.Add(new ReconciliationMessage(currentDate, results.amountSold, "Total investments sold"));
        results.messages.Add(new ReconciliationMessage(currentDate, totalIraDistribution, "Total tax deferred sold"));
        results.messages.Add(new ReconciliationMessage(currentDate, totalTaxableSold, "Total taxable sold"));
        results.messages.Add(new ReconciliationMessage(currentDate, totalLongTermCapitalGains, "Total long term capital gains"));
        results.messages.Add(new ReconciliationMessage(currentDate, totalLongTermCapitalGains, "Total short term capital gains"));
        results.messages.Add(new ReconciliationMessage(currentDate, totalTaxFree, "Total tax free sold"));
        results.messages.AddRange(depositResults.messages);
        results.messages.AddRange(recordIraResults.messages);
        results.messages.AddRange(recordLongTermCapitalGainsResults.messages);
        results.messages.AddRange(recordShortTermCapitalGainsResults.messages);
        
        
        return results;
    }
    
    // /// <summary>
    // /// sells enough full positions of the provided position type to reach the amountToSell, using the provided account
    // /// type order. It deposits proceeds into the cash account.
    // /// </summary>
    // /// <returns>the exact amount sold, a new book of accounts, and a new ledger</returns>
    // public static (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages) 
    //     SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType(
    //         decimal amountToSell, McInvestmentPositionType positionType, McInvestmentAccountType[] typeOrder,
    //         BookOfAccounts bookOfAccounts, TaxLedger taxLedger, LocalDateTime currentDate)
    // {
    //     (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger,
    //         List<ReconciliationMessage> messages) results = (
    //             0, 
    //             AccountCopy.CopyBookOfAccounts(bookOfAccounts),
    //             Tax.CopyTaxLedger(taxLedger),
    //             []
    //         );
    //     Dictionary<McInvestmentAccountType, int> orderRank = [];
    //     for (int i = 0; i < typeOrder.Length; i++) orderRank.Add(typeOrder[i], i);
    //     
    //     var oneYearAgo = currentDate.PlusYears(-1);
    //     var query = from account in results.newBookOfAccounts.InvestmentAccounts
    //         where typeOrder.Contains(account.AccountType)
    //         from position in account.Positions
    //         where (position.IsOpen && position.Entry < oneYearAgo && position.InvestmentPositionType == positionType)
    //         orderby (orderRank[account.AccountType], position.Entry)
    //         select (account, position)
    //         ;
    //     var totalIraDistribution = 0m;
    //     var totalTaxableSold = 0m;
    //     var totalCapitalGains = 0m;
    //     var totalTaxFree = 0m;
    //         
    //     foreach (var (account, position) in query)
    //     {
    //         if (results.amountSold >= amountToSell) break;
    //         results.amountSold += position.CurrentValue;
    //         switch (account.AccountType)
    //         {
    //             case McInvestmentAccountType.ROTH_401_K:
    //             case McInvestmentAccountType.HSA:
    //                 totalTaxFree += position.CurrentValue;
    //                 break;
    //             case McInvestmentAccountType.TRADITIONAL_401_K:
    //             case McInvestmentAccountType.TRADITIONAL_IRA:
    //                 totalIraDistribution += position.CurrentValue;
    //                 break;
    //             case McInvestmentAccountType.TAXABLE_BROKERAGE:
    //                 totalCapitalGains += (position.CurrentValue - position.InitialCost);
    //                 totalTaxableSold += position.CurrentValue;
    //                 break;
    //             default:
    //                 break;
    //         }
    //         // close the position
    //         position.Quantity = 0;
    //         position.IsOpen = false;
    //         
    //         
    //     
    //         position.Quantity = 0;
    //         position.IsOpen = false;
    //     }
    //     // deposit the proceeds
    //     var depositResults = AccountCashManagement.DepositCash(
    //         results.newBookOfAccounts, results.amountSold, currentDate);
    //     results.newBookOfAccounts = depositResults.accounts;
    //     
    //     // record the IRA distributions
    //     var recordIraResults = Tax.RecordIraDistribution(results.newLedger, currentDate, totalIraDistribution);
    //     results.newLedger = recordIraResults.ledger;
    //     
    //     // record the Capital Gains
    //     var recordCapitalGainsResults = Tax.RecordLongTermCapitalGain(results.newLedger, currentDate, totalCapitalGains);
    //     results.newLedger = recordCapitalGainsResults.ledger;
    //     
    //     if (!MonteCarloConfig.DebugMode) return results;
    //     results.messages.Add(new ReconciliationMessage(currentDate, results.amountSold, "Total investments sold"));
    //     results.messages.Add(new ReconciliationMessage(currentDate, totalIraDistribution, "Total tax deferred sold"));
    //     results.messages.Add(new ReconciliationMessage(currentDate, totalTaxableSold, "Total taxable sold"));
    //     results.messages.Add(new ReconciliationMessage(currentDate, totalCapitalGains, "Total capital gains"));
    //     results.messages.Add(new ReconciliationMessage(currentDate, totalTaxFree, "Total tax free sold"));
    //     results.messages.AddRange(depositResults.messages);
    //     results.messages.AddRange(recordIraResults.messages);
    //     results.messages.AddRange(recordCapitalGainsResults.messages);
    //     
    //     
    //     return results;
    // }
    //
    
   

    /// <summary>
    /// sells enough positions of the provided position type to reach the amountToSell. It does this strategically
    /// by first selling tax deferred positions until you've reached the annual income head room to avoid going into
    /// higher tax brackets. It deposits proceeds into the cash account.
    /// </summary>
    /// <returns>the exact amount sold, a new book of accounts, and a new ledger</returns>
    public static (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages) 
        SellInvestmentsToDollarAmountByPositionType(decimal amountNeeded, McInvestmentPositionType positionType,
            BookOfAccounts bookOfAccounts, TaxLedger taxLedger, LocalDateTime currentDate)
    {
        if (bookOfAccounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        if (bookOfAccounts.InvestmentAccounts.Count == 0) return (0, bookOfAccounts, taxLedger, []);
        
        var oneYearAgo = currentDate.PlusYears(-1);
        
        // set up the return tuple
        (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger, 
            List<ReconciliationMessage> messages) results = (
            0M, AccountCopy.CopyBookOfAccounts(bookOfAccounts), Tax.CopyTaxLedger(taxLedger), []);
        
        // calculate the amount of income room we have so we can sell tax deferred account positions first
        var incomeRoom = TaxCalculation.CalculateIncomeRoom(taxLedger, currentDate);

        if (incomeRoom > 0)
        {
            // we have income room. sell tax deferred positions, up to the incomeRoom amount
            List<(McInvestmentPositionType positionType, McInvestmentAccountType accountType)> salesOrderWithRoom = [];
            foreach (var accountType in InvestmentConfig.SalesOrderWithRoom)
            {
                salesOrderWithRoom.Add((positionType, accountType));
            }
            var amountToSell = Math.Min(amountNeeded, incomeRoom);
            var withRoomResult = SellInvestmentsToDollarAmount(results.newBookOfAccounts,
                results.newLedger, currentDate, amountToSell, salesOrderWithRoom.ToArray(), null, 
                oneYearAgo);
            results.amountSold += withRoomResult.amountSold;
            results.newBookOfAccounts = withRoomResult.accounts;
            results.newLedger = withRoomResult.ledger;
            results.messages.AddRange(withRoomResult.messages);
        }
        if (results.amountSold >= amountNeeded) return results;
        
        // we don't have any more income room and we still have sellin to do. sell taxed and tax-free positions, up to
        // the amountNeeded
        List<(McInvestmentPositionType positionType, McInvestmentAccountType accountType)> salesOrderWithNoRoom = [];
        foreach (var accountType in InvestmentConfig.SalesOrderWithNoRoom)
        {
            salesOrderWithNoRoom.Add((positionType, accountType));
        }
        var noRoomResult = SellInvestmentsToDollarAmount(results.newBookOfAccounts,
            results.newLedger, currentDate, amountNeeded - results.amountSold, salesOrderWithNoRoom.ToArray(),
            null, oneYearAgo);
        results.amountSold += noRoomResult.amountSold;
        results.newBookOfAccounts = noRoomResult.accounts;
        results.newLedger = noRoomResult.ledger;
        
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.AddRange(noRoomResult.messages);
        results.messages.Add(new ReconciliationMessage(
            currentDate, results.amountSold, $"Amount sold in investment accounts"));

        return results;
    }
    
    /// <summary>
    /// sells enough full positions of the provided position type to reach the amountToSell, but specifically in the RMD
    /// sales account order. It ignores the income "head room". It deposits proceeds into the cash account.
    /// </summary>
    /// <returns>the exact amount sold, a new book of accounts, and a new ledger</returns>
    public static (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages) 
        SellInvestmentsToRmdAmount(decimal amountNeeded, BookOfAccounts bookOfAccounts, TaxLedger taxLedger, 
            LocalDateTime currentDate)
    {
        if (bookOfAccounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        if (bookOfAccounts.InvestmentAccounts.Count == 0) return (0, bookOfAccounts, taxLedger, []);
        
        var oneYearAgo = currentDate.PlusYears(-1);
        
        // set up the return tuple
        (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger,
            List<ReconciliationMessage> messages)  results = (
            0m, AccountCopy.CopyBookOfAccounts(bookOfAccounts), Tax.CopyTaxLedger(taxLedger), []);

        List<(McInvestmentPositionType positionType, McInvestmentAccountType accountType)> salesOrder = [];
        foreach (var accountType in InvestmentConfig.SalesOrderRmd)
        {
            salesOrder.Add((McInvestmentPositionType.LONG_TERM, accountType));
        }
        foreach (var accountType in InvestmentConfig.SalesOrderRmd)
        {
            salesOrder.Add((McInvestmentPositionType.MID_TERM, accountType));
        }
        
        var salesResult = SellInvestmentsToDollarAmount(results.newBookOfAccounts,
            results.newLedger, currentDate, amountNeeded - results.amountSold, salesOrder.ToArray(),
            null, oneYearAgo);
        
        results.amountSold += salesResult.amountSold;
        results.newBookOfAccounts = salesResult.accounts;
        results.newLedger = salesResult.ledger;
        results.messages.AddRange(salesResult.messages);
        if (results.amountSold >= amountNeeded) return results;
        
        // nothing's left to try. not sure how we got here
        throw new InvalidDataException("RMD: Nothing left to try. Not sure how we got here");
    }
}