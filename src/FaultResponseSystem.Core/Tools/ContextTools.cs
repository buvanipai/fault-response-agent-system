using System.Text.Json;
using FaultResponseSystem.Data;

namespace FaultResponseSystem.Tools;

public static class ContextTools
{
    public static async Task<string> GetWeatherConditionsJsonAsync(IDataProvider dataProvider, string siteId, DateTime targetDate)
    {
        var weather = await dataProvider.GetWeatherAsync(siteId, targetDate.AddHours(-2), targetDate.AddHours(2));
        if (!weather.Any()) return "{\"error\": \"Weather data not available for this time range.\"}";

        return JsonSerializer.Serialize(new 
        { 
            SiteId = siteId, 
            TimeRangeCenter = targetDate,
            AverageTemperature = weather.Average(w => w.AirTemperature),
            MaxTemperature = weather.Max(w => w.AirTemperature),
            AverageCloudCover = weather.Average(w => w.CloudCoverage),
            HourlyRecords = weather 
        });
    }

    public static string GetOccupancyEstimateJson(string primaryUse, DateTime time, int squareFeet)
    {
        // Simple heuristic model for demo purposes
        double occupancyFactor = 0.0;
        var hour = time.Hour;
        var day = time.DayOfWeek;
        
        bool isWeekend = day == DayOfWeek.Saturday || day == DayOfWeek.Sunday;

        switch (primaryUse.ToLower())
        {
            case "office":
                if (!isWeekend && hour >= 8 && hour <= 18) occupancyFactor = 0.8;
                else occupancyFactor = 0.05;
                break;
            case "education":
                if (!isWeekend && hour >= 7 && hour <= 16) occupancyFactor = 0.9;
                else occupancyFactor = 0.1;
                break;
            case "lodging/residential":
                if (hour >= 20 || hour <= 8) occupancyFactor = 0.9;
                else occupancyFactor = 0.4;
                break;
            default:
                occupancyFactor = 0.5;
                break;
        }

        // Rough estimate: 1 person per 200 sqft at 100% capacity
        int maxOccupants = squareFeet / 200;
        int estimatedOccupants = (int)(maxOccupants * occupancyFactor);

        return JsonSerializer.Serialize(new 
        { 
            PrimaryUse = primaryUse, 
            Time = time, 
            IsBusinessHours = (occupancyFactor > 0.5),
            OccupancyFactor = occupancyFactor,
            EstimatedOccupants = estimatedOccupants 
        });
    }
}
