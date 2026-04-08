using System.Text.Json;
using FaultResponseSystem.Models;
using Microsoft.Extensions.Configuration;

namespace FaultResponseSystem.Agents;

public class ReportAgent : BaseAgent<FaultReport>
{
    public override string Name => "ReportAgent";

    public ReportAgent(IConfiguration config) : base(config)
    {
        // No tools needed for ReportAgent. It reads the previous outputs and synthesizes the executive summary.
    }

    protected override string GetSystemPrompt() => 
        "You are the Lead Integrator AI. " +
        "You consume outputs from 6 specialized agents and synthesize a single, cohesive Executive Summary and Final Recommended Action. " +
        "Summarize the root cause, business impact, and next steps for a human Facility Manager in less than 3 paragraphs. " +
        "Return your analysis strictly as JSON matching this schema: " +
        "{ \"ExecutiveSummary\": \"string\", \"RecommendedAction\": \"string\" }";

    protected override string GetUserPrompt(Dictionary<string, object> context)
    {
        // The ReportAgent will just receive the entire context object serialized
        var payload = new Dictionary<string, object>(context);
        // Exclude the raw alert object itself since agents have already chewed on it, unless we want to include it.
        // We'll leave it in for fullest context.
        return $"Synthesize this multi-agent incident response: {JsonSerializer.Serialize(payload)}";
    }

    // Since we only ask it for ExecutiveSummary and RecommendedAction, we'll patch the rest of the FaultReport data
    // in the Orchestrator, but we can do it via a wrapper task later.
}
