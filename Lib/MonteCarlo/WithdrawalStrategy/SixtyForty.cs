using Lib.DataTypes.MonteCarlo;
using Lib.DataTypes.Postgres;
using Lib.MonteCarlo.StaticFunctions;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.WithdrawalStrategy;

public class SixtyForty : IWithdrawalStrategy
{
    public (BookOfAccounts accounts, List<ReconciliationMessage> messages)
        InvestExcessCash(LocalDateTime currentDate, BookOfAccounts accounts, CurrentPrices prices,
            Model model, PgPerson person)
    {
        /*
         * you are here. you just implemented this method. use the spreadsheet to create a UT
         */
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
        var (longTermAmount, midTermAmount, ratio) = CalculateExistingRatio(accounts);
        var desiredRatio = CalculateDesiredRatio(currentDate, model);
        var totalAfterInvestment = longTermAmount + midTermAmount + dollarAmount;
        var desiredLongAfterInvestment = totalAfterInvestment * desiredRatio;
        var desiredMidAfterInvestment = totalAfterInvestment - desiredLongAfterInvestment;
        var diffLong = desiredLongAfterInvestment - longTermAmount;
        var diffMid = desiredMidAfterInvestment - midTermAmount;
        var amountToInvestLong =
            (diffLong <= 0)
                ? 0
                : (diffLong <= dollarAmount) 
                    ? diffLong
                    : dollarAmount;
        var amountToInvestMid = dollarAmount - amountToInvestLong;
        
        // set up return tuple
        (BookOfAccounts accounts, List<ReconciliationMessage> messages) results = (
            AccountCopy.CopyBookOfAccounts(accounts), []);
        
        var investLongResults = Investment.InvestFundsByAccountTypeAndPositionType(
            results.accounts, currentDate, amountToInvestLong, McInvestmentPositionType.LONG_TERM, 
            McInvestmentAccountType.TAXABLE_BROKERAGE, prices);
        results.accounts = investLongResults.accounts;
        
        var investMidResults = Investment.InvestFundsByAccountTypeAndPositionType(
            results.accounts, currentDate, amountToInvestMid, McInvestmentPositionType.MID_TERM, 
            McInvestmentAccountType.TAXABLE_BROKERAGE, prices);
        results.accounts = investMidResults.accounts;
        
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.AddRange(investLongResults.messages);
        results.messages.AddRange(investMidResults.messages);
        results.messages.Add(new ReconciliationMessage(
            currentDate, dollarAmount,
            $"Invested {amountToInvestLong} (long) and {amountToInvestMid} (mid) in brokerage account"));
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
            BookOfAccounts accounts, TaxLedger ledger, LocalDateTime currentDate, decimal amountToSell,
            LocalDateTime? minDateExclusive = null, LocalDateTime? maxDateInclusive = null,
            McInvestmentPositionType? positionTypeOverride = null, McInvestmentAccountType? accountTypeOverride = null
        )
    {
        throw new NotImplementedException();
    }

    public (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)
        SellInvestmentsToRmdAmount(decimal amountNeeded, BookOfAccounts accounts, TaxLedger ledger,
            LocalDateTime currentDate)
    {
        throw new NotImplementedException();
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
}