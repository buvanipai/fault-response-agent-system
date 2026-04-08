using System.Text.Json;
using Azure.AI.OpenAI;
using FaultResponseSystem.Data;
using FaultResponseSystem.Models;
using FaultResponseSystem.Tools;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace FaultResponseSystem.Agents;

public class ContextAgent : BaseAgent<ContextResult>
{
    public override string Name => "ContextAgent";
    private readonly IDataProvider _dataProvider;

    public ContextAgent(IConfiguration config, IDataProvider dataProvider) : base(config)
    {
        _dataProvider = dataProvider;
        
        _tools.Add(ChatTool.CreateFunctionTool(
            "GetWeatherConditions", 
            "Retrieves weather conditions at the time of the fault.",
            BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new
                {
                    siteId = new { type = "string" },
                    targetDate = new { type = "string", format = "date-time" }
                },
                required = new[] { "siteId", "targetDate" }
            })
        ));

        _tools.Add(ChatTool.CreateFunctionTool(
            "GetOccupancyEstimate", 
            "Estimates building occupancy based on time and primary use.",
            BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new
                {
                    primaryUse = new { type = "string" },
                    time = new { type = "string", format = "date-time" },
                    squareFeet = new { type = "integer" }
                },
                required = new[] { "primaryUse", "time", "squareFeet" }
            })
        ));
    }

    protected override string GetSystemPrompt() => 
        "You are an Environmental Context AI evaluating building sensor alerts. " +
        "Your role is to assess external factors (weather, occupancy) that might explain or exacerbate the alert. " +
        "Use tools to get weather and occupancy estimates. " +
        "Return your analysis strictly as JSON matching this schema: " +
        "{ \"OutdoorTemperature\": \"number\", \"DewPoint\": \"number\", \"WindSpeed\": \"number\", " +
        "\"CloudCoverage\": \"number\", \"WeatherConditions\": \"string\", \"OccupancyLevel\": \"string (High/Medium/Low)\", " +
        "\"EstimatedOccupants\": \"number\", \"AdjacentUnitIssues\": [\"array of strings\"], " +
        "\"ContextSummary\": \"string\" }";

    protected override string GetUserPrompt(Dictionary<string, object> context)
    {
        var alert = (Alert)context["Alert"];
        // We assume we pass building info into context before execution, or the agent can ask for it.
        // For simplicity, we just pass the alert.
        return $"Assess environmental context for this alert: {JsonSerializer.Serialize(alert)}\nAssuming PrimaryUse is 'Office' and SqFt is 100000 if not known.";
    }

    protected override async Task<string> ExecuteToolAsync(string functionName, string arguments)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        
        if (functionName == "GetWeatherConditions")
        {
            var targetDate = DateTime.Parse(args.GetProperty("targetDate").GetString()!);
            return await ContextTools.GetWeatherConditionsJsonAsync(
                _dataProvider, 
                args.GetProperty("siteId").GetString()!, 
                targetDate);
        }
        if (functionName == "GetOccupancyEstimate")
        {
            var time = DateTime.Parse(args.GetProperty("time").GetString()!);
            return ContextTools.GetOccupancyEstimateJson(
                args.GetProperty("primaryUse").GetString()!, 
                time,
                args.GetProperty("squareFeet").GetInt32());
        }
        
        return "{}";
    }
}
