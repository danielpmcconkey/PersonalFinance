using Lib.DataTypes.MonteCarlo;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Rebalance
{
    #region Calculation functions
    
    public static bool CalculateWhetherItsBucketRebalanceTime(LocalDateTime currentDate, Model model)
    {
        // check whether our frequency aligns to the calendar
        // monthly is a free-bee
        if(model.RebalanceFrequency == RebalanceFrequency.MONTHLY) return true;
        // quarterly and yearly need to be determined
        var currentMonthNum = currentDate.Month - 1; // we want it zero-indexed to make the modulus easier
        
        var modulus = model.RebalanceFrequency switch
        {
            RebalanceFrequency.MONTHLY => 1, // we already met this case
            RebalanceFrequency.QUARTERLY => 3,
            RebalanceFrequency.YEARLY => 12,
            _ => 0 // shouldn't happen but we get warnings if we don't have a default
        };
        return currentMonthNum % modulus == 0;
    }
    
    public static bool CalculateWhetherItsCloseEnoughToRetirementToRebalance(LocalDateTime currentDate, Model model)
    {
        // check whether it's close enough to retirement to think about rebalancing
        var rebalanceBegin = model.RetirementDate
            .PlusMonths(-1 * model.NumMonthsPriorToRetirementToBeginRebalance);
        return currentDate >= rebalanceBegin;
    }
    
    

    #endregion Calculation functions
    
    #region asset movement functions
    
    public static (decimal amountMoved, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) 
        MoveBetweenTaxDeferredAccountsWithoutTaxConsequences(decimal amountToMove, BookOfAccounts accounts, 
            TaxLedger ledger, LocalDateTime currentDate, CurrentPrices prices, McInvestmentPositionType sourceType)
    {
        /*
         * with either tax deferred or tax free accounts, you can sell and buy inside the account without tax
         * consequences. during rebalance, this is ideal as it doesn't create any income. if we're moving from mid to
         * long, go ahead and use tax free accounts. otherwise, we want to leave growth stocks in those accounts
         * 
         */
        var destinationType = sourceType == McInvestmentPositionType.LONG_TERM ?
            McInvestmentPositionType.MID_TERM :
            McInvestmentPositionType.LONG_TERM;
        var newPriceAtSource = sourceType == McInvestmentPositionType.LONG_TERM ?
            prices.CurrentLongTermInvestmentPrice :
            prices.CurrentMidTermInvestmentPrice;
        var newPriceAtDestination = sourceType == McInvestmentPositionType.LONG_TERM ?
            prices.CurrentMidTermInvestmentPrice :
            prices.CurrentLongTermInvestmentPrice;
        
        // set up return tuple
        (decimal amountMoved, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) 
            results = (0m, AccountCopy.CopyBookOfAccounts(accounts), Tax.CopyTaxLedger(ledger), []); 
        
        var accountTypes = new List<McInvestmentAccountType>()
        {
            McInvestmentAccountType.TRADITIONAL_401_K, 
            McInvestmentAccountType.TRADITIONAL_IRA
        };
        if (destinationType == McInvestmentPositionType.LONG_TERM)
        {
            accountTypes.Add(McInvestmentAccountType.ROTH_IRA);
            accountTypes.Add(McInvestmentAccountType.ROTH_401_K);
            accountTypes.Add(McInvestmentAccountType.HSA);
        }
        
        // pull all eligible positions
        var query = from account in results.accounts.InvestmentAccounts
                where (accountTypes.Contains(account.AccountType)) 
                from position in account.Positions
                where (position.IsOpen &&
                       position.InvestmentPositionType == sourceType)
                orderby (position.Entry)
                select (account, position)
            ;
        
        // loop over the pull types until we have the bucks
        
        foreach (var (account, position) in query)
        {
            if (results.amountMoved >= amountToMove) break;
            
            
            if (position.CurrentValue <= amountToMove - results.amountMoved)
            {
                // this consumes the entire position, set its type and change its price
                results.amountMoved += position.CurrentValue;
                // create a mid-term position
                position.InvestmentPositionType = destinationType;
            
                // change its price
                var oldValue = position.CurrentValue;
                var newQuantity = oldValue / newPriceAtDestination;
                position.Price = newPriceAtDestination;
                position.Quantity = newQuantity;
                var newValue = position.CurrentValue;
                if(Math.Round(oldValue, 4) != Math.Round(newValue, 4))
                    throw new InvalidDataException("Failed to set price of converting between long-term and mid-term");
            }
            else
            {
                // this only partially consumes the position.
                
                // determine how much to convert and how much to keep and what their prices and quantities would be
                var amountToConvert = amountToMove - results.amountMoved;
                var amountToKeep = position.CurrentValue - amountToConvert;
                var priceAtConvert = newPriceAtDestination;
                var priceAtKeep = newPriceAtSource;
                var quantityAtConvert = amountToConvert / priceAtConvert;
                var quantityAtKeep = amountToKeep / priceAtKeep;
                
                // diminish the existing position and change its type to destination
                position.InvestmentPositionType = destinationType;
                position.Price = priceAtConvert;
                position.Quantity = quantityAtConvert;
                
                // create a new position of source type with the keep values 
                var newKeepPosition = new McInvestmentPosition()
                {
                    Name = "Tax deferred keep position",
                    Id = Guid.NewGuid(),
                    Entry = currentDate,
                    InvestmentPositionType = sourceType,
                    Price = priceAtKeep,
                    Quantity = quantityAtKeep,
                    InitialCost = amountToKeep, // doesn't matter for tax deferred. it's all income
                    IsOpen = true,
                };
                account.Positions.Add(newKeepPosition);
                results.amountMoved += amountToConvert;
            }
        }
        return results;
    }
    
    public static (decimal amountMoved, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) 
        MoveLongToMidWithoutTaxConsequences(decimal amountToMove, BookOfAccounts accounts, TaxLedger ledger, 
            LocalDateTime currentDate, CurrentPrices prices)
    {
        return MoveBetweenTaxDeferredAccountsWithoutTaxConsequences(
            amountToMove, accounts, ledger, currentDate, prices, McInvestmentPositionType.LONG_TERM);
    }
    
    public static (decimal amountMoved, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) 
        MoveMidToLongWithoutTaxConsequences(decimal amountToMove, BookOfAccounts accounts, TaxLedger ledger, 
            LocalDateTime currentDate, CurrentPrices prices)
    {
        return MoveBetweenTaxDeferredAccountsWithoutTaxConsequences(
            amountToMove, accounts, ledger, currentDate, prices, McInvestmentPositionType.MID_TERM);
    }

    
    #endregion asset movement functions
}