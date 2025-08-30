namespace Lib.StaticConfig;

public static class PresentationConfig
{
    public const string AccountingFormat = "#,##0.00;(#,##0.00);--";
    public const string HomeDebtAccountName = "Home loan";
    public const string HomeInvestementAccountName = "Home Equity";
    public static string PresentationOutputDir;
    public static Dictionary<int, string> MothAbbreviations = [];

    static PresentationConfig()
    {
        PresentationOutputDir = ConfigManager.ReadStringSetting("PresentationOutputDir");
        MothAbbreviations.Add(1, "Jan");
        MothAbbreviations.Add(2, "Feb");
        MothAbbreviations.Add(3, "Mar");
        MothAbbreviations.Add(4, "Apr");
        MothAbbreviations.Add(5, "May");
        MothAbbreviations.Add(6, "Jun");
        MothAbbreviations.Add(7, "Jul");
        MothAbbreviations.Add(8, "Aug");
        MothAbbreviations.Add(9, "Sep");
        MothAbbreviations.Add(10, "Oct");
        MothAbbreviations.Add(11, "Nov");
        MothAbbreviations.Add(12, "Dec");
    }
}