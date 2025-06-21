using NodaTime;

namespace Lib.DataTypes.MonteCarlo;
public record McDebtPosition
{
    public Guid Id { get; set; }
    public required bool IsOpen { get; set; } = true;
    public required string Name { get; set; }
    public required LocalDateTime Entry { get; set; }
    public required decimal AnnualPercentageRate { get; set; }
    public required decimal MonthlyPayment { get; set; }
    public required decimal CurrentBalance { get; set; }
}

