using System.Text.Json;

namespace FaultResponseSystem.Tools;

public static class RiskTools
{
    // The RiskAgent mostly uses reasoning over upstream agent inputs rather than external data lookups,
    // but we can provide a tool to calculate explicit financial impact estimates.
    
    public static string CalculateFinancialImpactJson(double downtimeHours, int occupantsAffected, bool requiresComponentReplacement, double baseComponentCost)
    {
        // Rough mock formula for risk assessment
        double hourlyProductivityLossPerPerson = 50.0; 
        double productivityCost = occupantsAffected * downtimeHours * hourlyProductivityLossPerPerson;
        
        double repairCost = requiresComponentReplacement ? baseComponentCost + (downtimeHours * 100) : downtimeHours * 100;
        
        return JsonSerializer.Serialize(new
        {
            EstimatedProductivityLoss = productivityCost,
            EstimatedRepairCost = repairCost,
            TotalFinancialRisk = productivityCost + repairCost,
            CostSeverityLevel = (productivityCost + repairCost) > 10000 ? "High" : "Moderate"
        });
    }
}
