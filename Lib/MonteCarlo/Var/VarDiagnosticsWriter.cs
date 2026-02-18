using System.Text;
using Lib.DataTypes.MonteCarlo;

namespace Lib.MonteCarlo.Var;

/// <summary>
/// Generates a self-contained HTML file that visually compares VAR-generated hypothetical
/// lifetime growth trajectories against actual historical data.  Open the output file in
/// any browser — no server required.
/// </summary>
public static class VarDiagnosticsWriter
{
    // Colour palette for the five hypothetical lifetime lines
    private static readonly string[] HypoColors =
        ["#e41a1c", "#377eb8", "#4daf4a", "#ff7f00", "#984ea3"];

    /// <summary>
    /// Generates the diagnostic HTML and writes it to <paramref name="outputPath"/>.
    /// </summary>
    /// <param name="outputPath">Full path of the .html file to create.</param>
    /// <param name="model">A fitted VAR model (from VarFitter.Fit).</param>
    /// <param name="historicalObs">
    ///   The raw training observations used to fit the model.
    ///   Each element is a double[3]: [SpGrowth, CpiGrowth, TreasuryGrowth].
    ///   Hypothetical lifetimes will be generated to the same length so the axes are directly
    ///   comparable.
    /// </param>
    /// <param name="numLifetimes">Number of synthetic lifetimes to overlay (default 5).</param>
    public static void Write(string outputPath, VarModel model,
        IReadOnlyList<double[]> historicalObs, int numLifetimes = 5)
    {
        int months = historicalObs.Count;

        // ── Generate hypothetical lifetimes ──────────────────────────────────────
        var hypotheticals = Enumerable.Range(0, numLifetimes)
            .Select(i => VarLifetimeGenerator.Generate(model, i, months))
            .ToArray();

        // ── Cumulative series: historical ─────────────────────────────────────────
        double[] histSp  = Cumulative(historicalObs.Select(o => o[0]), 100.0);
        double[] histCpi = Cumulative(historicalObs.Select(o => o[1]), 100.0);
        double[] histTsy = Cumulative(historicalObs.Select(o => o[2]), 4.0);

        // ── Cumulative series: hypothetical ───────────────────────────────────────
        double[][] hypoSp  = hypotheticals
            .Select(h => Cumulative(h.Select(r => (double)r.SpGrowth),       100.0)).ToArray();
        double[][] hypoCpi = hypotheticals
            .Select(h => Cumulative(h.Select(r => (double)r.CpiGrowth),      100.0)).ToArray();
        double[][] hypoTsy = hypotheticals
            .Select(h => Cumulative(h.Select(r => (double)r.TreasuryGrowth), 4.0  )).ToArray();

        // ── Write file ────────────────────────────────────────────────────────────
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        File.WriteAllText(outputPath,
            BuildHtml(months, numLifetimes, histSp, histCpi, histTsy, hypoSp, hypoCpi, hypoTsy));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Compounds a sequence of monthly growth rates into a cumulative price / index series.
    /// </summary>
    private static double[] Cumulative(IEnumerable<double> rates, double start)
    {
        var result = new List<double>();
        double v = start;
        foreach (var r in rates) { v *= 1.0 + r; result.Add(v); }
        return [.. result];
    }

    /// <summary>Formats a double array as a compact JavaScript array literal.</summary>
    private static string Arr(double[] values) =>
        "[" + string.Join(",", values.Select(v => v.ToString("F4"))) + "]";

    /// <summary>Formats an array-of-arrays as a JavaScript 2-D array literal.</summary>
    private static string Arr2D(double[][] arrays) =>
        "[" + string.Join(",", arrays.Select(a => Arr(a))) + "]";

    // ── HTML construction ─────────────────────────────────────────────────────────

    private static string BuildHtml(int months, int numLifetimes,
        double[] histSp,  double[] histCpi,  double[] histTsy,
        double[][] hypoSp, double[][] hypoCpi, double[][] hypoTsy)
    {
        var sb = new StringBuilder(1 << 20); // pre-alloc ~1 MB

        // ── <head> ────────────────────────────────────────────────────────────────
        sb.Append(@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>VAR(3) Model Diagnostic</title>
  <script src=""https://cdn.jsdelivr.net/npm/chart.js@4.4.4/dist/chart.umd.min.js""></script>
  <style>
    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
    body   { background: #f0f2f5; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; padding: 28px 20px; color: #1a1a2e; }
    .wrap  { max-width: 1120px; margin: 0 auto; }
    h1     { font-size: 1.55rem; margin-bottom: 6px; }
    .intro { color: #555; font-size: 0.88rem; line-height: 1.65; margin-bottom: 30px; max-width: 820px; }
    .card  { background: #fff; border-radius: 10px; padding: 22px 26px 18px; margin-bottom: 26px;
             box-shadow: 0 2px 10px rgba(0,0,0,.07); }
    .card h2   { font-size: 1rem; margin-bottom: 3px; }
    .card p    { color: #666; font-size: 0.8rem; margin-bottom: 16px; }
    .chart-box { position: relative; height: 340px; }
  </style>
</head>
<body>
<div class=""wrap"">
  <h1>VAR(3) Model Diagnostic &mdash; Hypothetical vs Historical</h1>
  <p class=""intro"">
    Five synthetically generated lifetimes are overlaid against actual historical data (1980&ndash;present).
    Each hypothetical lifetime is seeded deterministically (life&nbsp;N always produces the same sequence),
    and monthly shocks are drawn from the VAR&nbsp;residual covariance structure so that equity, inflation,
    and interest-rate movements remain correlated the way they are in real markets.
    If the synthetic lines look like plausible economic histories &mdash; with crashes, bull runs, and
    inflationary periods &mdash; the model is working correctly.
  </p>

  <div class=""card"">
    <h2>S&amp;P&nbsp;500 Cumulative Price Index</h2>
    <p>Starting at $100 and compounding each month&rsquo;s equity growth rate. Captures long-run return trajectories and the magnitude of bull/bear cycles.</p>
    <div class=""chart-box""><canvas id=""spChart""></canvas></div>
  </div>

  <div class=""card"">
    <h2>Cumulative Inflation Index</h2>
    <p>Starting at 100 and compounding each month&rsquo;s CPI growth rate. Shows how purchasing power erodes (or, occasionally, recovers) across a lifetime.</p>
    <div class=""chart-box""><canvas id=""cpiChart""></canvas></div>
  </div>

  <div class=""card"">
    <h2>Treasury Coupon Rate (%)</h2>
    <p>Starting at 4.0&nbsp;% and compounding each month&rsquo;s treasury growth rate. Reflects the prevailing interest-rate environment used to reprice bond positions.</p>
    <div class=""chart-box""><canvas id=""tsyChart""></canvas></div>
  </div>
</div>
<script>
");

        // ── Inject data ───────────────────────────────────────────────────────────
        // x-axis labels: 1 … months
        sb.Append("const labels = [");
        for (int i = 1; i <= months; i++) { sb.Append(i); if (i < months) sb.Append(','); }
        sb.AppendLine("];");

        sb.AppendLine($"const histSp  = {Arr(histSp)};");
        sb.AppendLine($"const histCpi = {Arr(histCpi)};");
        sb.AppendLine($"const histTsy = {Arr(histTsy)};");

        sb.AppendLine($"const hypoSp  = {Arr2D(hypoSp)};");
        sb.AppendLine($"const hypoCpi = {Arr2D(hypoCpi)};");
        sb.AppendLine($"const hypoTsy = {Arr2D(hypoTsy)};");

        // Colour array (matches numLifetimes)
        var colorJs = string.Join(",", HypoColors.Take(numLifetimes).Select(c => $"'{c}'"));
        sb.AppendLine($"const colors = [{colorJs}];");

        // ── Static JavaScript: chart factory + instantiation ─────────────────────
        // (No C# interpolation inside this block — plain @"..." verbatim literal)
        sb.Append(@"
function makeChart(canvasId, yLabel, histData, hypoData, prefix, suffix) {
  const datasets = [{
    label:       'Historical',
    data:        histData,
    borderColor: '#1a1a2e',
    borderWidth: 2.5,
    pointRadius: 0,
    tension:     0
  }];
  hypoData.forEach(function(d, i) {
    datasets.push({
      label:       'Lifetime ' + (i + 1),
      data:        d,
      borderColor: colors[i],
      borderWidth: 1.5,
      pointRadius: 0,
      tension:     0
    });
  });

  new Chart(document.getElementById(canvasId), {
    type: 'line',
    data: { labels: labels, datasets: datasets },
    options: {
      responsive:          true,
      maintainAspectRatio: false,
      animation:           false,
      plugins: {
        legend: { position: 'top' },
        tooltip: {
          callbacks: {
            label: function(ctx) {
              return ctx.dataset.label + ': ' + prefix + ctx.parsed.y.toFixed(2) + suffix;
            }
          }
        }
      },
      scales: {
        x: {
          title: { display: true, text: 'Month' },
          ticks:  { maxTicksLimit: 15 }
        },
        y: {
          title: { display: true, text: yLabel },
          ticks:  {
            callback: function(v) { return prefix + v.toFixed(2) + suffix; }
          }
        }
      }
    }
  });
}

makeChart('spChart',  'Price ($)',  histSp,  hypoSp,  '$', '');
makeChart('cpiChart', 'Index',      histCpi, hypoCpi, '',  '');
makeChart('tsyChart', 'Rate (%)',   histTsy, hypoTsy, '',  '%');
</script>
</body>
</html>
");
        return sb.ToString();
    }
}
