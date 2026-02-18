using Lib.DataTypes.MonteCarlo;

namespace Lib.MonteCarlo.Var;

/// <summary>
/// Generates a synthetic monthly economic lifetime from a fitted VAR model.
/// </summary>
public static class VarLifetimeGenerator
{
    /// <summary>
    /// Generates <paramref name="months"/> monthly growth-rate observations from the VAR model.
    /// The same <paramref name="lifeIndex"/> always produces the same sequence (deterministic seed).
    /// </summary>
    public static HypotheticalLifeTimeGrowthRate[] Generate(VarModel model, int lifeIndex, int months)
    {
        var rng = new Random(lifeIndex);
        int K = 3;
        int p = model.LagCount;
        int features = K * p + 1;  // 10 for K=3, p=3

        // Lag buffer: lags[0] = most recent, lags[p-1] = oldest
        // Seed: SeedObservations[0]=oldest, [p-1]=most recent → reverse for lags
        var lags = new double[p][];
        for (int i = 0; i < p; i++)
            lags[i] = (double[])model.SeedObservations[p - 1 - i].Clone();

        var result = new HypotheticalLifeTimeGrowthRate[months];

        for (int month = 0; month < months; month++)
        {
            // ── Build x_t: [1, lag1[0..K-1], lag2[0..K-1], ..., lagp[0..K-1]] ──
            var x = new double[features];
            x[0] = 1.0;
            for (int lag = 0; lag < p; lag++)
                for (int k = 0; k < K; k++)
                    x[1 + lag * K + k] = lags[lag][k];

            // ── mean_t = x_t × CoefficientMatrix  (1×features) × (features×K) ──
            var mean = new double[K];
            for (int k = 0; k < K; k++)
                for (int f = 0; f < features; f++)
                    mean[k] += x[f] * model.CoefficientMatrix[f, k];

            // ── Draw 3 independent N(0,1) via Box-Muller ─────────────────────────
            var z = DrawThreeNormals(rng);

            // ── shock = L × z  (lower-triangular multiply) ───────────────────────
            var shock = new double[K];
            for (int i = 0; i < K; i++)
                for (int j = 0; j <= i; j++)
                    shock[i] += model.ResidualCholesky[i, j] * z[j];

            // ── Y_t = mean + shock ───────────────────────────────────────────────
            var Y = new double[K];
            for (int k = 0; k < K; k++)
                Y[k] = mean[k] + shock[k];

            result[month] = new HypotheticalLifeTimeGrowthRate
            {
                SpGrowth       = (decimal)Y[0],
                CpiGrowth      = (decimal)Y[1],
                TreasuryGrowth = (decimal)Y[2],
            };

            // ── Shift lag buffer: new observation becomes lag 1 ──────────────────
            var newLags = new double[p][];
            newLags[0] = Y;
            for (int i = 1; i < p; i++)
                newLags[i] = lags[i - 1];
            lags = newLags;
        }

        return result;
    }

    /// <summary>
    /// Returns three independent standard-normal draws via the Box-Muller transform.
    /// </summary>
    private static double[] DrawThreeNormals(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        double mag1 = Math.Sqrt(-2.0 * Math.Log(u1));
        double z0 = mag1 * Math.Cos(2.0 * Math.PI * u2);
        double z1 = mag1 * Math.Sin(2.0 * Math.PI * u2);

        double u3 = 1.0 - rng.NextDouble();
        double u4 = 1.0 - rng.NextDouble();
        double z2 = Math.Sqrt(-2.0 * Math.Log(u3)) * Math.Cos(2.0 * Math.PI * u4);

        return [z0, z1, z2];
    }
}
