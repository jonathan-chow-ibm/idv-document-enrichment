# Runbook — Updating the Configuration

How to change what the pipeline classifies and extracts. **Read the "Gotchas" section first** — several
of these were learned the hard way and will silently waste your time otherwise.

---

## ⚠️ Gotchas — read before you change anything

| # | Trap | What happens | Rule |
|---|---|---|---|
| **1** | **Prompts (`*.hbs`) are embedded resources** | Editing a `.hbs` and re-running does nothing — the old prompt is still compiled into the DLL | **Always `dotnet build` and restart `func start` after editing a prompt.** Tell-tale: your new output field comes back `null` |
| **2** | **`DocumentType` enum must match `taxonomy.yaml`** | If Agent 1 returns a label the C# enum lacks, deserialization **throws** → 3 retries → error path | Adding/renaming a document type = edit **both** `taxonomy.yaml` *and* `Models/Enums.cs` |
| **3** | **Provisioning script duplicates 4 choice lists** | Change a type in YAML and the SharePoint Choice column no longer accepts it → **the whole write-back PATCH 400s** | Update `Provision-SharePointSchema.ps1` in the same commit, **then re-run it** — the script now converges existing choice columns, but only when you run it |
| **3a** | **A site column patch does NOT reach existing libraries** | Each library holds its own copy of a Choice list. Patching the site column alone looks correct in the admin UI and changes nothing for the pipeline, which writes to **list** columns | Never hand-patch the site column. Re-run `Provision-SharePointSchema.ps1` — it reconciles both scopes. Verified 2026-09-17: site column 24→25 left all 69 libraries on 24 |
| **3b** | **`DocumentType` is the one Choice column written unguarded** | Taxonomy content fields are dropped on mismatch (`TryMatchAllowedValue`); `DocumentType` is not, so one missing choice value costs the document **every** field, not one | See "Why a missing choice value is worse than it looks" below |
| **4** | **Taxonomy is cached per app instance** (`Lazy<Task>`) | Editing the blob/file doesn't affect a running app | **Restart the Function App** after changing the taxonomy |
| **5** | **Removing a column: pipeline first, SharePoint last** | Deleting a live column while the pipeline still writes it → unknown-column 400 → **all write-back fails**, not just that field | Remove from `taxonomy.yaml` → redeploy → confirm drained → *then* touch SharePoint. Prefer hide/unlink over delete |
| **6** | **Concurrency must match quota** | `BatchMaxConcurrency` above what TPM supports → 429s, backoff, slower overall | See "Throughput" below |
| **7** | **Unknown column in a PATCH fails the entire PATCH** | One bad field name loses *every* field for that document | Any new taxonomy field needs its column provisioned before use |

---

## Where configuration lives

| What | File | Needs code change? |
|---|---|---|
| Document types, descriptions, decision rules | `docs/taxonomy/taxonomy.yaml` | ⚠️ Only if adding/renaming a type (enum) |
| Metadata fields + SharePoint column names | `docs/taxonomy/taxonomy.yaml` | No |
| Allowed values, free-text flags | `docs/taxonomy/taxonomy.yaml` | No |
| Confidence thresholds | `docs/taxonomy/taxonomy.yaml` | No |
| SharePoint columns | `scripts/Provision-SharePointSchema.ps1` | No (but must mirror YAML) |
| Endpoints, deployments, concurrency | app settings / `local.settings.json` | No |
| Prompts | `src/.../Prompts/*.hbs` | Rebuild required (gotcha #1) |
| Vision disciplines, override thresholds, trigger rules | hardcoded — see "Hardcoded values" | Yes |

---

## Common tasks

### Add a metadata field (YAML + provisioning only)

1. **`taxonomy.yaml`** — add under the right `metadata.content.*` group:
   ```yaml
   - field_name: myField
     sharepoint_column: "MyField"
     description: "What the AI should look for"
     is_freetext: true          # or allowed_values: ["A","B"]
   ```
   * `universal` = expected on every document → **always gates review**
   * `property` / `transaction` / `ownership` = sparse → empty values are skipped, won't force review
2. **`Provision-SharePointSchema.ps1`** — add the matching column:
   ```powershell
   @{ name = "MyField"; displayName = "My Field"; text = @{} }
   ```
3. Run the provisioning script, then restart the Function App.

> **Put dates and financial fields in `transaction`, not `universal`.** Fields in `universal` gate review
> even when correctly empty — that once put ~70% of the corpus into the review queue.

### Add a document type (needs a code change)

1. `taxonomy.yaml` → new entry under `document_types` with `label`, `group`, `description`, optional `decision_rules`
2. `Models/Enums.cs` → matching enum member:
   ```csharp
   [JsonStringEnumMemberName("My New Type")] MyNewType,
   ```
   **The string must match the YAML `label` exactly.**
3. `Provision-SharePointSchema.ps1` → add the label to `$documentTypes`
4. Rebuild, redeploy, **re-run provisioning** — step 3 alone changes nothing in SharePoint

> **Step 4 is not optional and used to be silently skippable.** The provisioning script was
> create-only: it reported `[EXISTS] Document Type` and never touched the deployed choice list, so
> a new type could sit in all three code locations while SharePoint rejected it. The script now
> converges choice lists on every run (site column *and* every library's own copy), so re-running
> is what deploys the value. Confirm with `-DryRun` first: a clean run prints no
> `Would update choices` lines.

### Why a missing choice value is worse than it looks

The write-back is **one** `PATCH .../items/{id}/fields` carrying every field. Graph validates the
whole body, so a single unacceptable value rejects **all** of it — the document ends up with no
metadata at all, not merely a blank `DocumentType`.

There **is** a guard in the code, but read carefully what it protects against. Every taxonomy
content field with `allowed_values` goes through `TryMatchAllowedValue`, and unparseable
`dateTime` / `number` values are skipped — an unmatched value is **omitted** from the payload, so
the rest of the document's metadata still lands.

That guard compares the AI's output against **`taxonomy.yaml`'s `allowed_values`** — it never reads
the deployed SharePoint column. So it protects against the AI returning something outside the
taxonomy; it does **not** protect against the taxonomy and SharePoint disagreeing. Add a value to a
field's `allowed_values`, skip the provisioning re-run, and that content field will 400 the whole
PATCH exactly like `DocumentType` — the guard waves it through as valid.

`DocumentType` has no guard at all. In `WriteMetadataActivity.BuildFieldsPayload` it is written
directly from the enum label:

```csharp
["DocumentType"] = JsonSerializer.Serialize(result.TypeClassification.DocumentType).Trim('"'),
```

So the blast radius of one missing choice value is the whole document, and the failure sequence is:

1. `TrySetContentTypeAsync` runs first and **swallows** its own errors — the content type is set.
2. The fields PATCH 400s. `SdkExceptionHelper` logs the Graph error code and rethrows.
3. Durable retries the activity **3 times** (5s, 10s, 20s). A 400 is deterministic, so all 3 fail.
4. `DocumentOrchestrator` catches `TaskFailedException`, sets `WriteBackSucceeded = false`, and
   records status `write-back-failed`.

The end state is a document **correctly typed** (e.g. `Reports`) with **zero metadata** — which
looks like a partially-working pipeline rather than a schema problem. Check the batch report's
`write-back-failed` count and look for the Graph error code in App Insights; the fix is to deploy
the missing choice value, then re-run those documents.

**Why `TryMatchAllowedValue` can't simply be reused for `DocumentType`.** It validates against the
taxonomy, and for `DocumentType` the taxonomy always agrees — Agent 1's output is an enum, so
deserialization already rejects anything the taxonomy lacks (gotcha #2). A taxonomy-based guard
would never fire on the case that actually hurts, which is taxonomy-vs-SharePoint drift. Guarding
this properly means reading the deployed column's `choice.choices` at runtime and omitting
`DocumentType` when the value isn't there — cached per list, the same shape as the existing
`ContentTypeCache` in the same activity. Not implemented; re-running provisioning is the current
mitigation.

Three sync axes exist, and only the first two are enforced anywhere:

| Axis | Enforced by | Failure if it drifts |
|---|---|---|
| AI output ↔ `taxonomy.yaml` | enum deserialization (types), `TryMatchAllowedValue` (fields) | throws / field omitted |
| script arrays ↔ deployed SharePoint | `Provision-SharePointSchema.ps1` converge pass, **when re-run** | whole PATCH 400s |
| `taxonomy.yaml` ↔ script arrays | **nothing** — manual discipline only | whole PATCH 400s |

All three were verified aligned on 2026-09-17.

Two related sharp edges when adding a document type:

- **A missing `[JsonStringEnumMemberName]` silently changes the label.** Without it the enum
  serializes as the C# identifier — `Cmt` instead of `CMT` — which SharePoint then rejects. The
  attribute is what makes the label match; it isn't decoration.
- **`AIProcessingStatus` is also written unguarded** (`"Classified"` / `"Under Review"`), but is
  safe only because those two literals happen to be in the provisioned choice list. If you ever
  rename them, rename the column's choices in the same commit.

### Tune classification accuracy (YAML only — no rebuild)

Add `decision_rules` to a document type. These are injected into Agent 1's prompt. What works:

- **Recognition signals, not preconditions** — "if the sheet is titled PLAT, classify as Plat" beats "must be a recorded instrument"
- **Order explicitly** — use `CHECK FIRST` / `CHECK SECOND` / `CHECK LAST` when types compete
- **Say what it is NOT** — "a survey citing recorded plats is still a Survey, NOT a Plat"
- **Watch for greedy rules** — "any document by an A/E firm is a Design Drawing" swallowed plats and surveys, because surveyors work at engineering firms

### Adjust review volume (thresholds)

```yaml
confidence_thresholds:
  agent1_classification: 0.80
  agent2_content:
    documentStatus: 0.75
    counterparty: 0.70
    transactionType: 0.75
    opportunityZone: 0.50     # "Unknown" is a valid answer — don't gate on it
    default: 0.75
```
Any single low-confidence field sends the whole document to **Review**. If one field flags constantly,
give it its own lower threshold rather than lowering `default`.

*Known frequent flagger:* `county` scores ~0.7 when inferred (Fort Worth→Tarrant) and 1.0 when stated.
A per-field threshold of ~0.65 clears it.

### Change models

App settings (or `local.settings.json`):
```
OpenAiEndpoint        https://<resource>.openai.azure.com/
OpenAiDeployment      <Agent 2 — metadata extraction>
OpenAiMiniDeployment  <Agent 1 — classification AND drawing vision>
```
The deployment **names** must exist on the resource. Both agents share one endpoint — **you cannot split
them across two OpenAI resources** without a code change.

---

## Hardcoded values (require a rebuild)

| Value | Location |
|---|---|
| Vision discipline list (7 values) | `Prompts/ClassifyDrawing.hbs` — duplicated from taxonomy `allowed_values` |
| `drawingType` → enum map | `Orchestrators/DocumentOrchestrator.cs` |
| Vision override thresholds `0.7` (fill-in) / `0.8` (correction) | `Orchestrators/DocumentOrchestrator.cs` |
| Low-text vision trigger `TextLength < 500` | `Orchestrators/DocumentOrchestrator.cs` |
| Extraction/classification truncation caps (24K / 4K) | `Shared/TextUtils.cs` |
| Spreadsheet budget + 50 MB cap | `Shared/SpreadsheetExtractor.cs` |
| PDF render resolution (1568×2048) | `Shared/PdfPageRenderer.cs` |

---

## Folder-derived fields

`State`, `PropertyName`, `ProjectName` are **not** written by the pipeline. They come from the folder
path convention `{STATE} {METRO} {Project}` (e.g. `TX DFW Risinger`) and should be populated via
**SharePoint folder default column values**:

```powershell
Set-PnPDefaultColumnValues -List "Documents" -Folder "TX DFW Risinger" -Field "State" -Value "Texas"
```

⚠️ **Folder defaults are not retroactive** — they only stamp items added *after* they're set. Existing
documents need a one-time back-fill.
