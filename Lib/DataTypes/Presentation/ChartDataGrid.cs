using System.Text;

namespace Lib.DataTypes.Presentation;

public record ChartDataGrid
{
    public required string[] ColumnNames { get; set; }
    public required string[][] Data { get; set; }
    
}