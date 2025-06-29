using Lib.DataTypes.MonteCarlo;
using NodaTime;
using Lib.Spreadsheets;

namespace Lib.MonteCarlo.StaticFunctions;

public static class Reconciliation
{
    public static void ExportToSpreadsheet()
    {
        if (StaticConfig.MonteCarloConfig.DebugMode == false || 
            ReconciliationLedger._reconciliationLineItems == null ||
            !ReconciliationLedger._reconciliationLineItems.Any()) 
            return;
        
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
            new SpreadsheetColumn(){ Ordinal = 18, ColumnType = SpreadsheetColumnType.Boolean, Header = "Is Retired", PropertyName = "IsRetired" },
            new SpreadsheetColumn(){ Ordinal = 19, ColumnType = SpreadsheetColumnType.Boolean, Header = "Is Bankrupt", PropertyName = "IsBankrupt" },
            new SpreadsheetColumn(){ Ordinal = 20, ColumnType = SpreadsheetColumnType.Boolean, Header = "Are We In A Recession", PropertyName = "AreWeInARecession" },
            new SpreadsheetColumn(){ Ordinal = 21, ColumnType = SpreadsheetColumnType.Boolean, Header = "Are We In Extreme Austerity Measures", PropertyName = "AreWeInExtremeAusterityMeasures" },
        ];
        
        SpreadsheetWriter writer = new SpreadsheetWriter(filePath, "Reconciliation", columns);
        writer.CreateSpreadsheet(ReconciliationLedger._reconciliationLineItems);
    }
    public static void AddFullReconLine(MonteCarloSim sim, Decimal amount, string description)
    {
        if (StaticConfig.MonteCarloConfig.DebugMode == false) return;
        var line = CreateFullReconLine(sim, amount, description);
        if (line is null) throw new InvalidDataException("line is null in AddReconLine");
        ReconciliationLedger.AddLine(line);
    }
    public static void AddMessageLine(LocalDateTime? date, decimal? amount, string? description)
    {
        if (StaticConfig.MonteCarloConfig.DebugMode == false) return;
        var line = new ReconciliationLineItem(
            0, // placeholder ordinal. The recon ledger will add the right value
            date, 
            null,
            amount, 
            description, 
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
        ReconciliationLedger.AddLine(line);
    }
    public static ReconciliationLineItem? CreateFullReconLine(MonteCarloSim sim,
            Decimal amount, string description)
        {
            if (StaticConfig.MonteCarloConfig.DebugMode == false) return null;
            if (sim.Person is null)
            {
                throw new InvalidDataException("Person is null in AddReconLine");
            }

            var ageTimeSpan = (sim.CurrentDateInSim - sim.Person.BirthDate);
            var yearsOld = ageTimeSpan.Years;
            var monthsOld = ageTimeSpan.Months;
            var daysOld = ageTimeSpan.Days;
            var age = yearsOld + (monthsOld / 12.0M) + (daysOld / 365.25M);
            var line = new ReconciliationLineItem(
                0, // placeholder ordinal. The recon ledger will add the right value
                sim.CurrentDateInSim,
                age,
                amount,
                description,
                sim.CurrentPrices.CurrentLongTermGrowthRate,
                sim.CurrentPrices.CurrentLongTermInvestmentPrice,
                Account.CalculateNetWorth(sim.BookOfAccounts),
                Account.CalculateLongBucketTotalBalance(sim.BookOfAccounts),
                Account.CalculateMidBucketTotalBalance(sim.BookOfAccounts),
                Account.CalculateShortBucketTotalBalance(sim.BookOfAccounts),
                Account.CalculateCashBalance(sim.BookOfAccounts),
                Account.CalculateDebtTotal(sim.BookOfAccounts),
                sim.LifetimeSpend.TotalSpendLifetime,
                sim.LifetimeSpend.TotalInvestmentAccrualLifetime,
                sim.LifetimeSpend.TotalDebtAccrualLifetime,
                sim.LifetimeSpend.TotalSocialSecurityWageLifetime,
                sim.LifetimeSpend.TotalDebtPaidLifetime,
                sim.CurrentDateInSim >= sim.SimParameters.RetirementDate,
                sim.LifetimeSpend.IsBankrupt,
                sim.RecessionStats.AreWeInADownYear,
                sim.RecessionStats.AreWeInExtremeAusterityMeasures
            );
            return line;
        }
    
}