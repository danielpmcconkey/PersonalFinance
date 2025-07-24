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
        if (amount > TaxConstants.FederalWorksheetVsTableThreshold) throw new InvalidDataException("can't use the tax table with income over 100k");
        
        /*
         * very small amounts won't be calculated. just use hard-wired constants. These numbers aren't likely to change
         */ 
        if (amount <= 5) return 0;
        if (amount < 15) return 1;
        if (amount < 25) return 2;
        /*
         * above $25, the table assigns one specific value to blocks of numbers. Those blocks go up in $25 increments
         * from $25 to $3000. From $3k to $100k, they then go up in increments of $50
         */
        var tableGrain = (amount < 3000) ? 25m : 50m;
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
        // round between the two values, but use AwayFromZero mode to match the IRS's table (standard dot net seems to
        // use banker's rounding)
        return Math.Round((liabilityAtFloor + liabilityAtCeiling) / 2m, 0, MidpointRounding.AwayFromZero);
        
    }
    public static decimal CalculatePreciseLiability(decimal amount)
    {
        // this calculates the to-the-penny amount.
        var totalLiability = 0m;
        // tax on ordinary income
        foreach (var bracket in TaxConstants.Federal1040TaxTableBrackets)
        {
            if(amount < bracket.min) continue;
            var amountInBracket = 0m;
            if(amount >= bracket.max) amountInBracket = bracket.max - bracket.min;
            else amountInBracket = amount - bracket.min;
            totalLiability += (amountInBracket * bracket.rate);
        }
        return totalLiability;
    }
}