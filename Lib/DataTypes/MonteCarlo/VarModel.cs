namespace Lib.DataTypes.MonteCarlo;

/// <summary>
/// Immutable record storing a fitted VAR(p) model.
/// </summary>
public record VarModel
{
    /// <summary>Number of lags (p).</summary>
    public required int LagCount { get; init; }

    /// <summary>
    /// OLS coefficient matrix B with shape (K*p+1) × K.
    /// Rows: [constant, Y1(t-1), Y2(t-1), Y3(t-1), Y1(t-2), ..., Y3(t-p)].
    /// Columns: [SP, CPI, Treasury].
    /// </summary>
    public required double[,] CoefficientMatrix { get; init; }

    /// <summary>
    /// Lower-triangular Cholesky factor L of the residual covariance matrix Σ,
    /// where L × L' = Σ. Used to draw correlated shocks.
    /// </summary>
    public required double[,] ResidualCholesky { get; init; }

    /// <summary>
    /// Last <see cref="LagCount"/> actual monthly observations from the training data,
    /// used to warm-start generation. Index 0 is oldest, index LagCount-1 is most recent.
    /// </summary>
    public required double[][] SeedObservations { get; init; }
}
