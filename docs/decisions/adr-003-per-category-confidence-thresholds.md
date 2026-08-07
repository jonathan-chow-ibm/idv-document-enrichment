# ADR-003: Per-Category Confidence Thresholds

## Status

Accepted

## Date

2026-07-29

## Context

The pipeline must decide whether to auto-apply a classification or route it to human review. A single global confidence threshold (e.g., 0.80) treats all categories equally, but:

- "Confidentiality" misclassification has compliance risk → needs higher certainty
- "Counterparty" extraction is typically high-confidence (letterhead/signature block)
- "Submarket" is often ambiguous for multi-area documents

## Decision

Use **per-category confidence thresholds** defined in the taxonomy configuration.

Initial thresholds:
| Category | Threshold | Rationale |
|----------|-----------|-----------|
| Deal Type | 0.75 | Moderate ambiguity |
| Submarket | 0.75 | Geographic references can be ambiguous |
| Counterparty | 0.70 | Usually clear from document |
| Document Classification | 0.80 | Format vs. content confusion exists |
| Confidentiality | 0.85 | Compliance risk — err conservative |

A document is routed to review if **any** category falls below its threshold.

## Consequences

- More tuning parameters to manage (5 thresholds vs. 1)
- Thresholds must be calibrated during pilot batch (Phase 5)
- Review queue volume directly controlled by threshold settings
- Taxonomy YAML must include threshold configuration per category
