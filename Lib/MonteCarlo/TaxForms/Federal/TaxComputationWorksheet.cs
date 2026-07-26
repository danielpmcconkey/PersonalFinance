using Lib.StaticConfig;

namespace Lib.MonteCarlo.TaxForms.Federal;

public static class TaxComputationWorksheet
{
    /*
     * https://www.irs.gov/pub/irs-pdf/i1040gi.pdf?os=wtmbzegmu5hwrefapp&ref=app
     * page 76
     * Section B
     */
    public static decimal CalculateTaxOwed(decimal amount, decimal cumulativeCpiMultiplier = 1m)
    {
        foreach (var bracket in TaxConstants.Fed1040TaxComputationWorksheetBrackets)
        {
            var scaledMin = bracket.min * cumulativeCpiMultiplier;
            var scaledMax = bracket.max == decimal.MaxValue ? decimal.MaxValue : bracket.max * cumulativeCpiMultiplier;
            var scaledSubtractions = bracket.subtractions * cumulativeCpiMultiplier;
            if (amount >= scaledMin && amount <= scaledMax)
                return (amount * bracket.rate) - scaledSubtractions;
        }


        throw new InvalidDataException(
            "We should never get here, something went wrong with the FederalTaxComputationWorksheet");
    }
}