using System.Diagnostics;

namespace FaultResponseSystem.Orchestration;

public class ExecutionTraceStep
{
    public string AgentName { get; set; } = string.Empty;
    public NodeStatus Status { get; set; }
    public TimeSpan Duration { get; set; }
    public int TokensUsed { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class ExecutionTrace
{
    public string RunId { get; } = Guid.NewGuid().ToString("N");
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan TotalDuration => EndTime - StartTime;
    public List<ExecutionTraceStep> Steps { get; } = new();

    public void AddStep(string agentName, NodeStatus status, TimeSpan duration, int tokens, string error = "")
    {
        lock (Steps)
        {
            Steps.Add(new ExecutionTraceStep
            {
                AgentName = agentName,
                Status = status,
                Duration = duration,
                TokensUsed = tokens,
                ErrorMessage = error
            });
        }
    }
}
