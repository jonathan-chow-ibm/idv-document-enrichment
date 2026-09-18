# Runbook — Provisioning SharePoint Schema

Everything about `scripts/Provision-SharePointSchema.ps1`: what it creates, how to run it, how it
stays in sync with the taxonomy, and how to extend the schema (a new document type or a new content
type). Running a batch is covered in [runbook-operations.md](runbook-operations.md); tuning what gets
classified/extracted once the schema exists is covered in
[runbook-configuration.md](runbook-configuration.md).

---

## What it provisions

Per SharePoint document library:

- **~39 metadata columns** mirroring `docs/taxonomy/taxonomy.yaml` — AI operational columns
  (`DocumentType`, `AIConfidence`, `AIProcessingStatus`, ...), folder-derived columns (`State`,
  `PropertyName`, `ProjectName`), and every `content.*` field group in the taxonomy.
- **5 SharePoint content types** — `Contracts`, `Drawing Files`, `Reports`, `Budget Files`, `Other` —
  one per taxonomy `group` value (see [ADR-009](decisions/adr-009-sharepoint-content-types.md) for
  the full rationale). Each gets the AI/folder/universal/property columns plus whatever additional
  column set its group needs.

It's idempotent — re-run after any taxonomy/script change and only the delta deploys; a no-change
re-run performs reads only. Run with `-DryRun` first; it reports a `[CHECK]` line per content type
and a `[target]`/`[skip] <reason>` line per library, plus summary counters for planned work.

---

## Running it

1. **Grant Graph permissions** (once per tenant) — requires Cloud Application Administrator or Global
   Admin in the client tenant:
   ```powershell
   ./scripts/Grant-GraphPermissions.ps1 -FunctionAppName <name> -ResourceGroupName <rg>
   ```

2. **List the target libraries** — read-only, no writes on any code path:
   ```powershell
   ./scripts/Provision-SharePointSchema.ps1 `
     -SiteUrl "tenant.sharepoint.com:/sites/SiteName" -ListLibraries
   ```
   Each library prints as `[target]` or `[skip] <reason>`. SharePoint system libraries are refused on
   every code path (matched by server-relative URL, not display name); `Documents`, `Template`,
   `Images` and `Pages` are skipped via the overridable `-ExcludeLibraries` default.
   > On `idvllc.sharepoint.com:/sites/IDVProjects` this returns 73 libraries — one per project.
   > `-DocumentLibraryName "Documents"` is the **wrong** target there: that library holds no project
   > content. Use `-AllLibraries`.

3. **Dry-run** — review the output before making any changes. Add any non-project libraries
   `-ListLibraries` surfaced to `-ExcludeLibraries`:
   ```powershell
   ./scripts/Provision-SharePointSchema.ps1 `
     -SiteUrl "tenant.sharepoint.com:/sites/SiteName" -AllLibraries -DryRun `
     -ExcludeLibraries 'Documents','Template','Dead Deals','IDV Property Stat Sheet'
   ```

4. **Run without `-DryRun`** once the dry-run output looks correct. A full run over 69 libraries makes
   ~420 Graph writes (one `contentTypesEnabled` PATCH plus five `addCopy` POSTs each) and may hit 429
   throttling. **Re-run it** after a throttle or transient failure rather than trying to work out how
   far it got — the idempotency guarantee is exactly what makes that safe.

5. **Create the "Under Review" filtered library view** — filter on `AIProcessingStatus = "Under
   Review"` ([ADR-006](decisions/adr-006-inline-review.md) — inline review, no separate list). Do this
   per content type introduced (see "Add a new content type" below) so reviewers get per-type views.

6. **Set folder default column values** for `State`, `PropertyName`, `ProjectName` on each project
   folder using `Set-PnPDefaultColumnValues`:
   ```powershell
   Set-PnPDefaultColumnValues -List "Documents" -Folder "TX DFW Risinger" -Field "State" -Value "Texas"
   ```
   ⚠️ **Folder defaults are not retroactive** — they only stamp items added *after* they're set.
   Existing documents need a one-time back-fill.

7. **Map columns to managed properties** in the SharePoint search schema — tenant-admin task,
   required for Microsoft 365 Copilot grounding.

---

## Keeping it in sync

Three sync axes exist around the taxonomy, and only the first two are enforced anywhere:

| Axis | Enforced by | Failure if it drifts |
|---|---|---|
| AI output ↔ `taxonomy.yaml` | enum deserialization (types), `TryMatchAllowedValue` (fields) | throws / field omitted |
| script arrays ↔ deployed SharePoint | `Provision-SharePointSchema.ps1` converge pass, **when re-run** | whole PATCH 400s |
| `taxonomy.yaml` ↔ script arrays | **nothing** — manual discipline only | whole PATCH 400s |

All three were verified aligned on 2026-09-17.

### Gotchas

| # | Trap | What happens | Rule |
|---|---|---|---|
| **A** | **Each library holds its own copy of a Choice list.** A site-scope column patch does NOT reach existing libraries | Patching the site column alone looks correct in the admin UI and changes nothing for the pipeline, which writes to **list** columns | Never hand-patch the site column. Re-run `Provision-SharePointSchema.ps1` — it reconciles both scopes. Verified 2026-09-17: site column 24→25 left all 69 libraries on 24 |
| **B** | **`DocumentType` is the one Choice column written unguarded** | Taxonomy content fields are dropped on mismatch (`TryMatchAllowedValue`); `DocumentType` is not, so one missing choice value costs the document **every** field, not one | See "Why a missing choice value is worse than it looks" below |
| **C** | **Unknown column in a PATCH fails the entire PATCH** | One bad field name loses *every* field for that document | Any new taxonomy field needs its column provisioned before use |
| **D** | **Concurrency must match quota** | `BatchMaxConcurrency` above what TPM supports → 429s, backoff, slower overall | See [runbook-operations.md, Throughput and quota](runbook-operations.md#throughput-and-quota) |

### Why a missing choice value is worse than it looks

The write-back is **one** `PATCH .../items/{id}/fields` carrying every field. Graph validates the
whole body, so a single unacceptable value rejects **all** of it — the document ends up with no
metadata at all, not merely a blank `DocumentType`.

There **is** a guard in the code, but read carefully what it protects against. Every taxonomy content
field with `allowed_values` goes through `TryMatchAllowedValue`, and unparseable `dateTime` / `number`
values are skipped — an unmatched value is **omitted** from the payload, so the rest of the document's
metadata still lands.

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

1. `TrySetContentTypeAsync` runs first — if it fails, it **swallows** the error and logs a warning
   (see "Add a new content type" below for why this one specifically is soft-failing).
2. The fields PATCH 400s. `SdkExceptionHelper` logs the Graph error code and rethrows.
3. Durable retries the activity **3 times** (5s, 10s, 20s). A 400 is deterministic, so all 3 fail.
4. `DocumentOrchestrator` catches `TaskFailedException`, sets `WriteBackSucceeded = false`, and
   records status `write-back-failed`.

The end state is a document **correctly typed** (e.g. `Reports`) with **zero metadata** — which looks
like a partially-working pipeline rather than a schema problem. Check the batch report's
`write-back-failed` count and look for the Graph error code in App Insights; the fix is to deploy the
missing choice value, then re-run those documents.

**Why `TryMatchAllowedValue` can't simply be reused for `DocumentType`.** It validates against the
taxonomy, and for `DocumentType` the taxonomy always agrees — Agent 1's output is an enum, so
deserialization already rejects anything the taxonomy lacks. A taxonomy-based guard would never fire
on the case that actually hurts, which is taxonomy-vs-SharePoint drift. Guarding this properly means
reading the deployed column's `choice.choices` at runtime and omitting `DocumentType` when the value
isn't there — cached per list, the same shape as the existing `ContentTypeCache` in the same activity.
Not implemented as a per-document guard; `ValidateSharePointSchemaActivity` covers it at the *batch*
level instead — see [runbook-operations.md](runbook-operations.md), it aborts a batch before any
document is touched if the deployed `DocumentType` column (or any taxonomy choice field) is missing a
required value on that library.

The **pre-flight batch check catches this before it costs anything**; the per-document guard described
above would only reduce the blast radius of a drift that slipped past it (e.g. a value added to
SharePoint mid-batch). Re-running provisioning is the mitigation for both.

---

## Add a new document type

A new label under an **existing** content type/group (e.g. adding "Environmental Survey" as a new
kind of Report). Needs a code change.

1. `taxonomy.yaml` → new entry under `document_types` with `label`, `group`, `description`, optional
   `decision_rules`. `group` must be one of the 5 existing content type names (see "Add a new content
   type" below if it doesn't fit any of them).
2. `Models/Enums.cs` → matching enum member:
   ```csharp
   [JsonStringEnumMemberName("My New Type")] MyNewType,
   ```
   **The string must match the YAML `label` exactly.**
3. `Provision-SharePointSchema.ps1` → add the label to `$documentTypes`.
4. Rebuild, redeploy, **re-run provisioning** — step 3 alone changes nothing in SharePoint.

> **Step 4 is not optional and used to be silently skippable.** The provisioning script was
> create-only: it reported `[EXISTS] Document Type` and never touched the deployed choice list, so a
> new type could sit in all three code locations while SharePoint rejected it. The script now
> converges choice lists on every run (site column *and* every library's own copy), so re-running is
> what deploys the value. Confirm with `-DryRun` first: a clean run prints no `Would update choices`
> lines.

Two related sharp edges:

- **A missing `[JsonStringEnumMemberName]` silently changes the label.** Without it the enum
  serializes as the C# identifier — `Cmt` instead of `CMT` — which SharePoint then rejects. The
  attribute is what makes the label match; it isn't decoration.
- **`AIProcessingStatus` is also written unguarded** (`"Classified"` / `"Under Review"`), but is safe
  only because those two literals happen to be in the provisioned choice list. If you ever rename
  them, rename the column's choices in the same commit.

To tune classification accuracy for the new type without a rebuild, add `decision_rules` — see
[runbook-configuration.md](runbook-configuration.md#tune-classification-accuracy-yaml-only--no-rebuild).

---

## Add a new content type

A genuinely new category — none of the 5 existing groups (`Contracts`, `Drawing Files`, `Reports`,
`Budget Files`, `Other`) fit the new document type(s) you're adding, and reviewers would benefit from
seeing a distinct set of columns for it. Rarer than adding a document type; per
[ADR-009](decisions/adr-009-sharepoint-content-types.md), the bar is real column-visibility
differentiation — the 23-content-types alternative was rejected because 22 of the 23 document types
would have been identical shells with no benefit.

1. **Pick the column set.** Every content type gets `$ctCommonCols` (AI operational + folder-derived +
   universal + property columns) automatically. Decide what else it needs from the existing groups —
   `$ctTransactionCols` (dates, parties, money — used by `Contracts`, `Reports`, `Other`),
   `$ctDrawingCols` (`Discipline`, `SheetNumber`, `DrawingTitle` — used by `Drawing Files` only), or a
   brand-new column set if the type needs fields none of the existing ones cover (add those columns to
   `$columns` first, per [runbook-configuration.md, "Add a metadata field"](runbook-configuration.md#add-a-metadata-field-yaml--provisioning-only)).

2. **`Provision-SharePointSchema.ps1`** → add an entry to `$contentTypeDefs`:
   ```powershell
   @{ name = "My New Category"; columns = $ctCommonCols + $ctTransactionCols }
   ```
   **The name must exactly match the `group` value used in `taxonomy.yaml`.** There is no separate
   mapping table — `WriteMetadataActivity` resolves the content type to set on a document via
   `taxonomy.GetGroupForDocumentType(documentType)`, i.e. the taxonomy `group` string *is* the content
   type name. A cosmetic rename on either side breaks resolution silently (see step 4).

3. **`taxonomy.yaml`** → set `group: "My New Category"` on the document type(s) that belong to it (new
   or existing — see "Add a new document type" above for adding one from scratch).

4. **Rebuild, redeploy, re-run provisioning.** This creates the new site content type, provisions its
   columns, and adds it to every target library.

5. **Create a per-type "Under Review" library view** for the new content type, same as step 5 under
   "Running it" above.

### This fails softer than a missing `DocumentType` value — know the difference

Unlike the `DocumentType` choice column (gotcha B above, no guard, fails the *whole* write-back),
`WriteMetadataActivity.TrySetContentTypeAsync` **swallows** a content-type resolution failure:

```csharp
if (!map.TryGetValue(group, out var contentTypeId) || string.IsNullOrEmpty(contentTypeId))
{
    logger.LogWarning("Skipping content type PATCH for item {ItemId}: content type {Group} not found...");
    return;
}
```

If step 2/3 above drift out of sync — the taxonomy `group` doesn't match any provisioned content
type's name — the document silently lands on the default `Document` content type with **all its
metadata fields still written correctly**. No batch failure, no `write-back-failed` count, nothing in
the report to flag it. The only symptom is a document that should show up under "My New Category" in
SharePoint's content-type-scoped views but doesn't — worth checking for after provisioning a new
content type, since nothing else will surface the mistake.

---

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Batch fails immediately, "SharePoint schema validation failed" | Named Choice column(s) missing a value on **this library's own copy** — re-run provisioning |
| Whole write-back fails, no fields saved | A column in the PATCH doesn't exist, or a Choice value isn't in its allowed list — see "Why a missing choice value is worse than it looks" |
| Document has correct type + metadata, but isn't under the expected content type in SharePoint | Content type name / taxonomy `group` drifted apart — see "This fails softer" above |
| `[EXISTS]` printed but SharePoint still rejects a new choice value | Old provisioning script behavior (create-only) — confirm you're on the converge-pass version (`a745570`+) |
| Dry-run shows unexpected `Would update choices` on every run | Taxonomy and script arrays have genuinely drifted — diff `taxonomy.yaml`'s `allowed_values`/`document_types` against the script's `$documentTypes`/choice arrays |
