using Lib.DataTypes.MonteCarlo;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class InvestmentSales
{
    /// <summary>
    /// sells enough full positions of the provided account and position type to reach the amountToSell. It deposits
    /// proceeds into the cash account.
    /// </summary>
    /// <returns>the exact amount sold, a new book of accounts, and a new ledger</returns>
    private static (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger)
        SellInvestmentsToDollarAmountByAccountTypeAndPositionType(
            decimal amountToSell, McInvestmentAccountType accountType, McInvestmentPositionType positionType,
            BookOfAccounts bookOfAccounts, TaxLedger taxLedger, LocalDateTime currentDate)
    {
        (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger) results = (
            0, 
            AccountCopy.CopyBookOfAccounts(bookOfAccounts),
            Tax.CopyTaxLedger(taxLedger)
        );
        
        // grab the relevant positions
        var positions = Investment.GetInvestmentPositionsToSellByAccountTypeAndPositionType(
            results.newBookOfAccounts.InvestmentAccounts, accountType, positionType, currentDate);
        
        foreach (var p in positions)
        {
            if(p.IsOpen == false) continue;
            if (results.amountSold >= amountToSell) break;

            // sell the whole thing; we should have split these
            // up into small enough pieces that that's okay
            var localResult = SellInvestmentPosition(
                p, results.newBookOfAccounts, currentDate, results.newLedger, accountType);
            
            results.amountSold += localResult.saleAmount;
            results.newBookOfAccounts = localResult.newBookOfAccounts;
            results.newLedger = localResult.newLedger;
        }
        return results;
    }
    
    /// <summary>
    /// sells enough full positions of the provided position type to reach the amountToSell, using the provided account
    /// type order. It deposits proceeds into the cash account.
    /// </summary>
    /// <returns>the exact amount sold, a new book of accounts, and a new ledger</returns>
    private static (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger) 
        SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType(
            decimal amountToSell, McInvestmentPositionType positionType, McInvestmentAccountType[] typeOrder,
            BookOfAccounts bookOfAccounts, TaxLedger taxLedger, LocalDateTime currentDate)
    {
        (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger) results = (
            0, 
            AccountCopy.CopyBookOfAccounts(bookOfAccounts),
            Tax.CopyTaxLedger(taxLedger)
            );
        foreach (var accountType in typeOrder)
        {
            decimal amountLeft = amountToSell - results.amountSold;
            if (amountLeft <= 0) break;
            var localResults = SellInvestmentsToDollarAmountByAccountTypeAndPositionType(
                amountLeft, accountType, positionType, results.newBookOfAccounts, results.newLedger, currentDate);
            results.amountSold += localResults.amountSold;
            results.newBookOfAccounts = localResults.newBookOfAccounts;
            results.newLedger = localResults.newLedger;
        }
        return results;
    }

    /// <summary>
    /// sells enough full positions of the provided position type to reach the amountToSell. It does this strategically
    /// by first selling tax deferred positions until you've reached the annual income head room to avoid going into
    /// higher tax brackets. It deposits proceeds into the cash account.
    /// </summary>
    /// <returns>the exact amount sold, a new book of accounts, and a new ledger</returns>
    public static (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger) 
        SellInvestmentsToDollarAmountByPositionType(decimal amountNeeded, McInvestmentPositionType positionType,
            BookOfAccounts bookOfAccounts, TaxLedger taxLedger, LocalDateTime currentDate)
    {
        if (bookOfAccounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        if (bookOfAccounts.InvestmentAccounts.Count == 0) return (0, bookOfAccounts, taxLedger);
        
        
        // set up the return tuple
        (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger) results = (
            0M, AccountCopy.CopyBookOfAccounts(bookOfAccounts), Tax.CopyTaxLedger(taxLedger));
        
        // calculate the amount of income room we have so we can sell tax deferred account positions first
        var incomeRoom = TaxCalculation.CalculateIncomeRoom(taxLedger, currentDate.Year);

        // set up an "empty" localresult so you can use that variable name multiple times later
        (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger) localResult = (0, bookOfAccounts, taxLedger);
        if (incomeRoom > 0)
        {
            // we have income room. sell tax deferred positions, up to the incomeRoom amount
            var amountToSell = Math.Min(amountNeeded, incomeRoom);
            localResult = SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType(
                amountToSell, positionType,  StaticConfig.InvestmentConfig._salesOrderWithRoom, results.newBookOfAccounts,
                results.newLedger, currentDate);
            results.amountSold += localResult.amountSold;
            results.newBookOfAccounts = localResult.newBookOfAccounts;
            results.newLedger = localResult.newLedger;
        }
        if (results.amountSold >= amountNeeded) return results;
        
        // we don't have any more income room and we still have sellin to do. sell tax deferred positions, up to the
        // amountNeeded
        localResult = SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType(
            amountNeeded - results.amountSold, positionType, StaticConfig.InvestmentConfig._salesOrderWithNoRoom, results.newBookOfAccounts,
            results.newLedger, currentDate);
        results.amountSold += localResult.amountSold;
        results.newBookOfAccounts = localResult.newBookOfAccounts;
        results.newLedger = localResult.newLedger;
        
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(null, results.amountSold, $"Amount sold in investment accounts");
        }

        return results;
    }
    
    /// <summary>
    /// sells enough full positions of the provided position type to reach the amountToSell, but specifically in the RMD
    /// sales accountd order. It ignores the income "head room". It deposits proceeds into the cash account.
    /// </summary>
    /// <returns>the exact amount sold, a new book of accounts, and a new ledger</returns>
    public static (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger) 
        SellInvestmentsToRmdAmount(decimal amountNeeded, BookOfAccounts bookOfAccounts, TaxLedger taxLedger, 
            LocalDateTime currentDate)
    {
        if (bookOfAccounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        if (bookOfAccounts.InvestmentAccounts.Count == 0) return (0, bookOfAccounts, taxLedger);
        
        // set up the return tuple
        (decimal amountSold, BookOfAccounts newBookOfAccounts, TaxLedger newLedger)  results = (
            0m, AccountCopy.CopyBookOfAccounts(bookOfAccounts), Tax.CopyTaxLedger(taxLedger));

        var salesOrder = StaticConfig.InvestmentConfig._salesOrderRmd;
        
        // try long-term first
        var localResult = SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType(
            amountNeeded - results.amountSold, McInvestmentPositionType.LONG_TERM, salesOrder, results.newBookOfAccounts,
            results.newLedger, currentDate);
        results.amountSold += localResult.amountSold;
        results.newBookOfAccounts = localResult.newBookOfAccounts;
        results.newLedger = localResult.newLedger;
        if (results.amountSold >= amountNeeded) return results;

        // now try mid-term
        localResult = SellInvestmentsToDollarAmountByPositionTypeOrderedByAccountType(
            amountNeeded - results.amountSold, McInvestmentPositionType.MID_TERM, salesOrder, results.newBookOfAccounts,
            results.newLedger, currentDate);
        results.amountSold += localResult.amountSold;
        results.newBookOfAccounts = localResult.newBookOfAccounts;
        results.newLedger = localResult.newLedger;
        if (results.amountSold >= amountNeeded) return results;
        
        
        // nothing's left to try. not sure how we got here
        throw new InvalidDataException("RMD: Nothing left to try. Not sure how we got here");
    }
    
    /// <summary>
    /// Sells the entire position and deposits proceeds into the cash account
    /// </summary>
    public static (decimal saleAmount, BookOfAccounts newBookOfAccounts, TaxLedger newLedger)
        SellInvestmentPosition(McInvestmentPosition position, BookOfAccounts bookOfAccounts, LocalDateTime currentDate,
            TaxLedger taxLedger, McInvestmentAccountType accountType)
    {
        if (position.IsOpen == false) return (0, bookOfAccounts, taxLedger);
        
        // set up the return tuple
        (decimal saleAmount, BookOfAccounts newBookOfAccounts, TaxLedger newLedger)
            results = (
                position.CurrentValue,
                AccountCopy.CopyBookOfAccounts(bookOfAccounts),
                Tax.CopyTaxLedger(taxLedger)
            );
        results.newLedger = Tax.RecordInvestmentSale(taxLedger, currentDate, position, accountType);
        position.Quantity = 0;
        position.IsOpen = false;
        results.newBookOfAccounts = AccountCashManagement.DepositCash(results.newBookOfAccounts, results.saleAmount, currentDate);
        results.newBookOfAccounts = RemovePositionFromBookOfAccounts(position, results.newBookOfAccounts);
        
        // if it was a tax deferred account, record the sale for RMD purposes
        if (accountType == McInvestmentAccountType.TRADITIONAL_401_K ||
            accountType == McInvestmentAccountType.TRADITIONAL_IRA)
        {
            results.newLedger = Tax.RecordRmdDistribution(results.newLedger, currentDate, results.saleAmount);
        }
        return results;
    }

    /// <summary>
    /// rebuilds the book of accounts, but excludes the removed position (by ID) this makes sure that we don't
    /// accidentally end up with a default account that contains a position that isn't also in the investment accounts
    /// position list. Might not be necessary, but there's a lot of editing of positions w/out their accounts and books.
    /// so this is a "better safe than sorry" play
    /// </summary>
    public static BookOfAccounts RemovePositionFromBookOfAccounts(McInvestmentPosition position,
        BookOfAccounts bookOfAccounts)
    {
        BookOfAccounts results = AccountCopy.CopyBookOfAccounts(bookOfAccounts);
        var debtAccounts = AccountCopy.CopyDebtAccounts(bookOfAccounts.DebtAccounts);
        List<McInvestmentAccount> investmentAccounts = [];
        foreach (var account in bookOfAccounts.InvestmentAccounts)
        {
            var newAccount = AccountCopy.CopyInvestmentAccount(account);
            newAccount.Positions = [];
            foreach (var p in account.Positions)
            {
                if (p.Id == position.Id) continue;
                newAccount.Positions.Add(p);
            }
            investmentAccounts.Add(newAccount);

        }
        
        return Account.CreateBookOfAccounts(investmentAccounts, debtAccounts);
    }
    
}