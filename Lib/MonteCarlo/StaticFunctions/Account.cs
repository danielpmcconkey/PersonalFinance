using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Account
{
    public static BookOfAccounts CreateBookOfAccounts(
        List<McInvestmentAccount> investmentAccounts, List<McDebtAccount> debtAccounts)
    {
        var localResult = GetOrCreateDefaultAccount(McInvestmentAccountType.ROTH_401_K, investmentAccounts);
        var defaultRoth401K = localResult.newDefaultAccount;
        
        localResult = GetOrCreateDefaultAccount(McInvestmentAccountType.ROTH_IRA, localResult.newInvestmentAccounts);
        var defaultRothIra = localResult.newDefaultAccount;
        
        localResult = GetOrCreateDefaultAccount(McInvestmentAccountType.TRADITIONAL_401_K, localResult.newInvestmentAccounts);
        var defaultTraditional401K = localResult.newDefaultAccount;
        
        localResult = GetOrCreateDefaultAccount(McInvestmentAccountType.TRADITIONAL_IRA, localResult.newInvestmentAccounts);
        var defaultTraditionalIra = localResult.newDefaultAccount;
        
        localResult = GetOrCreateDefaultAccount(McInvestmentAccountType.TAXABLE_BROKERAGE, localResult.newInvestmentAccounts);
        var defaultBrokerage = localResult.newDefaultAccount;
        
        localResult = GetOrCreateDefaultAccount(McInvestmentAccountType.HSA, localResult.newInvestmentAccounts);
        var defaultHsa = localResult.newDefaultAccount;
        
        localResult = GetOrCreateDefaultAccount(McInvestmentAccountType.CASH, localResult.newInvestmentAccounts);
        var defaultCash = localResult.newDefaultAccount;
        
        var book = new BookOfAccounts(){
            Roth401K = defaultRoth401K, 
            RothIra = defaultRothIra,
            Traditional401K = defaultTraditional401K,
            TraditionalIra = defaultTraditionalIra,
            Brokerage = defaultBrokerage,
            Hsa = defaultHsa,
            Cash = defaultCash,
            InvestmentAccounts = localResult.newInvestmentAccounts,
            DebtAccounts = debtAccounts
        };
        return book;
    }

    public static NetWorthMeasurement CreateNetWorthMeasurement(MonteCarloSim sim)
    {
        if (sim.BookOfAccounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        if (sim.BookOfAccounts.DebtAccounts is null) throw new InvalidDataException("DebtAccounts is null");

        var totalAssets = 0M;
        var totalLiabilities = 0M;
        foreach (var account in sim.BookOfAccounts.InvestmentAccounts)
        {
            if (account.AccountType is not McInvestmentAccountType.PRIMARY_RESIDENCE)
            {
                totalAssets += account.Positions.Where(x => x.IsOpen).Sum(x =>
                {
                    McInvestmentPosition ip = (McInvestmentPosition)x;
                    return ip.CurrentValue;
                });
            }
        }

        foreach (var account in sim.BookOfAccounts.DebtAccounts)
        {
            totalLiabilities += account.Positions.Where(x => x.IsOpen).Sum(x =>
            {
                McDebtPosition dp = (McDebtPosition)x;
                return dp.CurrentBalance;
            });
        }

        NetWorthMeasurement measurement = new NetWorthMeasurement()
        {
            MeasuredDate = sim.CurrentDateInSim,
            TotalAssets = totalAssets,
            TotalLiabilities = totalLiabilities,
            TotalCash = AccountCalculation.CalculateCashBalance(sim.BookOfAccounts),
            TotalMidTermInvestments = AccountCalculation.CalculateMidBucketTotalBalance(sim.BookOfAccounts),
            TotalLongTermInvestments = AccountCalculation.CalculateLongBucketTotalBalance(sim.BookOfAccounts),
            TotalSpend = sim.LifetimeSpend.TotalSpendLifetime,
            TotalTax = sim.TaxLedger.TotalTaxPaid,
        };

        

        return measurement;
    }
  
    public static McInvestmentAccountType GetAccountType(int taxBucket, int accountGroup)
    {
        // tax buckets
        // 1	"Tax deferred"
        // 2	"Tax free HSA"
        // 3	"Tax free Roth"
        // 4	"Tax on capital gains"

        // account groups
        // 1	"Dan's 401(k)"
        // 2	"Dan's IRAs"
        // 3	"Jodi's IRAs"
        // 4	"Brokerage Account"
        // 5	"Home Equity"
        // 6	"Health Equity"

        // McInvestmentAccountType
        // TAXABLE_BROKERAGE = 0,
        // TRADITIONAL_401_K = 1,
        // ROTH_401_K = 2,
        // TRADITIONAL_IRA = 3,
        // ROTH_IRA = 4,
        // HSA = 5,
        // PRIMARY_RESIDENCE = 6,
        // CASH = 7,
        
        if (taxBucket == 2) return McInvestmentAccountType.HSA;
        if (taxBucket == 1 && accountGroup == 1) return McInvestmentAccountType.TRADITIONAL_401_K;
        if (taxBucket == 3 && accountGroup == 1) return McInvestmentAccountType.ROTH_401_K;
        if (taxBucket == 3 && accountGroup == 2) return McInvestmentAccountType.ROTH_IRA;
        if (taxBucket == 1 && accountGroup == 2) return McInvestmentAccountType.TRADITIONAL_IRA;
        if (taxBucket == 4 && accountGroup == 4) return McInvestmentAccountType.TAXABLE_BROKERAGE;
        if (taxBucket == 4 && accountGroup == 5) return McInvestmentAccountType.PRIMARY_RESIDENCE;
        if (taxBucket == 1 && accountGroup == 3) return McInvestmentAccountType.TRADITIONAL_IRA;
        return McInvestmentAccountType.CASH;
    }
 
    // parses a list of McInvestmentAccount and determines which would make a suitable "default" account. If none
    // already exists, it creates an empty account to match the need
    public static (McInvestmentAccount newDefaultAccount, List<McInvestmentAccount> newInvestmentAccounts) 
        GetOrCreateDefaultAccount(McInvestmentAccountType accountTypeN, List<McInvestmentAccount> investmentAccounts)
    {
        // check if we already have one. if so, just return it, with the list as-is
        var firstAccount = investmentAccounts
            .FirstOrDefault(x => x.AccountType == accountTypeN);
        if (firstAccount is not null) return (firstAccount, investmentAccounts);
        
        // need to create
        var listCopy = AccountCopy.CopyInvestmentAccounts(investmentAccounts);
        McInvestmentAccount defaultAccount = new()
        {
            Id = Guid.NewGuid(),
            AccountType = accountTypeN,
            Name = $"default {accountTypeN.ToString()} account",
            Positions = []
        };
        listCopy.Add(defaultAccount);
        return (defaultAccount, listCopy);
    }

    
    

}