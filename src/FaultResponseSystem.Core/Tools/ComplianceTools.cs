using System.Text.Json;
using FaultResponseSystem.Data;
using FaultResponseSystem.Models;

namespace FaultResponseSystem.Tools;

public static class ComplianceTools
{
    public static async Task<string> EvaluateComplianceRulesJsonAsync(IDataProvider dataProvider, MeterType meterType, FaultType faultType, int severityScore)
    {
        var rules = await dataProvider.GetComplianceRulesAsync();
        
        var applicableRules = rules.Where(r => 
            r.ApplicableMeterTypes.Contains(meterType.ToString()) &&
            r.ApplicableFaultTypes.Contains(faultType.ToString()) &&
            r.SeverityThreshold <= severityScore
        ).ToList();

        return JsonSerializer.Serialize(new
        {
            MeterType = meterType.ToString(),
            FaultType = faultType.ToString(),
            SeverityScore = severityScore,
            ViolationsFound = applicableRules.Count > 0,
            ApplicableStandards = applicableRules.Select(r => r.Standard).Distinct().ToList(),
            Rules = applicableRules
        });
    }
}
