using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.WithdrawalStrategy;

public static class SharedWithdrawalFunctions
{
    public static (decimal amountMoved, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)
        MoveFromInvestmentToCash(BookOfAccounts accounts, decimal cashNeeded, McInvestmentPositionType positionType,
            LocalDateTime currentDate, TaxLedger ledger, Lib.DataTypes.MonteCarlo.Model model)
    {
        // set up the return tuple
        (decimal amountMoved, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) results = (
            0m, AccountCopy.CopyBookOfAccounts(accounts), Tax.CopyTaxLedger(ledger), []);

        var salesResults = model.WithdrawalStrategy.SellInvestmentsToDollarAmount(
            accounts, ledger, currentDate, cashNeeded, null, currentDate.PlusYears(-1),
            positionType, null);
        results.amountMoved = salesResults.amountSold;
        results.accounts = salesResults.accounts;
        results.ledger = salesResults.ledger;
        
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.AddRange(salesResults.messages);
        results.messages.Add(new ReconciliationMessage(
            currentDate, results.amountMoved, $"Rebalance: Selling {positionType} investment"));
        return results;
    }
    
    
    
    

    
    
    public static (decimal amountSold, BookOfAccounts newAccounts, TaxLedger newLedger, 
        List<ReconciliationMessage> messages) 
        SellInOrder(decimal cashNeeded, McInvestmentPositionType[] pullOrder, BookOfAccounts bookOfAccounts,
            TaxLedger taxLedger, LocalDateTime currentDate, Model model)
    {
        if(cashNeeded <= 0) return (0m, bookOfAccounts, taxLedger, []);
        // set up return tuple
        (decimal amountSold, BookOfAccounts newAccounts, TaxLedger newLedger, List<ReconciliationMessage> messages) 
            results = (0m, AccountCopy.CopyBookOfAccounts(bookOfAccounts), Tax.CopyTaxLedger(taxLedger), []); 
        
        // loop over the pull types until we have the bucks
        
        foreach (var positionType in pullOrder)
        {
            if (cashNeeded <= results.amountSold) return results;
            
            var localResults = MoveFromInvestmentToCash(
                results.newAccounts, cashNeeded - results.amountSold, positionType, currentDate, 
                results.newLedger, model);
            results.amountSold += localResults.amountMoved;
            results.newAccounts = localResults.accounts;
            results.newLedger = localResults.ledger;
            results.messages.AddRange(localResults.messages);
        }
        return results;
    }
    
    /// <summary>
    /// sells enough full positions of the provided position type to reach the amountToSell, but specifically in the RMD
    /// sales account order. It ignores the income "head room". It deposits proceeds into the cash account.
    /// </summary>
    /// <returns>the exact amount sold, a new book of accounts, and a new ledger</returns>
    public static (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) 
        SellInvestmentsToRmdAmountStandardBucketsStrat(decimal amountNeeded, BookOfAccounts bookOfAccounts, TaxLedger taxLedger, 
            LocalDateTime currentDate)
    {
        if (bookOfAccounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        if (bookOfAccounts.InvestmentAccounts.Count == 0) return (0, bookOfAccounts, taxLedger, []);
        
        (McInvestmentPositionType positionType, McInvestmentAccountType accountType)[] salesOrder = [
            (McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TRADITIONAL_401_K),
            (McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TRADITIONAL_IRA),
            (McInvestmentPositionType.MID_TERM, McInvestmentAccountType.TRADITIONAL_401_K),
            (McInvestmentPositionType.MID_TERM, McInvestmentAccountType.TRADITIONAL_IRA),
        ];
        
        var salesResult = InvestmentSales.SellInvestmentsToDollarAmount(
            AccountCopy.CopyBookOfAccounts(bookOfAccounts),
            Tax.CopyTaxLedger(taxLedger),
            currentDate, 
            amountNeeded, 
            salesOrder,
            null, 
            currentDate.PlusYears(-1));
        if (salesResult.amountSold >= amountNeeded) return salesResult;
        if (Math.Abs(salesResult.amountSold - amountNeeded) < 1m) return salesResult; // call it a wash due to floating point math
        
        // nothing's left to try. not sure how we got here
        throw new InvalidDataException("RMD: Nothing left to try. Not sure how we got here");
    }
    
    
    
    
    
    
    
}