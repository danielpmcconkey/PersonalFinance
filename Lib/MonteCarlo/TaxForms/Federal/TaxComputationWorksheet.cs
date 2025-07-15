using Lib.StaticConfig;

namespace Lib.MonteCarlo.TaxForms.Federal;

public static class TaxComputationWorksheet
{
    /*
     * https://www.irs.gov/pub/irs-pdf/i1040gi.pdf?os=wtmbzegmu5hwrefapp&ref=app
     * page 76
     * Section B
     */
    public static decimal CalculateTaxOwed(decimal amount)
    {
        foreach (var bracket in TaxConstants.Fed1040TaxComputationWorksheetBrackets)
        {
            if (amount >= bracket.min && amount <= bracket.max)
                return (amount * bracket.rate) - bracket.subtractions;
        }
        
        
        throw new InvalidDataException(
            "We should never get here, something went wrong with the FederalTaxComputationWorksheet");
    }
}