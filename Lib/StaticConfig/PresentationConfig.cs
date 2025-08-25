namespace Lib.StaticConfig;

public static class PresentationConfig
{
    public const string AccountingFormat = "#,##0.00;(#,##0.00);--";
    public const string HomeDebtAccountName = "Home loan";
    public const string HomeInvestementAccountName = "Home Equity";
    public static string PresentationOutputDir;

    static PresentationConfig()
    {
        PresentationOutputDir = ConfigManager.ReadStringSetting("PresentationOutputDir");
    }
}