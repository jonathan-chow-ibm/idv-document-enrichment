# Tasks — IDV Document Enrichment Pipeline

## Overview

Implementation tasks organized into 6 phases, mapped to SOW Workstream 2 deliverables.
Each task has an estimated effort and dependencies.

**Architecture:** Two-agent pipeline:
- **Agent 1 (Classifier):** GPT-4o-mini — determines document type (cheap, fast)
- **Agent 2 (Extractor):** GPT-4o with Structured Outputs — extracts common metadata + type-specific fields + suggests additional fields (different prompt per document type)

**Legend:**
- ⬜ Not started
- 🔄 In progress
- ✅ Complete
- 🚫 Blocked
- `[P]` = Parallelizable with other tasks in the same phase

---

## Phase 0: Project Setup & Infrastructure (Week 1)

> **Goal:** Dev environment, Azure resources, CI/CD pipeline, repo structure.
> **Exit criteria:** All team members can run Azure Functions locally, infrastructure deployed to dev environment.

| # | Task | Est. | Deps | Status |
|---|------|------|------|--------|
| 0.1 | Initialize Python Azure Functions v2 project with Durable Functions SDK | 2h | — | ⬜ |
| 0.2 | Create `pyproject.toml`, `requirements.txt`, dev dependencies (pytest, ruff, mypy) | 1h | 0.1 | ⬜ |
| 0.3 | Create Pydantic models for pipeline data contracts — `DocumentType` enum, `TypeClassificationResult`, `MetadataExtractionResult`, `SuggestedField`, `EnrichmentResult`, `QueueMessage`, `RoutingDecision` (see `models/pipeline.py`) | 3h | 0.1 | ✅ |
| 0.4 | Write Bicep modules: Resource Group, Storage Account, Function App (Consumption), App Insights, Key Vault | 4h | — | ⬜ `[P]` |
| 0.5 | Write Bicep modules: Azure AI Document Intelligence (S0), Azure OpenAI Service + GPT-4o deployment + GPT-4o-mini deployment | 4h | — | ⬜ `[P]` |
| 0.6 | Write `main.bicep` + `main.bicepparam` composing all modules, Managed Identity + RBAC assignments | 2h | 0.4, 0.5 | ⬜ |
| 0.7 | Create GitHub Actions CI workflow: lint (ruff), type check (mypy), unit tests (pytest), Bicep validate | 3h | 0.1, 0.4 | ⬜ |
| 0.8 | Create GitHub Actions CD workflow: deploy Bicep + deploy Function App (dev environment) | 2h | 0.7 | ⬜ |
| 0.9 | Create `local.settings.example.json` with all required config keys documented | 1h | 0.1 | ⬜ |
| 0.10 | Set up shared config module (`shared/config.py`) using `pydantic-settings` for env-based configuration | 2h | 0.3 | ⬜ |

**Phase 0 total: ~24h**

---

## Phase 1: Content Extraction (Week 2)

> **Goal:** Azure Functions can extract text from SharePoint documents via Document Intelligence.
> **Exit criteria:** Unit + integration tests pass for content extraction activity; can extract text from PDF, DOCX, XLSX.

| # | Task | Est. | Deps | Status |
|---|------|------|------|--------|
| 1.1 | Implement `fetch_document` activity — fetch document binary from SharePoint via Graph API (Managed Identity auth) | 4h | 0.3, 0.10 | ⬜ |
| 1.2 | Implement `extract_content` activity — call Document Intelligence Read API, poll for result, normalize output | 4h | 0.3, 0.10 | ⬜ `[P]` |
| 1.3 | Implement text truncation utility (`shared/text_utils.py`) — page-aware truncation for token budget management | 2h | — | ⬜ `[P]` |
| 1.4 | Write unit tests for `extract_content` with mocked Document Intelligence responses | 3h | 1.2 | ⬜ |
| 1.5 | Write unit tests for `fetch_document` with mocked Graph API responses | 2h | 1.1 | ⬜ `[P]` |
| 1.6 | Write integration test: extract text from sample PDF using real Document Intelligence endpoint | 3h | 1.2, 0.5 | ⬜ |
| 1.7 | Test extraction against 5 representative document formats (PDF, DOCX, XLSX, PPTX, scanned PDF) and document results | 2h | 1.6 | ⬜ |

**Phase 1 total: ~20h**

---

## Phase 2: Classification & Type-Specific Extraction (Weeks 2-4)

> **Goal:** Two-agent AI pipeline works: Agent 1 classifies document type; Agent 2 extracts type-specific metadata with structured output.
> **Exit criteria:** Agent 1 type classification accuracy ≥90% on evaluation sample; Agent 2 per-field extraction accuracy ≥85%; structured JSON output parses reliably.
> **SOW task mapping:** Tasks (a), (b), (e)

### Track A: Document Type Classification (Agent 1 — GPT-4o-mini)

| # | Task | Est. | Deps | Status |
|---|------|------|------|--------|
| 2.1 | **Taxonomy working sessions** — facilitate up to 3 sessions with client SMEs to define document types, per-type field schemas, common categories, and decision rules | 6h | — | ⬜ |
| 2.2 | Create hierarchical `taxonomy.yaml` v2.0 — document types with per-type fields, common metadata categories, allowed values, and examples | 4h | 2.1 | ⬜ |
| 2.3 | Implement taxonomy loader (`shared/taxonomy.py`) — load hierarchical taxonomy YAML, expose document types and per-type schemas as typed objects | 3h | 2.2 | ⬜ |
| 2.4 | Create Agent 1 classification prompt template (`prompts/classify_type.j2`) — document type classification rules, taxonomy injection, output schema | 3h | 2.2 | ⬜ |
| 2.5 | Implement `classify_type` activity — call GPT-4o-mini, parse type + confidence | 3h | 2.3, 2.4, 0.10 | ⬜ |
| 2.6 | Write unit tests for `classify_type` with mocked GPT-4o-mini responses (valid, malformed, edge cases) | 3h | 2.5 | ⬜ |
| 2.7 | Create Agent 1 evaluation sample — 50-100 documents with labeled document types | 4h | 2.1 | ⬜ `[P]` |
| 2.8 | Evaluate Agent 1 accuracy — per-type accuracy, confusion analysis (target: >90% type classification) | 3h | 2.5, 2.7 | ⬜ |

### Track B: Type-Specific Metadata Extraction (Agent 2 — GPT-4o Structured Outputs)

| # | Task | Est. | Deps | Status |
|---|------|------|------|--------|
| 2.9 | Create extraction prompt templates per document type (`prompts/extract_lease.j2`, `prompts/extract_offer_memo.j2`, etc.) — ~10 templates | 8h | 2.2 | ⬜ |
| 2.10 | Define Structured Output JSON schemas per document type — response_format schemas for GPT-4o | 4h | 2.2, 0.3 | ⬜ `[P]` |
| 2.11 | Create shared user prompt template (`prompts/user_document.j2`) — document text + key-value pair formatting | 1h | — | ⬜ `[P]` |
| 2.12 | Implement `extract_metadata` activity — load type-specific prompt + schema, call GPT-4o with Structured Outputs, return typed result | 4h | 2.3, 2.9, 2.10, 2.11, 0.10 | ⬜ |
| 2.13 | Implement suggested fields parsing — validate and return additional discovered fields from Agent 2 response | 2h | 2.12, 0.3 | ⬜ |
| 2.14 | Write unit tests for `extract_metadata` — test each document type schema with mocked GPT-4o responses | 4h | 2.12, 2.13 | ⬜ |
| 2.15 | Create Agent 2 evaluation sample — 50-100 documents with labeled per-type fields | 4h | 2.1 | ⬜ `[P]` |
| 2.16 | Evaluate Agent 2 per-field extraction accuracy — per-field accuracy per document type (target: >85% per field) | 3h | 2.12, 2.15 | ⬜ |

### Track C: Prompt Tuning (Both Agents)

| # | Task | Est. | Deps | Status |
|---|------|------|------|--------|
| 2.17 | **Prompt tuning iteration 1** — run both Agent 1 and Agent 2 evaluations, analyze errors, refine prompts and taxonomy rules | 4h | 2.8, 2.16 | ⬜ |
| 2.18 | **Prompt tuning iteration 2** — add few-shot examples for confusion patterns in both agents, re-evaluate | 3h | 2.17 | ⬜ |
| 2.19 | **Prompt tuning iteration 3** — final refinements, per-category threshold calibration, freeze prompts | 3h | 2.18 | ⬜ |
| 2.20 | Evaluate GPT-4o-mini vs GPT-4o for Agent 1 — validate mini is sufficient for classification task (cost/accuracy trade-off) | 2h | 2.19 | ⬜ |
| 2.21 | Document final performance metrics for both agents (accuracy per type, per-field accuracy, confidence distributions, known limitations) | 2h | 2.19 | ⬜ `[P]` |

**Phase 2 total: ~62h**

---

## Phase 3: Pipeline Orchestration (Weeks 4-5)

> **Goal:** Durable Functions orchestrator sequences extract → classify_type → extract_metadata → route for single documents and batch.
> **Exit criteria:** Single-document orchestrator runs end-to-end locally; batch orchestrator processes 100 docs with progress tracking.
> **SOW task mapping:** Tasks (b), (c)

| # | Task | Est. | Deps | Status |
|---|------|------|------|--------|
| 3.1 | Implement `document_processing_orchestrator` — Durable Functions orchestrator sequencing fetch → extract → classify_type → confidence gate → extract_metadata → route | 5h | 1.1, 1.2, 2.5, 2.12 | ⬜ |
| 3.2 | Implement confidence routing logic (`route_result` activity) — evaluate BOTH type confidence (Agent 1) and per-field confidence (Agent 2), make routing decision | 4h | 0.3, 2.2 | ⬜ `[P]` |
| 3.3 | Implement `write_metadata` activity — write two-agent enrichment results to SharePoint columns via Graph API | 3h | 0.10 | ⬜ `[P]` |
| 3.4 | Implement `write_to_review_queue` activity — create item in SharePoint review queue list with document type, proposed tags, per-field confidence, reasoning | 3h | 0.10 | ⬜ `[P]` |
| 3.5 | Implement HTTP trigger (`http_trigger`) — entry point for Power Automate, validates payload, starts orchestrator with dedup instance ID | 2h | 3.1 | ⬜ |
| 3.6 | Implement AIProcessingStatus guard — check status before starting orchestrator to prevent re-trigger loop (R1 fix) | 2h | 3.1, 3.5 | ⬜ |
| 3.7 | Implement queue trigger (`queue_trigger`) — picks up documents from processing queue, starts orchestrator | 2h | 3.1 | ⬜ |
| 3.8 | Implement `batch_processing_orchestrator` — enumerate library via Graph API, fan-out to queue, track progress, generate report | 6h | 3.1, 3.7 | ⬜ |
| 3.9 | Implement `enumerate_library` activity — paginated Graph API query to list all documents in target library | 3h | 0.10 | ⬜ |
| 3.10 | Implement `filter_processed` activity — check Azure Table Storage tracking table, skip already-processed documents | 2h | 0.10 | ⬜ |
| 3.11 | Implement `generate_batch_report` activity — produce report with coverage, confidence distribution, review queue metrics, per-agent performance | 3h | 0.3 | ⬜ |
| 3.12 | Configure `host.json` — Durable Functions concurrency limits, queue settings, timeout, logging | 1h | 3.1 | ⬜ |
| 3.13 | Write unit tests for orchestrator (mocked activities, test two-agent sequencing and error handling) | 4h | 3.1 | ⬜ |
| 3.14 | Write integration test: single document end-to-end (local Functions + real Azure AI services) | 3h | 3.5 | ⬜ |
| 3.15 | Write integration test: batch of 10 documents end-to-end | 2h | 3.8 | ⬜ |

**Phase 3 total: ~45h**

---

## Phase 4: Power Automate & SharePoint Integration (Weeks 5-6)

> **Goal:** Event-driven trigger mode works end-to-end; human review queue operational with type-specific fields.
> **Exit criteria:** New document in SharePoint → auto-classified and enriched within 5 minutes; SME can review type-specific fields and approve/correct in review interface.
> **SOW task mapping:** Tasks (c), (d)

| # | Task | Est. | Deps | Status |
|---|------|------|------|--------|
| 4.1 | Provision SharePoint metadata columns on target document library — DealType, Submarket, Counterparty, DocClassification, Confidentiality, AIConfidence, AIProcessingStatus, AIClassifiedDate, DocumentType, TypeSpecificFields (JSON), SuggestedFields (JSON) | 3h | 2.2 | ⬜ |
| 4.2 | Create SharePoint "AI Classification Review" list with updated schema — type-specific fields display, per-field confidence (per human-review-queue.md) | 2h | — | ⬜ `[P]` |
| 4.3 | Create SharePoint "Corrections Log" list for prompt tuning feedback capture | 1h | — | ⬜ `[P]` |
| 4.4 | Build Power Automate Flow 1: Document Event Trigger — detect create/modify, filter supported formats, check AIProcessingStatus before invoking (re-trigger guard), POST to Azure Function HTTP trigger | 5h | 3.5, 3.6, 4.1 | ⬜ |
| 4.5 | Build Power Automate Flow 2: Metadata Write-Back — receive two-agent enrichment result from Function, write document type + type-specific fields to SharePoint columns | 3h | 4.1 | ⬜ |
| 4.6 | Build Power Automate Flow 3: Review Approval — detect review status change, write approved/corrected tags to original document, log corrections | 3h | 4.2, 4.3 | ⬜ |
| 4.7 | Build Power Apps review form — side-by-side document viewer + classification editor + dynamically render type-specific fields based on document type + approve/correct/reject buttons | 9h | 4.2 | ⬜ |
| 4.8 | End-to-end test: upload PDF to SharePoint → auto-classified → type-specific metadata extracted → columns written | 2h | 4.4, 4.5 | ⬜ |
| 4.9 | End-to-end test: low-confidence document → review queue → SME approves → metadata written | 2h | 4.6, 4.7 | ⬜ |
| 4.10 | End-to-end test: low-confidence document → review queue → SME corrects → correction logged | 2h | 4.9 | ⬜ |
| 4.11 | Document Power Automate flow designs with screenshots and configuration details | 2h | 4.4-4.6 | ⬜ |

**Phase 4 total: ~34h**

---

## Phase 5: Batch Execution & Production (Weeks 6-7)

> **Goal:** Initial batch-tagging pass over 20,000-30,000 active project files; pipeline validated in production.
> **Exit criteria:** Batch complete, results report published, review queue staffed, trigger mode active.
> **SOW task mapping:** Tasks (d), (e), (f)

| # | Task | Est. | Deps | Status |
|---|------|------|------|--------|
| 5.1 | Request Azure OpenAI TPM quota increase for batch processing — both GPT-4o-mini (Agent 1) and GPT-4o (Agent 2) deployments (target: 300K TPM) | 1h | 0.5 | ⬜ |
| 5.2 | Validate GPT-4o-mini for Agent 1 in production — confirm cost savings vs GPT-4o with equivalent accuracy on real documents | 3h | 2.20, 3.14 | ⬜ |
| 5.3 | Run pilot batch: 500 documents — validate throughput, error rates, cost (Agent 1 + Agent 2), review queue volume | 4h | 3.8, 4.1, 5.2 | ⬜ |
| 5.4 | Analyze pilot results: per-agent accuracy metrics, confidence distribution, throughput, cost projection for full batch | 3h | 5.3 | ⬜ |
| 5.5 | Adjust confidence thresholds based on pilot results — both type confidence and per-field confidence (target: <15% review rate) | 2h | 5.4 | ⬜ |
| 5.6 | Execute full production batch — wave 1: 10,000 documents | 4h | 5.5 | ⬜ |
| 5.7 | Monitor wave 1: error rates, cost per agent, review queue depth, throughput | 2h | 5.6 | ⬜ |
| 5.8 | Execute production batch — wave 2: remaining 10,000-20,000 documents | 4h | 5.7 | ⬜ |
| 5.9 | Publish Initial Batch-Tagging Results Report (coverage, per-agent performance, confidence-score distribution, human-review queue metrics, cost breakdown) | 4h | 5.8 | ⬜ |
| 5.10 | Validate trigger-mode pipeline in production (test with 10 live document uploads) | 2h | 4.8 | ⬜ |
| 5.11 | Configure Azure Monitor alerts: failure rate, review queue depth, cost thresholds, batch stall detection | 3h | 5.8 | ⬜ |
| 5.12 | Set up Azure Cost Management budget alerts for Document Intelligence + OpenAI consumption (GPT-4o-mini + GPT-4o) | 1h | 5.8 | ⬜ |

**Phase 5 total: ~33h**

---

## Phase 6: Documentation & Handover (Week 7)

> **Goal:** All SOW deliverables complete; client team can operate the two-agent pipeline independently.
> **Exit criteria:** All deliverables accepted by client Project Manager.
> **SOW task mapping:** Task (f) — operational runbooks

| # | Task | Est. | Deps | Status |
|---|------|------|------|--------|
| 6.1 | Finalize **Metadata Taxonomy & Tagging Rules Specification** — document types, per-type field schemas, classification rules (SOW Deliverable a) | 3h | 2.19 | ⬜ |
| 6.2 | Finalize **Azure AI Tagging Pipeline Design Document** — two-agent architecture diagrams, Agent 1/Agent 2 design, code repo handover, Power Automate flow exports (SOW Deliverable b) | 4h | 5.8 | ⬜ |
| 6.3 | Finalize **Initial Batch-Tagging Results Report** with coverage, per-agent performance, confidence distribution, review queue metrics (SOW Deliverable c) | 2h | 5.9 | ⬜ |
| 6.4 | Write **Tagging Pipeline Operations Runbook** (SOW Deliverable d): | 8h | 5.11 | ⬜ |
| | — Pipeline monitoring (App Insights dashboards, alert response procedures) | | | |
| | — Azure cost controls (budget alerts, spending analysis, GPT-4o-mini vs GPT-4o cost optimization) | | | |
| | — Prompt and taxonomy change management (how to update taxonomy, per-type templates, re-tune prompts, deploy changes) | | | |
| | — Escalation procedures (who to contact, severity levels, response times) | | | |
| | — Troubleshooting guide (common errors, Agent 1 vs Agent 2 failure modes, resolution steps) | | | |
| 6.5 | Export Power Automate flows as solution packages (.zip) | 1h | 4.11 | ⬜ |
| 6.6 | Prepare code repository for handover (clean up, document environment setup, verify README) | 2h | 5.10 | ⬜ |
| 6.7 | Knowledge transfer session with client team (two-agent architecture, monitoring, day-2 operations) | 2h | 6.4 | ⬜ |

**Phase 6 total: ~22h**

---

## Summary

| Phase | Focus | Est. Hours | Duration |
|-------|-------|-----------|----------|
| 0 | Project Setup & Infrastructure | 24h | Week 1 |
| 1 | Content Extraction | 20h | Week 2 |
| 2 | Classification & Type-Specific Extraction | 62h | Weeks 2-4 |
| 3 | Pipeline Orchestration | 45h | Weeks 4-5 |
| 4 | Power Automate & SharePoint Integration | 34h | Weeks 5-6 |
| 5 | Batch Execution & Production | 33h | Weeks 6-7 |
| 6 | Documentation & Handover | 22h | Week 7 |
| **Total** | | **~240h** | **~7 weeks** |

### SOW Deliverable Mapping

| SOW Deliverable | Tasks |
|----------------|-------|
| (a) Metadata Taxonomy & Tagging Rules Specification | 2.1, 2.2, 2.17-2.19, 6.1 |
| (b) Azure AI Tagging Pipeline Design Document | 0.4-0.6, architecture docs, 6.2 |
| (c) Initial Batch-Tagging Results Report | 5.3-5.9, 6.3 |
| (d) Tagging Pipeline Operations Runbook | 5.11-5.12, 6.4 |

### Critical Path

```
0.1 → 1.1/1.2 → 2.5 → 3.1 → 3.5 → 4.4 → 4.8 → 5.3 → 5.6 → 5.8 → 6.2
                    ↕              ↕
      2.1 → 2.2 → 2.4 → 2.8 ──┐
                   2.9 → 2.12 → 2.16 ──→ 2.17 → 2.18 → 2.19
```

Agent 1 (classification) and Agent 2 (extraction) tracks can develop in parallel after taxonomy sessions (2.1-2.2). Prompt tuning (Track C) blocks on both agents completing evaluation. The per-type template work in Track B is the longest lead-time item.

### Risk Items

| Risk | Mitigation | Task Reference |
|------|-----------|---------------|
| Taxonomy sessions delayed by client availability | Schedule sessions in Week 1; provide draft taxonomy for async review | 2.1 |
| OpenAI TPM quota insufficient for batch | Request quota increase early; design for multi-deployment if needed | 5.1 |
| Agent 1 type classification accuracy < 90% | Evaluate GPT-4o as fallback; negotiate additional tuning iterations | 2.8, 2.17-2.19, 2.20 |
| Agent 2 per-field extraction accuracy < 85% | Refine per-type prompts; add few-shot examples; simplify schemas for problematic types | 2.16, 2.17-2.19 |
| Per-type template count exceeds 10 | Prioritize most common document types; use generic template for rare types | 2.9 |
| Power Automate action limits during batch | Graph API direct write-back for batch (already designed) | 3.3, 3.8 |
| Review queue overwhelms SMEs | Tune thresholds on pilot before full batch; bulk approve option | 5.4, 5.5 |
| Re-trigger loop from metadata write-back | AIProcessingStatus guard in orchestrator + PA flow (R1 fix) | 3.6, 4.4 |
