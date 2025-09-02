using Lib.DataTypes;
using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
using Lib.StaticConfig;
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
        MoveLongToMidWithoutTaxConsequences(decimal amountToMove, BookOfAccounts accounts, TaxLedger ledger, 
            LocalDateTime currentDate, CurrentPrices prices)
    {
        /*
         * with either tax deferred or tax free accounts, you can sell and buy inside the account without tax
         * consequences. during rebalance from long to mid, this is ideal as it doesn't create any income
         */
        // set up return tuple
        (decimal amountMoved, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) 
            results = (0m, AccountCopy.CopyBookOfAccounts(accounts), Tax.CopyTaxLedger(ledger), []); 
        
        // pull all eligible positions
        var query = from account in results.accounts.InvestmentAccounts
                where (account.AccountType is McInvestmentAccountType.TRADITIONAL_401_K || 
                       account.AccountType is McInvestmentAccountType.TRADITIONAL_IRA) 
                from position in account.Positions
                where (position.IsOpen &&
                       position.InvestmentPositionType == McInvestmentPositionType.LONG_TERM)
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
                position.InvestmentPositionType = McInvestmentPositionType.MID_TERM;
            
                // change its price
                var oldValue = position.CurrentValue;
                var newPrice = prices.CurrentMidTermInvestmentPrice;
                var newQuantity = oldValue / newPrice;
                position.Price = newPrice;
                position.Quantity = newQuantity;
                var newValue = position.CurrentValue;
                if(Math.Round(oldValue, 4) != Math.Round(newValue, 4))
                    throw new InvalidDataException("Failed to set price of long-term investment to mid-term");
            }
            else
            {
                // this only partially consumes the position; create a new position of type mid and diminish the
                // existing position
                var amountToConvert = amountToMove - results.amountMoved;
                var amountToKeep = position.CurrentValue - amountToConvert;
                var priceAtMid = prices.CurrentMidTermInvestmentPrice;
                var priceAtLong = prices.CurrentLongTermInvestmentPrice;
                var quantityAtMid = amountToConvert / priceAtMid;
                var quantityAtLong = amountToKeep / priceAtLong;
                position.InvestmentPositionType = McInvestmentPositionType.MID_TERM;
                position.Price = priceAtMid;
                position.Quantity = quantityAtMid;
                var newLongPosition = new McInvestmentPosition()
                {
                    Name = "Tax deferred long term position",
                    Id = Guid.NewGuid(),
                    Entry = currentDate,
                    InvestmentPositionType = McInvestmentPositionType.LONG_TERM,
                    Price = priceAtLong,
                    Quantity = quantityAtLong,
                    InitialCost = amountToKeep, // doesn't matter for tax deferred. it's all income
                    IsOpen = true,
                };
                results.accounts.TraditionalIra.Positions.Add(newLongPosition);
                results.amountMoved += amountToConvert;
            }
        }
        return results;
    }

    
    #endregion asset movement functions
}