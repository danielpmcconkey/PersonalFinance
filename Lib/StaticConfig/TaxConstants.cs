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
        _rmdTable[2048] = 0.0265M; // age 73
        _rmdTable[2049] = 0.0255M; // age 74
        _rmdTable[2050] = 0.0246M; // age 75
        _rmdTable[2051] = 0.0237M; // age 76
        _rmdTable[2052] = 0.0229M; // age 77
        _rmdTable[2053] = 0.0220M; // age 78
        _rmdTable[2054] = 0.0211M; // age 79
        _rmdTable[2055] = 0.0202M; // age 80
        _rmdTable[2056] = 0.0194M; // age 81
        _rmdTable[2057] = 0.0185M; // age 82
        _rmdTable[2058] = 0.0177M; // age 83
        _rmdTable[2059] = 0.0168M; // age 84
        _rmdTable[2060] = 0.0160M; // age 85
        _rmdTable[2061] = 0.0152M; // age 86
        _rmdTable[2062] = 0.0144M; // age 87
        _rmdTable[2063] = 0.0137M; // age 88
        _rmdTable[2064] = 0.0129M; // age 89
        _rmdTable[2065] = 0.0122M; // age 90
    }
}