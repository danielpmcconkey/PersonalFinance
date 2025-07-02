namespace Lib.StaticConfig;

public static class TaxConstants
{
    public static (decimal rate, decimal min, decimal max)[] _incomeTaxBrackets;
    public static (decimal rate, decimal min, decimal max)[] _capitalGainsBrackets;
    public static Dictionary<int, decimal> _rmdTable;
    

    public static decimal _standardDeduction = 30000.0M;
    public static decimal _ncFiatTaxRate = 0.0399M;
    public static decimal MaxSocialSecurityTaxPercent = 0.85M;
    public static decimal BaseIncomeTarget = 80000.0M; // the income target you start with to minimize income tax. this will get adjusted over time in the tax ledger class

    static TaxConstants()
    {
        _incomeTaxBrackets = [
            (0.1M, 0M, 23850.0M),
            (0.12M, 23850.0M, 96950.0M),
            (0.22M, 96950.0M, 206700.0M),
            (0.24M, 206700.0M, 394600.0M),
            (0.32M, 394600.0M, 501050.0M),
            (0.35M, 501050.0M, 751600.0M),
            (0.37M, 751600.0M, decimal.MaxValue),
        ];
        _capitalGainsBrackets = [
            (0M, 0M, 94050.0M),
            (0.15M, 94050.0M, 583750.0M),
            (0.20M, 583750.0M, decimal.MaxValue),
        ];
        _rmdTable = [];
        _rmdTable[2048] = 26.5M; // age 73
        _rmdTable[2049] = 25.5M; // age 74
        _rmdTable[2050] = 24.6M; // age 75
        _rmdTable[2051] = 23.7M; // age 76
        _rmdTable[2052] = 22.9M; // age 77
        _rmdTable[2053] = 22.0M; // age 78
        _rmdTable[2054] = 21.1M; // age 79
        _rmdTable[2055] = 20.2M; // age 80
        _rmdTable[2056] = 19.4M; // age 81
        _rmdTable[2057] = 18.5M; // age 82
        _rmdTable[2058] = 17.7M; // age 83
        _rmdTable[2059] = 16.8M; // age 84
        _rmdTable[2060] = 16.0M; // age 85
        _rmdTable[2061] = 15.2M; // age 86
        _rmdTable[2062] = 14.4M; // age 87
        _rmdTable[2063] = 13.7M; // age 88
        _rmdTable[2064] = 12.9M; // age 89
        _rmdTable[2065] = 12.2M; // age 90

    }
}