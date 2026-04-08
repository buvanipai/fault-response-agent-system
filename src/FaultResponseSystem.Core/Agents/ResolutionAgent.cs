using System.Text.Json;
using Azure.AI.OpenAI;
using FaultResponseSystem.Data;
using FaultResponseSystem.Models;
using FaultResponseSystem.Tools;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace FaultResponseSystem.Agents;

public class ResolutionAgent : BaseAgent<ResolutionPlan>
{
    public override string Name => "ResolutionAgent";

    public ResolutionAgent(IConfiguration config) : base(config)
    {
        _tools.Add(ChatTool.CreateFunctionTool(
            "GenerateDraftWorkOrder", 
            "Generates a standardized CMMS work order based on the diagnostic and fix details.",
            BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new
                {
                    systemType = new { type = "string" },
                    faultDescription = new { type = "string" },
                    recommendedFix = new { type = "string" }
                },
                required = new[] { "systemType", "faultDescription", "recommendedFix" }
            })
        ));
    }

    protected override string GetSystemPrompt() => 
        "You are an Action Planning AI for facility management. " +
        "You take in all upstream analysis and design a step-by-step resolution plan and work order. " +
        "Return your analysis strictly as JSON matching this schema: " +
        "{ \"WorkOrderTitle\": \"string\", \"WorkOrderDescription\": \"string\", \"Priority\": \"string (P1-P4)\", " +
        "\"AssignedTeam\": \"string\", \"EstimatedRepairHours\": \"number\", \"EstimatedCost\": \"number\", " +
        "\"RequiredParts\": [\"strings\"], \"RepairSteps\": [\"strings\"], \"PreventiveActions\": [\"strings\"], " +
        "\"ResolutionSummary\": \"string\" }";

    protected override string GetUserPrompt(Dictionary<string, object> context)
    {
        var diag = context.ContainsKey("DiagnosticResult") ? JsonSerializer.Serialize(context["DiagnosticResult"]) : "{}";
        var risk = context.ContainsKey("RiskAssessment") ? JsonSerializer.Serialize(context["RiskAssessment"]) : "{}";
        var comp = context.ContainsKey("ComplianceResult") ? JsonSerializer.Serialize(context["ComplianceResult"]) : "{}";
        var maint = context.ContainsKey("MaintenanceResult") ? JsonSerializer.Serialize(context["MaintenanceResult"]) : "{}";
        
        return $"Create a resolution plan for this fault. Use these prior analyses.\nDiag: {diag}\nRisk: {risk}\nMaint: {maint}\nComp: {comp}";
    }

    protected override Task<string> ExecuteToolAsync(string functionName, string arguments)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        
        if (functionName == "GenerateDraftWorkOrder")
        {
            return Task.FromResult(ResolutionTools.GenerateDraftWorkOrderJson(
                args.GetProperty("systemType").GetString()!, 
                args.GetProperty("faultDescription").GetString()!,
                args.GetProperty("recommendedFix").GetString()!));
        }
        
        return Task.FromResult("{}");
    }
}
