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

    // ── Ornstein-Uhlenbeck parameters for the treasury rate ──────────────────────────────────
    // Treasury rates are mean-reverting, not trending like equity prices.  The VAR captures
    // short-term autocorrelation in monthly rate changes, but without an explicit pull toward a
    // long-run equilibrium the process can drift to zero or infinity over a 46-year horizon.
    // These three parameters are estimated from the historical rate level data during fitting
    // and used by VarLifetimeGenerator to apply a monthly OU correction:
    //   corrected_delta = var_delta + TreasuryOuKappa * (TreasuryOuTheta - current_rate)
    // The raw VAR delta (not the corrected one) is kept in the lag buffer so the autoregressive
    // terms are not contaminated by the mean-reversion adjustment.

    /// <summary>
    /// Long-run equilibrium rate in decimal form (e.g. 0.0472 = 4.72 %).
    /// Estimated as the mean of historical rate levels over the training window.
    /// </summary>
    public required double TreasuryOuTheta { get; init; }

    /// <summary>
    /// Monthly mean-reversion speed (dimensionless, per month).
    /// Estimated via OLS regression of monthly rate changes on lagged rate levels.
    /// A value of 0.02 implies roughly a 3-year half-life back to theta.
    /// </summary>
    public required double TreasuryOuKappa { get; init; }

    /// <summary>
    /// The last historical rate in decimal form (e.g. 0.0347 = 3.47 %).
    /// Seeds the running rate level in VarLifetimeGenerator so each synthetic lifetime
    /// begins from current market conditions.
    /// </summary>
    public required double InitialTreasuryRate { get; init; }
}
