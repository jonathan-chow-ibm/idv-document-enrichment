# ADR-002: Hybrid Write-Back Strategy (Power Automate + Graph API)

## Status

Superseded

## Superseded By

Batch mode only is in scope for this engagement. `WriteMetadataActivity` (Graph API via
`Microsoft.Graph`) handles batch write-back directly from Azure Functions. Trigger mode
(single-document, event-driven) and its Power Automate write-back path are deferred — both
remain the intended design for trigger mode but have not been implemented yet. The full hybrid
strategy from this ADR will apply once trigger mode is built.

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
