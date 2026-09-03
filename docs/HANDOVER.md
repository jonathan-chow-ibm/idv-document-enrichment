# HANDOVER — Start Here

If you are picking up this repository, read this document first. It tells you what the system does, what
state it is actually in, **which existing documents are stale**, and what is left to do.

Last updated: 2026-08-27

---

## 1. What this system does

A document-enrichment pipeline that classifies and tags commercial real estate documents in SharePoint
Online so they can be filtered, searched, and used with Microsoft 365 Copilot.

```
SharePoint document
  → Document Intelligence (OCR / text)            [or ClosedXML for spreadsheets]
  → Agent 1: classify document type               (gpt-4.1-mini)
  → [drawings only] render page 1 → vision        (gpt-4.1-mini, multimodal)
  → Agent 2: extract ~26 metadata fields          (gpt-4o, strict JSON schema)
  → route: high confidence → Write | low → Review
  → write metadata back to SharePoint columns     (Graph API)
```

Orchestrated with **Durable Functions** (.NET 10 isolated worker): `BatchOrchestrator` →
`ChunkOrchestrator` (fan-out, chunks of 500) → `DocumentOrchestrator` (per document).

**Corpus scale:** **at least 117,752 files / 355 GB in a single site** (`03-Projects`) — the **full corpus
spans multiple sites and is larger; the total is not yet established.** Migrating from ShareFile into
SharePoint. Size everything (quota, elapsed time, review capacity, cost) against the *total*, not the
one-site figure — see the scaling table in
[runbook-operations.md](runbook-operations.md#scaling-recompute-for-the-actual-corpus-size).

> ⚠️ **Scope variance vs the SOW.** The SOW specifies *"initial batch-tagging over 20,000–30,000 files."*
> One site alone is ~118K — already 4–6× the contracted volume, before counting other sites. This affects
> cost, timeline, and the review-capacity assumptions the deliverable rests on. **Raise with the engagement
> owner rather than letting it surface at invoicing.**

---

## 2. Read in this order

| # | Document | Why |
|---|---|---|
| 1 | **This file** | Current state and gotchas |
| 2 | [runbook-operations.md](runbook-operations.md) | How to run it locally, deploy, execute a batch |
| 3 | [runbook-configuration.md](runbook-configuration.md) | How to change the taxonomy/fields — **read its Gotchas table before editing anything** |
| 4 | [taxonomy/taxonomy.yaml](taxonomy/taxonomy.yaml) | The actual configuration — document types, fields, thresholds |
| 5 | [decisions/](decisions/) | ADR-004 (two-agent), ADR-005 (.NET), ADR-006 (inline review), ADR-007 (XLSX), ADR-008 (models) |
| 6 | [architecture/pipeline-design.md](architecture/pipeline-design.md) | End-to-end design — **partially stale, see §4** |

The `docs/design/` folder holds discovery/UX analyses from earlier phases. Useful background, not
required reading.

---

## 3. Current state

### Working and tested
- **Classification** — strong. 0.90–0.95 confidence across contracts, drawings, plats, surveys.
- **Metadata extraction** — 26 fields via strict JSON schema. Good on stated facts (acreage, SF, parcel ID, city).
- **Drawing vision path** — renders page 1, extracts discipline / sheet number / drawing title. Verified on
  Architectural, Civil, Plat and Survey samples.
- **Spreadsheets** — native ClosedXML parsing (ADR-007), priority-sheet budgeting for large workbooks.
- **Batch orchestration** — chunked fan-out with per-document error isolation.
- **Metrics** — durations and token counts (including vision) flow into the batch report.

### Built but never run against the client environment
Everything has been tested **locally against Neudesic resources**. The IDV (client) subscription has
**never executed a single orchestration** — verified: Durable tables read `History=0, Instances=0,
Partitions=4`. Nothing has been processed for the client yet.

### Not done
- **No test project** — the solution contains none (see §4: the README claims otherwise)
- **No CI/CD** — `.github/workflows/` is empty
- **SharePoint schema not provisioned** in the client tenant
- **Power Automate flows not built** (SOW deliverable c — event-driven triggers)
- **Batch never executed**
- **Pro forma financial metrics not extracted** — deliberately deferred, see §6
- **Cost reporting shows $0.00** — token counts are real, dollar amounts unimplemented

---

## 4. ⚠️ Documents that are STALE — do not trust these

| Document | Problem |
|---|---|
| **README.md** | Described a `tests/` directory that **does not exist**. Corrected, but verify before trusting. |
| **architecture/human-review-queue.md** | **Superseded by ADR-006.** Describes a separate review list + Power Apps form that was never built. The implemented design is an inline filtered library view. |
| **architecture/pipeline-design.md**, **durable-functions-orchestration.md**, **prompt-engineering-strategy.md**, **power-automate-integration.md** | Last substantively updated 12–19 Aug, **before** the v4 taxonomy, the drawing vision path, and the folder-derived metadata model. Architecture is broadly right; specifics are out of date. |
| **taxonomy/content-type-fields.md** | Analysis from the *pre-v4* content-type exploration. Superseded by `taxonomy.yaml` v4. |
| **docs/tasks.md** | Phase plan from Phase 0. Not maintained. |

### Decisions made but NOT yet recorded as ADRs
These are significant and undocumented — **understand them before changing the taxonomy**:

1. **v4 taxonomy restructure** — the document types and metadata set were adopted **verbatim from the
   client's `Meta Data Request.xlsx`**, replacing our own inferred taxonomy. Types present in the sample
   projects but absent from the client's list (marketing flyers, correspondence, permits, entity docs,
   market reports) deliberately route to **Other**, pending SME review.
2. **Dates live in `transaction`, not `universal`** — this is deliberate. Fields in `content.universal` gate
   review **even when correctly empty**; having the three dates there sent ~70% of the corpus to Review.
   **Do not move them back.**
3. **Metadata provenance model** — `State` / `PropertyName` / `ProjectName` come from the **folder path**
   (`{STATE} {METRO} {Project}`, e.g. `TX DFW Risinger`), not from AI. `Confidentiality` and `Submarket`
   were removed from v3 on purpose.
4. **Vision extraction for drawings** — render page 1 → multimodal call → discipline/sheet/title, with an
   override that can reclassify `Other` into Plat / Survey / Design Drawing.

---

## 5. Known issues and pending work

### Should fix before a production batch
| Issue | Detail |
|---|---|
| **Anonymous HTTP triggers** | `HttpTestTrigger` and `SpikeDrawingRenderTrigger` are `AuthorizationLevel.Anonymous`. If published, they are open endpoints in Azure. Gate or exclude them. |
| **`county` false flags** | Scores ~0.7 when inferred (Fort Worth→Tarrant), 1.0 when stated. Frequently the *only* reason a document routes to Review. Give it a per-field threshold ≈0.65. |
| **Contract-only fields gate non-Contract document types** | Agent 2 evaluates every `content.property`/`content.transaction` field (e.g. `closingDate`, `purchasePrice`, `buyer`, `seller`, `titleCompany`, `earnestMoney`, `depositAmount`, `investorFund`) on every document type, including Design Drawings/Plats/Surveys where they're structurally irrelevant. On a real batch run (2026-09-02, 5 drawings), the model returned a clean `0` confidence for these fields on 1 of 5 documents (correctly signaling "not applicable" → routed to Write), but a hedged non-zero `0.3–0.4` confidence on the other 4 for the exact same irrelevant fields — which then gated all 4 to Review despite solid 0.95 type-classification confidence. Same root cause as the `opportunityZone` flooding fix, just on different fields, and not yet addressed: needs either (a) scoping which fields Agent 2 is asked to evaluate per document type, or (b) excluding contract-only fields from the Review gate when the document type doesn't warrant them. **Discuss with client before a production batch** — may also be a legitimate signal worth surfacing rather than suppressing, depending on their tolerance for Review-queue volume. |
| **`DocumentType` enum sync** | `taxonomy.yaml` labels and `Models/Enums.cs` must match exactly or classification deserialization throws. Currently in sync (24 each). No automated check — consider adding a startup validation. |
| **Provisioning script duplication** | `Provision-SharePointSchema.ps1` hardcodes 4 choice lists that mirror the taxonomy. Drift causes the **entire** write-back PATCH to fail. |
| **`TaxonomyBlobUrl` not in Bicep** | Set manually as an app setting; a fresh deploy won't know where the taxonomy lives. |
| **Legacy `.doc` files rejected by Document Intelligence** | The pipeline's `SupportedExtensions` list treats `.doc` (binary Word 97-2003) the same as `.docx`, but Azure Document Intelligence's prebuilt-layout model frequently rejects valid `.doc` files with `InvalidContent: "The file is corrupted or format is unsupported."` — confirmed on a real batch run (2026-09-02): 1 of 7 documents failed this way, a `.doc` JVA draft, while all `.docx`/`.pdf`/`.xlsx` files in the same batch processed fine. Narrow real-world impact — `.doc` is ~5 of 826 supported files in the TX DFW Risinger corpus sample vs. 62 `.docx` — but currently surfaces as an unhandled `DocumentProcessingOrchestrator` failure rather than a graceful skip/review routing. Consider either pre-converting `.doc`→`.docx` before extraction, or catching this specific Document Intelligence error and routing to Review with a clear reason instead of an orchestration-level failure. |

### Lower priority
- Cost reporting: needs per-1K-token pricing constants and `PageCount` on `BatchDocumentEntry` for the DI line
- HTML batch report has no cost/usage section (the JSON carries the data)
- `documentStatus` occasionally returns off-taxonomy values (e.g. "Preliminary")
- `entityName` can pick the preparing consultant rather than the owning entity — confidently wrong at ~0.9, so it passes the gate
- Vision override thresholds (0.7 / 0.8) are hardcoded rather than in `confidence_thresholds`

---

## 6. The blocker you will hit: gpt-4.1 quota

**Agent 2 currently runs on `gpt-4o` at 50K TPM** (regional Standard, fully allocated). The intended model
is `gpt-4.1`, but the client subscription has **0 TPM** for it and quota requests were **denied three times**
— East US and East US 2 both refused on regional capacity, automatically and instantly. Those regions are
locked for 30+ days.

Options, with the trade-offs:

| Option | Quality | Elapsed (117K docs) | Work |
|---|---|---|---|
| **A. Stay on gpt-4o @ 50K** (current) | ✅ validated | ~5.6 days | **none** — set `BatchMaxConcurrency` to ~7 to match quota |
| **B. Agent 2 on gpt-4.1-mini** (200K TPM available) | ⚠️ unproven; likely degrades on judgment fields (`counterparty`, `seller`/`buyer`, `documentStatus`) | ~2.5 days | none |
| **C. Global Batch API with gpt-4.1** | ✅ best | ~2 wks engineering + ~1 day run | ~10–11 days |

**Option C is worth serious consideration:** the subscription already holds **~50M TPM of GlobalBatch quota
on gpt-4.1** — no request, no region hunt — at **50% lower token cost**, and Structured Outputs + image
inputs are both confirmed supported in Batch. For a one-time enrichment where nothing waits on a live
response, it is arguably the correct architecture. See the scope discussion in the git history.

### ✅ Current decision: Option A (real-time on gpt-4o), iteratively

**Elapsed time is not a client constraint**, so real-time on the existing gpt-4o quota is the chosen
approach for now. Rationale:

- Validated quality — it is the model all testing has run against
- Zero engineering — runs today
- **Deliberately iterative:** prove the pipeline on real client data one site at a time before committing
  ~2 weeks to the Batch restructure. Build the async path only if the numbers justify it.

Set `BatchMaxConcurrency` to **~7** to match the 50K TPM ceiling (10 is over-subscribed and will throttle).

**Revisit Batch (Option C) when** the total corpus is known and any of these hold: elapsed time becomes a
constraint, the token bill becomes material at full scale, or a re-run of the whole corpus is needed. At
several hundred thousand documents, real-time on 50K TPM means weeks of continuous processing and roughly
double the token cost — at that point Batch stops being an optimisation and becomes the sensible
architecture.

---

## 7. Environments

| | Azure OpenAI | Doc Intelligence | Notes |
|---|---|---|---|
| **Dev (Neudesic)** | `oai-idv-enrich-dev` / `rg-idv-enrichment` | `di-idv-enrich-dev` | All testing done here. VS Professional sub — **MSDN credit with spending limit ON** |
| **Client (IDV)** | `oai-idv-doc-enrich-dev` / `rg-idv-enrich-dev`, sub `2fd2fd1c-…` | `di-idv-doc-enrich-dev` | Pay-as-you-go. Nothing has run yet. Idle cost ~$0.28/mo |

**Pause the client Function App between runs** — a deployed Durable app polls queues and renews leases
continuously even with zero work. See the runbook.

---

## 8. Suggested first steps for a successor

1. Read the two runbooks; get it running locally against Neudesic (`local.settings.json` is already configured)
2. Run a document through `POST /api/test/enrich` to see the shape of the output
3. Read `taxonomy.yaml` alongside §4 above so the deliberate choices are clear
4. Decide the Agent 2 model question (§6) — it gates the batch
5. Provision the SharePoint schema in the client tenant, then run a **small** batch (one project folder) before the full corpus
6. Fix the anonymous triggers before any client-facing deployment
