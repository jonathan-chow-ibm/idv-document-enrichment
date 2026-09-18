# Runbook — Extraction Metadata Reference

Every field the pipeline puts on a document, where it comes from, and what makes a value "good enough."
This is the technical reference; the *how-to-change-it* steps stay in
[runbook-configuration.md](runbook-configuration.md), and the client-facing *why* stays in
[taxonomy/capability-summary-client.md](taxonomy/capability-summary-client.md) and
[taxonomy/content-types-client-review.md](taxonomy/content-types-client-review.md).

---

## Five sources, only one of them AI

A document's metadata comes from five places. Only one involves the AI pipeline at all — worth knowing
before assuming every column on a document was "read" by something.

| Source | Fields | AI involved? | Confidence threshold? |
|---|---|---|---|
| **Folder path** | `State`, `PropertyName`, `ProjectName` | No — parsed once from `{State} {Metro} {Project}` | None |
| **SharePoint / file system** | `Title` (file name), `Author`, `Modified`, `SourceSystem` | No — already tracked | None |
| **Agent 1 (classification)** | `DocumentType`, `AIConfidence`, `AIProcessingStatus`, `AIClassifiedDate`, `AISuggestedType` | Yes | `agent1_classification: 0.80` |
| **Agent 2 (content extraction)** | Everything under `content.*` below | Yes | Per-field, see "Confidence thresholds" |
| **Agent 1's vision path (drawings only)** | `Discipline`, `SheetNumber`, `DrawingTitle` | Yes — a *different* call than content extraction | Its own `confidence` field, not gated the same way |

The drawing fields are easy to miss: they don't come from Agent 2 at all. `DrawingClassification` is
produced by classifying the rendered first page of a Design Drawing
([`AgentResults.cs:52-62`](../src/IdvEnrichment.Functions/Models/AgentResults.cs#L52-L62)), and Design
Drawing has `ExtractionEnabled: false` in the taxonomy — Agent 2 never runs for that type at all
(`GetTypeExtractionPolicyActivity.IsExtractionEnabled`). If a new document type needs both structured
content fields *and* is image-first like a drawing, that combination doesn't exist today; it would need
deciding whether Agent 2 runs alongside or instead of vision.

Folder-derived and system fields carry **no confidence threshold** — they're not a guess, so there's
nothing to gate on.

---

## The four `content` groups

Agent 2's output is organized into four groups in `taxonomy.yaml`'s `metadata.content`, and which group a
field is in changes its behavior, not just its organization:

- **`universal`** — expected on nearly every document. An empty value here **gates review**.
- **`property` / `transaction` / `ownership`** — sparse by design. An empty value is skipped and does
  **not** force review; a document doesn't need a `PurchasePrice` to be a valid, fully-processed lease
  amendment.

> Putting a field in the wrong group has real cost: `runbook-configuration.md` documents an incident
> where a date field in `universal` put ~70% of a corpus into the review queue for being correctly
> empty. Dates and financial fields belong in `transaction`.

### `universal`

| Field | Column | Type | Notes |
|---|---|---|---|
| `documentStatus` | `DocumentStatus` | `allowed_values`: Draft, Executed, Final, Superseded | Decision rules: executed/DocuSign→Executed, recorded→Final, marked draft/redline→Draft |
| `counterparty` | `Counterparty` | freetext | External party opposite the client |
| `transactionType` | `TransactionType` | `allowed_values`: Acquisition, Disposition, Lease, Easement, Development Agreement, Loan, Construction Contract | Prior from the numbered project subfolder (`03-Acq`→Acquisition, etc.), overridden only if content clearly disagrees |

### `property`

| Field | Column | Type |
|---|---|---|
| `propertyAddress` | `PropertyAddress` | freetext |
| `parcelId` | `ParcelID` | freetext |
| `county` | `County` | freetext |
| `cityJurisdiction` | `CityJurisdiction` | freetext |
| `acres` | `Acres` | number |
| `squareFootage` | `SquareFootage` | number |
| `landUse` | `LandUse` | freetext |
| `zoning` | `Zoning` | freetext |
| `opportunityZone` | `OpportunityZone` | `allowed_values`: Yes, No, Unknown |

### `transaction`

| Field | Column | Type |
|---|---|---|
| `executionDate` | `ExecutionDate` | dateTime |
| `effectiveDate` | `EffectiveDate` | dateTime |
| `expirationDate` | `ExpirationDate` | dateTime |
| `seller` | `Seller` | freetext |
| `buyer` | `Buyer` | freetext |
| `broker` | `Broker` | freetext |
| `titleCompany` | `TitleCompany` | freetext |
| `closingDate` | `ClosingDate` | dateTime |
| `purchasePrice` | `PurchasePrice` | freetext |
| `earnestMoney` | `EarnestMoney` | freetext |
| `depositAmount` | `DepositAmount` | freetext |
| `contractValue` | `ContractValue` | freetext |

### `ownership`

| Field | Column | Type |
|---|---|---|
| `entityName` | `EntityName` | freetext — e.g. "IDV Risinger, LLC" |
| `investorFund` | `InvestorFund` | freetext |

`ownership` is the smallest group and the newest — carried over from a Copilot-suggested field list,
flagged in the taxonomy comments as "validate at SME review."

---

## Confidence thresholds

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

Any field without its own entry falls back to `default`. **Any single low-confidence field sends the
whole document to Review** — there's no partial-credit routing. Tuning guidance (per-field thresholds,
the `county` frequent-flagger case) is in
[runbook-configuration.md, "Adjust review volume"](runbook-configuration.md#adjust-review-volume-thresholds) —
this doc is the field reference, that one is the dial.

---

## Where this maps to code

- **`Models/Taxonomy.cs`** — `TaxonomyConfig`/`DocumentTypeDefinition`/`FieldDefinition` deserialize
  `taxonomy.yaml` directly; field/group names here are the YAML keys, not renamed.
- **`TaxonomyData.ContentFields()`** — flattens all four `content` groups into one list; this is what
  Agent 2's prompt is built from.
- **`TaxonomyData.UniversalFieldNames()`** — the set `universal` field names come from; this is
  specifically what routing checks before sending a document to Review for an empty required field.
- **`WriteMetadataActivity.BuildFieldsPayload`** — the actual mapping from an `EnrichmentResult` to the
  SharePoint fields PATCH body. AI operational columns (`DocumentType`, `AIConfidence`,
  `AIClassifiedDate`, `SuggestedFields`, `AIOriginalClassification`, `AISuggestedType`,
  `AIProcessingStatus`) are written directly from code; every `content.*` field is written generically
  via the taxonomy's `field_name` → `sharepoint_column` mapping, so a new field needs no new code here —
  see [runbook-configuration.md, "Add a metadata field"](runbook-configuration.md#add-a-metadata-field-yaml--provisioning-only).
- **`AIProcessingStatus` doubles as Power Automate's re-trigger guard** — see
  [architecture/power-automate-integration.md](architecture/power-automate-integration.md) — which is
  why it's deliberately left unset on a `classifyOnly` run (see
  [runbook-operations.md](runbook-operations.md)): setting it would tell the real event-driven flow this
  document was already handled and skip it forever.
