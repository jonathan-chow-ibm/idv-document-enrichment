# Runbook — Running the Pipeline

How to run the enrichment pipeline locally, deploy it, and execute a batch.
Configuration changes are covered in [runbook-configuration.md](runbook-configuration.md).

---

## Environment

| Resource | Value |
|---|---|
| Azure OpenAI | `oai-idv-doc-enrich-dev` / `rg-idv-enrich-dev` |
| Doc Intelligence | `di-idv-doc-enrich-dev` |
| Function App | `func-idv-doc-enrich-dev` |
| Subscription | `2fd2fd1c-9d6c-4000-8fb8-ddec8b79a7f1` |

---

## Local development

### Prerequisites
- .NET 10 SDK · Azure Functions Core Tools v4 · **Azurite running** (ports 10000/10001/10002)
- `az login` as an account holding **`Cognitive Services OpenAI User`** on the OpenAI resource and
  **`Cognitive Services User`** on the Doc Intelligence resource.
  *(These are data-plane roles — subscription Owner alone is **not** sufficient.)*

### `local.settings.json`
```json
{
  "Values": {
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AZURE_TOKEN_CREDENTIALS": "AzureCliCredential",
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "OpenAiEndpoint": "https://oai-idv-enrich-dev.openai.azure.com/",
    "OpenAiDeployment": "gpt-4.1",
    "OpenAiMiniDeployment": "gpt-4.1-mini",
    "DocIntelligenceEndpoint": "https://di-idv-enrich-dev.cognitiveservices.azure.com/",
    "TaxonomyBlobUrl": "C:\\...\\docs\\taxonomy\\taxonomy.yaml",
    "ConfidenceThresholdDefault": "0.80",
    "BatchMaxConcurrency": "10"
  }
}
```
`AzureCliCredential` authenticates as your current `az login` identity — so make sure `az account show`
points at the subscription holding those resources.

### Run
```bash
dotnet build src/IdvEnrichment.Functions/IdvEnrichment.Functions.csproj
cd src/IdvEnrichment.Functions
func start
```
**Always `dotnet build` first** — prompts are embedded resources and won't refresh otherwise.

---

## Endpoints

| Route | Auth | Purpose |
|---|---|---|
| `POST /api/test/enrich` | Anonymous | **Single document by URL** — bypasses SharePoint entirely. Main testing tool. |
| `POST /api/enrich` | Function key | Single document from SharePoint (event-driven / Power Automate) |
| `POST /api/batch` | Function key | Bulk run over a SharePoint library or folder |

### Test a single document
```bash
curl -X POST http://localhost:7071/api/test/enrich \
  -H "Content-Type: application/json" \
  -d '{"documentUrl":"<blob SAS url>","fileName":"MyDoc.pdf"}'
```
- Document Intelligence fetches the URL **from Azure's side**, so it must be cloud-reachable — use a
  **blob + read SAS**, not a local path or `localhost`.
- **Keep the real extension in `fileName`** — the extractor and PDF renderer branch on it.
- Returns `202` with `instanceId` + `statusUrl`. That 202 means *started*, not *succeeded*.

### Read the result
```
GET http://localhost:7071/runtime/webhooks/durabletask/instances/{instanceId}
```
Add `?showHistory=true&showHistoryOutput=true` to see per-activity output.

> **Write-back will fail in test mode** (`siteId` is `"test-site"`), so expect
> `writeBackSucceeded: false` and `runtimeStatus: Failed` unless a test-mode skip is in place.
> The AI output is still in the `RouteResult` activity output.

### Run a batch
```bash
curl -X POST https://<app>.azurewebsites.net/api/batch?code=<key> \
  -H "Content-Type: application/json" \
  -d '{"url":"https://<tenant>.sharepoint.com/sites/<site>/Documents/TX DFW Risinger",
       "classifyOnly": true, "maxPages": 10}'
```
Accepts a whole library or a single folder. Optional: `maxConcurrency`, `chunkSize`, `label`,
`classifyOnly`, `maxPages`.
- **`classifyOnly`** — skip Agent 2 (metadata extraction) entirely. Every document routes to Review
  (there's no metadata to gate on), and `AIProcessingStatus` is deliberately left unset so these runs
  don't pollute the real review queue. Useful for a fast type-distribution pass over a new library
  before running full extraction.
- **`maxPages`** — cap how many pages are read/analyzed per document, independent of `classifyOnly`.
  Cuts Document Intelligence page cost and local-parsing time on long documents when only enough
  content to classify is needed.

Before enumerating the library, the batch validates that every Choice column the pipeline writes
(`DocumentType` and the taxonomy's own allowed-value fields) still has every value it needs **on that
library's own copy of the column** — see [runbook-configuration.md, gotcha #3a](runbook-configuration.md).
A mismatch aborts the batch immediately, before any document is touched, naming the missing values.

---

## First-time setup (per SharePoint site)

1. **Deploy Bicep**
   ```bash
   az deployment group create -g rg-idv-enrich-dev \
     --template-file infra/main.bicep --parameters infra/main.bicepparam
   ```

2. **Upload taxonomy**
   ```bash
   az storage blob upload \
     --account-name <storage> --container-name config \
     --name taxonomy.yaml --file docs/taxonomy/taxonomy.yaml \
     --auth-mode login
   ```

3. **Verify the Function App started cleanly** — check App Insights Live Metrics or:
   ```bash
   az functionapp show -n <name> -g <rg> --query state
   ```

4. **Provision the SharePoint schema** — grant Graph permissions, list target libraries, dry-run, then
   run for real. Full step-by-step, plus how to add a new document type or content type later, is in
   [runbook-provisioning.md](runbook-provisioning.md).

5. **Test with one document** via the test endpoint. Confirm `routingDecision` and `drawingClassification` look correct.

6. **Set `BatchMaxConcurrency`** — quota is no longer the binding constraint (see Throughput and quota
   below); concurrency **25** is validated in production for full extraction runs at 53.4 docs/min with
   zero 429s. For classify-only runs, use **80** under ~2,000 documents, **40** above that — see below.

7. **Start the first small batch** (~500 documents, one project folder) and monitor the review queue before scaling up.

---

## Deployment

```bash
az deployment group create -g rg-idv-enrich-dev \
  --template-file infra/main.bicep --parameters infra/main.bicepparam

func azure functionapp publish func-idv-doc-enrich-dev
```

Bicep provisions the OpenAI + Doc Intelligence resources, storage, the Function App, and RBAC
(OpenAI User, Cognitive Services User, Key Vault Secrets User, and Storage Blob/Queue/Table).

Both previously-anonymous triggers (`HttpTestTrigger`, `SpikeDrawingRenderTrigger`) have been removed;
every remaining HTTP function is `AuthorizationLevel.Function`. To confirm before any publish:

```bash
grep -o '"authLevel": "[^"]*"' src/IdvEnrichment.Functions/bin/Debug/net10.0/functions.metadata | sort -u
```

⚠️ Check the **built metadata**, not the source. `.funcignore` and `.gitignore` do not gate a compiled
isolated worker — any `.cs` file left in the project directory is compiled into the assembly and
registered as a live function even when it is excluded from git and from the upload payload.

---

## Throughput and quota

Two dials that must be raised **together**:
- **`BatchMaxConcurrency`** — how many documents process in parallel (creates throughput)
- **TPM quota** — how much Azure will accept (permits throughput)

Whichever is lower is the bottleneck. Over-subscribing concurrency yields 429s and backoff, not speed.

**Sizing formula** (~28 s/document measured):
```
docs/min  = concurrency × 60 ÷ 28
TPM needed = docs/min × tokens-per-document
```
Measured per document: Agent 1 ≈ 2,280 · Agent 2 ≈ 3,430 · vision ≈ 2,950 (drawings only).

| Concurrency | docs/min | Agent 1 + vision (mini) | Agent 2 |
|--:|--:|--:|--:|
| 10 | 21 | ~58K TPM | ~73K TPM |
| 20 | 43 | ~117K | ~147K |
| 30 | 64 | ~175K | ~220K |

**Current IDV quota:** `gpt-4.1-mini` **5,000,000 TPM** (GlobalStandard) · `gpt-4.1` **1,000,000 TPM**
(GlobalStandard). Quota is no longer the binding constraint on throughput — Durable Functions concurrency
settings are. Concurrency **25** is validated in production for **full extraction** runs (53.4 docs/min,
zero 429s across two batch runs totalling ~4,470 documents); headroom above 25 for full runs hasn't been
measured.

For **classify-only** runs (`classifyOnly: true` — no Agent 2 extraction load, so lower tokens/document),
concurrency **80** held up with zero 429s on three batches (~5,700 documents total, 2026-09-17), but the
fourth and largest (5,864 documents, 2026-09-18) hit real throttling: **24 documents failed with HTTP
429 on the file *download* itself** — SharePoint/Graph rate-limiting our own download requests, not
Azure OpenAI TPM. That's a different bottleneck than the sizing formula above accounts for, and it only
showed up once the batch got large enough. It's also not validated for full extraction at all — Agent
2's ~3,430 tokens/document aren't in play on a classify-only run, so the TPM headroom is very different.

**Current guidance (2026-09-18):** use **80** for classify-only batches under ~2,000 documents (holds
clean, matches what's actually been measured); dial back to **40** for anything larger, until the
download step has its own retry/backoff. 40 isn't independently validated against this specific
bottleneck either — it's a conservative halving pending more data, not a measured ceiling. Re-measure
before trusting either number again if the download-retry work below gets done.

Part of why higher concurrency is safe *at all*: `ChunkOrchestrator` uses a sliding window, not
fixed-size groups — a single wedged or slow document only occupies its own concurrency slot (bounded by
a 20-minute per-document timeout) rather than blocking the rest of its chunk.

Also available and unused: **~50M TPM of Global Batch quota** on gpt-4.1 (50% cheaper, 24h turnaround) —
would require restructuring the AI calls to the async Batch API.

### Scaling — recompute for the actual corpus size

⚠️ The corpus is **larger than one site**. `03-Projects` alone is ~117,752 files; the full multi-site total
is not yet established. Recompute against the real number before planning a full run.

Measured per document: Agent 1 ≈ 2,280 · Agent 2 ≈ 3,430 · vision ≈ 2,950 (on ~15% of docs) →
**~6,150 tokens/document** across all calls.

This is now a **measured** rate, not one derived from TPM quota: the second production batch run
processed 2,547 documents at `maxConcurrency` 25 and **53.4 docs/min** with zero HTTP 429s, so that rate —
not a quota calculation — is the basis for the elapsed-time column below.

⚠️ **Both runs used `maxPages: 10`.** An uncapped run reads every page — more PdfPig parsing, more
Document Intelligence pages on scans, and larger extraction prompts per document — so 53.4 docs/min is
an optimistic ceiling, not a full-run rate. Re-measure on an uncapped batch before committing to a
schedule from the table below.

```
Agent 2 tokens     = docs × 3,430
Elapsed (measured) = docs ÷ 53.4 docs/min      # measured @ concurrency 25, maxPages 10
Review items       = docs × review-rate        # ~15% observed pre-tuning
```

| Corpus | Agent 2 tokens | Elapsed @ 53.4 docs/min *(measured, `maxPages` 10)* | Review @ 15% |
|--:|--:|--:|--:|
| 117,752 *(one site)* | 404 M | ~1.5 days | ~17,700 |
| 250,000 | 858 M | ~3.25 days | ~37,500 |
| 500,000 | 1.72 B | ~6.5 days | ~75,000 |
| 1,000,000 | 3.43 B | ~13 days | ~150,000 |

**Two consequences at scale:**

1. **The review rate stops being cosmetic.** At 15%, 500K documents yields ~75,000 review items — years of
   work at a 2-reviewer, ~60/day capacity. Every percentage point off the review rate is ~5,000 fewer items
   per 500K documents, so threshold tuning (`county`, `opportunityZone`, keeping non-applicable fields out
   of `content.universal`) has real leverage.
2. **Run in phased waves, not one pass.** Process ~5K documents, let the review queue drain, then continue.
   This keeps the queue manageable and lets prompt/threshold fixes from wave N improve wave N+1. The
   existing `chunkSize` and folder-scoped `POST /api/batch` support this directly — pass one project folder
   at a time.

---

## ⭐ After a run: pause the Function App

**Stop the Function App when you're not actively processing.** A deployed Durable Functions app is never
truly idle — even with zero orchestrations it continuously polls 5 queues (4 control partitions +
work-items) and renews 4 partition leases, 24/7. That heartbeat alone accrues storage transactions.

```bash
# after a batch completes
az functionapp stop  -n func-idv-doc-enrich-dev -g rg-idv-enrich-dev --subscription <sub>

# before the next run
az functionapp start -n func-idv-doc-enrich-dev -g rg-idv-enrich-dev --subscription <sub>
```

Stopping is safe and fully reversible — the task hub re-initializes on start and nothing is lost. Only do
it once a batch has **completed**; stopping mid-run leaves orchestrations suspended (they resume on start,
but in-flight activities may retry).

**Measured idle cost:** ~**$0.007/day** (~$0.28/month) on the IDV subscription. Small in absolute terms, but
it accrues indefinitely on an environment doing no work — and it's the *only* cost when nothing is running,
so it's the whole bill during a pause.

**Baseline for "nothing has run":** the task hub tables read `History = 0`, `Instances = 0`,
`Partitions = 4`. The 4 partition rows are lease/heartbeat records created at hub init — if History and
Instances are empty but cost is accruing, that heartbeat is the reason, not accumulated run history.

### Purge Durable history after a large batch

Orchestration history grows **unbounded** and is never cleaned up automatically. A 117K-document batch
writes on the order of a million rows to `IdvEnrichmentTaskHubHistory` (each document is a sub-orchestration
with ~8 activity calls), and it persists across runs.

```
POST https://<app>.azurewebsites.net/runtime/webhooks/durabletask/instances
     ?code=<durabletask_extension key>&createdTimeTo=<ISO date>&runtimeStatus=Completed
     (HTTP DELETE on the instances endpoint purges matching instances)
```
Or call `IDurableClient.PurgeInstanceHistoryAsync(...)` from a maintenance function. Purge only
**Completed** instances, and keep failed/errored ones until they've been investigated.

---

## Monitoring & troubleshooting

**Batch reports** land in the `BatchReportsContainerUrl` blob container as `report.json` + `report.html`
under `{date}/{batchId}/`. Note the **cost section reports $0.00** — token counts are real, dollar amounts
are not yet implemented.

**Application Insights** — the `DocumentEnriched` custom event carries documentType, routingDecision,
type confidence, and per-field confidence scores.

⚠️ **`customEvents` are sampled and not authoritative at scale.** For at least one production batch,
`DocumentEnriched` events were lost **entirely** — zero rows survived for that batchId, while another
batch's events came through complete. Treat the KQL queries below as directional for spot-checking, not
as a reliable way to account for every document in a large batch. The Durable Task instance store
(`GET .../runtime/webhooks/durabletask/instances?instanceIdPrefix={batchId}&...`) is the source that
actually covers every document, if a full per-document accounting is needed.

### Useful KQL queries

```kusto
// All documents processed in a batch — type, decision, and key confidences
customEvents
| where name == "DocumentEnriched"
| project
    timestamp,
    fileName        = tostring(customDimensions.fileName),
    documentType    = tostring(customDimensions.documentType),
    routingDecision = tostring(customDimensions.routingDecision),
    batchId         = tostring(customDimensions.batchId),
    typeConfidence  = todouble(customMeasurements.typeConfidence),
    pageCount       = todouble(customMeasurements.pageCount)
| order by timestamp desc
```

```kusto
// Documents that went to Review — see which field was the gating issue
customEvents
| where name == "DocumentEnriched"
    and tostring(customDimensions.routingDecision) == "Review"
| project
    fileName       = tostring(customDimensions.fileName),
    documentType   = tostring(customDimensions.documentType),
    typeConf       = todouble(customMeasurements.typeConfidence),
    counterpartyC  = todouble(customMeasurements["field_counterparty_confidence"]),
    transTypeC     = todouble(customMeasurements["field_transactionType_confidence"]),
    countyC        = todouble(customMeasurements["field_county_confidence"])
| order by typeConf asc
```

```kusto
// Classification accuracy by document type
customEvents
| where name == "DocumentEnriched"
| summarize
    count(),
    avgTypeConf  = avg(todouble(customMeasurements.typeConfidence)),
    reviewRate   = countif(tostring(customDimensions.routingDecision) == "Review") * 100.0 / count()
  by documentType = tostring(customDimensions.documentType)
| order by reviewRate desc
```

```kusto
// Extraction method breakdown (closedxml vs document-intelligence)
customEvents
| where name == "DocumentEnriched"
| summarize count() by tostring(customDimensions.extractionMethod)
```

| Symptom | Likely cause |
|---|---|
| New prompt field returns `null` | `.hbs` not rebuilt (embedded resource) |
| `DeploymentNotFound` / 404 | `OpenAiDeployment` / `OpenAiMiniDeployment` names don't exist on the resource |
| 401/403 on OpenAI or DI | Missing **data-plane** role (`Cognitive Services OpenAI User` / `Cognitive Services User`) |
| Whole write-back fails, no fields saved | A column in the PATCH doesn't exist, or a Choice value isn't in its allowed list |
| Everything routes to Review | A field in `content.universal` is flagging while legitimately empty |
| 429 / slow batch | Concurrency above quota |
| Enum deserialization error on classify | `taxonomy.yaml` label missing from `DocumentType` enum |
| Durable orchestration stuck | Check Azurite is running (local) / storage RBAC (Azure) |
| Batch fails immediately, "SharePoint schema validation failed" | Named Choice column(s) missing a value on **this library's own copy** — re-run `Provision-SharePointSchema.ps1` (see [runbook-configuration.md, gotcha #3a](runbook-configuration.md)) |

---

## Known limitations

- **Some file types can't be read**: `.doc` (legacy), `.msg`, `.dwg`, `.mpp`, `.zip` → route to review.
  `.pptx` *is* supported — it's converted to PDF before extraction — but image-heavy decks can still fail
  born-digital detection after conversion and route to Document Intelligence, and chart/diagram content
  may extract thinly.
- **Multi-page drawings**: only page 1 is rendered for vision, so a permit set yields cover-sheet metadata
- **Spreadsheets >50 MB** are rejected; large workbooks are truncated to a 24K budget with priority sheets favoured
- **Pro forma financial metrics** (IRR, yield, NOI) are **not** extracted — deferred; models vary too much across projects
- **Cost reporting** shows $0.00 (no pricing constants, and no page count for the DI line)
