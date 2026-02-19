using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using NodaTime;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class AccountCleanupExtendedTests
{
    private readonly LocalDateTime _testDate = new(2025, 1, 1, 0, 0);

    // ── §8 — No primary residence: not added to output ────────────────────────

    [Fact(DisplayName = "§8 — When no PRIMARY_RESIDENCE account exists, CleanUpAccounts does not add one")]
    public void CleanUpAccounts_NoPrimaryResidence_NotIncludedInOutput()
    {
        // CreateEmptyBookOfAccounts produces 7 accounts: CASH, ROTH_401_K, ROTH_IRA,
        // TRADITIONAL_401_K, TRADITIONAL_IRA, TAXABLE_BROKERAGE, HSA — no PRIMARY_RESIDENCE.
        // After cleanup the output InvestmentAccounts should still contain no PRIMARY_RESIDENCE.
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();
        var prices   = TestDataManager.CreateTestCurrentPrices(0.02m, 100m, 100m, 50m);

        var result = AccountCleanup.CleanUpAccounts(_testDate, accounts, prices);

        Assert.False(
            result.InvestmentAccounts.Any(a => a.AccountType == McInvestmentAccountType.PRIMARY_RESIDENCE),
            "PRIMARY_RESIDENCE should not appear in cleaned-up accounts when none existed before");
    }
}
