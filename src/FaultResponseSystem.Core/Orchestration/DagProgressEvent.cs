namespace FaultResponseSystem.Orchestration;

/// <summary>
/// Fired by DagExecutor whenever a node's status changes.
/// Consumed by the Blazor dashboard to update the UI in real-time.
/// </summary>
public record DagProgressEvent(
    string NodeId,
    NodeStatus Status,
    TimeSpan? Duration = null,
    int TokensUsed = 0,
    string? ErrorMessage = null
);
