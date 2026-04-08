using System.Text.Json;
using Azure.AI.OpenAI;
using FaultResponseSystem.Data;
using FaultResponseSystem.Models;
using FaultResponseSystem.Tools;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace FaultResponseSystem.Agents;

public class MaintenanceAgent : BaseAgent<MaintenanceResult>
{
    public override string Name => "MaintenanceAgent";
    private readonly IDataProvider _dataProvider;

    public MaintenanceAgent(IConfiguration config, IDataProvider dataProvider) : base(config)
    {
        _dataProvider = dataProvider;
        
        _tools.Add(ChatTool.CreateFunctionTool(
            "GetMaintenanceHistory", 
            "Retrieves past work orders and open tickets for a piece of equipment.",
            BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new
                {
                    buildingId = new { type = "string" },
                    meterId = new { type = "string" }
                },
                required = new[] { "buildingId", "meterId" }
            })
        ));

        _tools.Add(ChatTool.CreateFunctionTool(
            "CheckPartAvailability", 
            "Checks warehouse inventory for replacement parts.",
            BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new
                {
                    componentNameKeyword = new { type = "string" }
                },
                required = new[] { "componentNameKeyword" }
            })
        ));
    }

    protected override string GetSystemPrompt() => 
        "You are an Operations and Maintenance AI evaluating a building fault. " +
        "Check recent maintenance records to see if this is a recurring issue or if there is an active work order. " +
        "Check part inventory if a physical repair seems likely. " +
        "Return your analysis strictly as JSON matching this schema: " +
        "{ \"RecentHistory\": [/* array of work order objects */], " +
        "\"HasOpenTicket\": \"boolean\", \"OpenTicketId\": \"string\", \"RepairCount12Months\": \"number\", " +
        "\"MaintenanceCost12Months\": \"number\", \"PartsAvailable\": \"boolean\", " +
        "\"RelevantParts\": [/* parts objects */], \"MaintenanceSummary\": \"string\" }";

    protected override string GetUserPrompt(Dictionary<string, object> context)
    {
        var alert = (Alert)context["Alert"];
        return $"Assess maintenance context for this meter alert: {JsonSerializer.Serialize(alert)}";
    }

    protected override async Task<string> ExecuteToolAsync(string functionName, string arguments)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        
        if (functionName == "GetMaintenanceHistory")
        {
            return await MaintenanceTools.GetMaintenanceHistoryJsonAsync(
                _dataProvider, 
                args.GetProperty("buildingId").GetString()!, 
                args.GetProperty("meterId").GetString()!);
        }
        if (functionName == "CheckPartAvailability")
        {
            return await MaintenanceTools.CheckPartAvailabilityJsonAsync(
                _dataProvider, 
                args.GetProperty("componentNameKeyword").GetString()!);
        }
        
        return "{}";
    }
}
