using System.Text.Json;
using Azure.AI.OpenAI;
using FaultResponseSystem.Data;
using FaultResponseSystem.Models;
using FaultResponseSystem.Tools;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace FaultResponseSystem.Agents;

public class ComplianceAgent : BaseAgent<ComplianceResult>
{
    public override string Name => "ComplianceAgent";
    private readonly IDataProvider _dataProvider;

    public ComplianceAgent(IConfiguration config, IDataProvider dataProvider) : base(config)
    {
        _dataProvider = dataProvider;
        
        _tools.Add(ChatTool.CreateFunctionTool(
            "EvaluateComplianceRules", 
            "Evaluates ASHRAE/OSHA/NFPA rules based on fault and severity level.",
            BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new
                {
                    meterType = new { type = "string" },
                    faultType = new { type = "string" },
                    severityScore = new { type = "integer" }
                },
                required = new[] { "meterType", "faultType", "severityScore" }
            })
        ));
    }

    protected override string GetSystemPrompt() => 
        "You are a Regulatory Compliance AI for physical facilities. " +
        "You check if a diagnosed building fault violates safety codes or energy standards (ASHRAE/OSHA/EPA). " +
        "Return your analysis strictly as JSON matching this schema: " +
        "{ \"IsCompliant\": \"boolean\", \"ViolatedRegulations\": [\"strings\"], " +
        "\"ApplicableStandards\": [\"strings\"], \"UrgencyLevel\": \"string (Immediate, 24h, 7d, 30d)\", " +
        "\"ComplianceSummary\": \"string\" }";

    protected override string GetUserPrompt(Dictionary<string, object> context)
    {
        var alert = (Alert)context["Alert"];
        var risk = context.ContainsKey("RiskAssessment") ? (RiskAssessment)context["RiskAssessment"] : null;
        var severity = risk?.SeverityScore ?? 5;
        
        return $"Check compliance for {alert.MeterType} {alert.FaultType}. Calculated severity score is {severity}.";
    }

    protected override async Task<string> ExecuteToolAsync(string functionName, string arguments)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        
        if (functionName == "EvaluateComplianceRules")
        {
            var meterType = Enum.Parse<MeterType>(args.GetProperty("meterType").GetString()!);
            var faultType = Enum.Parse<FaultType>(args.GetProperty("faultType").GetString()!);
            
            return await ComplianceTools.EvaluateComplianceRulesJsonAsync(
                _dataProvider, 
                meterType, 
                faultType, 
                args.GetProperty("severityScore").GetInt32());
        }
        
        return "{}";
    }
}
