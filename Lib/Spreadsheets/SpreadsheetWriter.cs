using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using NodaTime;

namespace Lib.Spreadsheets;
public class SpreadsheetWriter(string filePath, string sheetName, List<SpreadsheetColumn> columns)
{
    public void CreateSpreadsheet(IEnumerable<object> data)
    {
        var enumerable = data.ToList();
        if (enumerable.Count > 5000)
        {
            // truncate the data set
            CreateSpreadsheet( enumerable.Take(5000));
            return;
        }
        
        using var spreadsheet = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);
    
        // Add workbook part
        var workbookPart = spreadsheet.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
    
        // Add the worksheet part
        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        worksheetPart.Worksheet = new Worksheet(new SheetData());
    
        // Add the sheet to the workbook
        var sheets = spreadsheet.WorkbookPart?.Workbook.AppendChild(new Sheets());
        var sheet = new Sheet() 
        { 
            Id = spreadsheet.WorkbookPart?.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = sheetName
        };
        sheets?.AppendChild(sheet);
        
        // get sheet data
        var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
        
        // Add the header row
        var headerRow = new Row();
        foreach (var column in columns.OrderBy(x => x.Ordinal))
        {
            headerRow.AppendChild(CreateHeaderCell(column.Header));
        }
        sheetData?.AppendChild(headerRow);
        
        // Add data rows
        foreach (var item in enumerable)
        {
            var dataRow = new Row();
            foreach (var column in columns.OrderBy(x => x.Ordinal))
            {
                var propertyValue = GetPropertyValue(item, column.PropertyName);
                var cell = CreateCellBasedOnType(propertyValue, column.ColumnType);
                dataRow.AppendChild(cell);
            }
            sheetData?.AppendChild(dataRow);
        }
        workbookPart.Workbook.Save();
    }
    

    private static object? GetPropertyValue(object item, string propertyName)
    {
        
        var itemType = item.GetType();
        var props = itemType.GetProperties();
        var property =  itemType.GetProperty(propertyName);
        var value = property?.GetValue(item);
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
    private Cell CreateCell(string? text)
    {
        return new Cell 
        { 
            DataType = CellValues.String,
            CellValue = new CellValue(text ?? string.Empty)
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