# ADR-005: Switch to .NET Isolated Worker for Azure Functions

## Status

Accepted (supersedes ADR-001)

## Date

2026-08-10

## Context

ADR-001 selected Python + Azure Functions v2 + Durable Functions primarily for AI ecosystem familiarity and prompt engineering iteration speed. Since then, two facts have changed the calculation:

1. **Coding agents write most of the implementation.** Team familiarity with the language matters less when specs are translated to code by agents (Copilot Coding Agent, etc.). Both platforms produce idiomatic code equally well.
2. **The client is a Microsoft-shop CRE firm** using SharePoint, Power Platform, and Azure. Their internal dev/ops team is .NET-centric. A Python codebase would be foreign to them at handover.

Additional considerations:

- Durable Functions .NET isolated worker is significantly more mature than Python v2 Durable Functions
- Python Azure Functions cold starts are 2-5 seconds slower than .NET on Consumption plan
- Azure.AI.OpenAI 2.x supports first-class C# record deserialization for Structured Outputs
- Microsoft.Graph .NET SDK is the reference implementation for SharePoint/Graph API access
- Bicep infra, architecture docs, and Pydantic models are trivially portable to .NET (est. 4-6 hours agent-assisted migration)

## Decision

Use **.NET 10 with the Azure Functions isolated worker model** for the pipeline. Retain Python **only** for the prompt evaluation framework (Jupyter notebooks, tests/evaluation/) where the AI ecosystem is significantly stronger.

.NET 10 is the current LTS release (November 2025, supported through November 2028). .NET 8's LTS window ends November 2026 — too close to the client's early operations period to be a safe target. .NET 9 is already out of support (May 2026).

## Rationale

- **Client handover fit** — .NET aligns with the client's existing engineering practices; day-2 operations team can maintain and extend the pipeline
- **Durable Functions maturity** — battle-tested orchestrator/activity patterns, entities, critical sections, better local debugging
- **Faster cold starts** — trigger-mode SLA is easier to meet
- **Structured Outputs ergonomics** — Azure.AI.OpenAI 2.x deserializes directly into C# records; no manual Pydantic mapping ceremony
- **Managed Identity + Graph SDK** — Microsoft.Graph .NET SDK has richer SharePoint operations than msgraph-sdk (Python)
- **Migration cost is low** — very little code exists yet; Bicep and architecture docs are already language-agnostic where it matters

## Alternatives Considered

### Continue with Python (ADR-001)
- Rejected: client handover cost outweighs prompt-iteration convenience, especially since prompt tuning uses Jupyter notebooks in Python either way

### Node.js / TypeScript
- Rejected: no strong reason to introduce a third language; client is not a Node shop

### Hybrid — .NET for pipeline, Python for evaluation only
- **Adopted.** Best of both worlds: enterprise-fit runtime with data-science-friendly prompt tuning tooling

## Consequences

- Superseeds ADR-001 (which is now marked Superseded)
- Existing Python scaffolding (`src/functions/*.py`, `pyproject.toml`, `requirements.txt`) is deleted
- New project structure: `src/IdvEnrichment.Functions/` (.NET 10 isolated worker)
- Pydantic models → C# records with System.Text.Json attributes
- `pydantic-settings` → `IOptions<PipelineSettings>` bound from configuration
- Jinja2 templates → Handlebars.Net (`Handlebars.Net` NuGet package; chosen over Scriban, which had unresolved security advisories at migration time)
- Bicep `linuxFxVersion: PYTHON|3.11` → `DOTNET-ISOLATED|10.0`
- CI/CD workflows updated for `dotnet` toolchain instead of Python
- Prompt evaluation harness stays in Python (`tests/evaluation/` — Jupyter + pandas)
- Architecture doc code samples updated from Python to C#
- Team must be comfortable with C# 14 records, source generators, and Azure Functions isolated worker patterns (agents handle the ceremony)
