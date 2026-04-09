using FaultResponseSystem.Models;

namespace FaultResponseSystem.Data;

public interface IDataProvider
{
    Task<List<BuildingMetadata>> GetBuildingsAsync();
    Task<BuildingMetadata?> GetBuildingAsync(string buildingId);
    
    Task<List<MeterReading>> GetMeterReadingsAsync(string buildingId, string meterId, DateTime start, DateTime end);
    Task<List<MeterReading>> GetAllMeterReadingsAsync();
    
    Task<List<WeatherRecord>> GetWeatherAsync(string siteId, DateTime start, DateTime end);
    Task<List<MaintenanceRecord>> GetMaintenanceHistoryAsync(string buildingId, string meterId);
    Task<bool> HasOpenTicketsAsync(string buildingId, string meterId);
    
    Task<List<ComplianceRule>> GetComplianceRulesAsync();
    Task<List<PartAvailability>> GetAvailablePartsAsync();
}
