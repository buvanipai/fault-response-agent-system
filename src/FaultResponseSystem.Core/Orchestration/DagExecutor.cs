using FaultResponseSystem.Models;

namespace FaultResponseSystem.Orchestration;

public class DagExecutor
{
    private readonly List<AgentNode> _nodes = new();

    /// <summary>
    /// Subscribe to receive real-time progress updates as nodes change status.
    /// Fired on the thread that changes the status (use InvokeAsync in Blazor).
    /// </summary>
    public event Action<DagProgressEvent>? OnProgress;

    public void AddNode(AgentNode node)
    {
        if (!_nodes.Contains(node))
            _nodes.Add(node);
    }

    public async Task<(Dictionary<string, object> finalContext, ExecutionTrace trace)> ExecuteAsync(
        Dictionary<string, object> initialContext,
        CancellationToken cancellationToken = default)
    {
        var context = new Dictionary<string, object>(initialContext);
        var trace = new ExecutionTrace { StartTime = DateTime.UtcNow };

        // Reset all nodes
        foreach (var node in _nodes)
        {
            node.Status = NodeStatus.Pending;
            OnProgress?.Invoke(new DagProgressEvent(node.Id, NodeStatus.Pending));
        }

        bool anyPending = true;
        while (anyPending && !cancellationToken.IsCancellationRequested)
        {
            anyPending = false;
            var nodesToRun = new List<AgentNode>();

            foreach (var node in _nodes.Where(n => n.Status == NodeStatus.Pending))
            {
                bool depsMet = node.Dependencies.All(d =>
                    d.Status == NodeStatus.Completed || d.Status == NodeStatus.Skipped);

                if (depsMet)
                    nodesToRun.Add(node);
                else
                    anyPending = true;
            }

            if (!nodesToRun.Any() && anyPending)
                throw new InvalidOperationException("DAG is stuck. Circular dependency or failed node prevention.");

            if (!nodesToRun.Any()) break;

            // Execute parallel-ready nodes concurrently
            var tasks = nodesToRun.Select(async node =>
            {
                node.Status = NodeStatus.Running;
                OnProgress?.Invoke(new DagProgressEvent(node.Id, NodeStatus.Running));

                // Conditional branching
                if (node.Condition != null && !node.Condition(context))
                {
                    node.Status = NodeStatus.Skipped;
                    trace.AddStep(node.Id, NodeStatus.Skipped, TimeSpan.Zero, 0);
                    OnProgress?.Invoke(new DagProgressEvent(node.Id, NodeStatus.Skipped, TimeSpan.Zero));
                    return;
                }

                var result = await node.Agent.ExecuteAsync(context, cancellationToken);

                lock (context)
                {
                    string key = result.GetType().Name;
                    context[key] = result;
                }

                if (result.Success)
                {
                    node.Status = NodeStatus.Completed;
                    trace.AddStep(node.Id, NodeStatus.Completed, result.Duration, result.TokensUsed);
                    OnProgress?.Invoke(new DagProgressEvent(node.Id, NodeStatus.Completed, result.Duration, result.TokensUsed));
                }
                else
                {
                    node.Status = NodeStatus.Failed;
                    trace.AddStep(node.Id, NodeStatus.Failed, result.Duration, result.TokensUsed, result.ErrorMessage);
                    OnProgress?.Invoke(new DagProgressEvent(node.Id, NodeStatus.Failed, result.Duration, result.TokensUsed, result.ErrorMessage));
                }

            }).ToList();

            await Task.WhenAll(tasks);

            if (nodesToRun.Any(n => n.Status == NodeStatus.Failed))
                break;
        }

        trace.EndTime = DateTime.UtcNow;
        return (context, trace);
    }
}
