# ADR-008: Model Selection Strategy — GPT-4.1 Family for Batch Pipeline

## Status

Proposed (pending new Azure subscription with full quota)

## Date

2026-08-24

## Context

The pipeline currently runs both agents on `gpt-4o` (Agent 2, extraction) and `gpt-4.1-mini`
(Agent 1, classification) in the client's M365-linked subscription, which has constrained
Azure OpenAI quotas. A dedicated Azure subscription is being provisioned to give proper quota
allocation and model availability.

Key observations from testing ~20 real documents across 5 document types:
- Agent 1 (classification) is a trivial task — "pick 1 of 23 types" from 4K chars of text.
  Any capable model achieves near-perfect accuracy. Cost and speed matter more than capability.
- Agent 2 (extraction) benefits from strong instruction following and structured output
  compliance. The 128K context window on gpt-4o forced a 24K truncation strategy with
  TOC-skip heuristics for long documents (388K-char construction contracts).
- Design drawings need visual understanding that text extraction cannot provide. A multimodal
  model looking at page images can identify discipline, sheet number, and drawing type.
- GPT-5 series models default to reasoning mode, consuming extra tokens on "thinking" that
  classification and extraction do not need. Cost is 3-6× higher with no quality benefit for
  this workload.

## Decision

Target the **GPT-4.1 model family** across the pipeline when the new subscription is available:

| Role | Model | SKU | Rationale |
|------|-------|-----|-----------|
| Agent 1 (Classification) | gpt-4.1-mini | GlobalStandard | Cheapest model that supports structured outputs. Classification is a trivial task. |
| Agent 2 (Extraction) | gpt-4.1 | GlobalStandard | Better instruction following than gpt-4o. 1M context window relaxes truncation pressure. ~27% cheaper input tokens. |
| Drawing Vision | gpt-4.1 (reuse Agent 2 deployment) | — | Multimodal — accepts page images. No extra deployment needed. |
| Doc Intelligence | S0 | Regional | Purpose-built for OCR. Keep as-is. |

### Truncation strategy with gpt-4.1

The 1M context window does **not** mean "send everything." Raise the extraction budget from
24K to ~48-64K chars, but keep the smart truncation logic (TOC skip, head+tail) for documents
that exceed it. Reasons:
- Cost: 97K tokens at $2.00/1M = $0.19/doc vs $0.10 for 48K. At scale this adds up.
- Precision: focused content slices extract better than unfocused full-document dumps.
- The truncation heuristics (TOC detection, preamble preservation) add value beyond fitting
  in a window — they select the *right* content.

### GPT-5 for hard cases (deferred)

GPT-5 reasoning models are not used in the batch pipeline. However, they are the right tool
for a **post-batch re-processing pass** on the hardest documents (dense pro formas, complex
multi-party agreements) where the review queue rate is unacceptable. This is a data-driven
decision: run the batch with gpt-4.1, measure the review queue, then selectively re-process
low-confidence documents with gpt-5 if needed. Do not deploy speculatively.

## Alternatives Considered

### Stay on gpt-4o for Agent 2
- Working and tested
- Rejected: gpt-4.1 is cheaper, has better structured output compliance, and the 1M context
  window removes truncation as a failure mode

### GPT-5 for all agents
- Maximum capability
- Rejected: 3-6× cost increase, reasoning token overhead on non-reasoning tasks, no quality
  benefit for classification or structured extraction

### GPT-4.1-nano for Agent 1
- Cheapest option (~$0.10/1M input)
- Deferred: quota unavailable in current setup. Revisit if nano becomes available in the new
  subscription — it would be the optimal Agent 1 model.

## Consequences

- Both deployments on GlobalStandard SKU for highest throughput ceiling and lowest price
- Single model family simplifies prompt testing (consistent behavior across mini and full)
- Truncation budget raised but not removed — smart truncation remains valuable
- Drawing vision enrichment reuses the Agent 2 deployment (no infrastructure change)
- GPT-5 is reserved for post-batch optimization, not day-one deployment
- Contingent on new subscription having Azure OpenAI access and sufficient quota for
  gpt-4.1 + gpt-4.1-mini GlobalStandard deployments
