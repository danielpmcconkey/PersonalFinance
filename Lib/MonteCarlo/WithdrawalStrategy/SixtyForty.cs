using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.WithdrawalStrategy;

public class SixtyForty : IWithdrawalStrategy
{
    // todo: adjust the 60/40 strat to have a varible long target
    
    private static readonly McInvestmentAccountType[] SalesOrder = [
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
        if (!Rebalance.CalculateWhetherItsCloseEnoughToRetirementToRebalance(currentDate, model))
        {
            if (!MonteCarloConfig.DebugMode) return (accounts, ledger, []);
            return (accounts, ledger, [new ReconciliationMessage(
                currentDate, null, "Not close enough to retirement yet to rebalance")]);
        };
        if (!Rebalance.CalculateWhetherItsBucketRebalanceTime(currentDate, model))
        {
            if (!MonteCarloConfig.DebugMode) return (accounts, ledger, []);
            return (accounts, ledger, [new ReconciliationMessage(
                currentDate, null, "Not a rebalancing month")]);
        };
        
        // set up return tuple
        (BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages) results = (
            AccountCopy.CopyBookOfAccounts(accounts), Tax.CopyTaxLedger(ledger), []);
        if (MonteCarloConfig.DebugMode)
        {
            results.messages.Add(new ReconciliationMessage(
                currentDate, null, "Rebalance: time to move funds"));
        }
        
        var cashNeededOnHand =
            Spend.CalculateCashNeedForNMonths(model, person, results.accounts, currentDate,
                model.NumMonthsCashOnHand);
        var actualCashOnHand = AccountCalculation.CalculateCashBalance(results.accounts);
        var cashStillNeeded = Math.Max(0, cashNeededOnHand - actualCashOnHand);

        if (cashStillNeeded > 0)
        {
            var rebalanceCashResults = SellInvestmentsToDollarAmount(
                results.accounts, results.ledger, currentDate, cashStillNeeded, model, null,
                currentDate.PlusYears(-1));
            results.accounts = rebalanceCashResults.accounts;
            results.ledger = rebalanceCashResults.ledger;
            results.messages.AddRange(rebalanceCashResults.messages);
        }
        
        // now set your mid and long to target
        var (idealLongMovement, idealMidMovement) = CalculateRebalanceInvestmentTargets(
            results.accounts, currentDate, model);
        
        // check if the juice is worth the squeeze
        const decimal minJuice = 1000m;
        if (Math.Abs(idealLongMovement) < minJuice && Math.Abs(idealMidMovement) < minJuice) return results;

        // try to do it without tax consequence first
        switch (idealLongMovement)
        {
            case > 0 when idealMidMovement < 0:
            {
                // sell mid, buy long
                var taxFreeMove = Rebalance.MoveMidToLongWithoutTaxConsequences(
                    idealLongMovement, results.accounts, results.ledger, currentDate, currentPrices);
                results.accounts = taxFreeMove.accounts;
                results.messages.AddRange(taxFreeMove.messages);
                break;
            }
            case < 0 when idealMidMovement > 0:
            {
                // sell long, buy mid
                var taxFreeMove = Rebalance.MoveLongToMidWithoutTaxConsequences(
                    idealMidMovement, results.accounts, results.ledger, currentDate, currentPrices);
                results.accounts = taxFreeMove.accounts;
                results.messages.AddRange(taxFreeMove.messages);
                break;
            }
        }
        
        // recalc your mid and long to target
        (idealLongMovement, idealMidMovement) = CalculateRebalanceInvestmentTargets(
            results.accounts, currentDate, model);
        
        // check if the juice is worth the squeeze
        if (Math.Abs(idealLongMovement) < minJuice && Math.Abs(idealMidMovement) < minJuice) return results;
        
        // gotta move stuff around in your brokerage account. this involves selling and buying and produces a taxable
        // event
        var movementAmount = Math.Max(idealLongMovement, idealMidMovement);
        var sourceType = idealLongMovement > idealMidMovement
            ? McInvestmentPositionType.MID_TERM
            : McInvestmentPositionType.LONG_TERM;
        var destinationType = idealLongMovement > idealMidMovement
            ? McInvestmentPositionType.LONG_TERM
            : McInvestmentPositionType.MID_TERM;
        
        // sell source
        var sellResults = SellInvestmentsToDollarAmount(
            results.accounts, results.ledger, currentDate, movementAmount, model, null,
            currentDate.PlusYears(-1), sourceType, McInvestmentAccountType.TAXABLE_BROKERAGE);
        results.accounts = sellResults.accounts;
        results.ledger = sellResults.ledger;
        results.messages.AddRange(sellResults.messages);
        
        if (sellResults.amountSold <= 0) return results; // sometimes, there was nothing to sell, so don't try to buy
            
        // withdraw the cash
        var withdrawalResult = AccountCashManagement.TryWithdrawCash(
            results.accounts, sellResults.amountSold, currentDate);
        results.accounts = withdrawalResult.newAccounts;
        if(!withdrawalResult.isSuccessful) throw new InvalidDataException("Failed to withdraw cash after selling");
        results.messages.AddRange(withdrawalResult.messages);
        // buy destination
        var buyResult = Investment.InvestFundsByAccountTypeAndPositionType(
            results.accounts, currentDate, sellResults.amountSold, destinationType, 
            McInvestmentAccountType.TAXABLE_BROKERAGE, currentPrices);
        results.accounts = buyResult.accounts;
        results.messages.AddRange(buyResult.messages);
        
        // you did what you could
        return results;
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
            accountTypeOverride is null ? SalesOrder : [accountTypeOverride.Value]);
        var salesOrderMid = InvestmentSales.CreateSalesOrderPositionTypeFirst(
            [McInvestmentPositionType.MID_TERM],
            accountTypeOverride is null ? SalesOrder : [accountTypeOverride.Value]);
        
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

       // sometimes, we have enough to sell in the filter criteria, but there's an unbalance elsewhere that skews our
       // cals and makes us sell less than we're asking for in the name of proper balance. We're making the decision
       // here that it's more important to sell what's asked for than to land on the most perfect balance 
       if (results.amountSold < amountToSell)
       {
           var remainder = amountToSell - results.amountSold;
           var salesOrderCombined = salesOrderLong.Concat(salesOrderMid).ToArray();
           var lastChanceSalesResult = InvestmentSales.SellInvestmentsToDollarAmount(
               results.accounts, results.ledger, currentDate, remainder, salesOrderCombined, minDateExclusive,
               maxDateInclusive);
           results.amountSold += lastChanceSalesResult.amountSold;
           results.accounts = lastChanceSalesResult.accounts;
           results.ledger = lastChanceSalesResult.ledger;
           results.messages.AddRange(lastChanceSalesResult.messages);
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
        // you need to sell long and mid separately so it doesn't try to balance the sale and end up selling less than
        // you need
        
        var tradIraResultsLong = SellInvestmentsToDollarAmount(
            results.accounts, results.ledger, currentDate, amountNeeded, model, null, null, 
            McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TRADITIONAL_IRA);
        results.amountSold += tradIraResultsLong.amountSold;
        results.accounts = tradIraResultsLong.accounts;
        results.ledger = tradIraResultsLong.ledger;
        results.messages.AddRange(tradIraResultsLong.messages);
        var amountStillNeeded = amountNeeded - results.amountSold;
        if(Math.Round(amountStillNeeded, 2) <= 0) return results;
        
        var tradIraResultsMid = SellInvestmentsToDollarAmount(
            results.accounts, results.ledger, currentDate, amountNeeded, model, null, null, 
            McInvestmentPositionType.MID_TERM, McInvestmentAccountType.TRADITIONAL_IRA);
        results.amountSold += tradIraResultsMid.amountSold;
        results.accounts = tradIraResultsMid.accounts;
        results.ledger = tradIraResultsMid.ledger;
        results.messages.AddRange(tradIraResultsMid.messages);
        amountStillNeeded = amountNeeded - results.amountSold;
        if(Math.Round(amountStillNeeded, 2) <= 0) return results;
        
        var trad401KResultsLong = SellInvestmentsToDollarAmount(
            results.accounts, results.ledger, currentDate, amountNeeded, model, null, null, 
            McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.TRADITIONAL_401_K);
        results.amountSold += trad401KResultsLong.amountSold;
        results.accounts = trad401KResultsLong.accounts;
        results.ledger = trad401KResultsLong.ledger;
        results.messages.AddRange(trad401KResultsLong.messages);
        amountStillNeeded = amountNeeded - results.amountSold;
        if(amountStillNeeded <= 0) return results;
        
        var trad401KResultsMid = SellInvestmentsToDollarAmount(
            results.accounts, results.ledger, currentDate, amountNeeded, model, null, null, 
            McInvestmentPositionType.MID_TERM, McInvestmentAccountType.TRADITIONAL_401_K);
        results.amountSold += trad401KResultsMid.amountSold;
        results.accounts = trad401KResultsMid.accounts;
        results.ledger = trad401KResultsMid.ledger;
        results.messages.AddRange(trad401KResultsMid.messages);
        amountStillNeeded = amountNeeded - results.amountSold;
        if(amountStillNeeded <= 0) return results;
        
        // nothing's left to try. not sure how we got here
        throw new InvalidDataException("RMD: Nothing left to try. Not sure how we got here");
        // todo: UT the 60/40 SellInvestmentsToRmdAmount function
    }

    /// <summary>
    /// Determines what percentage of total investments we want in long-term growth stocks 
    /// </summary>
    private decimal 
        CalculateDesiredRatio(LocalDateTime currentDate, Model model)
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
        var ratio = (longTermAmount + midTermAmount == 0m)
            ? 0m
            : longTermAmount / (longTermAmount + midTermAmount);
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

    private (decimal idealLongMovement, decimal idealShortMovement) 
        CalculateRebalanceInvestmentTargets(BookOfAccounts accounts, LocalDateTime currentDate, Model model)
    {
        // now set your mid and long to target
        var targetRatio = CalculateDesiredRatio(currentDate, model);
        var (currentLongTermAmount, curretMidTermAmount, ratio) = CalculateExistingRatio(accounts);
        // determive what your ideal movement would be; what you would have to sell or buy to reach the target ratio 
        var combined = currentLongTermAmount + curretMidTermAmount;
        var idealLong = combined * targetRatio;
        var idealMid = combined * (1 - targetRatio);
        var idealLongMovement = idealLong - currentLongTermAmount;
        var idealMidMovement = idealMid - curretMidTermAmount;
        return (idealLongMovement, idealMidMovement);
    }
}