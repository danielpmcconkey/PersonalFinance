namespace Lib.Spreadsheets;

public class SpreadsheetColumn
{
    public required int Ordinal;
    public required string Header;
    public required SpreadsheetColumnType ColumnType;
    public string PropertyName;
    public string? Format; // todo: implement cell formats
}