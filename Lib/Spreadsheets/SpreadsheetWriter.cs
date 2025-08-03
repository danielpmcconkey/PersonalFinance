using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using NodaTime;
using System.Reflection;

namespace Lib.Spreadsheets;
public class SpreadsheetWriter
{
    private string _filePath;
    private string _sheetName;
    private List<SpreadsheetColumn> _columns;
    public SpreadsheetWriter(string filePath, string sheetName, List<SpreadsheetColumn> columns)
    {
        _filePath = filePath;
        _sheetName = sheetName;
        _columns = columns;
    }

    public void CreateSpreadsheet(IEnumerable<object> data)
    {
        if(data.Count() > 10000) throw new Exception($"Too many rows {data.Count()}");
        
        using var spreadsheet = SpreadsheetDocument.Create(_filePath, SpreadsheetDocumentType.Workbook);
    
        // Add workbook part
        var workbookPart = spreadsheet.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
    
        // Add worksheet part
        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        worksheetPart.Worksheet = new Worksheet(new SheetData());
    
        // Add sheet to workbook
        var sheets = spreadsheet.WorkbookPart.Workbook.AppendChild(new Sheets());
        var sheet = new Sheet() 
        { 
            Id = spreadsheet.WorkbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = _sheetName
        };
        sheets.AppendChild(sheet);
        
        // get sheet data
        var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
        
        // Add header row
        var headerRow = new Row();
        foreach (var column in _columns.OrderBy(x => x.Ordinal))
        {
            headerRow.AppendChild(CreateHeaderCell(column.Header));
        }
        sheetData.AppendChild(headerRow);
        
        // Add data rows
        foreach (var item in data)
        {
            var dataRow = new Row();
            foreach (var column in _columns.OrderBy(x => x.Ordinal))
            {
                var propertyValue = GetPropertyValue(item, column.PropertyName);
                var cell = CreateCellBasedOnType(propertyValue, column.ColumnType);
                dataRow.AppendChild(cell);
            }
            sheetData.AppendChild(dataRow);
        }
        workbookPart.Workbook.Save();
    }
    

    private object GetPropertyValue(object item, string propertyName)
    {
        
        var itemType = item.GetType();
        var props = itemType.GetProperties();
        var property =  itemType.GetProperty(propertyName);//?.GetValue(item);
        if (property is null) return null;
        var value = property.GetValue(item);
        return value;
    }
    private Cell CreateCellBasedOnType(object? value, SpreadsheetColumnType columnType)
    {
        if (value == null)
            return CreateCell(string.Empty);

        return columnType switch
        {
            SpreadsheetColumnType.Decimal => CreateCell(Convert.ToDecimal(value)),
            SpreadsheetColumnType.Integer => CreateCell(Convert.ToInt32(value)),
            SpreadsheetColumnType.DateTime => CreateCell((LocalDateTime)value),
            _ => CreateCell(value.ToString())
        };
    }


    private Cell CreateHeaderCell(string text)
    {
        return new Cell 
        { 
            DataType = CellValues.String,
            CellValue = new CellValue(text)
        };
    }
    private Cell CreateCell(string text)
    {
        return new Cell 
        { 
            DataType = CellValues.String,
            CellValue = new CellValue(text)
        };
    }
    private Cell CreateCell(decimal value)
    {
        return new Cell 
        { 
            DataType = CellValues.Number,
            CellValue = new CellValue(value)
        };
    }
    private Cell CreateCell(int value)
    {
        return new Cell 
        { 
            DataType = CellValues.Number,
            CellValue = new CellValue(value)
        };
    }
    private Cell CreateCell(NodaTime.LocalDateTime value)
    {
        var month = value.Date.Month;
        var year = value.Date.Year;
        var day = value.Date.Day;
        var datetime = new DateTime(year, month, day);
        return new Cell 
        { 
            DataType = CellValues.Date,
            CellValue = new CellValue(datetime)
        };
    }
}