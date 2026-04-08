using System.Text.Json;
using Azure.AI.OpenAI;
using FaultResponseSystem.Data;
using FaultResponseSystem.Models;
using FaultResponseSystem.Tools;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace FaultResponseSystem.Agents;

public class RiskAgent : BaseAgent<RiskAssessment>
{
    public override string Name => "RiskAgent";

    public RiskAgent(IConfiguration config) : base(config)
    {
        _tools.Add(ChatTool.CreateFunctionTool(
            "CalculateFinancialImpact", 
            "Calculates financial impact based on estimated downtime and affected occupants.",
            BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new
                {
                    downtimeHours = new { type = "number" },
                    occupantsAffected = new { type = "integer" },
                    requiresComponentReplacement = new { type = "boolean" },
                    baseComponentCost = new { type = "number" }
                },
                required = new[] { "downtimeHours", "occupantsAffected", "requiresComponentReplacement", "baseComponentCost" }
            })
        ));
    }

    protected override string GetSystemPrompt() => 
        "You are a Risk Assessment AI for building operations. " +
        "You receive diagnostic, context, and maintenance insights about a fault. " +
        "Calculate the risk score (1-10) and estimate the business impact. " +
        "Return your analysis strictly as JSON matching this schema: " +
        "{ \"SeverityScore\": \"integer 1-10\", \"SeverityLevel\": \"string (Critical/High/Medium/Low)\", " +
        "\"DowntimeHoursEstimate\": \"number\", \"AffectedOccupants\": \"number\", " +
        "\"EstimatedCostImpact\": \"number\", \"RiskFactors\": [\"array of strings\"], " +
        "\"RiskSummary\": \"string\" }";

    protected override string GetUserPrompt(Dictionary<string, object> context)
    {
        var alert = JsonSerializer.Serialize((Alert)context["Alert"]);
        var diag = context.ContainsKey("DiagnosticResult") ? JsonSerializer.Serialize(context["DiagnosticResult"]) : "{}";
        var ctx = context.ContainsKey("ContextResult") ? JsonSerializer.Serialize(context["ContextResult"]) : "{}";
        var maint = context.ContainsKey("MaintenanceResult") ? JsonSerializer.Serialize(context["MaintenanceResult"]) : "{}";
        
        return $"Assess risk for this fault.\nAlert: {alert}\nDiagnostic: {diag}\nContext: {ctx}\nMaintenance: {maint}";
    }

    protected override Task<string> ExecuteToolAsync(string functionName, string arguments)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        
        if (functionName == "CalculateFinancialImpact")
        {
            return Task.FromResult(RiskTools.CalculateFinancialImpactJson(
                args.GetProperty("downtimeHours").GetDouble(), 
                args.GetProperty("occupantsAffected").GetInt32(),
                args.GetProperty("requiresComponentReplacement").GetBoolean(),
                args.GetProperty("baseComponentCost").GetDouble()));
        }
        
        return Task.FromResult("{}");
    }
}
