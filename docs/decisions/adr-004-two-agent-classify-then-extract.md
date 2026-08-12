# ADR-004: Two-Agent Architecture — Classify Type Then Extract Metadata

## Status

Accepted

## Date

2026-07-30

## Context

The initial pipeline design used a single LLM call to classify all 5 metadata categories simultaneously (deal type, submarket, counterparty, document classification, confidentiality). This flat approach has three limitations:

1. **One prompt does two jobs** — document type classification and metadata extraction are distinct tasks with different accuracy characteristics
2. **Fixed schema for all documents** — a Lease Agreement and a Market Report get the same 5 fields, missing type-specific metadata (rental rates, cap rates, vacancy data)
3. **No discovery** — the system can only assign values from a predefined taxonomy; it cannot surface metadata the taxonomy designers didn't anticipate

## Decision

Use a **two-agent pipeline**:

- **Agent 1 (Classifier):** Determines the document type. Focused, single-question task. Can use a cheaper model (GPT-4o-mini or embeddings).
- **Agent 2 (Extractor):** Given the document type from Agent 1, extracts type-specific metadata fields using a schema tailored to that type. Also suggests additional metadata fields discovered in the document.

## Rationale

- **Higher classification accuracy** — Agent 1 answers one question instead of five, reducing prompt complexity and confusion
- **Richer metadata** — a Lease Agreement yields tenant, landlord, term, rental rate; an Offer Memorandum yields asking price, cap rate, NOI. The current 5-field flat schema captures none of this
- **Cost-neutral with upside** — Agent 1 uses GPT-4o-mini (~$0.001/doc), adding negligible cost. Agent 2 uses GPT-4o with Structured Outputs at similar token cost to the previous single-prompt approach. Net cost is comparable, but future migration of Agent 2 to GPT-4o-mini (after prompt stabilization) could reduce costs 10-20×
- **Discovery capability** — Agent 2's `suggested_fields` output surfaces metadata the taxonomy didn't define, informing taxonomy evolution
- **Independent tuning** — classification prompts and extraction prompts can be tuned independently with different evaluation criteria

## Alternatives Considered

### Single-prompt classification (previous approach)
- Simpler prompt management
- Rejected: insufficient metadata richness, accuracy ceiling from multi-task prompt

### Custom trained classifier + LLM extraction
- Higher classification accuracy ceiling
- Rejected: requires 500+ labeled samples per type, weeks of labeling effort, retraining for taxonomy changes

## Consequences

- Taxonomy YAML becomes hierarchical: document types → per-type field definitions
- Two prompt templates per document type (classification + extraction) instead of one universal prompt
- Durable Functions orchestrator adds a branching step after Agent 1
- C# record models support polymorphic type-specific fields via `IReadOnlyDictionary<string, CategoryClassification>`
- Evaluation framework splits into classification accuracy and extraction accuracy
- Review queue shows type-specific fields, not a fixed 5-column form
- Two LLM calls per document instead of one — net cost is roughly equivalent to the single-prompt approach; the value is richer metadata, not cost savings
