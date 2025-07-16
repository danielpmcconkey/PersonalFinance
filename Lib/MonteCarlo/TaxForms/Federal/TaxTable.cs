using Lib.StaticConfig;

namespace Lib.MonteCarlo.TaxForms.Federal;

public static class TaxTable
{
    /*
     * https://www.irs.gov/pub/irs-pdf/i1040gi.pdf?os=wtmbzegmu5hwrefapp&ref=app
     * page 64
     */
    public static decimal CalculateTaxOwed(decimal amount)
    {
        if (amount > 100000) throw new InvalidDataException("can't use the tax table with income over 100k");
        const decimal tableGrain = 50m; // the table increases in lumps of 50
        /*
         * the actual table uses an average between a low round number and a high round number. for example:
         *
         * actual taxable income: $65,013.23
         * precise calc based on brackets would be: $7,337.59
         * but the tax table says it should be: $7,339
         * that's an average between the precise value for $65,000 and $65,050
         */

        var floor = Math.Floor(amount / tableGrain);
        var ceiling = Math.Ceiling(amount / tableGrain);


        if (floor == ceiling)
        {
            // if floor and ceiling are the same amount, your amount is the floor and you need to calculate the average
            // between it and $50 higher
            ceiling += 1;
        }
        var liabilityAtFloor = CalculatePreciseLiability(floor * tableGrain);
        var liabilityAtCeiling = CalculatePreciseLiability(ceiling * tableGrain);
        return (liabilityAtFloor + liabilityAtCeiling) / 2m;
        
    }
    private static decimal CalculatePreciseLiability(decimal amount)
    {
        // this calculates the to-the-penny amount.
        var totalLiability = 0m;
        // tax on ordinary income
        foreach (var bracket in TaxConstants.Federal1040TaxTableBrackets)
        {
            var amountInBracket =
                    amount
                    - (Math.Max(amount, bracket.max) - bracket.max) // amount above max
                    - (Math.Min(amount, bracket.min)) // amount below min
                ;
            totalLiability += (amountInBracket * bracket.rate);
        }
        return totalLiability;
    }
}