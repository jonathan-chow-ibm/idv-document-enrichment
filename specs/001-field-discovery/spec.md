# Feature 001: Per-Type Field Discovery — Report Fixes

**Status**: implemented
**Branch**: `001-field-discovery`

## Goal

Make the existing batch report good enough to answer one question: **which metadata fields are
specific to each document type?** The answer feeds `specific_fields` in
`docs/taxonomy/taxonomy.yaml`, which is currently empty for every type — so today every document
type gets an identical universal schema.

## Why no new flag, agent, or pipeline path

The discovery capability already exists. Agent 2's schema requires a `suggestedFields` array, and
`GenerateBatchReportActivity` already groups those suggestions by taxonomy group and document type.
Running a batch over a stratified sample already produces the raw material.

What was broken was the *report*, not the pipeline. An earlier draft of this feature proposed a
`discoveryMode` flag threaded through `BatchRequest` → `ChunkRequest` → `QueueMessage` →
`EnrichmentResult`, plus a new non-terminal processing status, trigger-level conflict validation and
a config setting — roughly 15 requirements — to avoid two side effects of using the normal batch
path for a discovery run. Both turned out not to justify it:

- **Content-column write-back.** A discovery run and a normal run write back identically: Agent 2
  runs, and `BuildFieldsPayload` writes what it extracted. Suppressing content columns would have
  been a preference, not a safety fix.
- **Terminal processing status.** `FilterProcessedActivity` does durably skip documents recorded as
  `success`/`review`, so a sampled document would not be re-processed later. The remedy is deleting
  those rows from the `ProcessingTracking` table (partition = library key, row key = document ID) —
  an operational step, not a code change.

Since the taxonomy work is temporary, the flag was also the *worst* part of the scope to build: it
touched seven files and would have had to be unpicked from all seven. The report changes below are
permanent improvements worth keeping regardless.

## What changed

1. **Example values now reach the report.** `ChunkOrchestrator` projected
   `SuggestedFields.Select(f => f.Key)`, discarding every value, so `SuggestedFieldEntry.ExampleValues`
   was structurally always empty. `BatchDocumentEntry.SuggestedFieldKeys` (`IReadOnlyList<string>`) is
   now `SuggestedFields` (`IReadOnlyList<SuggestedField>`); up to 3 distinct example values are shown
   per field. A key name alone often does not say what a field holds.

2. **No top-N truncation.** The aggregation capped each document type at its 10 most frequent keys
   with no trace of what was dropped. Frequency-ranked truncation removes the low-frequency tail,
   which is exactly where type-specific fields live. Every distinct key is now reported.
   `SuggestedFieldsByDocumentType.TopFields` is renamed `Fields` to match.

3. **Ranked by exclusivity, not frequency.** New `SuggestedFieldEntry.OtherTypeCount` records how
   many *other* document types suggested the same key, computed across the whole batch. Fields no
   other type suggested sort first. A field in 3 of 40 Leases and nowhere else is a far better
   `specific_fields` candidate than one suggested for every type.

The HTML report renders one table per document type (Field / Docs / Other Types / Example Values)
instead of a single comma-joined cell, with exclusive fields marked.

## Files changed

| File | Change |
|---|---|
| `Models/BatchReport.cs` | `SuggestedFields` replaces `SuggestedFieldKeys`; `Fields` replaces `TopFields`; new `OtherTypeCount` |
| `Orchestrators/ChunkOrchestrator.cs` | Carry full `SuggestedField` records instead of keys only |
| `Activities/GenerateBatchReportActivity.cs` | New testable `BuildSuggestedFieldsByGroup`; cap removed; exclusivity ranking; per-type HTML tables |
| `Tests/GenerateBatchReportActivityTests.cs` | 9 tests covering values, dedup/cap, no truncation, cross-type counting, ranking, grouping, blank keys, empties |

## How to use it

1. Run a batch over a stratified sample across document types via the existing batch trigger — no
   new parameters.
2. Read `{yyyy-MM-dd}/{batchId}/report.html` (or `report.json`) in blob storage. Work down each
   document type's list from the top: `Other Types = 0` fields are the `specific_fields` candidates.
3. Author `specific_fields` in `docs/taxonomy/taxonomy.yaml`. `MetadataSchemaBuilder.BuildSchema` and
   `PromptRenderer` already consume it — the per-type branches exist and are currently inert only
   because the field is empty.
4. Deploy. `TaxonomyLoader` uses `Lazy<Task<TaxonomyData>>`, so the taxonomy is **not** hot-reloaded.
5. To re-process the sampled documents under the new taxonomy, delete their `ProcessingTracking`
   rows first.

## Out of scope

No changes to Agent 1 or classification prompts, no new discovery agent, no `discoveryMode` flag, no
changes to the real-time trigger path, no automated taxonomy mutation.

## Follow-up found during planning (unrelated to this feature)

`HttpEnrichTrigger` deserializes the raw HTTP body straight into `QueueMessage` with no field
allow-listing, so `classifyOnly` is already settable per-document on the real-time path by any caller
holding the function key. Pre-existing, worth its own ticket.

## Background

Full reasoning, business case and system analysis: [../../docs/plans/field-discovery-project-plan.md](../../docs/plans/field-discovery-project-plan.md)
and [../../docs/design/field-discovery-system-map.md](../../docs/design/field-discovery-system-map.md).
