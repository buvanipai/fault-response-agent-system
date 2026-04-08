using FaultResponseSystem.Models;

namespace FaultResponseSystem.Agents;

public interface IAgent
{
    string Name { get; }
    Task<AgentResultBase> ExecuteAsync(Dictionary<string, object> context, CancellationToken cancellationToken = default);
}
