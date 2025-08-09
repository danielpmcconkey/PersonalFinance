using Lib.DataTypes.MonteCarlo;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class AccountCleanup
{
    #region account cleanup functions

    /// <summary>
    /// rewrites the book of accounts' investment accounts to consolidate positions
    /// </summary>
    public static BookOfAccounts CleanUpAccounts(LocalDateTime currentDate, BookOfAccounts accounts, CurrentPrices prices)
    {
        var debtAccounts = RemoveClosedDebtPositions(accounts.DebtAccounts);
        var oneYearAgo = currentDate.PlusYears(-1);
        var fiveYearsAgo = currentDate.PlusYears(-5);
        
        var rothAccount = RebuildTaxFreeAccount(accounts, prices, fiveYearsAgo);
        var traditionalAccount = RebuildTaxDeferredAccount(accounts, prices, fiveYearsAgo);
        var taxableAccount = RebuildTaxableAccount(accounts, prices, oneYearAgo, fiveYearsAgo);
        var cashAccount = AccountCopy.CopyInvestmentAccount(accounts.Cash);
        var primaryResidence = RebuildPrimaryResidenceAccount(accounts);

        var investmentAccounts = new List<McInvestmentAccount>()
            { rothAccount, traditionalAccount, taxableAccount, cashAccount };
        if (primaryResidence is not null) investmentAccounts.Add(primaryResidence);

        return Account.CreateBookOfAccounts(investmentAccounts, debtAccounts);
    }

    public static McInvestmentAccount? RebuildPrimaryResidenceAccount(BookOfAccounts accounts)
    {
        var existingPrimaryResidence = accounts.InvestmentAccounts
            .FirstOrDefault(x => x.AccountType == McInvestmentAccountType.PRIMARY_RESIDENCE);
       return existingPrimaryResidence is null? null :
            AccountCopy.CopyInvestmentAccount(existingPrimaryResidence);
    }
    
    public static McInvestmentAccount RebuildTaxableAccount(
        BookOfAccounts accounts, CurrentPrices prices, LocalDateTime oneYearAgo, LocalDateTime fiveYearsAgo)
    {
        var taxableAccountTypes = new McInvestmentAccountType[] { McInvestmentAccountType.TAXABLE_BROKERAGE };
        var longPositionTypes = new McInvestmentPositionType[] { McInvestmentPositionType.LONG_TERM };
        var midPositionTypes = new McInvestmentPositionType[] { McInvestmentPositionType.MID_TERM };
        
        // get total balances of all long-held positions
        var taxableLongLongTotal = AccountCalculation.CalculateTotalBalanceByMultipleFactors(
            accounts, taxableAccountTypes, longPositionTypes, null, oneYearAgo);
        var taxableMidLongTotal = AccountCalculation.CalculateTotalBalanceByMultipleFactors(
            accounts, taxableAccountTypes, midPositionTypes, null, oneYearAgo);
        
        
        // get total costs of all long-held positions
        var taxableLongLongCost = AccountCalculation.CalculateAverageCostsOfBrokeragePositionsByMultipleFactors(
            accounts, longPositionTypes, null, oneYearAgo);
        var taxableMidLongCost = AccountCalculation.CalculateAverageCostsOfBrokeragePositionsByMultipleFactors(
            accounts, midPositionTypes, null, oneYearAgo);
        
        // get the short holdings as-is
        var shortHeldPositions = accounts.InvestmentAccounts
            .Where(a => a.AccountType == McInvestmentAccountType.TAXABLE_BROKERAGE)
            .SelectMany(x => x.Positions
                .Where(y => y.IsOpen && y.Entry > oneYearAgo)).ToList();
        
        var longLongPosition = new McInvestmentPosition()
        {
            Id = Guid.Empty,
            Entry = fiveYearsAgo,
            InvestmentPositionType = McInvestmentPositionType.LONG_TERM,
            InitialCost = taxableLongLongCost,
            IsOpen = true,
            Price = prices.CurrentLongTermInvestmentPrice,
            Quantity = taxableLongLongTotal / prices.CurrentLongTermInvestmentPrice,
            Name = "Taxable long-held long-term position"
        };
        var midLongPosition = new McInvestmentPosition()
        {
            Id = Guid.Empty,
            Entry = fiveYearsAgo,
            InvestmentPositionType = McInvestmentPositionType.MID_TERM,
            InitialCost = taxableMidLongCost,
            IsOpen = true,
            Price = prices.CurrentMidTermInvestmentPrice,
            Quantity = taxableMidLongTotal / prices.CurrentMidTermInvestmentPrice,
            Name = "Taxable long-held mid-term position"
        };
        var account = new McInvestmentAccount()
        {
            Id = Guid.NewGuid(),
            Name = "Taxable brokerage",
            AccountType = McInvestmentAccountType.TAXABLE_BROKERAGE,
            Positions = [longLongPosition, midLongPosition]
        };
        account.Positions.AddRange(shortHeldPositions);
        return account;
    }

    public static McInvestmentAccount RebuildTaxDeferredAccount(BookOfAccounts accounts, CurrentPrices prices, LocalDateTime fiveYearsAgo)
    {
        var accountTypes = new McInvestmentAccountType[]
        {
            McInvestmentAccountType.TRADITIONAL_401_K,
            McInvestmentAccountType.TRADITIONAL_IRA,
        };
        var longPositionTypes = new McInvestmentPositionType[] { McInvestmentPositionType.LONG_TERM };
        var midPositionTypes = new McInvestmentPositionType[] { McInvestmentPositionType.MID_TERM };
        
        // get total balances
        var longTotal = AccountCalculation.CalculateTotalBalanceByMultipleFactors(
            accounts, accountTypes, longPositionTypes);
        var midTotal = AccountCalculation.CalculateTotalBalanceByMultipleFactors(
            accounts, accountTypes, midPositionTypes);
        
        var longPosition = new McInvestmentPosition()
        {
            Id = Guid.Empty,
            Entry = fiveYearsAgo,
            InvestmentPositionType = McInvestmentPositionType.LONG_TERM,
            InitialCost = 0, // doesn't matter for Roth. It's all taxed as income
            IsOpen = true,
            Price = prices.CurrentLongTermInvestmentPrice,
            Quantity = longTotal / prices.CurrentLongTermInvestmentPrice ,
            Name = "Tax deferred long term position"
        };
        var midPosition = new McInvestmentPosition()
        {
            Id = Guid.Empty,
            Entry = fiveYearsAgo,
            InvestmentPositionType = McInvestmentPositionType.MID_TERM,
            InitialCost = 0, // doesn't matter for Roth. It's all taxed as income
            IsOpen = true,
            Price = prices.CurrentMidTermInvestmentPrice,
            Quantity = midTotal / prices.CurrentMidTermInvestmentPrice,
            Name = "Tax deferred mid term position"
        };
        var account = new McInvestmentAccount()
        {
            Id = Guid.NewGuid(),
            Name = "Traditional IRA",
            AccountType = McInvestmentAccountType.TRADITIONAL_IRA,
            Positions = [longPosition, midPosition]
        };
        return account;
    }
    
    public static McInvestmentAccount RebuildTaxFreeAccount(BookOfAccounts accounts, CurrentPrices prices, LocalDateTime fiveYearsAgo)
    {
        var accountTypes = new McInvestmentAccountType[]
        {
            McInvestmentAccountType.ROTH_401_K,
            McInvestmentAccountType.ROTH_IRA,
            McInvestmentAccountType.HSA,
        };
        var longPositionTypes = new McInvestmentPositionType[] { McInvestmentPositionType.LONG_TERM };
        var midPositionTypes = new McInvestmentPositionType[] { McInvestmentPositionType.MID_TERM };
        
        // get total balances
        var longTotal = AccountCalculation.CalculateTotalBalanceByMultipleFactors(
            accounts, accountTypes, longPositionTypes);
        var midTotal = AccountCalculation.CalculateTotalBalanceByMultipleFactors(
            accounts, accountTypes, midPositionTypes);
        
        var longPosition = new McInvestmentPosition()
        {
            Id = Guid.Empty,
            Entry = fiveYearsAgo,
            InvestmentPositionType = McInvestmentPositionType.LONG_TERM,
            InitialCost = 0, // doesn't matter for Roth. It's all tax free
            IsOpen = true,
            Price = prices.CurrentLongTermInvestmentPrice,
            Quantity = longTotal / prices.CurrentLongTermInvestmentPrice,
            Name = "Tax free long term position"
        };
        var midPosition = new McInvestmentPosition()
        {
            Id = Guid.Empty,
            Entry = fiveYearsAgo,
            InvestmentPositionType = McInvestmentPositionType.MID_TERM,
            InitialCost = 0, // doesn't matter for Roth. It's all tax free
            IsOpen = true,
            Price = prices.CurrentMidTermInvestmentPrice,
            Quantity = midTotal / prices.CurrentMidTermInvestmentPrice,
            Name = "Tax free mid term position"
        };
        var account = new McInvestmentAccount()
        {
            Id = Guid.NewGuid(),
            Name = "Roth IRA",
            AccountType = McInvestmentAccountType.ROTH_IRA,
            Positions = [longPosition, midPosition]
        };
        return account;
    }
    public static List<McDebtAccount> RemoveClosedDebtPositions(List<McDebtAccount> accounts)
    {
        List<McDebtAccount> newAccounts = [];
        foreach (var account in accounts)
        {
            var openPositions = account.Positions.Where(a => a.IsOpen).ToList();
            if (openPositions.Count > 0)
            {
                newAccounts.Add(new McDebtAccount()
                    {
                        Id = account.Id,
                        Name = account.Name,
                        Positions = openPositions
                    }
                );
            }
        }
        return newAccounts;
    }
    
    

    #endregion account cleanup functions
}