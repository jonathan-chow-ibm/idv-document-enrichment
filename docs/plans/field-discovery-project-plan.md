# Project Plan: Per-Type Attribute Discovery (Pre-Full-Run Step)

> **Revision note (2026-09-14).** This plan was rewritten after the requester clarified the
> actual goal, and after verifying that clarification against the code. The first draft framed
> discovery as "unlock `suggestedFields` for the Other / low-confidence documents Agent 2 skips."
> That framing was too narrow. The real goal is **discovering type-specific attributes** for
> document types whose metadata set is currently universal — which applies to *confidently
> classified* documents too, and is a **discrete step run before a full production run**, not an
> ongoing pipeline mode. See §11 for what changed and why.

## 1. Goal

Discover the type-specific metadata attributes each document type actually carries — today every
type is scored against one universal field set — so the taxonomy can be enriched per type before
committing to a full production run.

## 2. Users & Problem

Design Thinking Facilitator was deliberately **not invoked**: the users are an unambiguous internal
group (the requester's team running the discovery batch; taxonomy SMEs who approve additions to
`taxonomy.yaml`). There is no persona or journey to uncover.

- Point of View: *The taxonomy team needs to see which attributes are actually present per document
  type — beyond the universal set — so they can author `specific_fields` from evidence before a full
  run bakes in a one-size-fits-all schema.*
- **Verified premise (this is the crux).** The taxonomy is genuinely universal across types today:
  - [`docs/taxonomy/taxonomy.yaml`](../taxonomy/taxonomy.yaml) header states it outright — *"Metadata
    is ONE consistent set applied across all types, grouped by how each field is populated."*
  - `specific_fields` appears **zero times** in `taxonomy.yaml`, so `DocumentTypeDefinition.SpecificFields`
    is always empty.
  - Therefore [`MetadataSchemaBuilder.BuildSchema`](../../src/IdvEnrichment.Functions/Shared/MetadataSchemaBuilder.cs)
    produces an **identical schema for every document type** — its `documentType` parameter is
    effectively inert. The same is true of the per-type branch in `PromptRenderer`.
- Consequence: the discovery population is **not** limited to Other / low-confidence documents. A
  `Lease` classified at 0.95 confidence runs Agent 2 today and is still scored only against universal
  fields. Type-specific discovery applies across the whole corpus.

## 3. Answer to the Question Asked

> *"Should this be part of the classify agent, or should we set up a new one?"*

**Neither.** The capability already exists in Agent 2 plus the batch report, and it is already wired
per document type end to end:

| Piece | Status | Location |
| --- | --- | --- |
| Agent 2 must return `suggestedFields` (`key`/`value`/`confidence`) | **Built** — required property in the strict schema | `MetadataSchemaBuilder.BuildSchema` |
| Per-document capture into the batch result | **Built** | [`ChunkOrchestrator.cs:86`](../../src/IdvEnrichment.Functions/Orchestrators/ChunkOrchestrator.cs#L86) |
| Aggregation by taxonomy group → by document type → top 10 keys ranked by document frequency | **Built** | [`GenerateBatchReportActivity.cs:41-61`](../../src/IdvEnrichment.Functions/Activities/GenerateBatchReportActivity.cs#L41-L61) |
| HTML "Suggested Fields" section per type | **Built** | `GenerateBatchReportActivity` (`SuggestedFieldsByGroup`) |
| Consumption path for the answer (`specific_fields` → schema **and** prompt) | **Built, unpopulated** | `MetadataSchemaBuilder`, `PromptRenderer` |
| **Example values** per suggested field | **Designed but always empty** | `SuggestedFieldEntry.ExampleValues` is always passed `[]` because `ChunkOrchestrator.cs:86` projects keys only (`.Select(f => f.Key)`), discarding `f.Value` |

So a third agent would duplicate Agent 2's job, and extending Agent 1 (the cheap classification
model, on truncated text) would load a second responsibility onto a production-critical asset for a
temporary goal. Both subagents independently rejected the Agent 1 route.

**The gap is not an agent. It is three small things:** a way to run the discovery batch as a distinct
pass, example values carried through so the output is actually authorable, and someone writing
`specific_fields` into the taxonomy from the result.

## 4. Shape of the Work: An Intermediate Step, Not a Mode

Per the requester's framing, this is a **discrete step before the full run**, and the flag is the
right mechanism precisely because it is a **separate batch**:

```
discovery batch (type-stratified sample, classification-only write-back)
    → per-type Suggested Fields report (with example values)
    → SME authors specific_fields in taxonomy.yaml
    → deploy (taxonomy is NOT hot-reloaded)
    → full production run, now with per-type schemas and prompts
```

`BatchRequest` is already how a separate batch is launched and already carries exactly this kind of
run-shaping flag (`ClassifyOnly`, `MaxPages`), so a `discoveryMode` flag follows the established
pattern. Note it is close to the **inverse** of `ClassifyOnly`: a discovery run *wants* Agent 2 to
run. That is a further reason not to overload `ClassifyOnly`.

## 5. Business Case (from Product Coach)

**Value proposition.** Reconnecting and enriching an existing, already-built reporting path is a
small, bounded, batch-only cost — the right cost shape for a temporary initiative. The output has a
ready-made destination (`specific_fields`), so discovery converts directly into production behavior
with no new consumer to build.

**Stakeholders & incentives.**
- *Benefit:* the requester's team; taxonomy SMEs; the org indirectly (better per-type metadata, fewer
  review-routed documents once `specific_fields` is populated).
- *Bear cost:* the Azure OpenAI budget owner (capped, batch-only, full-model spend); engineering
  (build + removal — easy to underweight); taxonomy SMEs, who also sit on the **cost** side, since
  every discovery batch adds to their authoring/review queue.

**Recommendation: Proceed with conditions.** Conditions, in priority order:
1. **Decide how to take the first look.** A normal batch over a type-stratified sample already populates
   the per-type Suggested Fields table today — but not for free: it commits the full write-back and marks
   the sampled documents durably processed (§7.1, §7.2). Either sample documents you are willing to burn,
   or build `discoveryMode` first. This is Decision #1.
2. Carry **example values** through before scaling — field *names* alone are weak evidence for
   authoring a field definition with `allowed_values` and `decision_rules`.
3. A written "done" criterion and a **named removal owner** for anything temporary, decided at build
   time rather than left to memory.
4. Confirmed SME authoring cadence before running batches wider than a sample — discovery output that
   nobody converts into `specific_fields` is inventory, not progress.
5. Explicit budget sign-off. The downstream-consumer question on the `SuggestedFields` column stands
   either way, since discovery runs still write that column (§7.1) — but the broader concern about
   research-grade metadata landing in content columns is resolved by the classification-only write-back.

## 6. System Context (from System Thinking)

- **Boundary.** Inside: `ExtractMetadataActivity`, `MetadataSchemaBuilder`, `PromptRenderer`,
  `TaxonomyLoader`/`taxonomy.yaml`, `ChunkOrchestrator`, `GenerateBatchReportActivity`, the
  `skipExtraction`/`ClassifyOnly` gate at [`DocumentOrchestrator.cs:109-174`](../../src/IdvEnrichment.Functions/Orchestrators/DocumentOrchestrator.cs#L109-L174).
  Outside: the discovery team, taxonomy SMEs/governance, the real-time production trigger (must not
  regress).
- **Key feedback loops.**
  1. *Universal-Schema Lock-in* (reinforcing — **the dominant loop, and the one this plan targets**):
     no `specific_fields` → identical schema and prompt for every type → Agent 2 is never asked for
     type-specific attributes → they surface only as unstructured `suggestedFields` nobody converts →
     `specific_fields` stays empty.
  2. *Discovery Starvation* (reinforcing, secondary): Other / low-confidence documents are gated away
     from Agent 2, so they contribute nothing to discovery — real, but now the secondary concern.
  3. *Cheap Sweep Blindness* (balancing, by design): `ClassifyOnly` sweeps are cheapest but skip Agent 2
     entirely, yielding zero field-level signal.
  4. *Cost Tier Escalation* (balancing): discovery runs on the full-price model, trading directly
     against how fast loop 1 resolves.
- **Delays.** `TaxonomyLoader` uses `Lazy<Task<TaxonomyData>>` (`PublicationOnly`) — the taxonomy is
  **not hot-reloaded**, so every finding reaches production only after SME authoring *and* a redeploy.
  Running discovery faster than that human-plus-deploy cadence builds backlog, not results. This is
  also *why* the pre-full-run sequencing in §4 is the right shape.
- **Leverage points, in order.** (1) Carry example values through the keys-only projection — one line,
  no LLM cost, and it is what makes the report authorable. (2) Type-stratified sampling, so every type
  is represented rather than whatever a folder happens to hold. (3) `discoveryMode` on `BatchRequest`
  to shape the separate pass (sample, classification-only write-back, optionally include the Other /
  low-confidence population). (4) Only then consider relaxing the Agent 2 gate.

## 7. Scope

### In scope

- A **type-stratified discovery batch** over a sample, read through the existing per-type Suggested
  Fields report — attempted first with **no code changes at all**.
- Carrying **example values** through `ChunkOrchestrator`'s suggested-fields projection so
  `SuggestedFieldEntry.ExampleValues` stops being empty.
- A `discoveryMode` flag on `BatchRequest`/`QueueMessage`/`ChunkRequest` (default `false`, batch trigger
  only, never the real-time path) shaping the discovery pass: per-type sampling cap, and
  **classification-only write-back** — see §7.1.

### 7.1 Write-back shape on the discovery path

A discovery run should write back the **classification**, not the extracted metadata field set. The
metadata was scored against a universal schema and will be re-extracted properly once `specific_fields`
exists, so persisting it is churn at best. `BuildFieldsPayload` in
[`WriteMetadataActivity`](../../src/IdvEnrichment.Functions/Activities/WriteMetadataActivity.cs) already
separates these concerns, so this is a narrow change rather than a new write path:

| Payload block | Discovery run | Note |
| --- | --- | --- |
| `DocumentType`, `AIConfidence`, `AIClassifiedDate`, `AIOriginalClassification` | **Write** | The classification is real, paid-for, production-valid output |
| `SuggestedFields` | **Write** | Already in the unconditional block — the discovery payload lands per-document for free, alongside the aggregated report |
| `AIProcessingStatus` | **Skip** | Already gated on `!result.ClassifyOnly`; the same guard must hold here, or Power Automate treats the document as permanently handled and it never gets a real pass |
| `taxonomy.ContentFields()` loop (every content column) | **Skip** | The block to suppress |

The `ContentFields()` loop is the specific hazard, not just noise. Its `else` branch assigns `rawValue`
**unconditionally**, so free-text columns are written even when the value is empty — meaning a discovery
run would actively **blank** existing free-text columns. Typed columns (Choice / dateTime / number) are
omitted when unparseable, so they fail safe; free-text ones do not.

### 7.2 The recorded status must be non-terminal

There is a second side-effect, outside SharePoint, that matters just as much. A normal run records
`"success"` or `"review"` via `RecordProcessingResult`, and
[`FilterProcessedActivity`](../../src/IdvEnrichment.Functions/Activities/FilterProcessedActivity.cs)
treats exactly those two as **durably terminal** (`SkippableStatuses`), partitioned by library so the
skip survives across runs. So a discovery pass that records a normal status would permanently exclude
every sampled document from future batches — leaving them with universal-only metadata forever, which is
precisely the outcome this initiative exists to prevent. Power Automate's `AIProcessingStatus` guard
compounds it from the other side.

`ClassifyOnly` already solves this: it records `"classified-only"`, which is deliberately outside
`SkippableStatuses`. `DocumentOrchestrator` documents the reasoning inline — *"a test run must never
block a real one from processing these documents later."* A discovery run needs the same treatment.

### 7.3 Why the flag is needed

The three requirements above define the flag precisely: **`discoveryMode` is `ClassifyOnly`'s
side-effect profile with Agent 2 switched on.**

| | `ClassifyOnly` | Normal run | `discoveryMode` |
| --- | --- | --- | --- |
| Agent 2 runs (→ `suggestedFields`) | No | Yes | **Yes** |
| Content columns written | No | Yes | **No** |
| `AIProcessingStatus` set | No | Yes | **No** |
| Recorded status durably terminal | No | Yes | **No** |

Neither existing path produces that column. `ClassifyOnly` is safe but blind; a normal run sees but
commits. So something has to signal the combination — which is the flag, and it is why the retracted
instinct that "Agent 2 already does this" is only half true: Agent 2 already *produces* what is wanted,
but only on a run whose side effects burn the documents being studied.
- Optionally, within that flag: include Other / low-confidence documents by relaxing `skipExtraction`,
  with a per-type cap.
- Authoring `specific_fields` per type in `taxonomy.yaml` from the report output — **this is the actual
  deliverable**; everything above is instrumentation.
- A tracked removal task for the flag, tied to a written done-criterion.

### Out of scope (and why)

- Changing Agent 1 / `ClassifyTypeActivity` — cheap model, truncated text, production-critical; wrong
  place for a second responsibility serving a temporary goal.
- A new standalone discovery agent — would duplicate Agent 2 and still have to feed the same report.
- Any change to the real-time (non-batch) trigger path.
- Automated taxonomy updates — authoring `specific_fields` stays a human SME step, and the taxonomy is
  not hot-reloaded regardless.
- Design Thinking research — see §2.

## 8. Milestones

1. **Probe run — not free, choose deliberately.** A normal sample batch with today's code does populate
   the per-type Suggested Fields table, but it also commits the full write-back and records a durably
   terminal status (§7.1, §7.2), permanently burning the sampled documents from both Power Automate and
   future batches. So either (a) run it on a throwaway sample whose final metadata genuinely does not
   matter, or (b) skip straight to Milestone 3 and take the first look through `discoveryMode`. Exit: a
   judgment on whether field names alone are authorable — and an explicit choice of (a) or (b).
2. **Example values plumbed** — `ExampleValues` populated end to end. Exit: report shows sample values
   per suggested field per type.
3. **`discoveryMode` implemented** — flag + per-type sampling cap + classification-only write-back (§7.1),
   handed to `spec.architecture` for design review first, per System Thinking's recommendation. Exit:
   architecture review clean; a discovery batch writes classification and `SuggestedFields` but provably
   leaves content columns and `AIProcessingStatus` untouched.
4. **`specific_fields` authored & deployed** — SMEs convert report output into per-type field definitions;
   redeploy so `BuildSchema`/`PromptRenderer` differentiate by type. Exit: schema provably differs
   between at least two document types.
5. **Full run** — production run with per-type schemas. Exit: full run complete.
6. **Removal executed** — `discoveryMode` deleted once the done-criterion is met. Exit: removal PR merged.

## 9. Assumptions to Test Early

- *Field names alone are enough to author `specific_fields`.* → Milestone 1 output. → Success: SMEs can
  draft at least one type's fields from names only. If not, Milestone 2 becomes a prerequisite, not an
  enhancement.
- *Confidently-classified documents yield enough type-specific signal.* → Check whether Milestone 1's
  report is populated and varied per type. → Success: distinct top-field sets across types. If the sets
  look identical across types, that is evidence the universal prompt is anchoring Agent 2's suggestions
  and the prompt needs a discovery variant.
- *Discovery is a one-or-two-time step, not ongoing.* → Decision #1 below. → Success: a documented answer.
- *SMEs have capacity to author `specific_fields` promptly.* → Ask for a committed cadence before
  scaling. → Success: a stated turnaround.

## 10. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
| ---- | ---------- | ------ | ---------- |
| Agent 2's universal prompt anchors `suggestedFields` toward universal fields, suppressing the type-specific ones we are hunting | **Medium-High** | High — this would make discovery output misleadingly thin | Inspect Milestone 1 output for cross-type sameness; if present, add a discovery-specific prompt variant behind `discoveryMode` |
| "Temporary" flag becomes permanent debt | High (precedented — `taxonomy.yaml`'s own header records types dropped to Other "for now, flagged for SME review") | Medium | Written done-criterion + named removal owner + calendar checkpoint at build time |
| Field names without example values aren't authorable, wasting the first batch | Medium | Low-Medium | Milestone 1 is a cheap sample, explicitly framed as a probe; Milestone 2 is one line if it fails |
| Discovery outpaces SME authoring capacity | Medium | Medium | Confirm cadence before widening; taxonomy is not hot-reloaded, so pace to the deploy cycle |
| Full-model cost outruns visible value | Medium | Medium | Per-type sampling cap; sample-sized batches; budget sign-off before the first real run |
| New gate/flag regresses the real-time production path | Low | High | Default `false`; batch trigger only; test asserting the real-time path never sets it |
| Discovery run **blanks** existing free-text content columns — the `ContentFields()` loop's `else` branch writes `rawValue` unconditionally, empty included | **Medium-High if write-back is left alone** | High — silent data loss on already-populated columns | Classification-only write-back (§7.1): suppress the `ContentFields()` loop under `discoveryMode`. Test asserting no content column appears in the payload |
| Discovery run sets `AIProcessingStatus`, causing Power Automate to skip the document forever | Low | High | Reuse the existing `!result.ClassifyOnly` guard condition; cover with a test |
| Discovery run records `"success"`/`"review"`, so `FilterProcessed` permanently excludes the sampled documents from future batches | **High if status is left alone** | High — the sampled documents keep universal-only metadata forever, the exact failure this initiative targets | Record a non-terminal status under `discoveryMode`, mirroring `ClassifyOnly` → `"classified-only"` (§7.2); test asserting the status is outside `SkippableStatuses` |

## 11. Decisions Required

- **Decision #1:** How is the first look taken — a throwaway sample through today's normal batch path
  (accepting that those documents are burned per §7.1/§7.2), or build `discoveryMode` first and look
  through it? — Owner: requester, immediately. *Note this is no longer "do we need the flag." The
  side-effect analysis in §7.1–7.3 settles that: any repeated or corpus-representative discovery pass
  needs it. The open question is only whether one throwaway probe comes first.*
- **Decision #2:** Written done-criterion for "taxonomy work concluded," and named removal owner. —
  Owner: requester, before Milestone 3.
- **Decision #3:** SME cadence for converting report output into `specific_fields`. — Owner: SMEs, before
  batches wider than a sample.
- **Decision #4:** Budget sign-off for the discovery batches. — Owner: budget owner, before Milestone 3.
- **Decision #5:** Should the discovery pass also cover Other / low-confidence documents (relaxing
  `skipExtraction`), or only confidently-classified ones? — Owner: requester, at Milestone 3. Deferred
  deliberately: the type-specific goal is served by the classified population, and adding the starved
  population is a separable increment.

## 12. What Changed From the First Draft

| First draft | This draft | Why |
| --- | --- | --- |
| Goal: unlock `suggestedFields` for Other / low-confidence docs | Goal: discover **type-specific** attributes across the corpus | Requester clarified; `specific_fields` verified empty, so every type shares one schema |
| Discovery population = documents Agent 2 skips | Population = **all** types, primarily confidently-classified ones | A well-classified `Lease` is equally starved of type-specific fields |
| `discoveryMode` = "relax the Agent 2 gate" | `discoveryMode` = "shape a separate discovery batch" (sampling, no write-back); gate relaxation is an optional increment | Requester's framing: an intermediate step before the full run, run as a separate batch |
| Alternative floated: one-time manual/offline audit | Superseded by Milestone 1 — a **no-code** sample batch through the existing report | The report already aggregates per type, so the cheap probe needs no offline tooling |
| `exampleValues` gap | Newly identified: always `[]` because `ChunkOrchestrator.cs:86` projects keys only | Found while verifying the requester's clarification |
| Dominant loop: Discovery Starvation | Dominant loop: **Universal-Schema Lock-in**; Discovery Starvation demoted to secondary | Follows from the corrected premise |
| Discovery runs "skip SharePoint write-back" | **Classification-only write-back** — write `DocumentType`/confidence/`SuggestedFields`, suppress the content-column loop and `AIProcessingStatus` (§7.1) | Requester's refinement; the classification is valid paid-for output, and `BuildFieldsPayload` already separates the blocks |
| Milestone 1 framed as a free, no-code look | Reframed as a **deliberate probe with real cost** | Verified that a normal run both blanks free-text columns and records a durably-terminal status (§7.1, §7.2), burning the sampled documents |
| Flag justified loosely as "it's a separate batch" | Flag justified precisely: it is **`ClassifyOnly`'s side-effect profile with Agent 2 on** — a combination no existing path produces (§7.3) | Emerged from tracing write-back and `FilterProcessed` side effects |

## 13. Artifacts

- Design Thinking: not run — rationale in §2.
- Product Coach: synthesized inline in §5.
- System Thinking: [docs/design/field-discovery-system-map.md](../design/field-discovery-system-map.md)
  — note §6 and §11 supersede its framing of the dominant loop and of `discoveryMode`'s purpose.
