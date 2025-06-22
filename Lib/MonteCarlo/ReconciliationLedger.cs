namespace Lib.MonteCarlo;
using Lib.Spreadsheets;


public class ReconciliationLedger
{
    private List<ReconciliationLineItem>? _reconciliationLineItems;
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
        item.Ordinal = _ordinal;
        _reconciliationLineItems.Add(item);
        _ordinal++;
    }

    public void ExportToSpreadsheet()
    {
        if (_corePackage.DebugMode == false || _reconciliationLineItems == null || !_reconciliationLineItems.Any()) 
            return;
        
        string filePath = _corePackage.ReconFilePath;
        List<SpreadsheetColumn> columns =
        [
            new SpreadsheetColumn(){ Ordinal = 0, ColumnType = SpreadsheetColumnType.Integer, Header = "#", PropertyName = "Ordinal" },
            new SpreadsheetColumn(){ Ordinal = 1, ColumnType = SpreadsheetColumnType.DateTime, Header = "Date", PropertyName = "Date" },
            new SpreadsheetColumn(){ Ordinal = 2, ColumnType = SpreadsheetColumnType.Decimal, Header = "Age", PropertyName = "Age" },
            new SpreadsheetColumn(){ Ordinal = 3, ColumnType = SpreadsheetColumnType.Decimal, Header = "Amount", PropertyName = "Amount" },
            new SpreadsheetColumn(){ Ordinal = 4, ColumnType = SpreadsheetColumnType.String, Header = "Description", PropertyName = "Description" },
            new SpreadsheetColumn(){ Ordinal = 5, ColumnType = SpreadsheetColumnType.String, Header = "Type", PropertyName = "Type" },
            new SpreadsheetColumn(){ Ordinal = 6, ColumnType = SpreadsheetColumnType.Decimal, Header = "Current Month Growth Rate", PropertyName = "CurrentMonthGrowthRate" },
            new SpreadsheetColumn(){ Ordinal = 6, ColumnType = SpreadsheetColumnType.Decimal, Header = "Current Long Range Investment Cost", PropertyName = "CurrentLongRangeInvestmentCost" },
            new SpreadsheetColumn(){ Ordinal = 7, ColumnType = SpreadsheetColumnType.Decimal, Header = "Total Net Worth", PropertyName = "TotalNetWorth" },
            new SpreadsheetColumn(){ Ordinal = 8, ColumnType = SpreadsheetColumnType.Decimal, Header = "Total Long Term Investment", PropertyName = "TotalLongTermInvestment" },
            new SpreadsheetColumn(){ Ordinal = 9, ColumnType = SpreadsheetColumnType.Decimal, Header = "Total Mid Term Investment", PropertyName = "TotalMidTermInvestment" },
            new SpreadsheetColumn(){ Ordinal = 10, ColumnType = SpreadsheetColumnType.Decimal, Header = "Total Short Term Investment", PropertyName = "TotalShortTermInvestment" },
            new SpreadsheetColumn(){ Ordinal = 11, ColumnType = SpreadsheetColumnType.Decimal, Header = "Total Cash", PropertyName = "TotalCash" },
            new SpreadsheetColumn(){ Ordinal = 12, ColumnType = SpreadsheetColumnType.Decimal, Header = "Total Debt", PropertyName = "TotalDebt" },
            new SpreadsheetColumn(){ Ordinal = 13, ColumnType = SpreadsheetColumnType.Decimal, Header = "Total Spend Lifetime", PropertyName = "TotalSpendLifetime" },
            new SpreadsheetColumn(){ Ordinal = 14, ColumnType = SpreadsheetColumnType.Decimal, Header = "Total Investment Accrual Lifetime", PropertyName = "TotalInvestmentAccrualLifetime" },
            new SpreadsheetColumn(){ Ordinal = 15, ColumnType = SpreadsheetColumnType.Decimal, Header = "Total Debt Accrual Lifetime", PropertyName = "TotalDebtAccrualLifetime" },
            new SpreadsheetColumn(){ Ordinal = 16, ColumnType = SpreadsheetColumnType.Decimal, Header = "Total Social Security Wage Lifetime", PropertyName = "TotalSocialSecurityWageLifetime" },
            new SpreadsheetColumn(){ Ordinal = 17, ColumnType = SpreadsheetColumnType.Decimal, Header = "Total Debt Paid Lifetime", PropertyName = "TotalDebtPaidLifetime" },
            new SpreadsheetColumn(){ Ordinal = 18, ColumnType = SpreadsheetColumnType.Boolean, Header = "Is Retired", PropertyName = "IsRetired" },
            new SpreadsheetColumn(){ Ordinal = 19, ColumnType = SpreadsheetColumnType.Boolean, Header = "Is Bankrupt", PropertyName = "IsBankrupt" },
            new SpreadsheetColumn(){ Ordinal = 20, ColumnType = SpreadsheetColumnType.Boolean, Header = "Are We In A Recession", PropertyName = "AreWeInARecession" },
            new SpreadsheetColumn(){ Ordinal = 21, ColumnType = SpreadsheetColumnType.Boolean, Header = "Are We In Extreme Austerity Measures", PropertyName = "AreWeInExtremeAusterityMeasures" },
        ];
        
        SpreadsheetWriter writer = new SpreadsheetWriter(filePath, "Reconciliation", columns);
        writer.CreateSpreadsheet(_reconciliationLineItems);
    }

    
}