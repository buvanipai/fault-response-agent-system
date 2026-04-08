using System.Text.Json;
using Azure.AI.OpenAI;
using FaultResponseSystem.Data;
using FaultResponseSystem.Models;
using FaultResponseSystem.Tools;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace FaultResponseSystem.Agents;

public class DiagnosticAgent : BaseAgent<DiagnosticResult>
{
    public override string Name => "DiagnosticAgent";
    private readonly IDataProvider _dataProvider;

    public DiagnosticAgent(IConfiguration config, IDataProvider dataProvider) : base(config)
    {
        _dataProvider = dataProvider;
        
        _tools.Add(ChatTool.CreateFunctionTool(
            "GetRecentMeterReadings", 
            "Retrieves recent hourly meter readings to identify patterns preceding the fault.",
            BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new
                {
                    buildingId = new { type = "string" },
                    meterId = new { type = "string" },
                    hoursBack = new { type = "integer" }
                },
                required = new[] { "buildingId", "meterId", "hoursBack" }
            })
        ));

        _tools.Add(ChatTool.CreateFunctionTool(
            "GetBuildingMetadata", 
            "Retrieves building characteristics (age, use type, size) to contextualize the equipment.",
            BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new
                {
                    buildingId = new { type = "string" }
                },
                required = new[] { "buildingId" }
            })
        ));
    }

    protected override string GetSystemPrompt() => 
        "You are an expert HVAC and Building Systems Diagnostic AI. " +
        "Your role is to analyze a raw sensor anomaly (Alert) and classify the underlying physical fault. " +
        "Use the provided tools to examine recent meter history and building metadata. " +
        "Return your analysis strictly as JSON matching this schema: " +
        "{ \"ClassifiedFaultType\": \"number (0=Spike, 1=Drift, 2=Flatline, 3=Missing, 4=Oscillation)\", " +
        "\"FaultDescription\": \"string\", \"ConfidenceScore\": \"number (0-1)\", " +
        "\"UnitAge\": \"string (e.g. 20 years)\", \"UnitType\": \"string\", \"Zone\": \"string\", " +
        "\"AnalysisSummary\": \"string\" }";

    protected override string GetUserPrompt(Dictionary<string, object> context)
    {
        var alert = (Alert)context["Alert"];
        return $"Diagnose this alert: {JsonSerializer.Serialize(alert)}";
    }

    protected override async Task<string> ExecuteToolAsync(string functionName, string arguments)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        
        if (functionName == "GetRecentMeterReadings")
        {
            return await DiagnosticTools.GetRecentMeterReadingsJsonAsync(
                _dataProvider, 
                args.GetProperty("buildingId").GetString()!, 
                args.GetProperty("meterId").GetString()!, 
                args.GetProperty("hoursBack").GetInt32());
        }
        if (functionName == "GetBuildingMetadata")
        {
            return await DiagnosticTools.GetBuildingMetadataJsonAsync(
                _dataProvider, 
                args.GetProperty("buildingId").GetString()!);
        }
        
        return "{}";
    }
}
