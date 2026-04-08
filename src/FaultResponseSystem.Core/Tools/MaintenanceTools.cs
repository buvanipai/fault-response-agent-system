using System.Text.Json;
using FaultResponseSystem.Data;

namespace FaultResponseSystem.Tools;

public static class MaintenanceTools
{
    public static async Task<string> GetMaintenanceHistoryJsonAsync(IDataProvider dataProvider, string buildingId, string meterId)
    {
        var history = await dataProvider.GetMaintenanceHistoryAsync(buildingId, meterId);
        
        return JsonSerializer.Serialize(new
        {
            BuildingId = buildingId,
            MeterId = meterId,
            TotalRecords = history.Count,
            HasOpenTickets = history.Any(h => h.Status == "Open" || h.Status == "InProgress"),
            Records = history.Take(5) // Just return top 5 recent
        });
    }

    public static async Task<string> CheckPartAvailabilityJsonAsync(IDataProvider dataProvider, string componentNameKeyword)
    {
        var parts = await dataProvider.GetAvailablePartsAsync();
        var matches = parts.Where(p => p.Component.Contains(componentNameKeyword, StringComparison.OrdinalIgnoreCase)).ToList();
        
        return JsonSerializer.Serialize(new
        {
            Keyword = componentNameKeyword,
            MatchesFound = matches.Count,
            Parts = matches
        });
    }
}
