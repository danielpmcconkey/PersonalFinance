using Lib.MonteCarlo.TaxForms.Federal;

namespace Lib.Tests.MonteCarlo.TaxForms.Federal;

public class QualifiedDividendsAndCapitalGainTaxWorksheetTestsExtended
{
    [Fact(DisplayName = "§1.3 — Worksheet result cannot exceed regular tax on full income as ordinary income")]
    public void CalculateTaxOwed_WithQualifiedGains_WorksheetResultNeverExceedsRegularTax()
    {
        // $200,000 taxable income: $100,000 ordinary + $50,000 qualified dividends + $50,000 long-term gains.
        // The worksheet applies preferential rates on gains; the result must be ≤ the ordinary-income tax on $200k.
        //
        // Traced through QualifiedDividendsAndCapitalGainTaxWorksheet:
        //   line5 (ordinary)     = 200,000 − 100,000 = 100,000
        //   line9 (gains at 0%)  = 94,050 − 94,050   = 0        (ordinary fills the 0% bracket)
        //   line17 (gains at 15%)= 100,000
        //   line18               = 100,000 × 0.15    = 15,000
        //   line22 (ordinary tax)= 0.22 × 100,000 − 9,894 = 12,106  (TaxComputationWorksheet)
        //   line23               = 15,000 + 12,106   = 27,106
        //   line24 (regular tax) = 0.24 × 200,000 − 13,915 = 34,085
        //   line25               = min(27,106, 34,085) = 27,106
        decimal fed1040Line15 = 200_000m;
        decimal fed1040Line3A = 50_000m;
        decimal scheduleDLine15 = 50_000m;
        decimal scheduleDLine16 = 50_000m;

        var worksheetTax = QualifiedDividendsAndCapitalGainTaxWorksheet.CalculateTaxOwed(
            scheduleDLine15, scheduleDLine16, fed1040Line3A, fed1040Line15);

        var regularOrdinaryTax = TaxComputationWorksheet.CalculateTaxOwed(fed1040Line15);

        Assert.Equal(27_106m, worksheetTax);
        Assert.True(worksheetTax <= regularOrdinaryTax,
            $"Worksheet tax {worksheetTax:C} must not exceed regular tax {regularOrdinaryTax:C}");
    }

    [Fact(DisplayName = "§1.3 — Boundary: income exactly at 0% ceiling ($94,050) produces zero capital gains tax")]
    public void CalculateTaxOwed_IncomeExactlyAt0PercentCeiling_ZeroCapitalGainsTax()
    {
        // Taxable income = $94,050 (the 0%/15% boundary for MFJ), entirely composed of qualified dividends.
        // There is no ordinary income, so the entire qualified dividend amount falls in the 0% bracket.
        //
        // Traced through QualifiedDividendsAndCapitalGainTaxWorksheet:
        //   line5 (ordinary) = max(0, 94,050 − 94,050) = 0
        //   line9 (at 0%)    = 94,050 − 94,050         = 0     (all ordinary income fills the bracket)
        //   Wait — ordinary = 0, so:
        //   line8 = min(line5=0, line7=94,050) = 0
        //   line9 = line7 − line8 = 94,050            (all qualifies at 0%)
        //   line18, line21 = 0
        //   line22 = TaxTable/Worksheet(0) = 0
        //   line23 = 0, line24 = TaxTable(94,050)
        //   line25 = min(0, line24) = 0
        decimal fed1040Line15 = 94_050m;
        decimal fed1040Line3A = 94_050m; // all income is qualified dividends
        decimal scheduleDLine15 = 0m;
        decimal scheduleDLine16 = 0m;

        var result = QualifiedDividendsAndCapitalGainTaxWorksheet.CalculateTaxOwed(
            scheduleDLine15, scheduleDLine16, fed1040Line3A, fed1040Line15);

        Assert.Equal(0m, result);
    }
}
