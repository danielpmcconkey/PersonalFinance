// #define PERFORMANCEPROFILING
using System.Diagnostics;
using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.WithdrawalStrategy;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class InvestmentSales
{
    ///<summary>
    /// creates a cross joined list of account and position types, order by account type, then position type
    /// </summary> 
    public static (McInvestmentPositionType positionType, McInvestmentAccountType accountType)[]
        CreateSalesOrderAccountTypeFirst(
            McInvestmentPositionType[] positionTypes, McInvestmentAccountType[] accountTypes)
    {
        var size = accountTypes.Length * positionTypes.Length;
        var result = new (McInvestmentPositionType positionType, McInvestmentAccountType accountType)[size];
        for (var i = 0; i < accountTypes.Length; i++)
        for (var j = 0; j < positionTypes.Length; j++)
            result[i * positionTypes.Length + j] = (positionTypes[j], accountTypes[i]);
        return result;
    }
    
    ///<summary>
    /// creates a cross joined list of account and position types, order by position type, then account type
    /// </summary> 
    public static (McInvestmentPositionType positionType, McInvestmentAccountType accountType)[]
        CreateSalesOrderPositionTypeFirst(
            McInvestmentPositionType[] positionTypes, McInvestmentAccountType[] accountTypes)
    {
        var size = accountTypes.Length * positionTypes.Length;
        var result = new (McInvestmentPositionType positionType, McInvestmentAccountType accountType)[size];
        for (var i = 0; i < positionTypes.Length; i++)
        for (var j = 0; j < accountTypes.Length; j++)
        {
            var ordinal = i * positionTypes.Length + j;
            var thisPosition = positionTypes[i];
            var thisAccount = accountTypes[j];
            result[ordinal] = (thisPosition, thisAccount);
        }

        return result;
    }
    public static (decimal amountSold, BookOfAccounts accounts, TaxLedger ledger, List<ReconciliationMessage> messages)
        SellInvestmentsToDollarAmount(
            BookOfAccounts accounts, TaxLedger ledger, LocalDateTime currentDate, decimal amountToSell,
            (McInvestmentPositionType positionType, McInvestmentAccountType accountType)[]? typeOrder = null,
            LocalDateTime? minDateExclusive = null, LocalDateTime? maxDateInclusive = null)
    {
        if (accounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        if (accounts.InvestmentAccounts.Count == 0) return (0, accounts, ledger, []);
        
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
                    position.CurrentValue > 0m &&
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
                case McInvestmentAccountType.ROTH_IRA:
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
                case McInvestmentAccountType.PRIMARY_RESIDENCE:
                case McInvestmentAccountType.CASH:
                    throw new InvalidDataException("Cannot sell cash or primary residence accounts");
                default:
                    throw new ArgumentOutOfRangeException();
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
        
        // record the tax free withdrawals
        var recordTaxFreeResults = Tax.RecordTaxFreeWithdrawal(results.ledger, currentDate, totalTaxFree);
        results.ledger = recordTaxFreeResults.ledger;

        // record the Capital Gains
        var recordLongTermCapitalGainsResults = Tax.RecordLongTermCapitalGain(results.ledger, currentDate, totalLongTermCapitalGains);
        results.ledger = recordLongTermCapitalGainsResults.ledger;
        var recordShortTermCapitalGainsResults = Tax.RecordShortTermCapitalGain(results.ledger, currentDate, totalShortTermCapitalGains);
        results.ledger = recordShortTermCapitalGainsResults.ledger;
        
        if (!MonteCarloConfig.DebugMode) return results;
        results.messages.Add(new ReconciliationMessage(currentDate, results.amountSold, "Total investments sold"));
        results.messages.Add(new ReconciliationMessage(currentDate, totalIraDistribution, "Total tax deferred sold"));
        results.messages.Add(new ReconciliationMessage(currentDate, totalTaxFree, "Total tax free sold"));
        results.messages.Add(new ReconciliationMessage(currentDate, totalTaxableSold, "Total taxable sold"));
        results.messages.Add(new ReconciliationMessage(currentDate, totalLongTermCapitalGains, "Total long term capital gains"));
        results.messages.Add(new ReconciliationMessage(currentDate, totalShortTermCapitalGains, "Total short term capital gains"));
        results.messages.Add(new ReconciliationMessage(currentDate, totalTaxFree, "Total tax free sold"));
        results.messages.AddRange(depositResults.messages);
        results.messages.AddRange(recordIraResults.messages);
        results.messages.AddRange(recordLongTermCapitalGainsResults.messages);
        results.messages.AddRange(recordShortTermCapitalGainsResults.messages);
        
        
        return results;
    }
   

   
    
    
}