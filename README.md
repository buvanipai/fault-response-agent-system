# Smart Building Fault Response Agent System

A multi-agent DAG (Directed Acyclic Graph) system built in .NET 10, designed to autonomously diagnose, contextualize, assess, and recommend resolutions for building sensor anomalies using real data from the Building Data Genome Project 2 (BDG2).

## Architecture

The system bridges raw statistical anomaly detection with an intelligent multi-agent orchestration layer, powered by Azure AI Foundry via GitHub Models inference:

1. **Anomaly Preprocessor:** Scans real-world BDG2 sensor time-series to detect statistical anomalies (spikes, drift) and generates discrete Alert events.
2. **Parallel Context Agents (Tier 1):**
    - `DiagnosticAgent`: Queries historical sensor data and building metadata to classify the physical fault.
    - `ContextAgent`: Checks historical weather data and estimates occupancy.
    - `MaintenanceAgent`: Reviews past CMMS work orders and checks warehouse part availability.
3. **Assessment Layer (Tier 2):**
    - `RiskAgent`: Synthesizes Tier 1 outputs to calculate a 1-10 severity score and estimate financial/productivity impact.
4. **Conditional Action Layer (Tier 3):**
    - `ComplianceAgent`: Validates the fault against mock ASHRAE/OSHA rules. *Conditionally skips if Risk Score < 4.*
    - `ResolutionAgent`: Generates a step-by-step fix and constructs a standardized CMMS work order draft.
5. **Reporting Layer (Tier 4):**
    - `ReportAgent`: Aggregates the entire execution thread into an Executive Summary.

## Tech Stack
- **Language/Platform:** C# / .NET 10.0
- **UI Framework:** Blazor Server (Interactive Server-Side rendering)
- **AI Framework:** `Azure.AI.OpenAI` SDK (Targeting GitHub Models endpoint)
- **Data Source:** Subsets from [buds-lab/building-data-genome-project-2](https://github.com/buds-lab/building-data-genome-project-2)

## Setup & Execution

### 1. Verification of Requirements
This workspace requires the **.NET 10.0 SDK**. Ensure you have .NET 10 installed before building.

### 2. Configure Inference
Your credentials are intentionally bound to GitHub Models' inference endpoints within `src/FaultResponseSystem.Web/appsettings.json` for free, robust development testing using `gpt-4o-mini`:
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://models.inference.ai.azure.com",
    "DeploymentName": "gpt-4o-mini",
    "ApiKey": "YOUR_GITHUB_TOKEN"
  },
  "DataPath": "Data/SampleData"
}
```

### 3. Build and Run the Dashboard
The console-based orchestrator has been wrapped in an observable, premium UI.

```bash
cd src/FaultResponseSystem.Web
dotnet build
dotnet run
```

### 4. Viewing The Dashboard
- After the server launches, open your browser and navigate to the localhost port indicated in the terminal (typically `http://localhost:5144` or `https://localhost:7144`).
- **Initiate:** Click "Scan for Anomalies" to parse the time-series logs.
- **Execute DAG:** Select an anomalous alert and watch the multi-agent DAG execute asynchronously across the interface.
- **Review:** Read the unified action report, risk matrix, and estimated downtime calculations instantly.
