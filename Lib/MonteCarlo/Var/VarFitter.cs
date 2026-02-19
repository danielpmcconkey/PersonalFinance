using Lib.DataTypes.MonteCarlo;

namespace Lib.MonteCarlo.Var;

/// <summary>
/// Fits a Vector Autoregression model of order p to a list of multivariate observations.
/// </summary>
public static class VarFitter
{
    /// <summary>
    /// Fits a VAR(p) model via OLS.
    /// </summary>
    /// <param name="observations">
    /// T observations, each a double[] of length K (K = number of variables, e.g. 3 for SP/CPI/Treasury).
    /// observations[i][2] must be the absolute monthly change in the treasury rate in decimal form
    /// (e.g. a move from 4.0 % → 4.1 % is stored as 0.001).
    /// </param>
    /// <param name="treasuryLevels">
    /// Optional array of T treasury rate levels in decimal form (e.g. 0.04 = 4 %).
    /// When provided, Ornstein-Uhlenbeck parameters (kappa, theta) are estimated via OLS so the
    /// generator can apply mean reversion.  When null, safe defaults are used.
    /// </param>
    /// <param name="lagCount">Number of lags p (default 3).</param>
    /// <returns>A fitted <see cref="VarModel"/>.</returns>
    public static VarModel Fit(IReadOnlyList<double[]> observations, double[]? treasuryLevels = null, int lagCount = 3)
    {
        int T = observations.Count;
        int K = observations[0].Length;    // number of variables (3)
        int rows = T - lagCount;           // number of usable rows
        int cols = K * lagCount + 1;       // number of regressors per equation: 1 + K*p

        // ── Design matrix X  (rows × cols) ──────────────────────────────────────
        // Each row t (for t = lagCount..T-1):
        //   [1, Y(t-1)[0..K-1], Y(t-2)[0..K-1], ..., Y(t-p)[0..K-1]]
        var X = new double[rows, cols];
        for (int t = lagCount; t < T; t++)
        {
            int row = t - lagCount;
            X[row, 0] = 1.0;
            for (int lag = 1; lag <= lagCount; lag++)
                for (int k = 0; k < K; k++)
                    X[row, 1 + (lag - 1) * K + k] = observations[t - lag][k];
        }

        // ── Response matrix Y  (rows × K) ───────────────────────────────────────
        var Y = new double[rows, K];
        for (int t = lagCount; t < T; t++)
        {
            int row = t - lagCount;
            for (int k = 0; k < K; k++)
                Y[row, k] = observations[t][k];
        }

        // ── OLS: B = (X'X)^{-1} X'Y  →  shape (cols × K) ───────────────────────
        var Xt    = MatrixMath.Transpose(X);       // cols × rows
        var XtX   = MatrixMath.Multiply(Xt, X);   // cols × cols
        var XtXinv = MatrixMath.Invert(XtX);      // cols × cols
        var XtY   = MatrixMath.Multiply(Xt, Y);   // cols × K
        var B     = MatrixMath.Multiply(XtXinv, XtY); // cols × K

        // ── Residuals E = Y − X B  (rows × K) ───────────────────────────────────
        var XB = MatrixMath.Multiply(X, B);
        var E  = new double[rows, K];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < K; j++)
                E[i, j] = Y[i, j] - XB[i, j];

        // ── Residual covariance Σ = E'E / (T − p − K*p − 1)  (K × K) ────────────
        var Et  = MatrixMath.Transpose(E);         // K × rows
        var EtE = MatrixMath.Multiply(Et, E);      // K × K
        double dof = rows - (K * lagCount + 1);    // unbiased denominator
        var sigma = new double[K, K];
        for (int i = 0; i < K; i++)
            for (int j = 0; j < K; j++)
                sigma[i, j] = EtE[i, j] / dof;

        // ── Cholesky factor L of Σ  (K × K lower-triangular) ────────────────────
        var L = MatrixMath.CholeskyDecompose(sigma);

        // ── Seed observations: last lagCount rows of input ───────────────────────
        var seed = new double[lagCount][];
        for (int i = 0; i < lagCount; i++)
            seed[i] = (double[])observations[T - lagCount + i].Clone();

        // ── Ornstein-Uhlenbeck parameters for the treasury rate ─────────────────────────────
        // Regress monthly rate change (delta_r) on lagged rate level (r_{t-1}) using simple
        // univariate OLS: delta_r[t] = alpha + beta * r[t-1] + ε
        //   kappa = -beta  (positive → mean-reverting)
        //   theta = alpha / kappa  (long-run equilibrium level)
        // observations[i][2] already contains delta_r in decimal form.
        double ouTheta;
        double ouKappa;
        double initialTreasuryRate;

        if (treasuryLevels != null && treasuryLevels.Length >= lagCount + 2)
        {
            // x[i] = r_{t-1},  y[i] = delta_r[t],  aligned over t = lagCount..T-1
            // (skip the first lagCount rows that the VAR itself skips, so indices match)
            int n = rows; // rows = T - lagCount
            double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
            for (int i = 0; i < n; i++)
            {
                double x = treasuryLevels[i + lagCount - 1]; // r_{t-1}
                double y = observations[i + lagCount][2];    // delta_r[t]
                sumX  += x;
                sumY  += y;
                sumXY += x * y;
                sumXX += x * x;
            }
            double denom = n * sumXX - sumX * sumX;
            double beta  = Math.Abs(denom) > 1e-12 ? (n * sumXY - sumX * sumY) / denom : 0.0;
            double alpha = (sumY - beta * sumX) / n;

            ouKappa = Math.Max(-beta, 1e-4); // must be positive; floor avoids divide-by-zero
            ouTheta = ouKappa > 1e-4 ? alpha / ouKappa : sumX / n;
            initialTreasuryRate = treasuryLevels[^1];
        }
        else
        {
            // Safe defaults: mild mean reversion toward 4.5 %, starting at 4 %
            ouTheta = 0.045;
            ouKappa = 0.02;
            initialTreasuryRate = 0.04;
        }
        // ────────────────────────────────────────────────────────────────────────────────────

        return new VarModel
        {
            LagCount           = lagCount,
            CoefficientMatrix  = B,
            ResidualCholesky   = L,
            SeedObservations   = seed,
            TreasuryOuTheta    = ouTheta,
            TreasuryOuKappa    = ouKappa,
            InitialTreasuryRate = initialTreasuryRate,
        };
    }
}
