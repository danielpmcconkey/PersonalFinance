namespace Lib.StaticConfig;

public static class TaxConstants
{
    public static readonly (decimal rate, decimal min, decimal max)[] Federal1040TaxTableBrackets = [
        (0.1M, 0M, 23850.0M),
        (0.12M, 23850.0M, 96950.0M),
        (0.22M, 96950.0M, 206700.0M),
        (0.24M, 206700.0M, 394600.0M),
        (0.32M, 394600.0M, 501050.0M),
        (0.35M, 501050.0M, 751600.0M),
        (0.37M, 751600.0M, decimal.MaxValue),
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

    public static readonly Dictionary<int, decimal> RmdTable;
    /*
     * Married filing jointly or Qualifying surviving spouse, $29,200
     */
    public static readonly decimal FederalStandardDeduction = 29200.0M;
    public static readonly decimal NorthCarolinaFlatTaxRate = 0.0399M;
    public static readonly decimal MaxSocialSecurityTaxPercent = 0.85M;
    public static readonly decimal PlaceholderLastYearsSocialSecurityIncome = 48000.0M; // in case you're calculating income head room and haven't had any social security yet.

    static TaxConstants()
    {
        
        // lifetime expectancy factor table: https://www.irs.gov/publications/p590b#en_US_2024_publink100089977
        // see Appendix B. Uniform Lifetime Table for my situation
        RmdTable = [];
        RmdTable[2048] = 26.5M; // age 73
        RmdTable[2049] = 25.5M; // age 74
        RmdTable[2050] = 24.6M; // age 75
        RmdTable[2051] = 23.7M; // age 76
        RmdTable[2052] = 22.9M; // age 77
        RmdTable[2053] = 22.0M; // age 78
        RmdTable[2054] = 21.1M; // age 79
        RmdTable[2055] = 20.2M; // age 80
        RmdTable[2056] = 19.4M; // age 81
        RmdTable[2057] = 18.5M; // age 82
        RmdTable[2058] = 17.7M; // age 83
        RmdTable[2059] = 16.8M; // age 84
        RmdTable[2060] = 16.0M; // age 85
        RmdTable[2061] = 15.2M; // age 86
        RmdTable[2062] = 14.4M; // age 87
        RmdTable[2063] = 13.7M; // age 88
        RmdTable[2064] = 12.9M; // age 89
        RmdTable[2065] = 12.2M; // age 90

    }
}