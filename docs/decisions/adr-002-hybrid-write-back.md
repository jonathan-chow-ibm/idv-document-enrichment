# ADR-002: Hybrid Write-Back Strategy (Power Automate + Graph API)

## Status

Superseded

## Superseded By

The Power Automate trigger-mode path was not implemented. `WriteMetadataActivity` (Graph API
via `Microsoft.Graph`) handles write-back for both trigger and batch modes. The action-limit
concern only applied to batch scale; single-document trigger mode never approached that limit.
The hybrid complexity was eliminated in favour of a single write-back path.

## Date

2026-07-29

## Context

The pipeline needs to write classification metadata back to SharePoint document columns. Two modes exist:

1. **Trigger mode** — single document at a time, event-driven
2. **Batch mode** — 20,000-30,000 documents in a production run

Power Automate Premium has a 100,000 actions/day limit. With ~5 actions per document write-back, batch mode with 25K docs would require 125K actions — exceeding the limit.

## Decision

Use a **hybrid approach**:
- **Trigger mode:** Power Automate for write-back (simple, auditable, within action limits)
- **Batch mode:** Direct Microsoft Graph API calls from Azure Functions (no PA limits, faster)

## Consequences

- Two write-back code paths to maintain and test
- Azure Functions need Graph API application permissions (Sites.Selected) for batch mode
- Power Automate flows remain simpler (only handle single-document scenarios)
- Batch mode is self-contained — no dependency on Power Automate availability or licensing
