using System.Text.Json.Serialization;

namespace FaultResponseSystem.Models;

/// <summary>
/// Base class for all agent execution results.
/// Carries metadata about the agent run alongside domain-specific output.
/// </summary>
public abstract class AgentResultBase
{
    public string AgentName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration => CompletedAt - StartedAt;
    public int TokensUsed { get; set; }
    public string RawLlmOutput { get; set; } = string.Empty;
}

/// <summary>
/// DiagnosticAgent output: fault classification and sensor analysis.
/// </summary>
public class DiagnosticResult : AgentResultBase
{
    public FaultType ClassifiedFaultType { get; set; }
    public string FaultDescription { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string UnitAge { get; set; } = string.Empty;
    public string UnitType { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public List<MeterReading> RecentReadings { get; set; } = new();
    public string AnalysisSummary { get; set; } = string.Empty;
}

/// <summary>
/// ContextAgent output: environmental and occupancy context.
/// </summary>
public class ContextResult : AgentResultBase
{
    public double OutdoorTemperature { get; set; }
    public double DewPoint { get; set; }
    public double WindSpeed { get; set; }
    public int CloudCoverage { get; set; }
    public string WeatherConditions { get; set; } = string.Empty;
    public string OccupancyLevel { get; set; } = string.Empty;  // High, Medium, Low
    public int EstimatedOccupants { get; set; }
    public List<string> AdjacentUnitIssues { get; set; } = new();
    public string ContextSummary { get; set; } = string.Empty;
}

/// <summary>
/// MaintenanceAgent output: service history and repair feasibility.
/// </summary>
public class MaintenanceResult : AgentResultBase
{
    public List<MaintenanceRecord> RecentHistory { get; set; } = new();
    public bool HasOpenTicket { get; set; }
    public string? OpenTicketId { get; set; }
    public int RepairCount12Months { get; set; }
    public double MaintenanceCost12Months { get; set; }
    public bool PartsAvailable { get; set; }
    public List<PartAvailability> RelevantParts { get; set; } = new();
    public string MaintenanceSummary { get; set; } = string.Empty;
}

/// <summary>
/// RiskAgent output: severity scoring and impact assessment.
/// </summary>
public class RiskAssessment : AgentResultBase
{
    public int SeverityScore { get; set; }  // 1-10
    public string SeverityLevel { get; set; } = string.Empty; // Critical, High, Medium, Low
    public double DowntimeHoursEstimate { get; set; }
    public int AffectedOccupants { get; set; }
    public double EstimatedCostImpact { get; set; }
    public List<string> RiskFactors { get; set; } = new();
    public string RiskSummary { get; set; } = string.Empty;
}

/// <summary>
/// ComplianceAgent output: regulatory assessment.
/// </summary>
public class ComplianceResult : AgentResultBase
{
    public bool IsCompliant { get; set; }
    public List<string> ViolatedRegulations { get; set; } = new();
    public List<string> ApplicableStandards { get; set; } = new();
    public string UrgencyLevel { get; set; } = string.Empty;  // Immediate, 24h, 7d, 30d
    public string ComplianceSummary { get; set; } = string.Empty;
}

/// <summary>
/// ResolutionAgent output: work order and prevention recommendations.
/// </summary>
public class ResolutionPlan : AgentResultBase
{
    public string WorkOrderTitle { get; set; } = string.Empty;
    public string WorkOrderDescription { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;  // P1, P2, P3, P4
    public string AssignedTeam { get; set; } = string.Empty;
    public double EstimatedRepairHours { get; set; }
    public double EstimatedCost { get; set; }
    public List<string> RequiredParts { get; set; } = new();
    public List<string> RepairSteps { get; set; } = new();
    public List<string> PreventiveActions { get; set; } = new();
    public string ResolutionSummary { get; set; } = string.Empty;
}

/// <summary>
/// ReportAgent output: final aggregated fault response report.
/// </summary>
public class FaultReport : AgentResultBase
{
    public string ReportId { get; set; } = string.Empty;
    public Alert OriginalAlert { get; set; } = new();
    public DiagnosticResult? Diagnostics { get; set; }
    public ContextResult? Context { get; set; }
    public MaintenanceResult? Maintenance { get; set; }
    public RiskAssessment? Risk { get; set; }
    public ComplianceResult? Compliance { get; set; }
    public ResolutionPlan? Resolution { get; set; }
    public string ExecutiveSummary { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}
