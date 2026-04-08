namespace FaultResponseSystem.Models;

/// <summary>
/// Building metadata derived from BDG2's metadata.csv schema.
/// Fields align with the ASHRAE GEPIII competition building descriptors.
/// </summary>
public class BuildingMetadata
{
    public string BuildingId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string PrimaryUse { get; set; } = string.Empty;  // Office, Education, Lodging, etc.
    public int SquareFeet { get; set; }
    public int YearBuilt { get; set; }
    public int FloorCount { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public List<string> MeterIds { get; set; } = new();
}

/// <summary>
/// A single hourly meter reading from BDG2 time-series data.
/// </summary>
public class MeterReading
{
    public string MeterId { get; set; } = string.Empty;
    public string BuildingId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public MeterType MeterType { get; set; }
    public double Value { get; set; }  // kWh for electricity, kBtu for thermal
}

/// <summary>
/// Hourly weather observation from BDG2's weather.csv schema.
/// </summary>
public class WeatherRecord
{
    public string SiteId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double AirTemperature { get; set; }      // °C
    public double DewTemperature { get; set; }       // °C
    public double WindSpeed { get; set; }            // m/s
    public int WindDirection { get; set; }           // degrees
    public int CloudCoverage { get; set; }           // oktas (0-8)
    public double PrecipDepth { get; set; }          // mm
    public double SeaLevelPressure { get; set; }     // mbar
}

/// <summary>
/// Maintenance work order record (mocked operational data).
/// </summary>
public class MaintenanceRecord
{
    public string WorkOrderId { get; set; } = string.Empty;
    public string BuildingId { get; set; } = string.Empty;
    public string MeterId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;        // Preventive, Corrective, Emergency
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;      // Completed, Open, InProgress
    public string TechnicianNotes { get; set; } = string.Empty;
    public double Cost { get; set; }
}

/// <summary>
/// A compliance/safety regulation entry (mocked).
/// </summary>
public class ComplianceRule
{
    public string RuleId { get; set; } = string.Empty;
    public string Standard { get; set; } = string.Empty;    // ASHRAE, OSHA, NFPA, etc.
    public string Category { get; set; } = string.Empty;    // Temperature, Ventilation, Electrical
    public string Description { get; set; } = string.Empty;
    public string Requirement { get; set; } = string.Empty;
    public int SeverityThreshold { get; set; }               // Min severity to trigger this rule
    public List<string> ApplicableFaultTypes { get; set; } = new();
    public List<string> ApplicableMeterTypes { get; set; } = new();
}

/// <summary>
/// A part/component available for repair (mocked).
/// </summary>
public class PartAvailability
{
    public string PartId { get; set; } = string.Empty;
    public string Component { get; set; } = string.Empty;
    public int QuantityInStock { get; set; }
    public double UnitCost { get; set; }
    public int LeadTimeDays { get; set; }
    public string Supplier { get; set; } = string.Empty;
}
