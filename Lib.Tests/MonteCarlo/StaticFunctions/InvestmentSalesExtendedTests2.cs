using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using NodaTime;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class InvestmentSalesExtendedTests2
{
    private readonly LocalDateTime _testDate = new(2025, 1, 1, 0, 0);

    // ── §6.1 — Cash and PRIMARY_RESIDENCE cannot be sold ─────────────────────

    [Fact(DisplayName = "§6.1 — Cash and PRIMARY_RESIDENCE accounts are excluded from all investment sales")]
    public void SellInvestmentsToDollarAmount_CashAndPrimaryResidence_AreExcludedFromSales()
    {
        // Only the CASH account has any positions; no other investment account has value.
        // Requesting a $1,000 sale should yield amountSold = $0 because CASH is
        // filtered out before the sale query runs.
        var accounts = TestDataManager.CreateEmptyBookOfAccounts();

        // A MID_TERM position in the CASH account (unusual setup, but tests the exclusion boundary)
        accounts.Cash.Positions.Add(
            TestDataManager.CreateTestInvestmentPosition(
                100m, 50m, McInvestmentPositionType.MID_TERM));

        // Also add a PRIMARY_RESIDENCE account with a LONG_TERM position
        var primaryResidence = TestDataManager.CreateTestInvestmentAccount(
            [TestDataManager.CreateTestInvestmentPosition(300_000m, 1m, McInvestmentPositionType.LONG_TERM)],
            McInvestmentAccountType.PRIMARY_RESIDENCE);
        accounts.InvestmentAccounts.Add(primaryResidence);

        var ledger = new TaxLedger();

        // Broad sales order — would target everything if not for account-type exclusion
        (McInvestmentPositionType positionType, McInvestmentAccountType accountType)[] salesOrder =
        [
            (McInvestmentPositionType.MID_TERM,  McInvestmentAccountType.CASH),
            (McInvestmentPositionType.LONG_TERM, McInvestmentAccountType.PRIMARY_RESIDENCE),
        ];

        var result = InvestmentSales.SellInvestmentsToDollarAmount(
            accounts, ledger, _testDate, 1_000m, salesOrder);

        // Nothing should have been sold
        Assert.Equal(0m, result.amountSold);

        // CASH position quantity unchanged
        var cashPos = result.accounts.InvestmentAccounts
            .First(a => a.AccountType == McInvestmentAccountType.CASH).Positions;
        Assert.Single(cashPos);
        Assert.Equal(50m, cashPos[0].Quantity);

        // PRIMARY_RESIDENCE position unchanged
        var primaryPos = result.accounts.InvestmentAccounts
            .First(a => a.AccountType == McInvestmentAccountType.PRIMARY_RESIDENCE).Positions;
        Assert.Single(primaryPos);
        Assert.Equal(1m, primaryPos[0].Quantity);
    }
}
