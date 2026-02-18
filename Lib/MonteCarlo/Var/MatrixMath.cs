namespace Lib.MonteCarlo.Var;

/// <summary>
/// Static helper for basic double-precision matrix operations used by VAR fitting and generation.
/// </summary>
public static class MatrixMath
{
    /// <summary>Standard matrix multiplication: (m×n) × (n×p) → (m×p).</summary>
    public static double[,] Multiply(double[,] a, double[,] b)
    {
        int m = a.GetLength(0);
        int n = a.GetLength(1);
        int p = b.GetLength(1);
        var result = new double[m, p];
        for (int i = 0; i < m; i++)
            for (int j = 0; j < p; j++)
                for (int k = 0; k < n; k++)
                    result[i, j] += a[i, k] * b[k, j];
        return result;
    }

    /// <summary>Matrix transpose: (m×n) → (n×m).</summary>
    public static double[,] Transpose(double[,] a)
    {
        int m = a.GetLength(0);
        int n = a.GetLength(1);
        var result = new double[n, m];
        for (int i = 0; i < m; i++)
            for (int j = 0; j < n; j++)
                result[j, i] = a[i, j];
        return result;
    }

    /// <summary>
    /// Matrix inversion via Gauss-Jordan elimination with partial pivoting.
    /// <paramref name="a"/> must be square and non-singular.
    /// </summary>
    public static double[,] Invert(double[,] a)
    {
        int n = a.GetLength(0);

        // Build augmented matrix [a | I]
        var aug = new double[n, 2 * n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
                aug[i, j] = a[i, j];
            aug[i, n + i] = 1.0;
        }

        for (int col = 0; col < n; col++)
        {
            // Partial pivoting: find row with largest absolute value in this column
            int pivotRow = col;
            double pivotMag = Math.Abs(aug[col, col]);
            for (int row = col + 1; row < n; row++)
            {
                double mag = Math.Abs(aug[row, col]);
                if (mag > pivotMag) { pivotMag = mag; pivotRow = row; }
            }

            // Swap pivot row into position
            if (pivotRow != col)
            {
                for (int j = 0; j < 2 * n; j++)
                    (aug[col, j], aug[pivotRow, j]) = (aug[pivotRow, j], aug[col, j]);
            }

            double diag = aug[col, col];
            if (Math.Abs(diag) < 1e-14)
                throw new InvalidOperationException("Matrix is singular or nearly singular.");

            // Normalize the pivot row
            for (int j = 0; j < 2 * n; j++)
                aug[col, j] /= diag;

            // Eliminate all other rows
            for (int row = 0; row < n; row++)
            {
                if (row == col) continue;
                double factor = aug[row, col];
                for (int j = 0; j < 2 * n; j++)
                    aug[row, j] -= factor * aug[col, j];
            }
        }

        // Extract the right half (the inverse)
        var result = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                result[i, j] = aug[i, n + j];
        return result;
    }

    /// <summary>
    /// Cholesky decomposition: returns lower-triangular L such that L × L' = <paramref name="a"/>.
    /// <paramref name="a"/> must be symmetric positive-definite.
    /// </summary>
    public static double[,] CholeskyDecompose(double[,] a)
    {
        int n = a.GetLength(0);
        var L = new double[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                double sum = 0.0;
                for (int k = 0; k < j; k++)
                    sum += L[i, k] * L[j, k];

                if (i == j)
                    L[i, j] = Math.Sqrt(a[i, j] - sum);
                else
                    L[i, j] = (a[i, j] - sum) / L[j, j];
            }
        }
        return L;
    }
}
