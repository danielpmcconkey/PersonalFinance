namespace Lib.StaticConfig;

public static class TaxConstants
{
    public static (long rate, long min, long max)[] _incomeTaxBrackets;
    public static (long rate, long min, long max)[] _capitalGainsBrackets;
    public static Dictionary<int, long> _rmdTable;
    

    public static long _standardDeduction = 300000000L;
    public static long _ncFiatTaxRate = 399L;
    public static long MaxSocialSecurityTaxPercent = 8500L; // 85%

    static TaxConstants()
    {
        _incomeTaxBrackets = [
            (1000L, 0L, 238500000L),
            (1200L, 238500000L, 969500000L),
            (2200L, 969500000L, 2067000000L),
            (2400L, 2067000000L, 3946000000L),
            (3200L, 3946000000L, 5010500000L),
            (3500L, 5010500000L, 7516000000L),
            (3700L, 7516000000L, long.MaxValue),
        ];
        _capitalGainsBrackets = [
            (0L, 0L, 940500000L),
            (1500L, 940500000L, 5837500000L),
            (2000L, 5837500000L, long.MaxValue),
        ];
        _rmdTable = [];
        _rmdTable[2048] = 265000L; // age 73
        _rmdTable[2049] = 255000L; // age 74
        _rmdTable[2050] = 246000L; // age 75
        _rmdTable[2051] = 237000L; // age 76
        _rmdTable[2052] = 229000L; // age 77
        _rmdTable[2053] = 220000L; // age 78
        _rmdTable[2054] = 211000L; // age 79
        _rmdTable[2055] = 202000L; // age 80
        _rmdTable[2056] = 194000L; // age 81
        _rmdTable[2057] = 185000L; // age 82
        _rmdTable[2058] = 177000L; // age 83
        _rmdTable[2059] = 168000L; // age 84
        _rmdTable[2060] = 160000L; // age 85
        _rmdTable[2061] = 152000L; // age 86
        _rmdTable[2062] = 144000L; // age 87
        _rmdTable[2063] = 137000L; // age 88
        _rmdTable[2064] = 129000L; // age 89
        _rmdTable[2065] = 122000L; // age 90

    }
}