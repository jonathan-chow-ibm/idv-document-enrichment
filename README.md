# IDV Document Enrichment Pipeline

AI-powered document classification, tagging, and indexing pipeline for SharePoint Online documents.

## Overview

Automated document enrichment pipeline that classifies, tags, and indexes active project documents inside SharePoint Online using:

- **Azure AI Document Intelligence** — text and key-value extraction from documents
- **Azure OpenAI (GPT-family)** — classification and tag generation against an approved taxonomy
- **Azure Functions (.NET 10 isolated worker) with Durable Task extension** — orchestration engine with retry, checkpointing, and fan-out/fan-in
- **Power Automate Premium** — event triggers (create/modify/move) and metadata write-back
- **SharePoint Online** — source documents and metadata column write-back target

## Architecture

See [docs/architecture/](docs/architecture/) for detailed architecture documentation:

- [Pipeline Design](docs/architecture/pipeline-design.md) — end-to-end architecture, component interactions, data flow
- [Durable Functions Orchestration](docs/architecture/durable-functions-orchestration.md) — orchestrator design, retry policies, fan-out/fan-in
- [Prompt Engineering Strategy](docs/architecture/prompt-engineering-strategy.md) — classification prompts, structured output, taxonomy integration
- [Power Automate Integration](docs/architecture/power-automate-integration.md) — flow design, triggers, write-back patterns
- [Human Review Queue](docs/architecture/human-review-queue.md) — review interface, correction capture, feedback loop

## Project Structure

```
idv-document-enrichment/
├── IdvEnrichment.sln
├── docs/
│   ├── architecture/          # Deep-dive architecture documents
│   └── decisions/             # ADRs
├── infra/                     # Bicep IaC
│   ├── main.bicep
│   └── main.bicepparam
├── src/
│   └── IdvEnrichment.Functions/       # .NET 10 Azure Functions isolated worker
│       ├── IdvEnrichment.Functions.csproj
│       ├── Program.cs
│       ├── host.json
│       ├── local.settings.json        # gitignored
│       ├── Configuration/             # IOptions bindings
│       ├── Models/                    # Data contracts (C# records)
│       ├── Orchestrators/             # Durable orchestrators
│       ├── Activities/                # Durable activities
│       ├── Prompts/                   # Handlebars templates (per document type)
│       └── Shared/                    # Shared services and utilities
├── tests/
│   ├── IdvEnrichment.UnitTests/        # xUnit unit tests
│   ├── IdvEnrichment.IntegrationTests/ # xUnit + Aspire integration tests
│   └── evaluation/                     # Python + Jupyter prompt evaluation harness
├── .github/workflows/
├── .editorconfig
└── .gitignore
```

## Getting Started

### Prerequisites

- .NET 10 SDK
- Azure Functions Core Tools v4
- Azure CLI
- An Azure subscription with:
  - Azure OpenAI Service (GPT-4o deployment)
  - Azure AI Document Intelligence (S0 tier)
  - Azure Functions (Consumption or Premium plan)
  - Azure Storage Account (for Durable Functions + queues)

### Local Development

```bash
# Restore and build
dotnet restore

# Configure local settings
cd src/IdvEnrichment.Functions
cp local.settings.example.json local.settings.json
# Edit local.settings.json with your Azure resource connection strings

# Run locally
func start
```

### Deploy Infrastructure

```bash
az login
az deployment group create \
  --resource-group rg-idv-enrichment \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam
```

## License

Proprietary — IBM Consulting engagement deliverable.
