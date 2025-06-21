namespace Lib.MonteCarlo;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

public class ReconciliationLedger
{
    private Dictionary<int, ReconciliationLineItem>? _reconciliationLineItems;
    private CorePackage _corePackage;
    private int _ordinal = 0;
    

    public ReconciliationLedger(CorePackage corePackage)
    {
        _corePackage = corePackage;
        if(_corePackage.DebugMode) _reconciliationLineItems = [];
        else _reconciliationLineItems = null;
    }
    public void AddLine(ReconciliationLineItem item)
    {
        if (_corePackage.DebugMode == false || _reconciliationLineItems is null) return;
        _reconciliationLineItems.Add(_ordinal, item);
        _ordinal++;
    }

    public void ExportToSpreadsheet()
    {
        if (_corePackage.DebugMode == false || _reconciliationLineItems == null || !_reconciliationLineItems.Any()) 
            return;
        
        string filePath = _corePackage.ReconFilePath;

        using var spreadsheet = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);
    
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
            Name = "Reconciliation"
        };
        sheets.AppendChild(sheet);
    
        // Get sheet data
        var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
    
        // Add header row
        var headerRow = new Row();
        var headers = new[]
        {
            "Ordinal",
            "Date",
            "Age",
            "Amount",
            "Description",
            "Type",
            "CurrentMonthGrowthRate",
            "TotalNetWorth",
            "TotalLongTermInvestment",
            "TotalMidTermInvestment",
            "TotalShortTermInvestment",
            "TotalCash",
            "TotalDebt",
            "TotalSpendLifetime",
            "TotalInvestmentAccrualLifetime",
            "TotalDebtAccrualLifetime",
            "TotalSocialSecurityWageLifetime",
            "TotalDebtPaidLifetime",
            "IsRetired",
            "IsBankrupt,",
            "AreWeInARecession",
            "AreWeInExtremeAusterityMeasures",
        };
        foreach (var header in headers)
        {
            headerRow.AppendChild(CreateCell(header));
        }
        sheetData.AppendChild(headerRow);
    
        // Add data rows
        foreach (var item in _reconciliationLineItems)
        {
            var row = new Row();
            row.AppendChild(CreateCell(item.Key));
            row.AppendChild(CreateCell(item.Value.Date));
            row.AppendChild(CreateCell(item.Value.Age));
            row.AppendChild(CreateCell(item.Value.Amount)); 
            row.AppendChild(CreateCell(item.Value.Description));
            row.AppendChild(CreateCell(item.Value.Type.ToString()));
            row.AppendChild(CreateCell(item.Value.CurrentMonthGrowthRate));
            row.AppendChild(CreateCell(item.Value.TotalNetWorth));
            row.AppendChild(CreateCell(item.Value.TotalLongTermInvestment));
            row.AppendChild(CreateCell(item.Value.TotalMidTermInvestment));
            row.AppendChild(CreateCell(item.Value.TotalShortTermInvestment));
            row.AppendChild(CreateCell(item.Value.TotalCash));
            row.AppendChild(CreateCell(item.Value.TotalDebt));
            row.AppendChild(CreateCell(item.Value.TotalSpendLifetime));
            row.AppendChild(CreateCell(item.Value.TotalInvestmentAccrualLifetime));
            row.AppendChild(CreateCell(item.Value.TotalDebtAccrualLifetime));
            row.AppendChild(CreateCell(item.Value.TotalSocialSecurityWageLifetime));
            row.AppendChild(CreateCell(item.Value.TotalDebtPaidLifetime));
            row.AppendChild(CreateCell(item.Value.IsRetired.ToString()));
            row.AppendChild(CreateCell(item.Value.IsBankrupt.ToString()));
            row.AppendChild(CreateCell(item.Value.AreWeInARecession.ToString()));
            row.AppendChild(CreateCell(item.Value.AreWeInExtremeAusterityMeasures.ToString()));
        
            sheetData.AppendChild(row);
        }
    
        workbookPart.Workbook.Save();
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