namespace Lib.StaticConfig;

public static class TaxConstants
{
    // todo: update tax constants and forms to 2025 versions
    // todo: organize TaxConstants better
    
    public static readonly (decimal rate, decimal min, decimal max)[] Federal1040TaxTableBrackets = [
        /*
         * https://taxfoundation.org/data/all/federal/2024-tax-brackets/
         */
        (0.1M, 0M, 23200.0M),
        (0.12M, 23200.0M, 94300.0M),
        (0.22M, 94300.0M, 201050.0M),
        (0.24M, 201050.0M, 383900.0M),
        (0.32M, 383900.0M, 487450.0M),
        (0.35M, 487450.0M, 731200.0M),
        (0.37M, 731200.0M, decimal.MaxValue),
    ];
    
    public static readonly (decimal rate, decimal min, decimal max, decimal subtractions)[]
        Fed1040TaxComputationWorksheetBrackets = [
            /*
             * https://www.irs.gov/pub/irs-pdf/i1040gi.pdf?os=wtmbzegmu5hwrefapp&ref=app
             * page 76, section B
             */
            (.22m, 100000m, 201050m, 9894m),
            (.24m, 201050m, 383900m, 13915m),
            (.32m, 383900m, 487450m, 44627m),
            (.35m, 487450m, 731200m, 59250.5m),
            (.37m, 731200m, decimal.MaxValue, 73874.5m),
        ];
    
    public static readonly (decimal rate, decimal min, decimal max)[] FederalCapitalGainsBrackets = [
        /*
         * https://taxfoundation.org/data/all/federal/2024-tax-brackets/
         */
        (0.0m, 0m, 94050m),
        (0.15m, 94050m, 583750m),
        (0.20m, 583750m, decimal.MaxValue),
    ];

    //public static readonly Dictionary<int, decimal> RmdTable;
    /*
     * Married filing jointly or Qualifying surviving spouse, $29,200
     */
    public const decimal FederalStandardDeduction = 29200.0M;
    public const decimal NcStandardDeduction = 25500m;
    public const decimal NorthCarolinaFlatTaxRate = 0.045M;
    public const decimal MaxSocialSecurityTaxPercent = 0.85M;
    //public const decimal PlaceholderLastYearsSocialSecurityIncome = 48000.0M; // in case you're calculating income head room and haven't had any social security yet.
    public const decimal SocialSecurityWorksheetCreditLine8 = 32000m;
    public const decimal SocialSecurityWorksheetCreditLine10 = 12000m;
    public const decimal ScheduleDMaximumCapitalLoss = -3000m;
    public const decimal FxaixAnnualDividendYield = 0.012m; // based on July 20, 2025 // todo: take dividends out of stock price and record them for tax purposes
    public const decimal DividendPercentQualified = 0.95m; // 5% ordinary, 95% qualified
    // public const decimal MaximumCapitalGains15Percent = ;
    //public const decimal MaximumCapitalGains0Percent = ;
    public const decimal FederalWorksheetVsTableThreshold = 100000m; // below this amount, use the table; above or equal, use the worksheet

    public static readonly (decimal rate, int age)[] RmdTable =
    [
        /*
         * lifetime expectancy factor table: https://www.irs.gov/publications/p590b#en_US_2024_publink100089977
           see Appendix B. Uniform Lifetime Table for my situation
         */
        (26.5M, 73),
        (25.5M, 74),
        (24.6M, 75),
        (23.7M, 76),
        (22.9M, 77),
        (22.0M, 78),
        (21.1M, 79),
        (20.2M, 80),
        (19.4M, 81),
        (18.5M, 82),
        (17.7M, 83),
        (16.8M, 84),
        (16.0M, 85),
        (15.2M, 86),
        (14.4M, 87),
        (13.7M, 88),
        (12.9M, 89),
        (12.2M, 90),
    ];
}