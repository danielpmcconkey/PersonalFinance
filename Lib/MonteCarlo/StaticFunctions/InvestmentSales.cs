// #define PERFORMANCEPROFILING
using System.Diagnostics;
using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class InvestmentSales
{
    /// <summary>
    /// sells enough full positions of the provided position type to reach the amountToSell, using the provided account
    /// type order. It deposits proceeds into the cash account.
    /// </summary>
    /// <returns>the exact amount sold, a new book of accounts, and a new ledger</returns>
    public static (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages) 
        SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType(
            decimal amountToSell, McInvestmentPositionType positionType, McInvestmentAccountType[] typeOrder,
            BookOfAccounts bookOfAccounts, TaxLedger taxLedger, LocalDateTime currentDate)
    {
        (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger,
            List<ReconciliationMessage> messages) results = (
                0, 
                AccountCopy.CopyBookOfAccounts(bookOfAccounts),
                Tax.CopyTaxLedger(taxLedger),
                []
            );
        Dictionary<McInvestmentAccountType, int> orderRank = [];
        for (int i = 0; i < typeOrder.Length; i++) orderRank.Add(typeOrder[i], i);
        
        var oneYearAgo = currentDate.PlusYears(-1);
        var query = from account in results.newBookOfAccounts.InvestmentAccounts
            where typeOrder.Contains(account.AccountType)
            from position in account.Positions
            where (position.IsOpen && position.Entry < oneYearAgo && position.InvestmentPositionType == positionType)
            orderby (orderRank[account.AccountType], position.Entry)
            select (account, position)
            ;
        var totalIraDistribution = 0m;
        var totalTaxableSold = 0m;
        var totalCapitalGains = 0m;
        var totalTaxFree = 0m;
            
        foreach (var (account, position) in query)
        {
            if (results.amountSold >= amountToSell) break;
            results.amountSold += position.CurrentValue;
            switch (account.AccountType)
            {
                case McInvestmentAccountType.ROTH_401_K:
                case McInvestmentAccountType.HSA:
                    totalTaxFree += position.CurrentValue;
                    break;
                case McInvestmentAccountType.TRADITIONAL_401_K:
                case McInvestmentAccountType.TRADITIONAL_IRA:
                    totalIraDistribution += position.CurrentValue;
                    break;
                case McInvestmentAccountType.TAXABLE_BROKERAGE:
                    totalCapitalGains += (position.CurrentValue - position.InitialCost);
                    totalTaxableSold += position.CurrentValue;
                    break;
                default:
                    break;
            }
            // close the position
            position.Quantity = 0;
            position.IsOpen = false;
            
            
        
            position.Quantity = 0;
            position.IsOpen = false;
        }
        // deposit the proceeds
        var depositResults = AccountCashManagement.DepositCash(
            results.newBookOfAccounts, results.amountSold, currentDate);
        results.newBookOfAccounts = depositResults.accounts;
        
        // record the IRA distributions
        var recordIraResults = Tax.RecordIraDistribution(results.newLedger, currentDate, totalIraDistribution);
        results.newLedger = recordIraResults.ledger;
        
        // record the Capital Gains
        var recordCapitalGainsResults = Tax.RecordLongTermCapitalGain(results.newLedger, currentDate, totalCapitalGains);
        results.newLedger = recordCapitalGainsResults.ledger;
        
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.Add(new ReconciliationMessage(currentDate, results.amountSold, "Total investments sold"));
        results.messages.Add(new ReconciliationMessage(currentDate, totalIraDistribution, "Total tax deferred sold"));
        results.messages.Add(new ReconciliationMessage(currentDate, totalTaxableSold, "Total taxable sold"));
        results.messages.Add(new ReconciliationMessage(currentDate, totalCapitalGains, "Total capital gains"));
        results.messages.Add(new ReconciliationMessage(currentDate, totalTaxFree, "Total tax free sold"));
        results.messages.AddRange(depositResults.messages);
        results.messages.AddRange(recordIraResults.messages);
        results.messages.AddRange(recordCapitalGainsResults.messages);
        
        
        return results;
    }
    
    
   

    /// <summary>
    /// sells enough full positions of the provided position type to reach the amountToSell. It does this strategically
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
        
        
        // set up the return tuple
        (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger, 
            List<ReconciliationMessage> messages) results = (
            0M, AccountCopy.CopyBookOfAccounts(bookOfAccounts), Tax.CopyTaxLedger(taxLedger), []);
        
        // calculate the amount of income room we have so we can sell tax deferred account positions first
        var incomeRoom = TaxCalculation.CalculateIncomeRoom(taxLedger, currentDate);

        if (incomeRoom > 0)
        {
            // we have income room. sell tax deferred positions, up to the incomeRoom amount
            var amountToSell = Math.Min(amountNeeded, incomeRoom);
            var withRoomResult = SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType(
                amountToSell, positionType,  InvestmentConfig.SalesOrderWithRoom, results.newBookOfAccounts,
                results.newLedger, currentDate);
            results.amountSold += withRoomResult.amountSold;
            results.newBookOfAccounts = withRoomResult.newBookOfAccounts;
            results.newLedger = withRoomResult.newLedger;
            results.messages.AddRange(withRoomResult.messages);
        }
        if (results.amountSold >= amountNeeded) return results;
        
        // we don't have any more income room and we still have sellin to do. sell tax deferred positions, up to the
        // amountNeeded
        var noRoomResult = SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType(
            amountNeeded - results.amountSold, positionType, InvestmentConfig.SalesOrderWithNoRoom, results.newBookOfAccounts,
            results.newLedger, currentDate);
        results.amountSold += noRoomResult.amountSold;
        results.newBookOfAccounts = noRoomResult.newBookOfAccounts;
        results.newLedger = noRoomResult.newLedger;
        
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
        
        // set up the return tuple
        (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger,
            List<ReconciliationMessage> messages)  results = (
            0m, AccountCopy.CopyBookOfAccounts(bookOfAccounts), Tax.CopyTaxLedger(taxLedger), []);

        var salesOrder = InvestmentConfig.SalesOrderRmd;

        
        
        // try long-term first
        var longTermResult = SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType(
            amountNeeded - results.amountSold, McInvestmentPositionType.LONG_TERM, salesOrder, results.newBookOfAccounts,
            results.newLedger, currentDate);
        results.amountSold += longTermResult.amountSold;
        results.newBookOfAccounts = longTermResult.newBookOfAccounts;
        results.newLedger = longTermResult.newLedger;
        results.messages.AddRange(longTermResult.messages);
        if (results.amountSold >= amountNeeded) return results;

        // now try mid-term
        var midTermResult = SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType(
            amountNeeded - results.amountSold, McInvestmentPositionType.MID_TERM, salesOrder, results.newBookOfAccounts,
            results.newLedger, currentDate);
        results.amountSold += midTermResult.amountSold;
        results.newBookOfAccounts = midTermResult.newBookOfAccounts;
        results.newLedger = midTermResult.newLedger;
        results.messages.AddRange(midTermResult.messages);
        if (results.amountSold >= amountNeeded) return results;
        
        // nothing's left to try. not sure how we got here
        throw new InvalidDataException("RMD: Nothing left to try. Not sure how we got here");
    }
}