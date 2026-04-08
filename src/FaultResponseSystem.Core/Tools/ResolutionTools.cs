using System.Text.Json;

namespace FaultResponseSystem.Tools;

public static class ResolutionTools
{
    public static string GenerateDraftWorkOrderJson(string systemType, string faultDescription, string recommendedFix)
    {
        // Real system would call a CMMS API (e.g. Maximo, ServiceNow)
        // Here we just standardize the format
        return JsonSerializer.Serialize(new
        {
            SuggestedTitle = $"Fix {systemType} - {faultDescription}",
            RecommendedPriority = "High",
            WorkType = "Corrective",
            ActionPlan = recommendedFix,
            DraftApiPayload = new
            {
                recordType = "WORKORDER",
                asset = systemType,
                description = recommendedFix
            }
        });
    }
}
