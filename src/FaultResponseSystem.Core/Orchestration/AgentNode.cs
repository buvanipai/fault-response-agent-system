using FaultResponseSystem.Agents;

namespace FaultResponseSystem.Orchestration;

public enum NodeStatus
{
    Pending,
    Running,
    Completed,
    Skipped,
    Failed
}

public class AgentNode
{
    public string Id => Agent.Name;
    public IAgent Agent { get; }
    public List<AgentNode> Dependencies { get; } = new();
    public NodeStatus Status { get; set; } = NodeStatus.Pending;
    public Func<Dictionary<string, object>, bool>? Condition { get; set; }

    public AgentNode(IAgent agent)
    {
        Agent = agent;
    }

    public void AddDependency(AgentNode node)
    {
        if (!Dependencies.Contains(node))
        {
            Dependencies.Add(node);
        }
    }
}
