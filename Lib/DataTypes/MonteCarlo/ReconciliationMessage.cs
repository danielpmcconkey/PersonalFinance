using NodaTime;

namespace Lib.DataTypes.MonteCarlo;

public record ReconciliationMessage(LocalDateTime? Date, decimal? Amount, string? Description);