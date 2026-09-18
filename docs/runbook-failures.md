# Runbook — Document Failures

Why a document didn't process cleanly, and what (if anything) can be done about it. Two categories,
and they show up very differently in a batch report:

- **Soft failures** — the pipeline recognized the problem, routed the document to Review with
  `DocumentType: Other`, and moved on. Counted under `skipped`/`underReview`, not `errors`. Working as
  designed; nothing to fix.
- **Hard failures** — the document's orchestration threw and never produced a result. Counted in
  `errors`, listed in the batch report's `failedDocuments`, and shows up as a blank row in
  `05-document-detail.csv` (no file name, no type — the row carries only whatever identity survived).
  These are the ones worth tracking down.

---

## Soft failures (routes to Review, no fix needed)

All three set `LowConfidenceCategories` to a single tag naming the reason, so they're filterable in the
review queue without opening the file.

| Trigger | `ExtractionResult` sentinel | Tag |
|---|---|---|
| Unsupported spreadsheet format, or a `.xlsx`/`.xlsm` ClosedXML can't parse (`InvalidDataException`, `IOException`, malformed OOXML relationships, `XmlException`) | `UnsupportedFormat` | `format` |
| PDF exceeds the local-parsing (250 MB) or Document Intelligence (50 MB / 120 pages, uncapped) size limits; spreadsheet exceeds `SpreadsheetExtractor.MaxFileSizeBytes` or has no reported content-length | `TooLargeForProcessing` | `size` |
| PDF is encrypted (`PdfDocumentEncryptedException` — DI can't open it either) | `PasswordProtected` | `password` |

Handled in code: [`ExtractContentActivity.cs`](../src/IdvEnrichment.Functions/Activities/ExtractContentActivity.cs),
routed in [`DocumentOrchestrator.cs:51-121`](../src/IdvEnrichment.Functions/Orchestrators/DocumentOrchestrator.cs#L51-L121).

**Already fixed, not currently a failure mode despite appearing in older client reports:**

- **Invalid date value in a spreadsheet** (`"Ticks must be between DateTime.MinValue.Ticks and
  DateTime.MaxValue.Ticks"`) — a cell carries a date format applied to a numeric serial outside the
  representable range. `SpreadsheetExtractor.SafeFormattedString` catches the `ArgumentOutOfRangeException`
  and falls back to the raw numeric value instead of throwing.
- **Malformed spreadsheet URI relationships** — caught by the same `ExtractSpreadsheetAsync` catch
  clause as any other unparseable spreadsheet; routes to `UnsupportedFormat` above rather than failing
  the document.

**Other known limitations, not failures as such:**

- **Some file types are never attempted**: `.doc` (legacy), `.msg`, `.dwg`, `.mpp`, `.zip` → route
  straight to review. `.pptx` *is* supported — converted to PDF before extraction — but image-heavy
  decks can still fail born-digital detection after conversion and route to Document Intelligence, and
  chart/diagram content may extract thinly.
- **Spreadsheets are truncated to a 24K character budget**, favouring priority sheets, rather than
  rejected outright — see `SpreadsheetExtractor`.

**Not yet handled:**

- **Large-format drawing sheet exceeds Document Intelligence's pixel-dimension limit** (its own error:
  `"The input image is too large. Refer to documentation for the maximum file size."`, distinct from
  `InvalidContentLength`). The existing size/page caps are byte- and page-count-based — a 36" sheet
  scanned at high DPI can be small in bytes and page count while still exceeding DI's ~10,000×10,000 px
  ceiling. Currently a hard failure (below), not a soft route to Review — the document isn't actually
  too large to want, just too large in the wrong dimension.
- **Multi-page drawing sets**: only page 1 renders for vision, so a permit set yields cover-sheet
  metadata only.
- **Pro forma financial metrics** (IRR, yield, NOI) are not extracted — deferred, models vary too much
  across projects.
- **Cost reporting** shows `$0.00` in every batch report — token counts are real, dollar pricing
  constants aren't implemented.

---

## Hard failures (batch errors)

### Document Intelligence: "Could not download the file from the given URL" (HTTP 400 / `InvalidContent`)

```
{"error":{"code":"InvalidRequest","message":"Invalid request.",
  "innererror":{"code":"InvalidContent","message":"Could not download the file from the given URL."}}}
```

**Where it happens:** `ExtractWithDocumentIntelligenceAsync` — DI fetches the document URL **itself**,
server-side, independent of anything our own `HttpClient` already downloaded. This is not the same
download as the one below.

**Why it isn't retried today:** `ExtractContent` deliberately has zero whole-activity retry — it
submits a billable DI job, and re-running the activity after a failure risks resubmitting work DI
already accepted and is billing for. See the comment at
[`DocumentOrchestrator.cs:33-41`](../src/IdvEnrichment.Functions/Orchestrators/DocumentOrchestrator.cs#L33-L41).
Transient DI failures are otherwise handled by the Azure SDK's own retry pipeline — but that retries
individual HTTP requests for statuses like 429/5xx, not a deterministic 400, so this specific error
propagates on the first attempt.

**Confirmed reproducible on demand**: fetching a fresh Graph `@microsoft.graph.downloadUrl` for the
same item and POSTing it straight to DI's `analyze` endpoint reproduces the identical error live — this
isn't specific to one file. Seen twice on 2026-09-17 (1 document each in two ~1,500-2,100 document
batches) and 44 times on 2026-09-18 in a 5,864-document batch — frequency scales with corpus size,
consistent with an intermittent condition rather than a per-file defect.

**Possible fix (not implemented):** a narrowly-scoped retry on this exact signature specifically — safe
precisely *because* nothing was billed if DI never got past its own download step, unlike a retry after
a real analysis failure. Would need to distinguish this `innererror.code == "InvalidContent"` +
"Could not download" case from other `InvalidContent` causes (e.g. a genuinely corrupt file) so it
doesn't retry something that will fail identically every time.

### HTTP 429 on the file download itself (SharePoint/Graph throttling)

**Where it happens:** `ExtractPdfAsync`/`ExtractSpreadsheetAsync`/`ExtractPlainTextAsync` — our own
`HttpClient.GetAsync(input.DocumentUrl, ...)` against the Graph-issued download URL, **before** anything
reaches Document Intelligence.

**This already has retry** — `DownloadRetryHelper.GetWithRetryAsync` retries up to 3 attempts,
respecting `Retry-After` (capped at 60s) or a `2×attempt` second backoff otherwise. 429 is not in its
`PermanentRefusals` set (`{400, 403, 404, 406, 415}`), so it isn't given up on immediately. The 24
failures seen on 2026-09-18 exhausted all 3 attempts — this was sustained throttling under load, not a
single blip that a retry should have absorbed.

**First appeared** on the largest batch run to date (5,864 documents at concurrency 80); the three
smaller same-day/prior-day batches (~1,500-2,100 documents each) saw zero. See
[runbook-operations.md, Throughput and quota](runbook-operations.md#throughput-and-quota) for the
concurrency guidance this drove (80 under ~2,000 documents, 40 above).

**Possible fixes (not implemented), in order of how directly they address it:**
1. Lower concurrency on large batches (done — see above) — fewer parallel downloads means less
   pressure on whatever throttle this is hitting.
2. Raise `DownloadRetryHelper.MaxAttempts` or `MaxRetryAfterSeconds` for 429 specifically, since the
   existing retry exists but wasn't enough at this scale.
3. Investigate whether this is a per-app or per-user Graph throttle with a documented threshold, to
   size concurrency against a known ceiling rather than a halved guess.

### Azure OpenAI content filter rejects the classification prompt (HTTP 400 / `content_filter`)

```
HTTP 400 (content_filter)
Parameter: prompt
The response was filtered due to the prompt triggering Azure OpenAI's content management policy.
```

**Where it happens:** `ClassifyTypeActivity` — the request itself is rejected before any completion is
returned (distinct from the `ChatFinishReason.ContentFilter` path in the same file, which handles a
200 response with empty content). Extraction had already succeeded; only classification failed.

**Observed on:** a Texas Comptroller "Public Information Report" filing (2026-09-17) — plausibly
triggered by hazardous-material/incident narrative language, common in TCEQ-style environmental
filings, but not confirmed against the actual flagged span.

**No code-level fix applies.** This is deterministic on the same input — retrying would fail identically
every time, unlike the two failures above. The only levers are outside this codebase: an Azure OpenAI
content-filter configuration change (a "modified" or less strict filtering tier, which requires
Microsoft approval) on the resource, or manual review of the specific document outside the automated
pipeline. Treat as a permanent small-tail category, not a bug to fix.

### Other download failures (from `DownloadRetryHelper.PermanentRefusals`)

Not retried at all — the service will return the identical answer every time:

| Status | Meaning | Retried? |
|--:|---|---|
| 400 | Bad request | No |
| 401 | Pre-authenticated download URL expired *before* the retry loop's clock — not in `PermanentRefusals`, so this one **is** retried up to 3 attempts first | Yes, then permanent |
| 403 | Expired pre-authenticated download URL | No |
| 404 | Item no longer exists at that URL | No |
| 406 | Graph's PDF-conversion service refuses a file it can't convert (confirmed on a corrupt `.pptx` and a `.docx` carrying embedded PDF objects) | No — but see below |
| 415 | Unsupported media type | No |

**406 usually isn't visible as a failure** — `ExtractContent`'s outer catch specifically handles this
case for converted Office files: it falls back to reading the *original*, unconverted file rather than
failing the document. See [`ExtractContentActivity.cs:68-86`](../src/IdvEnrichment.Functions/Activities/ExtractContentActivity.cs#L68-L86).

**Caution when triaging a generic 400/`InvalidRequest`**: don't assume it means a damaged or
unsupported file without checking `innererror.code`. If it's specifically `InvalidContent` with
"Could not download the file from the given URL," that's the transient DI-side failure above, not
evidence the source file itself is bad.

### Taxonomy / schema drift

Covered in depth elsewhere — cross-referenced here so this is a complete index of "why did this
document fail":

- **Agent 1 returns a label the `DocumentType` enum lacks** → deserialization throws → 3 retries → error.
  [runbook-configuration.md, gotcha #2](runbook-configuration.md).
- **A SharePoint Choice column is missing a value the pipeline needs** → write-back PATCH 400s → whole
  document loses all metadata, not just the mismatched field.
  [runbook-provisioning.md, "Why a missing choice value is worse than it looks"](runbook-provisioning.md#why-a-missing-choice-value-is-worse-than-it-looks).
  A batch-level pre-flight check (`ValidateSharePointSchemaActivity`) catches this **before** any
  document is touched, for the common case of a library never having been re-provisioned.

### Wedged or slow document (20-minute timeout)

**Defensive guard, not yet observed firing in production.** `ChunkOrchestrator` bounds each document's
sub-orchestration to 20 minutes; if it doesn't resolve in that window, the document is recorded as
failed and the chunk moves on — the sliding-window concurrency model means this only ever costs that
document's own slot, not the rest of the batch. See
[`ChunkOrchestrator.cs:11-16`](../src/IdvEnrichment.Functions/Orchestrators/ChunkOrchestrator.cs#L11-L16).
If this starts appearing in a batch report, the fix is investigating what made that specific document
slow (unusually large scan, DI queue depth) rather than raising the timeout blindly.
