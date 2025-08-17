namespace Lib.Spreadsheets;

public class SpreadsheetColumn
{
    public required int Ordinal { get; init; }
    public required string Header { get; init; }
    public required SpreadsheetColumnType ColumnType { get; init; }
    public required string PropertyName { get; init; }
    public string Format { get; init; } = string.Empty; // todo: implement cell formats
}