using Lib.DataTypes.MonteCarlo;
using Lib.MonteCarlo.StaticFunctions;
using Lib.MonteCarlo.Var;

namespace Lib.Tests.MonteCarlo.StaticFunctions;

public class VarPricingExtendedTests
{
    // ── §14 — VAR pricing / growth rate generation ────────────────────────────

    /// <summary>
    /// Builds a minimal deterministic VAR(1) model for unit testing.
    /// LagCount=1 → CoefficientMatrix is 4×3 (K*p+1 rows × K cols).
    /// All coefficients are zero → mean = 0 every month.
    /// cholTreasury controls the standard deviation of the treasury shock (column 2 in Cholesky).
    /// </summary>
    private static VarModel BuildMinimalVarModel(
        double kappa        = 0.02,
        double theta        = 0.04,
        double initialRate  = 0.04,
        double cholEquity   = 0.01,
        double cholCpi      = 0.01,
        double cholTreasury = 0.001)
    {
        var chol = new double[3, 3];
        chol[0, 0] = cholEquity;
        chol[1, 1] = cholCpi;
        chol[2, 2] = cholTreasury;

        return new VarModel
        {
            LagCount          = 1,
            CoefficientMatrix = new double[4, 3],  // all zeros — mean = 0 every month
            ResidualCholesky  = chol,
            SeedObservations  = [new double[] { 0.0, 0.0, 0.0 }],
            TreasuryOuKappa   = kappa,
            TreasuryOuTheta   = theta,
            InitialTreasuryRate = initialRate,
        };
    }

    // ── §14 — Treasury coupon updated additively ──────────────────────────────

    [Fact(DisplayName = "§14 — Treasury coupon is updated additively: newCoupon = oldCoupon + TreasuryGrowth")]
    public void SetLongTermGrowthRateAndPrices_TreasuryGrowthPositive_AddedToExistingCoupon()
    {
        // TreasuryGrowth = +0.1 pp (0.001 in decimal form) on top of a 4.0% coupon → 4.1%
        var prices = new CurrentPrices
        {
            CurrentEquityInvestmentPrice    = 100m,
            CurrentMidTermInvestmentPrice   = 100m,
            CurrentShortTermInvestmentPrice = 100m,
            CurrentTreasuryCoupon           = 0.04m,
        };
        var rates = new HypotheticalLifeTimeGrowthRate
        {
            SpGrowth       = 0m,
            CpiGrowth      = 0m,
            TreasuryGrowth = 0.001m,  // absolute monthly change (+0.1 pp)
        };

        var result = Pricing.SetLongTermGrowthRateAndPrices(prices, rates);

        Assert.Equal(0.041m, result.CurrentTreasuryCoupon);
    }

    [Fact(DisplayName = "§14 — Treasury coupon decreases additively when TreasuryGrowth is negative")]
    public void SetLongTermGrowthRateAndPrices_TreasuryGrowthNegative_SubtractedFromCoupon()
    {
        // TreasuryGrowth = −0.2 pp on a 4.0% coupon → 3.8%
        var prices = new CurrentPrices
        {
            CurrentEquityInvestmentPrice    = 100m,
            CurrentMidTermInvestmentPrice   = 100m,
            CurrentShortTermInvestmentPrice = 100m,
            CurrentTreasuryCoupon           = 0.04m,
        };
        var rates = new HypotheticalLifeTimeGrowthRate
        {
            SpGrowth       = 0m,
            CpiGrowth      = 0m,
            TreasuryGrowth = -0.002m,
        };

        var result = Pricing.SetLongTermGrowthRateAndPrices(prices, rates);

        Assert.Equal(0.038m, result.CurrentTreasuryCoupon);
    }

    // ── §14 — Determinism: same lifeIndex → same sequence ────────────────────

    [Fact(DisplayName = "§14 — VarLifetimeGenerator produces identical output for the same lifeIndex")]
    public void Generate_SameLifeIndex_ProducesIdenticalSequence()
    {
        var model = BuildMinimalVarModel();  // non-zero Cholesky ensures random draws matter

        var first  = VarLifetimeGenerator.Generate(model, lifeIndex: 42, months: 12);
        var second = VarLifetimeGenerator.Generate(model, lifeIndex: 42, months: 12);

        Assert.Equal(first.Length, second.Length);
        for (int i = 0; i < first.Length; i++)
        {
            Assert.Equal(first[i].SpGrowth,       second[i].SpGrowth);
            Assert.Equal(first[i].TreasuryGrowth, second[i].TreasuryGrowth);
        }
    }

    [Fact(DisplayName = "§14 — VarLifetimeGenerator produces different output for different lifeIndex values")]
    public void Generate_DifferentLifeIndex_ProducesDifferentSequence()
    {
        var model = BuildMinimalVarModel(cholEquity: 0.05, cholCpi: 0.05, cholTreasury: 0.005);

        var a = VarLifetimeGenerator.Generate(model, lifeIndex: 1, months: 12);
        var b = VarLifetimeGenerator.Generate(model, lifeIndex: 2, months: 12);

        // With non-zero Cholesky and different seeds, at least one month should differ
        bool anyDifference = a.Zip(b).Any(pair =>
            pair.First.SpGrowth != pair.Second.SpGrowth ||
            pair.First.TreasuryGrowth != pair.Second.TreasuryGrowth);

        Assert.True(anyDifference, "Different lifeIndex values must produce different growth sequences");
    }

    // ── §14 — Treasury rate bounded between 0.1% and 20% ────────────────────

    [Fact(DisplayName = "§14 — Treasury rate never falls below 0.1% even when OU target is deeply negative")]
    public void Generate_OuTargetBelowFloor_RateClampedAtFloor()
    {
        // TreasuryOuTheta = -0.10 (below the 0.1% floor) with kappa = 1.0 (strong pull).
        // Without clamping the rate would go negative. After the first month the OU correction
        // tries to pull the rate from 0.04 toward -0.10 → unclamped ≈ 0.04 + 1.0*(-0.14) = -0.10.
        // The floor clamp forces it to 0.001 and keeps it there for every subsequent month.
        var model = BuildMinimalVarModel(
            kappa:        1.0,
            theta:        -0.10,   // below floor — ensures clamping is exercised every month
            initialRate:  0.04,
            cholTreasury: 0.0);    // zero shocks → fully deterministic

        var result = VarLifetimeGenerator.Generate(model, lifeIndex: 0, months: 24);

        const double RateFloor   = 0.001;
        const double RateCeiling = 0.20;

        double runningRate = model.InitialTreasuryRate;
        for (int i = 0; i < result.Length; i++)
        {
            runningRate += (double)result[i].TreasuryGrowth;
            Assert.True(runningRate >= RateFloor - 1e-9,
                $"Month {i + 1}: running treasury rate {runningRate:F6} is below the 0.1% floor");
            Assert.True(runningRate <= RateCeiling + 1e-9,
                $"Month {i + 1}: running treasury rate {runningRate:F6} exceeds the 20% ceiling");
        }
    }

    [Fact(DisplayName = "§14 — Treasury rate never exceeds 20% even when OU target is far above ceiling")]
    public void Generate_OuTargetAboveCeiling_RateClampedAtCeiling()
    {
        // TreasuryOuTheta = 0.50 (above the 20% ceiling) with kappa = 1.0.
        // OU correction: 1.0*(0.50 - 0.04) = +0.46 → unclamped = 0.50 → clamped to 0.20.
        var model = BuildMinimalVarModel(
            kappa:        1.0,
            theta:        0.50,    // above ceiling — ensures ceiling clamping is exercised
            initialRate:  0.04,
            cholTreasury: 0.0);

        var result = VarLifetimeGenerator.Generate(model, lifeIndex: 0, months: 24);

        const double RateFloor   = 0.001;
        const double RateCeiling = 0.20;

        double runningRate = model.InitialTreasuryRate;
        for (int i = 0; i < result.Length; i++)
        {
            runningRate += (double)result[i].TreasuryGrowth;
            Assert.True(runningRate >= RateFloor - 1e-9,
                $"Month {i + 1}: running rate {runningRate:F6} is below floor");
            Assert.True(runningRate <= RateCeiling + 1e-9,
                $"Month {i + 1}: running rate {runningRate:F6} exceeds the 20% ceiling");
        }
    }

    // ── §14 — OU mean reversion pulls rate toward TreasuryOuTheta ────────────

    [Fact(DisplayName = "§14 — OU mean reversion pulls treasury rate toward long-run mean over time")]
    public void Generate_InitialRateFarFromTheta_RateConvergesTowardTheta()
    {
        // With zero treasury shocks (cholTreasury = 0), the only driver is the OU correction.
        // kappa = 0.5 gives half-life of ~1.4 months: after 12 months the rate should be
        // extremely close to theta (within 0.001 of 0.04).
        // Recurrence: r(n) = theta + (r0 - theta) * (1 - kappa)^n
        //   = 0.04 + 0.11 * 0.5^12 ≈ 0.04003   (well below the initial 0.15)
        const double InitialRate = 0.15;
        const double Theta       = 0.04;
        const double Kappa       = 0.5;

        var model = BuildMinimalVarModel(
            kappa:        Kappa,
            theta:        Theta,
            initialRate:  InitialRate,
            cholTreasury: 0.0);    // no random shocks → deterministic OU path

        var result = VarLifetimeGenerator.Generate(model, lifeIndex: 0, months: 12);

        // Track the running treasury rate level
        double runningRate = InitialRate;
        foreach (var r in result)
            runningRate += (double)r.TreasuryGrowth;

        // After 12 months with strong kappa, the rate must be much closer to theta
        // than it was initially (initial distance = 0.11; expected final distance ≈ 0.00003)
        double initialDistance = Math.Abs(InitialRate - Theta);
        double finalDistance   = Math.Abs(runningRate  - Theta);

        Assert.True(finalDistance < initialDistance * 0.01,
            $"OU reversion should reduce the gap to theta by >99 % in 12 months; " +
            $"initial gap={initialDistance:F4}, final gap={finalDistance:F6}");
    }

    // ── §14 — CopyPrices propagates CurrentTreasuryCoupon ────────────────────

    [Fact(DisplayName = "§14 — CopyPrices faithfully copies CurrentTreasuryCoupon to the new instance")]
    public void CopyPrices_CurrentTreasuryCoupon_IsPreservedInCopy()
    {
        const decimal OriginalCoupon = 0.0472m;  // 4.72 %
        var original = new CurrentPrices
        {
            CurrentEquityInvestmentPrice    = 100m,
            CurrentMidTermInvestmentPrice   = 100m,
            CurrentShortTermInvestmentPrice = 100m,
            CurrentTreasuryCoupon           = OriginalCoupon,
        };

        var copy = Pricing.CopyPrices(original);

        Assert.Equal(OriginalCoupon, copy.CurrentTreasuryCoupon);
    }
}
