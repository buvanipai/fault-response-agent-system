using FaultResponseSystem.Models;

namespace FaultResponseSystem.FaultDetection;

/// <summary>
/// Pre-processor that scans BDG2 meter time-series data for statistical anomalies.
/// Converts raw sensor data to discrete Alert events to feed into the DAG.
/// </summary>
public class AnomalyDetector
{
    private readonly double _zScoreThreshold;
    private readonly int _windowSizeHours;

    public AnomalyDetector(double zScoreThreshold = 3.0, int windowSizeHours = 24)
    {
        _zScoreThreshold = zScoreThreshold;
        _windowSizeHours = windowSizeHours;
    }

    public List<Alert> ScanForAnomalies(List<BuildingMetadata> buildings, List<MeterReading> allReadings)
    {
        var alerts = new List<Alert>();
        var groupedReadings = allReadings.GroupBy(r => new { r.BuildingId, r.MeterId, r.MeterType });

        foreach (var group in groupedReadings)
        {
            var readings = group.OrderBy(r => r.Timestamp).ToList();
            if (readings.Count < _windowSizeHours) continue;

            var building = buildings.FirstOrDefault(b => b.BuildingId == group.Key.BuildingId);
            var siteId = building?.SiteId ?? "Unknown";

            // Simple rolling window z-score anomaly detection
            for (int i = _windowSizeHours; i < readings.Count; i++)
            {
                var window = readings.Skip(i - _windowSizeHours).Take(_windowSizeHours).Select(r => r.Value).ToList();
                var current = readings[i];
                
                double mean = window.Average();
                double variance = window.Select(v => Math.Pow(v - mean, 2)).Average();
                double stdDev = Math.Sqrt(variance);

                // Prevent div-by-zero for perfectly flat lines (which themselves could be anomalies)
                if (stdDev < 0.01) continue; 

                double zScore = Math.Abs(current.Value - mean) / stdDev;

                if (zScore > _zScoreThreshold)
                {
                    // Basic heuristic for fault typing
                    FaultType type = FaultType.Spike;
                    if (current.Value < 0.01) type = FaultType.Missing;
                    
                    alerts.Add(new Alert
                    {
                        AlertId = $"ALT-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}",
                        MeterId = current.MeterId,
                        BuildingId = current.BuildingId,
                        SiteId = siteId,
                        Timestamp = current.Timestamp,
                        MeterType = current.MeterType,
                        FaultType = type,
                        AnomalousValue = current.Value,
                        ExpectedMin = Math.Max(0, mean - (stdDev * 2)),
                        ExpectedMax = mean + (stdDev * 2),
                        DeviationScore = Math.Round(zScore, 2),
                        Description = $"Detected anomalous {current.MeterType} reading (Z-Score: {zScore:F2}). Value {current.Value:F1} deviates from {window.Count}-hour mean {mean:F1}."
                    });

                    // Skip ahead to avoid alerting on the same spike multiple times
                    i += 6; 
                }
            }
        }

        return alerts.OrderBy(a => a.Timestamp).ToList();
    }
}
