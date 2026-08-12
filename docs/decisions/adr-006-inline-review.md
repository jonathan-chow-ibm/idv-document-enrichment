# ADR-006: Inline Review via Filtered Library View (No Separate Review Queue)

## Status

Accepted

## Date

2026-08-12

## Context

The original architecture (documented in human-review-queue.md) specified a separate SharePoint list ("AI Classification Review") as a human review queue, with:
- A 24-column schema for proposed tags, confidence scores, reasoning
- A Power Apps form for side-by-side review
- A Power Automate Flow 3 to write approved/corrected tags back to the original document
- A "Corrections Log" list for prompt tuning feedback

This introduced significant complexity:
- Two data stores for the same document's metadata (library columns + review list item)
- A Power Apps form (~9h implementation, ongoing maintenance)
- An approval flow that syncs data between the review list and the document
- SMEs forced into a separate app outside their normal SharePoint workflow

Meanwhile, the client's SMEs work in SharePoint document libraries all day. They don't want another app.

## Decision

Eliminate the separate review queue list and approval flow. Low-confidence documents receive metadata written directly to their columns (same as high-confidence docs) with `AIProcessingStatus = "Under Review"`. SMEs review via a **filtered SharePoint library view** and edit columns inline.

Corrections for prompt tuning are captured by diffing current column values against the `AIOriginalClassification` column (JSON snapshot of the AI's original output, written at classification time).

## Rationale

- **SMEs stay in SharePoint** — no app-switching, no Power Apps adoption risk
- **No data sync problem** — metadata lives in one place (document columns), not two
- **Eliminates 3 components** — review list, Power Apps form, Flow 3 (~14h implementation saved)
- **Corrections are captured without real-time flows** — batch analysis during prompt tuning iterations
- **Simpler permissions** — no separate list to create or secure

## Alternatives Considered

### Separate SharePoint list (previous approach)
- Rich review UX with side-by-side view
- Rejected: adoption risk, sync complexity, 14h of unnecessary implementation within a 4-week timeline

### Teams Adaptive Cards for micro-review
- Low-friction 10-second reviews embedded in chat
- Deferred: good for steady-state trickle post-engagement; overkill for initial batch

## Consequences

- human-review-queue.md is superseded (retained as historical reference)
- Power Automate integration reduced from 3 flows to 2 (trigger + write-back only)
- `Provision-SharePointSchema.ps1` no longer creates the review list or corrections log list
- `AIOriginalClassification` column added to store the AI's original output for correction diffing
- Prompt tuning corrections are detected via `tests/evaluation/analyze_corrections.py` (batch query, not real-time)
- No Power Apps form needed
- tasks.md Phase 4 reduced from ~24h to ~13.5h
