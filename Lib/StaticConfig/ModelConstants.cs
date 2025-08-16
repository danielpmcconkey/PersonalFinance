namespace Lib.StaticConfig;

public static class ModelConstants
{
    /// <summary>
    /// MajorVersion increments only when you've implemented a major feature change
    /// </summary>
    public static int MajorVersion = 0;
    /// <summary>
    /// MinorVersion is your general version increment. If two model runs have differing major and minor numbers then
    /// you should consider them as apples-to-oranges comparisons  
    /// </summary>
    public static int MinorVersion = 6; // just updated personal finance in the DB
    /// <summary>
    /// PatchVersion can be used when a change doesn't affect the LifeSimulator.Run outcome. If two model runs have the
    /// same major and minor versions, but their patch version differs, you can still consider them apples-to-apples
    /// comparisons
    /// </summary>
    public static int PatchVersion = 0;
    
    public static decimal FunPenaltyBankruptcy = -35000m;
    public static decimal FunBonusRetirement = 1000m; // just fun points, not a modifier. just "hey, we're free"
    public static decimal FunPenaltyNotRetiredPercentOfRequiredSpend = 3m;
    public static decimal FunPenaltyRetiredInRecessionPercentOfRequiredSpend = 0.5m;
    public static decimal FunPenaltyRetiredInExtremeAusterityPercentOfRequiredSpend = 1m;
    
    public static (int years, int months) RetirementAgeMin = (59, 6);
    public static (int years, int months) RetirementAgeMax = (63, 0);
    
    public static (int years, int months) SocialSecurityElectionStartMin = (62, 1);
    public static (int years, int months) SocialSecurityElectionStartMax = (70, 0);
    
    /// <summary>
    /// the percentage of desired monthly spend that you actually spend when in a recession 
    /// </summary>
    public const decimal AusterityRatioMin = 0.5M;
    public const decimal AusterityRatioMax = 1.25M;
    
    /// <summary>
    /// the percentage of desired monthly spend that you actually spend when times are really tough
    /// </summary>
    public const decimal ExtremeAusterityRatioMin = 0.25M;
    public const decimal ExtremeAusterityRatioMax = 1.0M;

    /// <summary>
    /// If net worth is below this level, you go into extreme austerity
    /// </summary>
    public const decimal ExtremeAusterityNetWorthTriggerMin = 250000m;
    public const decimal ExtremeAusterityNetWorthTriggerMax = 1500000m;

    public const int NumMonthsCashOnHandMin = 1;
    public const int NumMonthsCashOnHandMax = 60;
    
    public const int NumMonthsMidBucketOnHandMin = 1;
    public const int NumMonthsMidBucketOnHandMax = 60;
    
    public const int NumMonthsPriorToRetirementToBeginRebalanceMin = 0;
    public const int NumMonthsPriorToRetirementToBeginRebalanceMax = 60;
    
    public const int RecessionCheckLookBackMonthsMin = 1;
    public const int RecessionCheckLookBackMonthsMax = 24;
    
    /// <summary>
    /// what percentage of the previous high water mark (recession recover point) you need the current prices to be
    /// before declaring yourself done with the recession eg:
    ///     isRecessionOver = (currentPrice > recessionRecoveryPoint * recessionRecoveryPointModifier)) ? 
    ///          true : false
    /// </summary>
    public const decimal RecessionRecoveryPointModifierMin = 0.9M;
    public const decimal RecessionRecoveryPointModifierMax = 1.5M;

    /// <summary>
    /// the amount of fun bucks you get to blow pre retirement (above + beyond required spend)
    /// </summary>
    public const decimal DesiredMonthlySpendPreRetirementMin = 500m;
    public const decimal DesiredMonthlySpendPreRetirementMax = 20000m;
    
    // <summary>
    /// the amount of fun bucks you get to blow post retirement (above + beyond required spend)
    /// </summary>
    public const decimal DesiredMonthlySpendPostRetirementMin = 500m;
    public const decimal DesiredMonthlySpendPostRetirementMax = 20000m;

    /// <summary>
    /// the percentage of teh overall employee contribution goes to teh traditional 401K account
    /// </summary>
    public const decimal Percent401KTraditionalMin = 0m;
    public const decimal Percent401KTraditionalMax = 1m;

}