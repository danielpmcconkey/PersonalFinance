using Lib.DataTypes.MonteCarlo;
using Lib.StaticConfig;
using NodaTime;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Account
{
    #region DB read functions

    public static decimal FetchDbCashTotalByPersonId(Guid personId)
    {
        // todo: change get cash method to actually use the person ID
        
        using var context = new PgContext();
        var currentCash = 0M;
        
        // get the max date by symbol
        var maxDateByAccount = (
            from p in context.PgCashPositions
            group p by p.CashAccountId
            into g
            select new { g.Key, maxdate = g.Max(x => x.PositionDate) }
        ).ToList();
        // get any positions at max date
        foreach (var maxDate in maxDateByAccount)
        {
            var positionAtMaxDate = 
                context
                    .PgCashPositions
                    .Where(x => 
                        x.PositionDate == maxDate.maxdate && 
                        x.CashAccountId == maxDate.Key)
                    .OrderByDescending(x => x.CurrentBalance)
                    .FirstOrDefault()
                ??  throw new InvalidDataException();
            if (positionAtMaxDate.CurrentBalance >= 0)
            {
                currentCash += positionAtMaxDate.CurrentBalance;
            }
        }
        
        return currentCash;
    }
    
    public static List<McDebtAccount> FetchDbDebtAccountsByPersonId(Guid personId)
    {
        // todo: change this method to use the person Id in the DB pull
        
        List<McDebtAccount> accounts = [];
        using var context = new PgContext();
        var accountsPg = (
            context.PgDebtAccounts
            ?? throw new InvalidDataException()
        ).ToList();
        
        accounts.AddRange(
            from accountPg in accountsPg 
            let positionsPg = FetchDbOpenDebtPositionsByAccountId(
                accountPg.Id, 
                accountPg.AnnualPercentageRate,
                accountPg.MonthlyPayment
                )
            where positionsPg.Any() select new McDebtAccount()
            {
                Id = Guid.NewGuid(), Name = accountPg.Name, Positions = positionsPg,
            });

        return accounts;
    }
    
    public static List<McInvestmentAccount> FetchDbInvestmentAccountsByPersonId(Guid personId)
    {
        // todo: change this method to use the person Id in the DB pull
        
        List<McInvestmentAccount> accounts = [];
        using var context = new PgContext();
        var accountsPg = context.PgInvestmentAccounts
            ?? throw new InvalidDataException();
        foreach (var pgInvestmentAccount in accountsPg)
        {
            var positions = FetchDbOpenInvestmentPositionsByAccountId(pgInvestmentAccount.Id);
            if(!positions.Any()) continue;
            accounts.Add(new McInvestmentAccount()
            {
                Id = Guid.NewGuid(), 
                Name = pgInvestmentAccount.Name, 
                AccountType = GetAccountType(
                    pgInvestmentAccount.TaxBucketId,
                    pgInvestmentAccount.InvestmentAccountGroupId),
                Positions = positions,
            });
        }
        
        // the Monte Carlo sim considers cash accounts as investment
        // positions with a type of CASH so add any cash here 
        var currentCash = FetchDbCashTotalByPersonId(personId);
        accounts.Add(new McInvestmentAccount()
        {
            Id = Guid.NewGuid(),
            Name = "Cash",
            AccountType = McInvestmentAccountType.CASH,
            Positions = [new McInvestmentPosition()
            {
                Id = Guid.NewGuid(),
                IsOpen  = true,
                Name = "cash",
                Entry = new LocalDateTime(2025, 2, 1, 0, 0), // entry date doesn't matter here
                InvestmentPositionType = McInvestmentPositionType.SHORT_TERM,
                InitialCost = currentCash,
                Quantity  = currentCash,
                Price  = 1.0M,
            }],
        });
        return accounts;
    }
    public static List<McDebtPosition> FetchDbOpenDebtPositionsByAccountId(int accountId, decimal apr, decimal payment)
    {
        List<McDebtPosition> positions = [];
        using var context = new PgContext();
        
        var latestPosition = context.PgDebtPositions
                                 .Where(x => x.DebtAccountId == accountId)
                                 .OrderByDescending(x => x.PositionDate)
                                 .FirstOrDefault()
                             ??  throw new InvalidDataException();
        if (latestPosition.CurrentBalance <= 0) return positions;
        
            
        positions.Add(new McDebtPosition()
        {
            Id = Guid.NewGuid(),
            IsOpen = true,
            Name = $"Position {latestPosition.CurrentBalance}",
            Entry = latestPosition.PositionDate,
            CurrentBalance = latestPosition.CurrentBalance,
            MonthlyPayment = payment,
            AnnualPercentageRate = apr
        });
            
        
        
        return positions;
    }
    public static List<McInvestmentPosition> FetchDbOpenInvestmentPositionsByAccountId(int accountId)
    {
        List<McInvestmentPosition> positions = [];
        using var context = new PgContext();
        
        // get the max date by symbol
        var maxDateBySymbol = (
                from p in context.PgPositions
                where p.InvestmentAccountId == accountId
                group p by p.Symbol
                into g
                select new { g.Key, maxdate = g.Max(x => x.PositionDate) })
            .ToList();
        foreach (var maxDate in maxDateBySymbol)
        {
            // order by totalQuantity and pick the first so that anytime we have a
            // positive quantity and a zero quantity, we'll know we picked the right
            // one (the zero means we closed it out)
            var positionAtMaxDate =
                context.PgPositions
                    .Where(x =>
                        x.PositionDate == maxDate.maxdate &&
                        x.InvestmentAccountId == accountId &&
                        x.Symbol == maxDate.Key)
                    .OrderBy(x => x.TotalQuantity)
                    .FirstOrDefault()
                ?? throw new InvalidDataException();
            if (positionAtMaxDate.TotalQuantity > 0)
            {
                positions.Add(new McInvestmentPosition()
                {
                    Id = Guid.NewGuid(),
                    IsOpen = true,
                    Name = $"Position {maxDate.Key}",
                    Entry = positionAtMaxDate.PositionDate,
                    InvestmentPositionType = Investment.GetInvestmentPositionType(maxDate.Key),
                    InitialCost = positionAtMaxDate.CostBasis,
                    Quantity = positionAtMaxDate.TotalQuantity,
                    Price = positionAtMaxDate.Price,
                });
            }
        }
        
        return positions;
    }

    #endregion DB read functions

    #region Calculation functions
    public static decimal CalculateCashBalance(BookOfAccounts accounts)
    {
        if (accounts.Cash is null) throw new InvalidDataException("Cash is null");
        if (accounts.Cash.Positions is null) throw new InvalidDataException("Cash.Positions is null");
        
        return accounts.Cash.Positions.Sum(x => {
            if (!x.IsOpen) return 0;
            var ip = (McInvestmentPosition)x;
            return ip.CurrentValue;
        });
    }
    public static decimal CalculateInvestmentAccountTotalValue(McInvestmentAccount account)
    {
        return account.Positions.Sum(x => {
            if (x.IsOpen && x is McInvestmentPosition)
            {
                var ip = x as McInvestmentPosition;
                if (ip is null) return 0;
                return ip.CurrentValue;
            }
            return 0;
        });
    }
    public static decimal CalculateLongBucketTotalBalance(BookOfAccounts accounts)
    {
        return CalculateTotalBalanceByBucketType(accounts, McInvestmentPositionType.LONG_TERM);
    }
    public static decimal CalculateMidBucketTotalBalance(BookOfAccounts accounts)
    {
        return CalculateTotalBalanceByBucketType(accounts, McInvestmentPositionType.MID_TERM);
    }
    public static decimal CalculateShortBucketTotalBalance(BookOfAccounts accounts)
    {
        return CalculateTotalBalanceByBucketType(accounts, McInvestmentPositionType.SHORT_TERM);
    }

    public static decimal CalculateNetWorth(BookOfAccounts accounts)
    {
        if (accounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        if (accounts.DebtAccounts is null) throw new InvalidDataException("DebtAccounts is null");
        
        var totalAssets = 0M;
        var totalLiabilities = 0M;
        foreach (var account in accounts.InvestmentAccounts)
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

        foreach (var account in accounts.DebtAccounts)
        {
            totalLiabilities += account.Positions.Where(x => x.IsOpen).Sum(x =>
            {
                McDebtPosition dp = (McDebtPosition)x;
                return dp.CurrentBalance;
            });
        }

        return totalAssets - totalLiabilities;
    }

    public static decimal CalculateTotalBalanceByBucketType(BookOfAccounts bookOfAccounts, McInvestmentPositionType bucketType)
    {
        if (bookOfAccounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        
        var totalBalance = 0M;
        var accounts = bookOfAccounts.InvestmentAccounts.Where(x => {
            if (x.AccountType == McInvestmentAccountType.PRIMARY_RESIDENCE) return false;
            if (x.AccountType == McInvestmentAccountType.CASH) return false;
            return true;
        });
        foreach (var account in accounts)
        {
            var totalValueBegin = CalculateInvestmentAccountTotalValue(account);
            totalBalance += account.Positions
                .Where(x => x.IsOpen && x is McInvestmentPosition)
                .Sum(x => {
                    var ip = x as McInvestmentPosition;
                    if (ip is null) return 0;
                    if (ip.InvestmentPositionType != bucketType) return 0;
                    return ip.CurrentValue;
                });
        }
        return totalBalance;
    }
    public static decimal CalculateDebtTotal(BookOfAccounts accounts)
    {
        if (accounts.DebtAccounts is null) throw new InvalidDataException("DebtAccounts is null");
        
        decimal total = 0L;
        foreach (var a in accounts.DebtAccounts)
        {
            var positions = a.Positions
                .Where(x => x.IsOpen)
                .ToList();
            foreach (var p in positions)
            {
                total += p.CurrentBalance;
            }
        }
        return total;
    }
    

    #endregion Calculation functions

   
    public static void AccrueInterest(LocalDateTime currentDate, BookOfAccounts bookOfAccounts, CurrentPrices prices, LifetimeSpend lifetimeSpend)
    {
        if (bookOfAccounts.DebtAccounts is null) throw new InvalidDataException("DebtAccounts is null");
        if (bookOfAccounts.InvestmentAccounts is null) throw new InvalidDataException("InvestmentAccounts is null");
        
        
        /*
         * for debt accounts, we just need to update the balances according to the apr
         * 
         * for investment accounts, if all accounts have previously normalized, we should only have to update the
         * price on all positions
         */
        
        // debt stuff
        foreach (var account in bookOfAccounts.DebtAccounts)
        {
            // bad number goes up
            foreach (var p in account.Positions)
            {
                decimal oldBalance = p.CurrentBalance;

                if (p is not McDebtPosition) break;
                if (!p.IsOpen) break;
                
                decimal amount = p.CurrentBalance * (p.AnnualPercentageRate / 12);
                p.CurrentBalance += amount;

                lifetimeSpend.TotalDebtAccrualLifetime += amount;
                if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcileInterestAccrual)
                {
                    Reconciliation.AddMessageLine(currentDate, -1 * amount, $"Debt accrual for account {account.Name}; position {p.Name}");
                }
            }
        }
        
        // investment accounts
        foreach (var account in bookOfAccounts.InvestmentAccounts
                     .Where(x => x.AccountType is not McInvestmentAccountType.PRIMARY_RESIDENCE
                     && x.AccountType is not McInvestmentAccountType.CASH))
        {
            foreach (var p in account.Positions)
            {
                var oldAmount = (StaticConfig.MonteCarloConfig.DebugMode == false) ? 0 : p.CurrentValue;
                
                var newPrice = p.InvestmentPositionType switch
                {
                    McInvestmentPositionType.MID_TERM => (decimal)prices.CurrentMidTermInvestmentPrice,
                    McInvestmentPositionType.SHORT_TERM => (decimal)prices.CurrentShortTermInvestmentPrice,
                    _ => (decimal)prices.CurrentLongTermInvestmentPrice
                };
                p.Price = newPrice;
                
                if (MonteCarloConfig.DebugMode && MonteCarloConfig.ShouldReconcileInterestAccrual)
                {
                    var newAmount =  p.CurrentValue;
                    lifetimeSpend.TotalInvestmentAccrualLifetime += newAmount - oldAmount;
                    Reconciliation.AddMessageLine(currentDate, newAmount - oldAmount,
                        $"Interest accrual for account {account.Name}; position {p.Name}");
                }
            }
        }
    }
    
    /// <summary>
    /// removes closed positions and splits up large investment positions
    /// </summary>
    public static void CleanUpAccounts(LocalDateTime currentDate, BookOfAccounts bookOfAccounts, CurrentPrices prices)
    {
        Investment.RemoveClosedPositions(bookOfAccounts);
        Investment.SplitLargePositions(bookOfAccounts, prices);
    }
    
    /// <summary>
    /// Used to create a new object with the same characteristics as the original so we don't have to worry about one
    /// sim run updating another's stats
    /// </summary>
    public static List<McDebtAccount> CopyDebtAccounts(List<McDebtAccount> oldAccounts)
    {
        Func<List<McDebtPosition>, Guid, List<McDebtPosition>>
            CopyPositions = (positions, newAccountId) =>
            {
                List<McDebtPosition> newList = [];
                foreach (McDebtPosition p in positions)
                {
                    newList.Add(new McDebtPosition()
                    {
                        Id = Guid.NewGuid(),
                        IsOpen = p.IsOpen,
                        Name = p.Name,
                        Entry = p.Entry,
                        AnnualPercentageRate = p.AnnualPercentageRate,
                        MonthlyPayment = p.MonthlyPayment,
                        CurrentBalance = p.CurrentBalance,
                    });
                }
                return newList;
            };
        List<McDebtAccount> newAccounts = [];
        foreach (McDebtAccount a in oldAccounts)
        {
            var newAccountId = Guid.NewGuid();
            newAccounts.Add(new()
            {
                Id = Guid.NewGuid(),
                Name = a.Name,
                Positions = CopyPositions(a.Positions, newAccountId),
            });
        }
        return newAccounts;
    }

    /// <summary>
    /// Used to create a new object with the same characteristics as the original so we don't have to worry about one
    /// sim run updating another's stats
    /// </summary>
    public static List<McInvestmentAccount> CopyInvestmentAccounts(List<McInvestmentAccount> oldAccounts)
    {
        Func<List<McInvestmentPosition>, Guid, List<McInvestmentPosition>>
            CopyPositions = (positions, newAccountId) =>
            {
                List<McInvestmentPosition> newList = [];
                foreach (McInvestmentPosition p in positions)
                {
                    newList.Add(new McInvestmentPosition()
                    {
                        Id = Guid.NewGuid(),
                        // InvestmentAccountId = newAccountId,
                        IsOpen = p.IsOpen,
                        Name = p.Name,
                        Entry = p.Entry,
                        InvestmentPositionType = p.InvestmentPositionType,
                        InitialCost = p.InitialCost,
                        Quantity = p.Quantity,
                        Price = p.Price,
                    });
                }

                return newList;
            };
        List<McInvestmentAccount> newAccounts = [];
        foreach (McInvestmentAccount a in oldAccounts)
        {
            var newAccountId = Guid.NewGuid();
            newAccounts.Add(new()
            {
                Id = Guid.NewGuid(),
                // PersonId = a.PersonId,
                Name = a.Name,
                AccountType = a.AccountType,
                Positions = CopyPositions(a.Positions, newAccountId),
            });
        }

        return newAccounts;
    }


    public static BookOfAccounts CreateBookOfAccounts(
        List<McInvestmentAccount> investmentAccounts, List<McDebtAccount> debtAccounts)
    {
        Func<McInvestmentAccountType, McInvestmentAccount> getOrCreateAccount = (McInvestmentAccountType accountType) =>
        {
            var firstAccount = investmentAccounts
                .FirstOrDefault(x => x.AccountType == accountType);
            if (firstAccount is null)
            {
                McInvestmentAccount defaultAccount = new()
                {
                    Id = Guid.NewGuid(),
                    AccountType = accountType,
                    Name = $"default {accountType.ToString()} account",
                    Positions = []
                };
                investmentAccounts.Add(defaultAccount);
                return defaultAccount;
            }
            return firstAccount;
        };
        var book = new BookOfAccounts(
            getOrCreateAccount(McInvestmentAccountType.ROTH_401_K), 
            getOrCreateAccount(McInvestmentAccountType.ROTH_IRA),
            getOrCreateAccount(McInvestmentAccountType.TRADITIONAL_401_K),
            getOrCreateAccount(McInvestmentAccountType.TRADITIONAL_IRA),
            getOrCreateAccount(McInvestmentAccountType.TAXABLE_BROKERAGE),
            getOrCreateAccount(McInvestmentAccountType.HSA),
            getOrCreateAccount(McInvestmentAccountType.CASH),
            investmentAccounts,
            debtAccounts
            );
        return book;
    }
    
    
    public static void DepositCash(BookOfAccounts accounts, decimal amount, LocalDateTime currentDate)
    {

        var totalCash = CalculateCashBalance(accounts);
        totalCash += amount;
        UpdateCashAccountBalance(accounts, totalCash, currentDate);
        if (StaticConfig.MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(currentDate, amount, "Generic cash deposit");
        }
    }
    

    public static NetWorthMeasurement CreateNetWorthMeasurement(MonteCarloSim sim)
    {
        if (sim.BookOfAccounts is null) throw new InvalidDataException("BookOfAccounts is null");
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
            TotalCash = CalculateCashBalance(sim.BookOfAccounts),
            TotalMidTermInvestments = CalculateMidBucketTotalBalance(sim.BookOfAccounts),
            TotalLongTermInvestments = CalculateLongBucketTotalBalance(sim.BookOfAccounts),
            TotalSpend = sim.LifetimeSpend.TotalSpendLifetime,
            TotalTax = sim.TaxLedger.TotalTaxPaid,
        };

        // see if we're in extreme austerity measures based on total net worth
        if (measurement.NetWorth <= sim.SimParameters.ExtremeAusterityNetWorthTrigger)
        {

            sim.RecessionStats.AreWeInExtremeAusterityMeasures = true;
            // set the end date to now. if we stay below the line, the date
            // will keep going up with it
            sim.RecessionStats.LastExtremeAusterityMeasureEnd = sim.CurrentDateInSim;
        }
        else
        {
            // has it been within 12 months that we were in an extreme measure?
            if (sim.RecessionStats.LastExtremeAusterityMeasureEnd < sim.CurrentDateInSim.PlusYears(-1))
            {

                sim.RecessionStats.AreWeInExtremeAusterityMeasures = false;
            }
        }

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

    public static bool PayDownLoans(
        BookOfAccounts accounts, LocalDateTime currentDate, McPerson person, 
        TaxLedger taxLedger, LifetimeSpend lifetimeSpend)
    {
        if (accounts.DebtAccounts is null) throw new InvalidDataException("DebtAccounts is null");
        foreach (var account in accounts.DebtAccounts)
        {
            if (person.IsBankrupt) return false;
            foreach (var p in account.Positions)
            {
                if (person.IsBankrupt) return false;
                if (!p.IsOpen) continue;
                    
                
                    
                decimal amount = p.MonthlyPayment;
                if (amount > p.CurrentBalance) amount = p.CurrentBalance;
                if (!Account.WithdrawCash(accounts, amount, currentDate, taxLedger))
                {
                    person.IsBankrupt = true;
                    return false;
                }
                p.CurrentBalance -= amount;
                lifetimeSpend.TotalDebtPaidLifetime += amount;
                

                if (MonteCarloConfig.DebugMode)
                {
                    Reconciliation.AddMessageLine(currentDate, amount, $"Pay down loan {account.Name} {p.Name}");
                }
                if(p.CurrentBalance <= 0)
                {
                    Reconciliation.AddMessageLine(currentDate, 0, $"Paid off loan {account.Name} {p.Name}");
                    p.CurrentBalance = 0;
                    p.IsOpen = false;
                }
            }
        }
        return true;
    }
    
    public static void UpdateCashAccountBalance(BookOfAccounts accounts, decimal newBalance, LocalDateTime currentDate)
    {
        accounts.Cash.Positions = [
            new McInvestmentPosition(){
                Id = Guid.NewGuid(),
                Entry = currentDate,
                Price = 1m,
                Quantity = newBalance,
                InitialCost = 0,
                InvestmentPositionType = McInvestmentPositionType.SHORT_TERM,
                IsOpen = true,
                Name = "default cash account"}
        ];
    }



    /// <summary>
    /// deduct cash from the cash account
    /// </summary>
    /// <returns>true if able to pay. false if not</returns>
    public static bool WithdrawCash(
        BookOfAccounts accounts, decimal amount, LocalDateTime currentDate, TaxLedger taxLedger)
    {
        var totalCashOnHand = CalculateCashBalance(accounts);

        if (totalCashOnHand < amount)
        {
            // can we pull it from the mid bucket?
            var amountNeeded = amount - totalCashOnHand;
            var cashSold = Investment. SellInvestment(
                accounts, amountNeeded, McInvestmentPositionType.MID_TERM, currentDate, taxLedger);
            if (MonteCarloConfig.DebugMode == true)
            {
                Reconciliation.AddMessageLine(currentDate, cashSold, "Investment sales from mid-term to support cash withdrawal");
            }
            totalCashOnHand += cashSold;
            
            // is that enough?
            
            
            if (totalCashOnHand < amount)
            {
                // can we pull it from the long-term bucket?
                cashSold = Investment.SellInvestment(
                    accounts, amountNeeded, McInvestmentPositionType.LONG_TERM, currentDate, taxLedger);
                if (MonteCarloConfig.DebugMode == true)
                {
                    Reconciliation.AddMessageLine(currentDate, cashSold, "Investment sales from long-term to support cash withdrawal");
                }
                totalCashOnHand += cashSold;
                if (totalCashOnHand < amount)
                {
                    // we broke. update the account balance just in case.
                    // returning false here should result in a bankruptcy
                    // witch sets everything to 0, but we may change code
                    // flow later and it's important to add our sales
                    // proceeds to the cash account
                    UpdateCashAccountBalance(accounts, totalCashOnHand, currentDate);
                    return false;
                }
            }
        }
        totalCashOnHand -= amount;
        // that's enough selling. Now put whatever surplus back into the cash account
        UpdateCashAccountBalance(accounts, totalCashOnHand, currentDate);
        if (MonteCarloConfig.DebugMode == true)
        {
            Reconciliation.AddMessageLine(currentDate, amount, "Cash withdrawal");
        }
        return true;
    }

    

}