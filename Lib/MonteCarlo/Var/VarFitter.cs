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
    /// </param>
    /// <param name="lagCount">Number of lags p (default 3).</param>
    /// <returns>A fitted <see cref="VarModel"/>.</returns>
    public static VarModel Fit(IReadOnlyList<double[]> observations, int lagCount = 3)
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

        return new VarModel
        {
            LagCount           = lagCount,
            CoefficientMatrix  = B,
            ResidualCholesky   = L,
            SeedObservations   = seed,
        };
    }
}
