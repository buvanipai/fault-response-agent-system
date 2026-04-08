using FaultResponseSystem.Agents;
using FaultResponseSystem.Data;
using FaultResponseSystem.FaultDetection;
using FaultResponseSystem.Models;
using FaultResponseSystem.Orchestration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FaultResponseSystem.Web.Services;

/// <summary>
/// Singleton service that orchestrates DAG runs and maintains run history.
/// Blazor components subscribe to OnRunUpdated to get real-time UI refreshes.
/// </summary>
public class DagRunnerService
{
    private readonly IConfiguration _config;
    private readonly IDataProvider _dataProvider;
    private readonly ILogger<DagRunnerService> _logger;

    private readonly List<DagRun> _history = new();
    private DagRun? _activeRun;
    private List<Alert> _cachedAlerts = new();
    private CancellationTokenSource? _runCts;

    /// <summary>Fired whenever any run state changes. Subscribe in Blazor with InvokeAsync.</summary>
    public event Func<Task>? OnRunUpdated;

    // DAG node names in display order (mirrors the DAG topology)
    public static readonly string[] NodeOrder =
    [
        "DiagnosticAgent", "ContextAgent", "MaintenanceAgent",
        "RiskAgent",
        "ComplianceAgent", "ResolutionAgent",
        "ReportAgent"
    ];

    public DagRunnerService(IConfiguration config, IDataProvider dataProvider, ILogger<DagRunnerService> logger)
    {
        _config = config;
        _dataProvider = dataProvider;
        _logger = logger;
    }

    // ── Public Read Properties ────────────────────────────────────────────────

    public DagRun? ActiveRun => _activeRun;
    public IReadOnlyList<DagRun> History => _history.AsReadOnly();
    public IReadOnlyList<Alert> CachedAlerts => _cachedAlerts.AsReadOnly();
    public bool IsScanning { get; private set; }
    public bool IsRunning => _activeRun?.Status == DagRunStatus.Running;

    /// <summary>
    /// Total estimated cost at risk: heuristic for queued alerts + for every non-resolved run,
    /// the actual LLM cost if available, otherwise the heuristic (covers failed/partial runs).
    /// </summary>
    public double TotalValueAtRisk =>
        _cachedAlerts.Sum(a => AlertEstimator.Estimate(a).Cost)
        + _history.Where(r => !r.IsResolved)
                  .Sum(r => r.Report?.Risk != null
                      ? r.Report.Risk.EstimatedCostImpact
                      : AlertEstimator.Estimate(r.SelectedAlert).Cost);

    /// <summary>
    /// Total projected labor hours: heuristic for queued alerts + for every non-resolved run,
    /// the actual LLM hours if available, otherwise the heuristic.
    /// </summary>
    public double TotalLaborHours =>
        _cachedAlerts.Sum(a => AlertEstimator.Estimate(a).Hours)
        + _history.Where(r => !r.IsResolved)
                  .Sum(r => r.Report?.Resolution != null
                      ? r.Report.Resolution.EstimatedRepairHours
                      : AlertEstimator.Estimate(r.SelectedAlert).Hours);

    // ── Alert Scanning ────────────────────────────────────────────────────────

    public async Task ScanForAlertsAsync(double zScoreThreshold = 3.5)
    {
        if (IsScanning) return;
        IsScanning = true;
        await NotifyAsync();

        try
        {
            var buildings = await _dataProvider.GetBuildingsAsync();
            var readings  = await _dataProvider.GetAllMeterReadingsAsync();
            var detector  = new AnomalyDetector(zScoreThreshold: zScoreThreshold);
            _cachedAlerts = detector.ScanForAnomalies(buildings, readings).Take(10).ToList();
        }
        finally
        {
            IsScanning = false;
            await NotifyAsync();
        }
    }

    public async Task MarkResolvedAsync(string runId)
    {
        var run = _history.FirstOrDefault(r => r.RunId == runId);
        if (run != null)
        {
            run.IsResolved = true;
            await NotifyAsync();
        }
    }

    public async Task CancelRunAsync()
    {
        if (_runCts != null && !_runCts.IsCancellationRequested)
        {
            _logger.LogWarning("[DAG] Run cancelled by user.");
            _runCts.Cancel();
        }
        if (_activeRun != null && _activeRun.Status == DagRunStatus.Running)
        {
            _activeRun.Status = DagRunStatus.Failed;
            foreach (var name in NodeOrder.Where(n => _activeRun.NodeStatuses[n] == NodeStatus.Running || _activeRun.NodeStatuses[n] == NodeStatus.Pending))
                _activeRun.NodeErrors[name] = "Cancelled by user.";
            _activeRun.EndTime = DateTime.UtcNow;
            await NotifyAsync();
        }
    }

    // ── DAG Execution ─────────────────────────────────────────────────────────

    public async Task RunDagAsync(Alert alert)
    {
        if (IsRunning) return;

        _runCts = new CancellationTokenSource(TimeSpan.FromMinutes(3)); // Hard 3-min wall-clock timeout

        var run = new DagRun
        {
            SelectedAlert = alert,
            StartTime     = DateTime.UtcNow,
            Status        = DagRunStatus.Running
        };

        _logger.LogInformation("[DAG {RunId}] Starting run for alert {AlertId} ({FaultType} on {BuildingId})",
            run.RunId, alert.AlertId, alert.FaultType, alert.BuildingId);

        // Pre-populate all node statuses as Pending
        foreach (var name in NodeOrder)
        {
            run.NodeStatuses[name] = NodeStatus.Pending;
            run.NodeDurations[name] = TimeSpan.Zero;
            run.NodeTokens[name]   = 0;
        }

        _activeRun = run;
        _history.Insert(0, run);
        await NotifyAsync();

        try
        {
            // Build agents
            var diagAgent    = new AgentNode(new DiagnosticAgent(_config, _dataProvider));
            var contextAgent = new AgentNode(new ContextAgent(_config, _dataProvider));
            var maintAgent   = new AgentNode(new MaintenanceAgent(_config, _dataProvider));
            var riskAgent    = new AgentNode(new RiskAgent(_config));
            var compAgent    = new AgentNode(new ComplianceAgent(_config, _dataProvider));
            var resAgent     = new AgentNode(new ResolutionAgent(_config));
            var reportAgent  = new AgentNode(new ReportAgent(_config));

            // Build DAG topology
            riskAgent.AddDependency(diagAgent);
            riskAgent.AddDependency(contextAgent);
            riskAgent.AddDependency(maintAgent);

            compAgent.AddDependency(riskAgent);
            resAgent.AddDependency(riskAgent);

            compAgent.Condition = ctx =>
            {
                if (ctx.TryGetValue("RiskAssessment", out var r) && r is RiskAssessment risk)
                    return risk.SeverityScore >= 4;
                return true;
            };

            reportAgent.AddDependency(compAgent);
            reportAgent.AddDependency(resAgent);

            var dag = new DagExecutor();
            dag.AddNode(diagAgent);
            dag.AddNode(contextAgent);
            dag.AddNode(maintAgent);
            dag.AddNode(riskAgent);
            dag.AddNode(compAgent);
            dag.AddNode(resAgent);
            dag.AddNode(reportAgent);

            // Subscribe: update run state and notify blazor on every node change
            dag.OnProgress += async evt =>
            {
                run.NodeStatuses[evt.NodeId] = evt.Status;
                if (evt.Duration.HasValue)  run.NodeDurations[evt.NodeId] = evt.Duration.Value;
                if (evt.TokensUsed > 0)     run.NodeTokens[evt.NodeId]   = evt.TokensUsed;
                if (evt.ErrorMessage != null)
                {
                    run.NodeErrors[evt.NodeId] = evt.ErrorMessage;
                    _logger.LogError("[DAG {RunId}] {NodeId} failed: {Error}", run.RunId, evt.NodeId, evt.ErrorMessage);
                }
                _logger.LogInformation("[DAG {RunId}] {NodeId} → {Status} ({Tokens} tok)", run.RunId, evt.NodeId, evt.Status, evt.TokensUsed);
                try { await NotifyAsync(); } catch { /* circuit may be disposed */ }
            };

            var initialContext = new Dictionary<string, object> { { "Alert", alert } };
            var (finalContext, trace) = await dag.ExecuteAsync(initialContext, _runCts.Token);

            run.Trace = trace;

            // Stitch sub-agent results — build a report even from partial/failed runs
            FaultReport? report = null;
            if (finalContext.TryGetValue("FaultReport", out var reportObj) && reportObj is FaultReport fr)
                report = fr;
            else
                report = new FaultReport { ReportId = run.RunId, OriginalAlert = alert };

            if (finalContext.TryGetValue("DiagnosticResult",  out var dObj)  && dObj  is DiagnosticResult  dr)  { report.Diagnostics  = dr;  run.NodeResults["DiagnosticAgent"]  = dr;  }
            if (finalContext.TryGetValue("ContextResult",     out var cObj)  && cObj  is ContextResult     cr)  { report.Context      = cr;  run.NodeResults["ContextAgent"]     = cr;  }
            if (finalContext.TryGetValue("MaintenanceResult", out var mObj)  && mObj  is MaintenanceResult  mr)  { report.Maintenance  = mr;  run.NodeResults["MaintenanceAgent"] = mr;  }
            if (finalContext.TryGetValue("RiskAssessment",    out var rObj)  && rObj  is RiskAssessment     ra)  { report.Risk         = ra;  run.NodeResults["RiskAgent"]        = ra;  }
            if (finalContext.TryGetValue("ComplianceResult",  out var coObj) && coObj is ComplianceResult   comp){ report.Compliance   = comp; run.NodeResults["ComplianceAgent"] = comp; }
            if (finalContext.TryGetValue("ResolutionPlan",    out var resObj)&& resObj is ResolutionPlan    rp)  { report.Resolution  = rp;   run.NodeResults["ResolutionAgent"] = rp;   }

            // Only attach the report if at least one agent produced data
            if (report.Diagnostics != null || report.Risk != null || report.Resolution != null
                || !string.IsNullOrEmpty(report.ExecutiveSummary))
                run.Report = report;

            run.Status = trace.Steps.Any(s => s.Status == NodeStatus.Failed)
                            ? DagRunStatus.Failed
                            : DagRunStatus.Completed;

            _logger.LogInformation("[DAG {RunId}] Finished with status {Status} in {Duration:F1}s",
                run.RunId, run.Status, run.Duration.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            run.Status = DagRunStatus.Failed;
            _logger.LogWarning("[DAG {RunId}] Run timed out or was cancelled.", run.RunId);
        }
        catch (Exception ex)
        {
            run.Status = DagRunStatus.Failed;
            _logger.LogError(ex, "[DAG {RunId}] Unhandled exception during run.", run.RunId);
            foreach (var name in NodeOrder.Where(n => run.NodeStatuses[n] == NodeStatus.Running))
                run.NodeErrors[name] = ex.Message;
        }
        finally
        {
            run.EndTime = DateTime.UtcNow;
            _runCts?.Dispose();
            _runCts = null;
            _cachedAlerts.RemoveAll(a => a.AlertId == alert.AlertId);
            try { await NotifyAsync(); } catch { /* circuit may be disposed */ }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task NotifyAsync()
    {
        if (OnRunUpdated != null)
            await OnRunUpdated.Invoke();
    }
}
