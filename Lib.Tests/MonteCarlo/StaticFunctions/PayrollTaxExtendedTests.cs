using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using NodaTime;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class PayrollTaxExtendedTests
{
    private readonly LocalDateTime _testDate = new(2025, 1, 1, 0, 0);

    [Fact(DisplayName = "§2 — Additional Medicare 0.9% surcharge applies on annual income above $250,000")]
    public void WithholdTaxesFromPaycheck_HighSalary_AdditionalMedicareSurchargeApplied()
    {
        // Annual salary = $312,000 (grossMonthlyPay = $26,000).
        // $312,000 − $250,000 = $62,000 above the additional Medicare threshold.
        //
        // Expected monthly withholding (federal and state withholding = 0):
        //   OASDI:              min(0.062 × $312,000, $11,439) / 12 = $11,439 / 12 = $953.25
        //   Standard Medicare:  0.0145 × $312,000 / 12             = $377.00
        //   Additional Medicare: 0.009 × $62,000 / 12             = $46.50
        //   Total = $953.25 + $377.00 + $46.50 = $1,376.75
        var grossMonthlyPay = 26_000m;
        var person = TestDataManager.CreateTestPerson();
        person.FederalAnnualWithholding = 0;
        person.StateAnnualWithholding = 0;
        var ledger = new TaxLedger();

        var result = Payday.WithholdTaxesFromPaycheck(person, _testDate, ledger, grossMonthlyPay);

        Assert.Equal(1_376.75m, result.amount);
    }

    [Fact(DisplayName = "§2 — Additional Medicare 0.9% surcharge is zero when income at or below $250,000")]
    public void WithholdTaxesFromPaycheck_SalaryBelowThreshold_NoAdditionalMedicare()
    {
        // Annual salary = $240,000 (grossMonthlyPay = $20,000): below the $250,000 threshold.
        // Expected monthly withholding (federal and state withholding = 0):
        //   OASDI:             min(0.062 × $240,000, $11,439) / 12 = $11,439 / 12 = $953.25
        //   Standard Medicare: 0.0145 × $240,000 / 12             = $290.00
        //   Additional Medicare: 0 (below threshold)
        //   Total = $953.25 + $290.00 = $1,243.25
        var grossMonthlyPay = 20_000m;
        var person = TestDataManager.CreateTestPerson();
        person.FederalAnnualWithholding = 0;
        person.StateAnnualWithholding = 0;
        var ledger = new TaxLedger();

        var result = Payday.WithholdTaxesFromPaycheck(person, _testDate, ledger, grossMonthlyPay);

        Assert.Equal(1_243.25m, result.amount);
    }
}
