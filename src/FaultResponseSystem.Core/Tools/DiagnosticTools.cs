using System.Text.Json;
using FaultResponseSystem.Data;

namespace FaultResponseSystem.Tools;

public static class DiagnosticTools
{
    public static async Task<string> GetRecentMeterReadingsJsonAsync(IDataProvider dataProvider, string buildingId, string meterId, int hoursBack)
    {
        // For our demo, we don't have a real time "now", so we'll look at the latest reading time for this meter
        var allReadings = await dataProvider.GetAllMeterReadingsAsync();
        var meterReadings = allReadings.Where(r => r.BuildingId == buildingId && r.MeterId == meterId).ToList();
        
        if (!meterReadings.Any()) return "{\"error\": \"No readings found for this meter.\"}";

        var latestTime = meterReadings.Max(r => r.Timestamp);
        var startTime = latestTime.AddHours(-hoursBack);

        var recent = meterReadings.Where(r => r.Timestamp >= startTime).OrderBy(r => r.Timestamp).ToList();
        return JsonSerializer.Serialize(new { BuildingId = buildingId, MeterId = meterId, Readings = recent });
    }

    public static async Task<string> GetBuildingMetadataJsonAsync(IDataProvider dataProvider, string buildingId)
    {
        var building = await dataProvider.GetBuildingAsync(buildingId);
        if (building == null) return "{\"error\": \"Building not found.\"}";
        return JsonSerializer.Serialize(building);
    }
}
