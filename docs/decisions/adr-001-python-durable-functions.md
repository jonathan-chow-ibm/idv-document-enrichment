# ADR-001: Python Azure Functions with Durable Functions for Pipeline Orchestration

## Status

Accepted

## Date

2026-07-29

## Context

The document enrichment pipeline requires:
- Reliable orchestration of multi-step AI processing (extract → classify → route)
- Fan-out/fan-in for batch processing of 20,000-30,000 documents
- Retry, checkpointing, and dead-letter queue semantics
- Integration with Azure AI Document Intelligence, Azure OpenAI, SharePoint Graph API

We need to choose the implementation language and orchestration framework.

## Decision

Use **Python** with **Azure Functions v2** and **Durable Functions SDK** for pipeline orchestration.

## Rationale

- Python is the lingua franca of AI/ML tooling; the OpenAI Python SDK is the reference implementation
- Faster prompt engineering iteration (edit-and-run, notebook prototyping)
- Pydantic provides strong typing and validation equivalent to C# DTOs
- Durable Functions Python v2 SDK supports all required patterns (fan-out/fan-in, sub-orchestrations, retries, timers)
- Existing team familiarity with Python

## Alternatives Considered

### C# / .NET Azure Functions
- Most mature Durable Functions SDK
- Strong typing advantages
- Rejected: slower iteration cycle for prompt engineering, smaller AI ecosystem

### Power Automate as primary orchestrator
- No custom code needed
- Rejected: 100K actions/day limit, no unit testing, limited error handling, slow for pipeline core

## Consequences

- Team must learn Durable Functions Python SDK patterns (replay-safe orchestrator code)
- Must use `pydantic-settings` for configuration management (no built-in DI like .NET)
- Deployment via Azure Functions Core Tools or GitHub Actions (not Visual Studio)
