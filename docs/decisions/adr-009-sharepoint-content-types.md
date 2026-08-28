# ADR-009: SharePoint Content Types Grouped by Taxonomy Category

## Status

Accepted

## Date

2026-08-28

## Context

The SOW deliverable (a) specifies metadata taxonomy "aligned to SharePoint content types." The current
implementation creates 39 columns directly on the document library with no content types — every
document uses the default `Document` type and all columns are visible on every document regardless of
type.

This has three consequences:
1. Reviewers editing a design drawing see PurchasePrice, EarnestMoney, and TitleCompany — irrelevant
   fields that add noise.
2. The SOW alignment requirement is unmet.
3. Retrofitting content types after a 117K-document batch means either re-running the pipeline (full
   token cost) or a bulk-update pass. Doing it before the first batch means every document gets typed
   on first write, for free.

The taxonomy already carries a `group` field per document type: Contracts, Drawing Files, Reports,
Budget Files, Other. These groups originate from the client's own Metadata Request.

## Decision

Create **5 SharePoint content types**, one per taxonomy group, with columns assigned per group. The
`DocumentType` Choice column (24 values) remains on all content types as the fine-grained filter.

### Content type structure

| Content Type | Taxonomy group | Additional columns beyond universal |
|---|---|---|
| Contracts | Contracts | `content.transaction` + `content.ownership` |
| Drawing Files | Drawing Files | Discipline, SheetNumber, DrawingTitle |
| Reports | Reports | `content.transaction` (easements/closings carry dates and parties) |
| Budget Files | Budget Files | Universal + property only |
| Other | Other | `content.transaction` (Other is the catch-all; keep all columns available) |

All 5 content types inherit: AI operational columns (DocumentType, AIConfidence, AIProcessingStatus,
AIClassifiedDate, SuggestedFields, AIOriginalClassification), folder-derived columns (State,
PropertyName, ProjectName), system columns (SourceSystem), and all `content.universal` + `content.property`
fields.

### Content type naming convention

Content types are named **exactly** as the taxonomy `group` values. This is deliberate —
`taxonomy.GetDocumentType(dt)?.Group` is the content type name, with no mapping table. Renaming
content types for cosmetic reasons would break resolution.

### Two-PATCH write-back pattern

Setting content type on a SharePoint item requires two separate Graph API calls in order:

```
// 1. Set content type (listItem property, not a field)
PATCH /sites/{siteId}/lists/{listId}/items/{itemId}
{ "contentType": { "id": "0x0101..." } }

// 2. Set field values (existing call, unchanged)
PATCH /sites/{siteId}/lists/{listId}/items/{itemId}/fields
{ "DocumentType": "PSA - Acquisition", "AIConfidence": 0.95, ... }
```

Content type must be set **first** so type-specific columns are valid for the assigned type. These
cannot be merged into a single call — `contentType` is a listItem property, not a field value.

### Content type ID resolution

Content type IDs are library-specific GUIDs. `WriteMetadataActivity` resolves them at runtime via
`GET /sites/{siteId}/lists/{listId}/contentTypes`, cached per `listId` using a thread-safe
`ConcurrentDictionary<string, Lazy<Task<Dictionary<string, string>>>>` pattern. If resolution fails
for a document, the content type PATCH is **skipped** (never send null/empty) and fields are still
written — the document lands on the default Document type with all metadata intact.

### Relationship between DocumentType and content type

These are **complementary, not redundant**:
- `DocumentType` (24 values, Choice column): fine-grained classification for filtering and search
  ("show me all PSA - Acquisitions")
- Content type (5 values): coarse grouping that drives forms, column visibility, and views
  ("show me all Contracts")

Neither should be removed in favour of the other.

## Alternatives Considered

### 23 individual content types (one per document type)
- Maximum column specificity per type.
- Rejected: only one document type (Design Drawing) has type-specific fields. The other 22 would be
  identical shells with the same columns, creating 23 definitions to maintain across three sync
  points (enum, YAML, provisioning script) with no column-visibility benefit.

### Flat columns with no content types (previous approach)
- Simplest to implement and maintain.
- Rejected: does not meet SOW deliverable (a), provides no column visibility differentiation for
  reviewers, and retrofitting after the batch is expensive.

### Managed properties scoped per content type
- Considered as a Copilot benefit.
- Rejected: managed properties are tenant-wide, mapped per column, not per content type. The mapping
  work is proportional to columns (39) regardless of content type count.

## Consequences

- Provisioning script (`Provision-SharePointSchema.ps1`) creates 5 content types and enables content
  type management on the library, in addition to columns.
- `WriteMetadataActivity` makes two Graph API calls per document (doubles write-back call volume).
  The Graph SDK's default retry middleware handles 429/503 throttling with `Retry-After`.
- Content type resolution cache is needed — thread-safe, keyed by listId.
- Partial failure (content type set, fields failed) results in `WriteBackSucceeded = false` — visible
  in the batch report.
- Content types must be provisioned **before** the batch runs. Without them, every document silently
  lands on the default Document type.
- Per-type library views should be created for reviewers (manual or scripted).
- Multi-site: content types don't cross site collections. Run provisioning per site, or set up a
  Content Type Hub for central publishing (deferred).
- GraphServiceClient uses the default middleware pipeline (no custom retry handler needed — confirmed
  by code inspection, `Program.cs` line 32).

## Prerequisites

Before implementing, verify with a spike:
- Can the managed identity set `contentType` on a listItem with only `Sites.ReadWrite.All`?
  If not, the MI permission grant must be widened to `Sites.Manage.All`, which reopens the security
  conversation.

## Build Order

1. Permission spike — PATCH `contentType` on one item using MI credentials
2. Provisioning script rewrite — create content types + columns + enable content type management
3. WriteMetadataActivity — two-PATCH pattern + content type ID cache
4. Per-type library views (manual or scripted)
