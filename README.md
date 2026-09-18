# IDV Document Enrichment Pipeline

AI-powered document classification, tagging, and indexing pipeline for SharePoint Online documents.

> ### 👉 New to this repository? Start with **[docs/HANDOVER.md](docs/HANDOVER.md)**
> It covers current state, which documents are stale, known issues, and open decisions.
>
> - **[Operations runbook](docs/runbook-operations.md)** — run locally, deploy, execute a batch
> - **[Configuration runbook](docs/runbook-configuration.md)** — change the taxonomy, fields, thresholds
> - **[Provisioning runbook](docs/runbook-provisioning.md)** — SharePoint schema setup, add a document type or content type

## Overview

Automated document enrichment pipeline that classifies, tags, and indexes active project documents inside SharePoint Online using:

- **Azure AI Document Intelligence** — text and key-value extraction from documents
- **Azure OpenAI (GPT-family)** — classification and tag generation against an approved taxonomy
- **Azure Functions (.NET 10 isolated worker) with Durable Task extension** — orchestration engine with retry, checkpointing, and fan-out/fan-in
- **Power Automate Premium** — event triggers (create/modify/move) and metadata write-back
- **SharePoint Online** — source documents and metadata column write-back target

## Architecture

⚠️ **These deep-dives predate the v4 taxonomy and the drawing vision path** (last substantively updated
mid-Aug 2026). The overall architecture is accurate; specifics are out of date. See
[HANDOVER.md §4](docs/HANDOVER.md) for what changed.

- [Pipeline Design](docs/architecture/pipeline-design.md) — end-to-end architecture, component interactions, data flow
- [Durable Functions Orchestration](docs/architecture/durable-functions-orchestration.md) — orchestrator design, retry policies, fan-out/fan-in
- [Prompt Engineering Strategy](docs/architecture/prompt-engineering-strategy.md) — classification prompts, structured output, taxonomy integration
- [Power Automate Integration](docs/architecture/power-automate-integration.md) — flow design, triggers, write-back patterns
- ~~[Human Review Queue](docs/architecture/human-review-queue.md)~~ — **SUPERSEDED by [ADR-006](docs/decisions/adr-006-inline-review.md)**. Describes a separate review list + Power Apps form that was never built; the implemented design is an inline filtered library view.

**Current configuration** lives in [docs/taxonomy/taxonomy.yaml](docs/taxonomy/taxonomy.yaml) (v4) —
document types, metadata fields, SharePoint column mappings, and confidence thresholds.

## Project Structure

```
idv-document-enrichment/
├── IdvEnrichment.sln
├── docs/
│   ├── HANDOVER.md            # START HERE — current state, stale docs, known issues
│   ├── runbook-operations.md  # run locally, deploy, execute a batch
│   ├── runbook-configuration.md # change taxonomy / fields / thresholds
│   ├── runbook-provisioning.md # SharePoint schema setup, add a document type or content type
│   ├── architecture/          # Deep-dive design docs (partially stale)
│   ├── decisions/             # ADRs 001-008
│   ├── taxonomy/              # taxonomy.yaml (v4) + client-facing docs
│   └── design/                # discovery / UX analyses (background)
├── infra/                     # Bicep IaC
│   ├── main.bicep
│   └── main.bicepparam
├── scripts/
│   ├── Grant-GraphPermissions.ps1
│   └── Provision-SharePointSchema.ps1  # creates SharePoint columns
├── src/
│   └── IdvEnrichment.Functions/       # .NET 10 Azure Functions isolated worker
│       ├── Program.cs · host.json
│       ├── local.settings.json        # gitignored
│       ├── Configuration/             # IOptions bindings
│       ├── Models/                    # Data contracts (C# records)
│       ├── Orchestrators/             # Batch → Chunk → Document
│       ├── Activities/                # Durable activities
│       ├── Triggers/                  # HTTP triggers (batch, enrich, test)
│       ├── Prompts/                   # Handlebars templates — EMBEDDED RESOURCES
│       └── Shared/                    # TaxonomyLoader, extractors, renderer
├── .github/workflows/         # empty — no CI/CD yet
├── .editorconfig
└── .gitignore
```

> **No test project exists.** An earlier version of this README described `tests/` with unit, integration,
> and evaluation sub-projects — none were built. Core logic in `TextUtils`, `SpreadsheetExtractor`,
> `MetadataSchemaBuilder`, and `RouteResultActivity` is pure and straightforward to test if you add one.
>
> **Prompts are embedded resources** — editing a `.hbs` requires `dotnet build` before it takes effect.

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
