using Spectre.Console;
using System.Text.Json;
using FaultResponseSystem.Agents;
using FaultResponseSystem.Data;
using FaultResponseSystem.FaultDetection;
using FaultResponseSystem.Models;
using FaultResponseSystem.Orchestration;
using Microsoft.Extensions.Configuration;

namespace FaultResponseSystem;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        AnsiConsole.Write(new FigletText("Smart Fault DAG").Color(Color.Blue));
        AnsiConsole.MarkupLine("[grey]Multi-Agent Fault Response System powered by Azure AI Foundry[/]");
        AnsiConsole.WriteLine();

        // 1. Setup Configuration
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        if (config["AzureOpenAI:ApiKey"] == "YOUR-API-KEY-HERE")
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Please set your AzureOpenAI:ApiKey and Endpoint in appsettings.json");
            return;
        }

        // 2. Initialize Data Provider
        IDataProvider dataProvider = new JsonDataProvider(config);

        // 3. Run Anomaly Detection (Preprocessing)
        var buildings = await dataProvider.GetBuildingsAsync();
        var readings = await dataProvider.GetAllMeterReadingsAsync();

        AnsiConsole.MarkupLine("[bold cyan]1. Running Statistical Anomaly Detection...[/]");
        var detector = new AnomalyDetector(zScoreThreshold: 3.5);
        var alerts = detector.ScanForAnomalies(buildings, readings);

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Alert ID");
        table.AddColumn("Building");
        table.AddColumn("Meter");
        table.AddColumn("Time");
        table.AddColumn("Fault");
        table.AddColumn("Z-Score");

        foreach (var a in alerts.Take(5))
        {
            table.AddRow(
                $"[yellow]{a.AlertId}[/]",
                a.BuildingId,
                a.MeterType.ToString(),
                a.Timestamp.ToString("g"),
                a.FaultType.ToString(),
                a.DeviationScore.ToString("F1")
            );
        }
        AnsiConsole.Write(table);

        if (!alerts.Any())
        {
            AnsiConsole.MarkupLine("[red]No anomalies detected in the sample dataset.[/]");
            return;
        }

        // 4. Select an alert to process
        var alertOptions = alerts.Take(5).ToDictionary(a => a.AlertId, a => a);
        var selectedAlertId = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select an alert to process through the Agent DAG:")
                .PageSize(5)
                .AddChoices(alertOptions.Keys)
        );

        var targetAlert = alertOptions[selectedAlertId];
        AnsiConsole.MarkupLine($"\n[bold green]Selected Alert:[/] {targetAlert.Description}\n");

        // 5. Initialize Agents and DAG
        AnsiConsole.MarkupLine("[bold cyan]2. Initializing Agent DAG...[/]");

        var diagAgent    = new AgentNode(new DiagnosticAgent(config, dataProvider));
        var contextAgent = new AgentNode(new ContextAgent(config, dataProvider));
        var maintAgent   = new AgentNode(new MaintenanceAgent(config, dataProvider));
        var riskAgent    = new AgentNode(new RiskAgent(config));
        var compAgent    = new AgentNode(new ComplianceAgent(config, dataProvider));
        var resAgent     = new AgentNode(new ResolutionAgent(config));
        var reportAgent  = new AgentNode(new ReportAgent(config));

        // DAG Edges
        riskAgent.AddDependency(diagAgent);
        riskAgent.AddDependency(contextAgent);
        riskAgent.AddDependency(maintAgent);

        compAgent.AddDependency(riskAgent);
        resAgent.AddDependency(riskAgent);

        compAgent.Condition = ctx =>
        {
            if (ctx.TryGetValue("RiskAssessment", out var riskObj) && riskObj is RiskAssessment risk)
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

        // Subscribe to progress events for Spectre console output
        dag.OnProgress += evt =>
        {
            var statusMarkup = evt.Status switch
            {
                NodeStatus.Running   => $"[yellow]▶ Starting [bold]{evt.NodeId}[/][/]",
                NodeStatus.Completed => $"[green]✔ Completed [bold]{evt.NodeId}[/] in {evt.Duration?.TotalSeconds:F1}s ({evt.TokensUsed} tokens)[/]",
                NodeStatus.Skipped   => $"[grey]⏭ Skipped {evt.NodeId} due to condition.[/]",
                NodeStatus.Failed    => $"[red]✖ Failed [bold]{evt.NodeId}[/]: {evt.ErrorMessage}[/]",
                _                   => null
            };
            if (statusMarkup != null)
                AnsiConsole.MarkupLine(statusMarkup);
        };

        var initialContext = new Dictionary<string, object> { { "Alert", targetAlert } };

        // 6. Execute DAG
        AnsiConsole.MarkupLine("[bold cyan]3. Executing Agent DAG...[/]\n");
        var (finalContext, trace) = await dag.ExecuteAsync(initialContext);

        // 7. Output trace
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold cyan]4. Execution Trace[/]");
        var traceTable = new Table().Border(TableBorder.SimpleHeavy);
        traceTable.AddColumn("Agent");
        traceTable.AddColumn("Status");
        traceTable.AddColumn("Duration (s)");
        traceTable.AddColumn("Tokens");

        foreach (var step in trace.Steps)
        {
            var statusStr = step.Status switch
            {
                NodeStatus.Completed => "[green]Completed[/]",
                NodeStatus.Skipped   => "[grey]Skipped[/]",
                NodeStatus.Failed    => "[red]Failed[/]",
                _                   => step.Status.ToString()
            };
            traceTable.AddRow(step.AgentName, statusStr, step.Duration.TotalSeconds.ToString("F1"), step.TokensUsed.ToString());
        }
        AnsiConsole.Write(traceTable);
        AnsiConsole.MarkupLine($"[bold]Total Execution Time:[/] {trace.TotalDuration.TotalSeconds:F1}s\n");

        if (finalContext.TryGetValue("FaultReport", out var reportObj) && reportObj is FaultReport report)
        {
            AnsiConsole.MarkupLine("[bold cyan]5. Final Executive Report[/]");
            var panel = new Panel(
                new Markup($"[bold]Executive Summary:[/] {report.ExecutiveSummary}\n\n[bold]Recommended Action:[/] {report.RecommendedAction}")
            )
            {
                Header  = new PanelHeader($" Fault Report: {targetAlert.AlertId} "),
                Border  = BoxBorder.Rounded,
                Padding = new Padding(1, 1, 1, 1)
            };
            AnsiConsole.Write(panel);
        }
        else
        {
            AnsiConsole.MarkupLine("[red]ReportAgent did not generate a final report or DAG failed.[/]");
        }
    }
}
