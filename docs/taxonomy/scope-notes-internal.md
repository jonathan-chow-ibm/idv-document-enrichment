# Taxonomy v4 — Internal Scope Notes (delivery team)

Technical backing for the two client-facing docs ([content-types-client-review.md](content-types-client-review.md)
and [capability-summary-client.md](capability-summary-client.md)). Audience: delivery team. Status: taxonomy
[taxonomy.yaml](taxonomy.yaml) is **Pending SME review**; pipeline is wired to v4 and, as of 2026-09-18,
has run against live Azure OpenAI and Document Intelligence across four production batches
(~10,600 documents total) — see [runbook-failures.md](../runbook-failures.md) for what that surfaced.

---

## 1. Why v4 exists

v4 was rebuilt from the client's `Meta Data Request.xlsx` (their document types + requested metadata), then
cross-checked against the real `TX DFW Risinger` and `TX HOU Flowserve` sample projects. It replaces the
v3 taxonomy, which was our inferred/brokerage-oriented model.

## 2. What changed v3 → v4

- **Document types → the client's groups & granularity.** Contracts split to their level (PSA Acq/Dispo,
  Lease, Lease Amendment, JV, Development, Vendor, Commission, Term Sheet all separate); added Design Drawing
  (+ discipline), Bid Tab, Draw Request. Enum `DocumentType` (Enums.cs) rewritten to 24 v4 labels.
- **Single consistent metadata set** (universal / property / transaction / ownership), replacing per-type
  field lists. `MetadataExtractionResult` is now a `Fields` dictionary; schema builder flattens all content
  fields + type-specific fields into one `fields` object.
- **Provenance model** — every field is `folder` / `system` / `content`. Location/project come from the
  folder path (deterministic); Author/Modified from SharePoint; the rest from AI.
- **Submarket removed** → folder-derived **State** + content **County/City** (see §4).
- **Confidentiality removed** → not requested by the client, and it was the #1 review-queue driver
  (unreliable AI guess, e.g. 0.70–0.80 with "no explicit clause"). Recommend Purview sensitivity labels.
- **DealType → Transaction Type** with the client's 7 values (adds Easement, Construction Contract;
  Acquisition/Disposition instead of generic "Sale"; Loan instead of Financing).
- Routing rule: only **universal** fields, or non-universal fields with a non-empty value, gate review —
  prevents the ~25-field set from sending every document to review (RouteResultActivity).

## 3. Types the client omitted but that exist in their files

Confirm at the SME session — do **not** silently drop. Currently route to **Other**:
Marketing Flyer, Proposal / Pitch Deck, Correspondence (emails), Permit / Municipal Approval,
Entity / Corporate Governance, Market Report, Listing Agreement, Title Commitment/Policy.
(Their list also omits any confidentiality tag — see §2.)

## 4. The County/City gap

Client's location model is **State · County · City/ETJ · Parcel** — there is **no "metro/submarket"** in
their schema. Folder path gives State + metro (HOU/DFW) + Project. So:
- **State, Property, Project** → folder-derived (deterministic).
- **Metro** (HOU/DFW) → useful navigation/grouping, but *not* a field they asked for.
- **County, City/ETJ, Parcel** → must come from **document content** (title/survey/plat/ESA carry them) or a
  metro→county lookup. Reliable on DD docs, not universal. **Open decision for the client.**

## 5. Feasibility findings behind the capability ratings

- **Classification is strong** — test runs hit 0.95–0.99 and correctly caught a mislabeled file by content
  (a title policy named "REVISED PROFORMA"). This is why categorization is the broad, committable capability.
- **Format coverage** — of 839 sample files, ~5% are format-blocked for content reading: `.doc` (legacy Word;
  DI supports `.docx` only), `.msg`, `.dwg`, `.mpp`, `.zip`. These route to review or need conversion.
  The ~5% was counted while `.pptx` was still format-blocked, so it now slightly overstates the gap —
  PowerPoint is converted to PDF and processed. Recount against the 839-file sample if the figure matters.
  Note: `pdftotext`-measured "0-text" PDFs (scanned drawings) will recover text via Document Intelligence OCR.
- **Drawings are image/CAD** — classify the drawing *type* from the title block; discipline + deep fields are
  unreliable; pure-raster sheets → review.
- **Pro forma metric extraction is deferred** — the models are **not** a single template:
  - Risinger = `DevModel_v7` (2025): 37 sheets, metrics on "Bank Budget"/"Assumptions", labels like
    "Yield on Cost", "Deal Level IRR".
  - Flowserve = "Project Estimate & Proforma" (2014): 12 sheets, metrics on "Bank Summary"/"10 yr Proforma",
    labels like "Yield on AIC", "Project Unleveraged XIRR".
  - Neither has usable named ranges (4,295 in Risinger, all junk/FactSet/legacy; 20 in Flowserve, all
    loan-math). So **cell-maps and fixed sheet-name selection won't generalize.** The viable path is
    **content-based sheet selection + LLM semantic extraction** (maps varying labels to canonical fields),
    built and validated as a later phase. Financial values demand "accurate or nothing."
  - Also: heavy **version sprawl** (dozens of proforma variants per project) → a document-selection /
    source-of-truth problem for any per-property rollup.

## 6. Pipeline state (v4)

- **Wired end-to-end and builds clean** (0 warnings/errors): Enums, AgentResults, Taxonomy, TaxonomyLoader,
  MetadataSchemaBuilder, ExtractMetadata.hbs, PromptRenderer, RouteResultActivity, WriteMetadataActivity.
- **Deserialization verified offline** — real taxonomy.yaml loads into the v4 model (24 types; content
  universal=6/property=9/transaction=9/ownership=2); enum labels with `/` and `-` round-trip through
  System.Text.Json.
- **Provisioning** — [Provision-SharePointSchema.ps1](../../scripts/Provision-SharePointSchema.ps1) updated to
  the 37 v4 columns; separate review/corrections lists dropped (ADR-006 inline review).
- **SpreadsheetExtractor** — priority-sheet budget assembly (GHCP) as a fallback for non-template
  spreadsheets; note the priority-share dilution on many-tab workbooks and the two coupled 24K limits
  (SpreadsheetExtractor + TextUtils).

## 7. Known wiring gaps / follow-ups

- **Folder-derived fields** (State/Property/Project) need population via SharePoint **folder default column
  values** (`Set-PnPDefaultColumnValues`) or a `parentReference.path` parse step; defaults are **not
  retroactive**, so the initial batch needs an explicit back-fill.
- **Design Drawing `discipline`** — write-back writes the raw field key `discipline`; provisioning creates
  internal name `Discipline`. Case mismatch — rename one to match before that field will populate.
- **Not yet run against live Azure services** — categorization confidence and field accuracy claims are
  based on prior test-trigger runs + offline validation; a live pass per category is the validation gate.
- **County/City sourcing** (§4) and **Confidentiality approach** (Purview) — open client decisions.
- **Pro forma metric extraction** and a **per-property "memory"/rollup** tier — proposed later phases.
