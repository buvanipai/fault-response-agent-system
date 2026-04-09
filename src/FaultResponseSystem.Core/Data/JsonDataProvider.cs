using System.Text.Json;
using System.Text.Json.Nodes;
using FaultResponseSystem.Models;
using Microsoft.Extensions.Configuration;

namespace FaultResponseSystem.Data;

public class JsonDataProvider : IDataProvider
{
    private readonly string _basePath;
    
    // Cached data
    private List<BuildingMetadata>? _buildings;
    private List<MeterReading>? _readings;
    private List<WeatherRecord>? _weather;
    private List<MaintenanceRecord>? _maintenance;
    private List<ComplianceRule>? _rules;
    private List<PartAvailability>? _parts;

    public JsonDataProvider(IConfiguration config)
    {
        _basePath = config["DataPath"] ?? "Data/SampleData";
    }

    private async Task<T> LoadJsonAsync<T>(string filename)
    {
        var path = Path.Combine(_basePath, filename);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Cannot find sample data file: {path}");

        using var stream = File.OpenRead(path);
        // Deserializing with CaseInsensitive to handle mapping easily
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return await JsonSerializer.DeserializeAsync<T>(stream, options) ?? throw new InvalidOperationException($"Failed to deserialize {filename}");
    }

    public async Task<List<BuildingMetadata>> GetBuildingsAsync()
    {
        _buildings ??= await LoadJsonAsync<List<BuildingMetadata>>("buildings.json");
        return _buildings;
    }

    public async Task<BuildingMetadata?> GetBuildingAsync(string buildingId)
    {
        var buildings = await GetBuildingsAsync();
        return buildings.FirstOrDefault(b => b.BuildingId == buildingId);
    }

    public async Task<List<MeterReading>> GetMeterReadingsAsync(string buildingId, string meterId, DateTime start, DateTime end)
    {
        var allReadings = await GetAllMeterReadingsAsync();
        return allReadings
            .Where(r => r.BuildingId == buildingId && r.MeterId == meterId && r.Timestamp >= start && r.Timestamp <= end)
            .OrderBy(r => r.Timestamp)
            .ToList();
    }

    public async Task<List<MeterReading>> GetAllMeterReadingsAsync()
    {
        _readings ??= await LoadJsonAsync<List<MeterReading>>("meters.json");
        return _readings;
    }

    public async Task<List<WeatherRecord>> GetWeatherAsync(string siteId, DateTime start, DateTime end)
    {
        _weather ??= await LoadJsonAsync<List<WeatherRecord>>("weather.json");
        return _weather
            .Where(w => w.SiteId == siteId && w.Timestamp >= start && w.Timestamp <= end)
            .OrderBy(w => w.Timestamp)
            .ToList();
    }

    public async Task<List<MaintenanceRecord>> GetMaintenanceHistoryAsync(string buildingId, string meterId)
    {
        if (_maintenance == null)
        {
            var path = Path.Combine(_basePath, "maintenance.json");
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                _maintenance = await JsonSerializer.DeserializeAsync<List<MaintenanceRecord>>(stream, options);
            }
            else
            {
                _maintenance = new List<MaintenanceRecord>();
            }
        }
        
        return _maintenance!
            .Where(m => m.BuildingId == buildingId && m.MeterId == meterId)
            .OrderByDescending(m => m.Date)
            .ToList();
    }

    public async Task<bool> HasOpenTicketsAsync(string buildingId, string meterId)
    {
        var history = await GetMaintenanceHistoryAsync(buildingId, meterId);
        return history.Any(m => m.Status.Equals("Open", StringComparison.OrdinalIgnoreCase) || 
                                m.Status.Equals("InProgress", StringComparison.OrdinalIgnoreCase));
    }

    private async Task LoadComplianceDataAsync()
    {
        if (_rules != null && _parts != null) return;
        
        var path = Path.Combine(_basePath, "compliance_rules.json");
        if (File.Exists(path))
        {
            using var stream = File.OpenRead(path);
            var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (root.TryGetProperty("Rules", out var rulesElement))
                _rules = JsonSerializer.Deserialize<List<ComplianceRule>>(rulesElement, options) ?? new List<ComplianceRule>();
            else
                _rules = new List<ComplianceRule>();

            if (root.TryGetProperty("Parts", out var partsElement))
                _parts = JsonSerializer.Deserialize<List<PartAvailability>>(partsElement, options) ?? new List<PartAvailability>();
            else
                _parts = new List<PartAvailability>();
        }
        else
        {
            _rules = new List<ComplianceRule>();
            _parts = new List<PartAvailability>();
        }
    }

    public async Task<List<ComplianceRule>> GetComplianceRulesAsync()
    {
        await LoadComplianceDataAsync();
        return _rules!;
    }

    public async Task<List<PartAvailability>> GetAvailablePartsAsync()
    {
        await LoadComplianceDataAsync();
        return _parts!;
    }
}
