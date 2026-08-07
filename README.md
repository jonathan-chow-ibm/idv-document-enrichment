# IDV Document Enrichment Pipeline

AI-powered document classification, tagging, and indexing pipeline for SharePoint Online documents.

## Overview

Automated document enrichment pipeline that classifies, tags, and indexes active project documents inside SharePoint Online using:

- **Azure AI Document Intelligence** — text and key-value extraction from documents
- **Azure OpenAI (GPT-family)** — classification and tag generation against an approved taxonomy
- **Azure Functions (Durable)** — orchestration engine with retry, checkpointing, and fan-out/fan-in
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
├── docs/
│   ├── architecture/          # Deep-dive architecture documents
│   ├── decisions/             # Architecture Decision Records (ADRs)
│   ├── runbooks/              # Operational runbooks
│   └── taxonomy/              # Taxonomy specification and examples
├── infra/                     # Bicep IaC for Azure resources
│   ├── main.bicep
│   ├── main.bicepparam
│   └── modules/
├── src/
│   └── functions/             # Azure Functions (Python v2)
│       ├── function_app.py
│       ├── orchestrators/     # Durable Functions orchestrators
│       ├── activities/        # Durable Functions activities
│       ├── models/            # Pydantic models for taxonomy, metadata
│       ├── prompts/           # Prompt templates (Jinja2 or plain text)
│       └── shared/            # Shared utilities, clients, config
├── tests/
│   ├── unit/                  # Unit tests
│   ├── integration/           # Integration tests
│   └── evaluation/            # Prompt evaluation / accuracy tests
├── scripts/                   # Utility scripts (batch kick-off, reporting)
├── .github/
│   └── workflows/             # CI/CD pipelines
├── host.json                  # Azure Functions host configuration
├── local.settings.json        # Local development settings (gitignored)
├── requirements.txt           # Python dependencies
└── pyproject.toml             # Project configuration
```

## Getting Started

### Prerequisites

- Python 3.11+
- Azure Functions Core Tools v4
- Azure CLI
- An Azure subscription with:
  - Azure OpenAI Service (GPT-4o deployment)
  - Azure AI Document Intelligence (S0 tier)
  - Azure Functions (Consumption or Premium plan)
  - Azure Storage Account (for Durable Functions + queues)

### Local Development

```bash
# Clone and setup
cd idv-document-enrichment
python -m venv .venv
.venv\Scripts\activate       # Windows
pip install -r requirements.txt

# Configure local settings
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
