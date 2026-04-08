using FaultResponseSystem.Models;

namespace FaultResponseSystem.FaultDetection;

/// <summary>
/// Produces lightweight heuristic cost and labor estimates from an Alert,
/// without requiring an LLM call. Used for dashboard pre-run risk totals.
/// </summary>
public static class AlertEstimator
{
    // Base cost ($) and labor (hours) by fault type
    private static readonly Dictionary<FaultType, (double Cost, double Hours)> _baselines = new()
    {
        { FaultType.Spike,       (2_500, 3.0) },
        { FaultType.Drift,       (1_800, 2.5) },
        { FaultType.Flatline,    (1_200, 2.0) },
        { FaultType.Missing,     (  600, 1.0) },
        { FaultType.Oscillation, (2_000, 2.5) },
    };

    // Multiplier by meter type (electricity faults are costlier)
    private static readonly Dictionary<MeterType, double> _meterMultiplier = new()
    {
        { MeterType.Electricity,  1.4 },
        { MeterType.Steam,        1.2 },
        { MeterType.ChilledWater, 1.0 },
        { MeterType.HotWater,     1.0 },
        { MeterType.Irrigation,   0.7 },
    };

    /// <summary>
    /// Returns (estimatedCost, estimatedHours) using only alert metadata.
    /// Scales linearly with deviation score relative to a baseline of 3.5.
    /// </summary>
    public static (double Cost, double Hours) Estimate(Alert alert)
    {
        var (baseCost, baseHours) = _baselines.TryGetValue(alert.FaultType, out var b)
            ? b : (1_500, 2.0);

        var meterMult = _meterMultiplier.TryGetValue(alert.MeterType, out var m)
            ? m : 1.0;

        // Scale by how far the deviation exceeds our baseline z-score of 3.5
        var deviationMult = Math.Max(1.0, alert.DeviationScore / 3.5);

        var cost  = Math.Round(baseCost  * meterMult * deviationMult, 0);
        var hours = Math.Round(baseHours * deviationMult, 1);

        return (cost, hours);
    }
}
