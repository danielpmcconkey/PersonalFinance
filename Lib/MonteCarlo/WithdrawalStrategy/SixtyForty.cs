using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.WithdrawalStrategy;

public class SixtyForty : IWithdrawalStrategy
{
    private static McInvestmentAccountType[] _salesOrder = [
        // tax on growth only
        McInvestmentAccountType.TAXABLE_BROKERAGE,
        // tax deferred
        McInvestmentAccountType.TRADITIONAL_401_K,
        McInvestmentAccountType.TRADITIONAL_IRA,
        // no tax, period
        McInvestmentAccountType.HSA,
        McInvestmentAccountType.ROTH_IRA,
        McInvestmentAccountType.ROTH_401_K,
    ];
    
    public (BookOfAccounts accounts, List<ReconciliationMessage> messages)
        InvestExcessCash(LocalDateTime currentDate, BookOfAccounts accounts, CurrentPrices prices,
            Model model, PgPerson person)
    {
        var totalInvestmentAmount = SharedWithdrawalFunctions.CalculateExcessCash(
            currentDate, accounts, model, person);

        if (totalInvestmentAmount <= 0)
        {
            if (!MonteCarloConfig.DebugMode)return (accounts, []);
            return (accounts, [new ReconciliationMessage(
                currentDate, null, "We don't enough spare cash to invest.")]);
        }
        
        // set up return tuple
        (BookOfAccounts accounts, List<ReconciliationMessage> messages) results = (
            AccountCopy.CopyBookOfAccounts(accounts), []);
    
        // we have excess cash, withdraw the cash so we can invest it
        var withdrawalResults = AccountCashManagement.TryWithdrawCash(
            accounts, totalInvestmentAmount, currentDate); 
        if (withdrawalResults.isSuccessful == false) throw new InvalidDataException("Failed to withdraw excess cash");
        results.accounts = withdrawalResults.newAccounts;
        results.messages.AddRange(withdrawalResults.messages);
        
        // now put it in the brokerage account, according to the 60/40 rules
        var investResults = InvestFundsWithoutCashWithdrawal(results.accounts, currentDate,
            totalInvestmentAmount, McInvestmentAccountType.TAXABLE_BROKERAGE, prices, model);
        results.accounts = investResults.accounts;
        
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.AddRange(investResults.messages);
        results.messages.Add(new ReconciliationMessage(
            currentDate, totalInvestmentAmount,$"Invested {totalInvestmentAmount} excess cash into brokerage account"));
        return results;
    }
    /// <summary>
    /// Invest funds per the withdrawal strategy rules. Assumes that the cash has already been taken out of the cash
    /// account. This is because this method is sometimes used for investing extra cash, but also used to invest
    /// paycheck contributions (like 401k, HSA, etc.)
    /// </summary>
    public (BookOfAccounts accounts, List<ReconciliationMessage> messages) 
        InvestFundsWithoutCashWithdrawal(BookOfAccounts accounts, LocalDateTime currentDate, decimal dollarAmount,
            McInvestmentAccountType accountType, CurrentPrices prices, Model model)
    {
        var (amountToMoveLong, amountToMoveMid) = CalculateMovementAmountNeededByPositionType(
            accounts, currentDate, dollarAmount, model);
        
        // set up the return tuple
        (BookOfAccounts accounts, List<ReconciliationMessage> messages) results = (
            AccountCopy.CopyBookOfAccounts(accounts), []);
        if (amountToMoveLong > 0)
        {
            var investLongResults = Investment.InvestFundsByAccountTypeAndPositionType(
                results.accounts, currentDate, amountToMoveLong, McInvestmentPositionType.LONG_TERM, 
                McInvestmentAccountType.TAXABLE_BROKERAGE, prices);
            results.accounts = investLongResults.accounts;
            results.messages.AddRange(investLongResults.messages);
        }

        if (amountToMoveMid > 0)
        {
            var investMidResults = Investment.InvestFundsByAccountTypeAndPositionType(
                results.accounts, currentDate, amountToMoveMid, McInvestmentPositionType.MID_TERM, 
                McInvestmentAccountType.TAXABLE_BROKERAGE, prices);
            results.accounts = investMidResults.accounts;
            results.messages.AddRange(investMidResults.messages);
        }
        
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.Add(new ReconciliationMessage(
            currentDate, dollarAmount,
            $"Invested {amountToMoveLong} (long) and {amountToMoveMid} (mid) in brokerage account"));
        return results;
    }

    public (BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)
        RebalancePortfolio(LocalDateTime currentDate, BookOfAccounts accounts, RecessionStats recessionStats,
            CurrentPrices currentPrices, Model model, TaxLedger ledger, PgPerson person)
    {
        throw new NotImplementedException();
    }
    
    public (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)
        SellInvestmentsToDollarAmount(
            BookOfAccounts accounts, TaxLedger ledger, LocalDateTime currentDate, decimal amountToSell, Model model,
            LocalDateTime? minDateExclusive = null, LocalDateTime? maxDateInclusive = null,
            McInvestmentPositionType? positionTypeOverride = null, McInvestmentAccountType? accountTypeOverride = null
        )
    {
        var salesOrderLong = InvestmentSales.CreateSalesOrderPositionTypeFirst(
            [McInvestmentPositionType.LONG_TERM], 
            accountTypeOverride is null ? _salesOrder : [accountTypeOverride.Value]);
        var salesOrderMid = InvestmentSales.CreateSalesOrderPositionTypeFirst(
            [McInvestmentPositionType.MID_TERM],
            accountTypeOverride is null ? _salesOrder : [accountTypeOverride.Value]);
        
        // if we're overriding the position type, just sell the amount we're given in the position type requested
        if (positionTypeOverride is not null && positionTypeOverride == McInvestmentPositionType.LONG_TERM)
            return InvestmentSales.SellInvestmentsToDollarAmount(
                accounts, ledger, currentDate, amountToSell, salesOrderLong, minDateExclusive, maxDateInclusive);
        if (positionTypeOverride is not null && positionTypeOverride == McInvestmentPositionType.MID_TERM)
            return InvestmentSales.SellInvestmentsToDollarAmount(
                accounts, ledger, currentDate, amountToSell, salesOrderMid, minDateExclusive, maxDateInclusive);
        
        var (amountToMoveLong, amountToMoveMid) = CalculateMovementAmountNeededByPositionType(
           accounts, currentDate, -amountToSell, model); // flip the sign as the calc movement function works for both selling and investing
        // flip the sign back now as the sales function take a positive number as the amount to sell
        var amountToSellLong = -amountToMoveLong;
        var amountToSellMid = -amountToMoveMid;
        
        // set up return tuple
        (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) 
            results = (
                0, AccountCopy.CopyBookOfAccounts(accounts), Tax.CopyTaxLedger(ledger), []
            );
        
       // actually sell stuff
       if (amountToSellLong > 0)
       {
           var longSalesResult = InvestmentSales.SellInvestmentsToDollarAmount(
               results.accounts, results.ledger, currentDate, amountToSellLong, salesOrderLong, minDateExclusive,
               maxDateInclusive);
           results.amountSold += longSalesResult.amountSold;
           results.accounts = longSalesResult.accounts;
           results.ledger = longSalesResult.ledger;
           results.messages.AddRange(longSalesResult.messages);
       }

       if (amountToSellMid > 0)
       {
           var midSalesResult = InvestmentSales.SellInvestmentsToDollarAmount(
               results.accounts, results.ledger, currentDate, amountToSellMid, salesOrderMid, minDateExclusive,
               maxDateInclusive);
           results.amountSold += midSalesResult.amountSold;
           results.accounts = midSalesResult.accounts;
           results.ledger = midSalesResult.ledger;
           results.messages.AddRange(midSalesResult.messages);
       }
       
       if (!MonteCarloConfig.DebugMode) return results;
       
       results.messages.Add(new ReconciliationMessage(
           currentDate, results.amountSold, $"Amount sold in investment accounts"));
        return results;
    }

    public (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)
        SellInvestmentsToRmdAmount(decimal amountNeeded, BookOfAccounts accounts, TaxLedger ledger,
            LocalDateTime currentDate, Model model)
    {
        if (accounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        if (accounts.InvestmentAccounts.Count == 0) return (0, accounts, ledger, []);
        
        // set up return tuple
        (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) 
            results = (
                0, AccountCopy.CopyBookOfAccounts(accounts), Tax.CopyTaxLedger(ledger), []
            );
        
        // try the traditional IRA first
        var tradIraResults = SellInvestmentsToDollarAmount(
            accounts, ledger, currentDate, amountNeeded, model);
        results.amountSold += tradIraResults.amountSold;
        results.accounts = tradIraResults.accounts;
        results.ledger = tradIraResults.ledger;
        results.messages.AddRange(tradIraResults.messages);
        
        var amountStillNeeded = amountNeeded - results.amountSold;
        if(amountStillNeeded <= 0) return results;
        
        // now try the traditional 401k
        var trad401KResults = SellInvestmentsToDollarAmount(
            accounts, ledger, currentDate, amountNeeded, model);
        results.amountSold += trad401KResults.amountSold;
        results.accounts = trad401KResults.accounts;
        results.ledger = trad401KResults.ledger;
        results.messages.AddRange(trad401KResults.messages);
        
        amountStillNeeded = amountNeeded - results.amountSold;
        if(amountStillNeeded <= 0) return results;
        
        // nothing's left to try. not sure how we got here
        throw new InvalidDataException("RMD: Nothing left to try. Not sure how we got here");
        // todo: UT the 60/40 SellInvestmentsToRmdAmount function
    }

    /// <summary>
    /// Determines what percentage of total investments we want in long-term growth stocks 
    /// </summary>
    private decimal CalculateDesiredRatio(LocalDateTime currentDate, Model model)
    {
        const decimal minRatio = 0.6m;
        const decimal maxRatio = 1.0m;
        if(currentDate >= model.RetirementDate) return minRatio;
        if (!Rebalance.CalculateWhetherItsCloseEnoughToRetirementToRebalance(currentDate, model))
            return maxRatio;
        
        // okay, so we're somewhere in that magic land where we need to "ramp up" our mid-term rate
        var percentClimbPerMonth = (maxRatio - minRatio) / model.NumMonthsPriorToRetirementToBeginRebalance;
        var rebalanceStart = model.RetirementDate
            .PlusMonths(-model.NumMonthsPriorToRetirementToBeginRebalance);
        var spanSinceRebalanceStart = currentDate - rebalanceStart;
        var monthsSinceRebalanceStart = (spanSinceRebalanceStart.Years * 12) + spanSinceRebalanceStart.Months;
        var percentClimb = percentClimbPerMonth * monthsSinceRebalanceStart;
        return maxRatio - percentClimb;
    }
    
    private (decimal longTermAmount, decimal midTermAmount, decimal ratio) 
        CalculateExistingRatio(BookOfAccounts accounts)
    {
        var longTermAmount = AccountCalculation.CalculateLongBucketTotalBalance(accounts);
        var midTermAmount = AccountCalculation.CalculateMidBucketTotalBalance(accounts);
        var ratio = longTermAmount / (longTermAmount + midTermAmount);
        return (longTermAmount, midTermAmount, ratio);
    }

    /// <summary>
    /// Analyzes current long / mid mix, how much you want to move (buy or sell), and determines how much of each bucket
    /// would need to move to reach ideal 60/40 state. Positive numbers assume purchase. Negative assumes sale.
    /// Method is public so I can unit test it independently.
    /// </summary>
    public (decimal diffLong, decimal diffMid) 
        CalculateMovementAmountNeededByPositionType(
            BookOfAccounts accounts, LocalDateTime currentDate, decimal movementAmount, Model model)
    {
        if(movementAmount == 0) return (0, 0);
        var isSale = movementAmount < 0;
        var targetRatio = CalculateDesiredRatio(currentDate, model);
        var (currentLongTermAmount, curretMidTermAmount, ratio) = CalculateExistingRatio(accounts);
        // determive what your ideal movement would be; what you would have to sell or buy to reach the target ratio 
        var finalCombinedAfterMovement = currentLongTermAmount + curretMidTermAmount + movementAmount;
        var idealLongAfterMovement = finalCombinedAfterMovement * targetRatio;
        var idealMidAfterMovement = finalCombinedAfterMovement * (1 - targetRatio);
        var idealLongMovement = idealLongAfterMovement - currentLongTermAmount;
        var idealMidMovement = idealMidAfterMovement - curretMidTermAmount;
        
        // calculate and return buy values first as that's way easier
        if (!isSale)
        {
            // if both ideal movement values are positive, return the ideal values
            if(idealLongMovement >= 0 && idealMidMovement >= 0 ) return (idealLongMovement, idealMidMovement);
            // if one is positive and the other is negative, put everything into the positive one
            var isLongMovementNegative = idealLongMovement < 0;
            var isMidMovementNegative = idealMidMovement < 0;
            var finalLongMovement = isMidMovementNegative ? movementAmount : 0;
            var finalMidMovement = isLongMovementNegative ? movementAmount : 0;
            return (finalLongMovement, finalMidMovement);
        }
        
        // now do sales, which are more complex because you have to make sure you have the funds as well
        else
        {
            // if you either can't afford the total sale, or it'll take you to zero, sell everything you got
            if (currentLongTermAmount + curretMidTermAmount <= -movementAmount) 
                return (-currentLongTermAmount, -curretMidTermAmount);
            
            // set up the variables to determine which route to take
            var isLongMovementNegative = idealLongMovement < 0;
            var isMidMovementNegative = idealMidMovement < 0;
            var canYouAffordTheLongSale = currentLongTermAmount >= -idealLongMovement;
            var canYouAffordTheMidSale = curretMidTermAmount >= -idealMidMovement;
            var youHaveBothAndBothAreNegative = canYouAffordTheLongSale && canYouAffordTheMidSale &&
                                                isLongMovementNegative && isMidMovementNegative;
            
            // if you can afford the total sale, and each of your current balances would support the ideal sale, return the ideal
            if (youHaveBothAndBothAreNegative) return (idealLongMovement, idealMidMovement);
            
            // calculate long and mid independently
            
            // if you can afford the long sale, take the max between the movement amount and the ideal long
            var finalLongMovement = (isLongMovementNegative && canYouAffordTheLongSale) ?
                Math.Max(movementAmount, idealLongMovement)
                : 0m;
            // if you can afford the mid sale, take the max between the movement amount and the ideal mid
            var finalMidMovement = (isMidMovementNegative && canYouAffordTheMidSale) ?
                Math.Max(movementAmount, idealMidMovement)
                : 0m;
            
            return (finalLongMovement, finalMidMovement);
        }
        
        
    }
}