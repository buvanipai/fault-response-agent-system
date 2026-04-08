using FaultResponseSystem.Models;
using FaultResponseSystem.Orchestration;

namespace FaultResponseSystem.Web.Services;

/// <summary>
/// Represents a single completed or in-progress execution of the agent DAG.
/// </summary>
public class DagRun
{
    public string RunId { get; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : DateTime.UtcNow - StartTime;
    public DagRunStatus Status { get; set; } = DagRunStatus.Running;

    public Alert SelectedAlert { get; set; } = new();
    public ExecutionTrace? Trace { get; set; }
    public FaultReport? Report { get; set; }
    public bool IsResolved { get; set; } = false;

    /// <summary>Per-agent results keyed by agent name, populated after the DAG completes.</summary>
    public Dictionary<string, AgentResultBase> NodeResults { get; set; } = new();

    /// <summary>Live per-node status updated in real-time during execution.</summary>
    public Dictionary<string, NodeStatus> NodeStatuses { get; set; } = new();
    public Dictionary<string, TimeSpan> NodeDurations { get; set; } = new();
    public Dictionary<string, int> NodeTokens { get; set; } = new();
    public Dictionary<string, string?> NodeErrors { get; set; } = new();
}

public enum DagRunStatus
{
    Running,
    Completed,
    Failed
}
