using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using NodaTime;
using Lib.Spreadsheets;
using Lib.StaticConfig;

namespace Lib.MonteCarlo;



public class ReconciliationLedger
{
    private List<ReconciliationLineItem> _reconciliationLineItems = [];
    private int _ordinal = 0;
    private readonly bool _debugMode = MonteCarloConfig.DebugMode;
    public void ExportToSpreadsheet()
    {
        if (!MonteCarloConfig.DebugMode || _reconciliationLineItems.Count == 0) return;
        
        string timeSuffix = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
        string filePath = $"{StaticConfig.MonteCarloConfig.ReconOutputDirectory}MonteCarloRecon{timeSuffix}.xlsx";
        List<SpreadsheetColumn> columns =
        [
            new SpreadsheetColumn(){ Ordinal = 0, ColumnType = SpreadsheetColumnType.Integer, Header = "#", PropertyName = "Ordinal" },
            new SpreadsheetColumn(){ Ordinal = 1, ColumnType = SpreadsheetColumnType.DateTime, Header = "Date", PropertyName = "Date" },
            new SpreadsheetColumn(){ Ordinal = 2, ColumnType = SpreadsheetColumnType.Decimal, Header = "Age", PropertyName = "Age" },
            new SpreadsheetColumn(){ Ordinal = 3, ColumnType = SpreadsheetColumnType.Decimal, Header = "Amount", PropertyName = "Amount" },
            new SpreadsheetColumn(){ Ordinal = 4, ColumnType = SpreadsheetColumnType.String, Header = "Description", PropertyName = "Description" },
            new SpreadsheetColumn(){ Ordinal = 5, ColumnType = SpreadsheetColumnType.Decimal, Header = "Current Month Growth Rate", PropertyName = "CurrentMonthGrowthRate" },
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
            new SpreadsheetColumn(){ Ordinal = 18, ColumnType = SpreadsheetColumnType.Decimal, Header = "Total Fun Points Lifetime", PropertyName = "TotalFunPointsLifetime" },
            new SpreadsheetColumn(){ Ordinal = 19, ColumnType = SpreadsheetColumnType.Decimal, Header = "Total Health Care Spend Lifetime", PropertyName = "TotalHealthCareSpendLifetime" },
            new SpreadsheetColumn(){ Ordinal = 20, ColumnType = SpreadsheetColumnType.Decimal, Header = "Total Tax Paid Lifetime", PropertyName = "TotalTaxPaidLifetime" },
            new SpreadsheetColumn(){ Ordinal = 21, ColumnType = SpreadsheetColumnType.Boolean, Header = "Is Retired", PropertyName = "IsRetired" },
            new SpreadsheetColumn(){ Ordinal = 22, ColumnType = SpreadsheetColumnType.Boolean, Header = "Is Bankrupt", PropertyName = "IsBankrupt" },
            new SpreadsheetColumn(){ Ordinal = 23, ColumnType = SpreadsheetColumnType.Boolean, Header = "Are We In A Recession", PropertyName = "AreWeInARecession" },
            new SpreadsheetColumn(){ Ordinal = 24, ColumnType = SpreadsheetColumnType.Boolean, Header = "Are We In Extreme Austerity Measures", PropertyName = "AreWeInExtremeAusterityMeasures" },
        ];
        
        SpreadsheetWriter writer = new SpreadsheetWriter(filePath, "Reconciliation", columns);
        writer.CreateSpreadsheet(_reconciliationLineItems);
    }
    public void AddFullReconLine(MonteCarloSim sim, string description)
    {
        if (_debugMode == false) return;
        var line = CreateFullReconLine(sim, description);
        if (line is null) throw new InvalidDataException("line is null in AddReconLine");
        _reconciliationLineItems.Add(line);
    }
    public void AddMessageLine(ReconciliationMessage message)
    {
        if (_debugMode == false) return;
        var line = new ReconciliationLineItem(
            ++_ordinal, 
            message.Date, 
            null,
            message.Amount, 
            message.Description, 
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null
        );
        _reconciliationLineItems.Add(line);
    }

    public void AddMessages(List<ReconciliationMessage> messages)
    {
        foreach (var message in messages) AddMessageLine(message);
    }
    private ReconciliationLineItem? CreateFullReconLine(MonteCarloSim sim, string description)
    {
        if (_debugMode == false) return null;
        if (sim.PgPerson is null)
        {
            throw new InvalidDataException("Person is null in AddReconLine");
        }

        var ageTimeSpan = (sim.CurrentDateInSim - sim.PgPerson.BirthDate);
        var yearsOld = ageTimeSpan.Years;
        var monthsOld = ageTimeSpan.Months;
        var daysOld = ageTimeSpan.Days;
        var age = yearsOld + (monthsOld / 12.0M) + (daysOld / 365.25M);
        var line = new ReconciliationLineItem(
            ++_ordinal, 
            sim.CurrentDateInSim,
            age,
            null,
            description,
            sim.CurrentPrices.CurrentLongTermGrowthRate,
            sim.CurrentPrices.CurrentLongTermInvestmentPrice,
            AccountCalculation.CalculateNetWorth(sim.BookOfAccounts),
            AccountCalculation.CalculateLongBucketTotalBalance(sim.BookOfAccounts),
            AccountCalculation.CalculateMidBucketTotalBalance(sim.BookOfAccounts),
            AccountCalculation.CalculateShortBucketTotalBalance(sim.BookOfAccounts),
            AccountCalculation.CalculateCashBalance(sim.BookOfAccounts),
            AccountCalculation.CalculateDebtTotal(sim.BookOfAccounts),
            sim.LifetimeSpend.TotalSpendLifetime,
            sim.LifetimeSpend.TotalInvestmentAccrualLifetime,
            sim.LifetimeSpend.TotalDebtAccrualLifetime,
            sim.LifetimeSpend.TotalSocialSecurityWageLifetime,
            sim.LifetimeSpend.TotalDebtPaidLifetime,
            sim.CurrentDateInSim >= sim.SimParameters.RetirementDate,
            sim.PgPerson.IsBankrupt,
            sim.RecessionStats.AreWeInARecession,
            sim.RecessionStats.AreWeInExtremeAusterityMeasures,
            sim.LifetimeSpend.TotalFunPointsLifetime,
            sim.LifetimeSpend.TotalLifetimeHealthCareSpend,
            sim.TaxLedger.TotalTaxPaidLifetime
        );
        return line;
    }
}