using Lib.DataTypes.Postgres;
using Lib.Presentation;
using NodaTime;

namespace Lib.Tests.Presentation;

public class ChartDataTest
{
    [Fact]
    public void GetEndOfMonthPositionsBySymbol_ShouldReturnAccurateList()
    {
        // Arrange
        using var context = new PgContext();
        var fxaix = context.PgFunds.First(x => x.Symbol == "FXAIX");
        var schd = context.PgFunds.First(x => x.Symbol == "SCHD");
        
        var account = new PgInvestmentAccount()
        {
            Id = 0, 
            Name = "burp",
            TaxBucketId = 1,
            InvestmentAccountGroupId = 10,
            Positions = []
        };
        
        var mar17Fxaix = new PgPosition()
        {
            Id = 0, InvestmentAccount = account,
            PositionDate = new LocalDateTime(2025, 3, 17, 0, 0),
            Symbol = "FXAIX", Fund = fxaix,
            CurrentValue = 485.12m, Price = 485.12m, TotalQuantity = 1m, CostBasis = 100m, 
            InvestmentAccountId = 0
        };
        account.Positions.Add(mar17Fxaix);
        fxaix.Positions.Add(mar17Fxaix);
        
        var mar17Schd = new PgPosition()
        {
            Id = 0, InvestmentAccount = account,
            PositionDate = new LocalDateTime(2025, 3, 17, 0, 0),
            Symbol = "SCHD", Fund = schd,
            CurrentValue = 300m, Price = 150m, TotalQuantity = 2m, CostBasis = 100m, 
            InvestmentAccountId = 0
        };
        account.Positions.Add(mar17Schd);
        fxaix.Positions.Add(mar17Schd);
        
        var mar21Schd = new PgPosition()
        {
            Id = 0, InvestmentAccount = account,
            PositionDate = new LocalDateTime(2025, 3, 21, 0, 0),
            Symbol = "SCHD", Fund = schd,
            CurrentValue = 310m, Price = 155m, TotalQuantity = 2m, CostBasis = 100m, 
            InvestmentAccountId = 0
        };
        account.Positions.Add(mar21Schd);
        fxaix.Positions.Add(mar21Schd);
        
        var apr21Fxaix = new PgPosition()
        {
            Id = 0, InvestmentAccount = account,
            PositionDate = new LocalDateTime(2025, 4, 21, 0, 0),
            Symbol = "FXAIX", Fund = fxaix,
            CurrentValue = 503.12m, Price = 503.12m, TotalQuantity = 1m, CostBasis = 100m, 
            InvestmentAccountId = 0
        };
        account.Positions.Add(apr21Fxaix);
        fxaix.Positions.Add(apr21Fxaix);
        
        var may30Fxaix = new PgPosition()
        {
            Id = 0, InvestmentAccount = account,
            PositionDate = new LocalDateTime(2025, 5, 30, 0, 0),
            Symbol = "FXAIX", Fund = fxaix,
            CurrentValue = 519.60m, Price = 519.60m, TotalQuantity = 1m, CostBasis = 100m, 
            InvestmentAccountId = 0
        };
        account.Positions.Add(may30Fxaix);
        fxaix.Positions.Add(may30Fxaix);
        
        var may30Schd = new PgPosition()
        {
            Id = 0, InvestmentAccount = account,
            PositionDate = new LocalDateTime(2025, 5, 30, 0, 0),
            Symbol = "SCHD", Fund = schd,
            CurrentValue = 250m, Price = 125m, TotalQuantity = 2m, CostBasis = 100m, 
            InvestmentAccountId = 0
        };
        account.Positions.Add(may30Schd);
        fxaix.Positions.Add(may30Schd);

        var expectedCount = 6;
        var expectedEndOfMarchFxaix = 485.12m;
        var expectedEndOfMarchSchd = 310m;
        var expectedEndOfAprilFxaix = 503.12m;
        var expectedEndOfAprilSchd = 310m;
        var expectedEndOfMayFxaix = 519.60m;
        var expectedEndOfMaySchd = 250m;
        
        // Act
        var result = ChartData.GetEndOfMonthPositionsBySymbol(account);
        var actualCount = result.Count;
        var actualEndOfMarchFxaix = result
            .First(x => x.Symbol == "FXAIX" &&
                        x.MonthEnd == new LocalDate(2025, 3, 31)).Value;
        var actualEndOfMarchSchd = result
            .First(x => x.Symbol == "SCHD" &&
                        x.MonthEnd == new LocalDate(2025, 3, 31)).Value;
        var actualEndOfAprilFxaix = result
            .First(x => x.Symbol == "FXAIX" &&
                        x.MonthEnd == new LocalDate(2025, 4, 30)).Value;
        var actualEndOfAprilSchd = result
            .First(x => x.Symbol == "SCHD" &&
                        x.MonthEnd == new LocalDate(2025, 4, 30)).Value;
        var actualEndOfMayFxaix = result
            .First(x => x.Symbol == "FXAIX" &&
                        x.MonthEnd == new LocalDate(2025, 5, 31)).Value;
        var actualEndOfMaySchd = result
            .First(x => x.Symbol == "SCHD" &&
                        x.MonthEnd == new LocalDate(2025, 5, 31)).Value;
        
        
        // Assert
        Assert.Equal(expectedCount, actualCount);
        Assert.Equal(expectedEndOfMarchFxaix, actualEndOfMarchFxaix);
        Assert.Equal(expectedEndOfMarchSchd, actualEndOfMarchSchd);
        Assert.Equal(expectedEndOfAprilFxaix, actualEndOfAprilFxaix);
        Assert.Equal(expectedEndOfAprilSchd, actualEndOfAprilSchd);
        Assert.Equal(expectedEndOfMayFxaix, actualEndOfMayFxaix);
        Assert.Equal(expectedEndOfMaySchd, actualEndOfMaySchd);
    }
    
}