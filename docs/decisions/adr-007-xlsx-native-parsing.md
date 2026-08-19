# ADR-007: Native XLSX Parsing for Spreadsheet Documents

## Status

Accepted

## Date

2026-08-17

## Context

The pipeline extracts document content via Azure AI Document Intelligence
(`prebuilt-layout`, Markdown output) before the two-agent classify → extract flow.
A subset of the client's active project files are Excel workbooks — the client
refers to them collectively as "pro forma," but the format actually spans multiple
document types (pro formas, rent rolls, operating statements / T-12s, budgets, cash
flow models, debt schedules, comp sets). "Excel" is a **container format, not a
document type.**

Document Intelligence v4.0 (`2024-11-30` GA) *does* accept XLSX for the Read and
Layout models — the API will not reject the file. However, for Office formats it
"extracts all embedded text as is" and explicitly returns **no bounding polygon, no
`lines` object, and no page-range support** (per the v4.0 Read model docs). For a
spreadsheet this means the rich structure that gives a financial workbook meaning —
sheet boundaries, row/column headers, formula-evaluated values vs. formula strings,
named ranges — is flattened into an unstructured text stream. The extraction agents
would receive de-contextualized numbers and produce low-confidence results, inflating
the human-review queue for exactly the documents (financials) where accuracy matters
most.

Two facts about the current implementation shape the fix:

1. `ExtractContentActivity` analyzes documents **by URL** and already emits content in
   **Markdown** (`DocumentContentFormat.Markdown`). The downstream content contract is
   therefore already markdown — a spreadsheet renderer only needs to produce markdown
   tables to slot in without changing Agent 1 / Agent 2.
2. The pipeline assumes **one file = one document = one classification**. This holds for
   PDFs but can break for workbooks that contain multiple document types across tabs.

## Decision

Branch on **file extension** (not MIME type — SharePoint/Graph MIME reporting is
inconsistent) inside content extraction. For Open XML spreadsheets (`.xlsx` and `.xlsm`),
**skip Document Intelligence** and parse the workbook natively with **ClosedXML**
(MIT-licensed, no service dependency), serializing each worksheet to a labelled Markdown
table (sheet-name headers + formula-evaluated cell values). The serialized markdown flows
into the existing `classify_type` (Agent 1) and `extract_metadata` (Agent 2) activities
unchanged. All other formats continue through Document Intelligence as today.

`.xlsm` (macro-enabled) is structurally identical to `.xlsx` plus an embedded
`vbaProject.bin`; ClosedXML reads its worksheet data identically and ignores the macro
payload. Because the pipeline never opens the file in Excel or executes VBA — ClosedXML
only reads XML parts — there is **no macro-execution risk**.

Legacy binary formats **`.xls`** (pre-2007) and **`.xlsb`** (binary workbook) are **not
readable by ClosedXML** and are explicitly routed to human review rather than silently
failing the parse. If they prove common, revisit with a library that supports them
(e.g., NPOI) or a conversion step.

**Multi-sheet policy (v1):** treat the whole workbook as a single logical document and
produce one classification. Workbooks that legitimately contain multiple document types
across tabs are accepted as-is; when the classifier or extractor confidence falls below
threshold (the common outcome for a mixed-purpose workbook), the document routes to human
review via the existing confidence gate. Per-sheet classification is explicitly deferred.

## Rationale

- **Format-driven, not type-driven** — one content-type branch handles every kind of
  spreadsheet; no separate "Excel pipeline" and no fragmentation of the single
  classification path. The diversity of Excel *document types* is absorbed by the existing
  taxonomy-based classifier, not by new plumbing.
- **Preserves structure DI discards** — sheet names, headers, and formula-evaluated values
  survive, which is precisely what the agents need to reason over financial tables.
- **Fits the existing contract** — the pipeline already speaks Markdown; ClosedXML emitting
  markdown tables requires no downstream change.
- **Cheaper and simpler** — eliminates a Document Intelligence call for these files; no new
  Azure service, endpoint, or async run/thread model to operate or hand over.
- **Robust to unknown structure** — generic per-sheet serialization assumes no fixed layout,
  which suits a corpus whose structural consistency is not yet known.
- **Lowest-complexity v1** — whole-workbook single classification avoids fan-out and
  multi-result write-back; the confidence gate already handles the mixed-workbook tail.

### Known risk: structural serialization may underperform on complex financials

The documents in scope are commercial real estate financial models — pro formas, rent
rolls, T-12 operating statements, debt schedules. These are not simple data tables.
They routinely feature merged cells, hidden rows/columns, multi-tab cross-references,
spatially meaningful layouts (e.g., a cap rate is the value to the right of a label on
row 47), and 50+ column widths with hundreds of rows. Serializing that to markdown
tables produces a wall of decontextualized numbers. Classification will likely succeed
(sheet names alone often reveal document type), but metadata extraction may struggle,
pushing a significant portion of spreadsheets to human review — undermining the
automation goal for the documents where it matters most.

This is accepted as a v1 trade-off: start simple, measure real-world confidence scores,
and escalate to an agent-based approach (Alternative C) when the data justifies it.
The architecture should make that escalation easy — see Consequences.

## Alternatives Considered

### A. Let XLSX pass through Document Intelligence unchanged
- Rejected as the primary path: the API accepts the file but flattens spreadsheet structure,
  producing low-confidence extractions and a swollen review queue for financial documents.
  Retained implicitly as the "do nothing" baseline for measuring quality if desired.

### B. Native XLSX parsing with ClosedXML → markdown tables
- **Adopted.** See Decision / Rationale.

### C. Azure AI Foundry agent with Code Interpreter (pandas/openpyxl)
- **Deferred, not dismissed.** This is the stronger approach for complex financial
  workbooks — Code Interpreter can programmatically navigate sheets via pandas/openpyxl,
  find named ranges, identify header rows, and trace cell relationships rather than
  reconstructing that context from serialized text. However, it introduces a new service
  dependency, an async run/thread model unlike the current Durable activity pattern,
  per-session billing, and higher latency.
- **Likely near-term escalation path:** if real-world pro formas produce low confidence
  scores through the ClosedXML path (expected for complex financials), this becomes the
  primary extraction method for spreadsheets. Design the content-extraction abstraction
  so swapping in an agent-based extractor requires only a new activity implementation,
  not pipeline restructuring.

### D. XLSX → CSV, then feed to agents (or to Document Intelligence)
- Rejected: same effort as ClosedXML markdown, marginally worse for GPT header inference,
  and no structural advantage over emitting markdown tables directly.

### Per-sheet classification (multi-sheet policy)
- Deferred: more robust for multi-type workbooks but adds fan-out and multi-result
  write-back complexity against a single SharePoint file. Revisit if SMEs report that
  multi-purpose workbooks are common.

## Consequences

- `ExtractContentActivity` gains an extension-based branch; the spreadsheet path fetches
  the workbook binary (Document Intelligence takes a URL; ClosedXML reads a stream) and
  serializes sheets to Markdown. Branch matches `.xlsx` and `.xlsm`; `.xls` and `.xlsb`
  route to review.
- New dependency: `ClosedXML` NuGet package.
- **Upstream ingestion — external dependency (not owned by this team):** SharePoint Online
  remains the system of record. Source files that currently live on a **Citrix ShareFile**
  server (organized per real-estate project) are migrated into SharePoint document libraries
  *before* the pipeline runs. **This migration is owned by a separate party**, and the
  pipeline itself does not integrate with ShareFile (Microsoft Graph cannot reach it). The
  pipeline's input therefore depends on that migration succeeding — a cross-team dependency
  to track as a risk, not a task on this backlog. Two constraints must be **handed off to
  the migration owner**, since they cannot be remediated in pipeline code:
  1. **Preserve original file extensions** (a `.xlsm` must stay `.xlsm`, or be deliberately
     converted to `.xlsx`) so the extension-based branch fires correctly. Files landing as
     an opaque or renamed type would bypass the spreadsheet path.
  2. **Confirm the SharePoint tenant does not block macro-enabled (`.xlsm`) uploads** via
     blocked-file-type policy. If it does, either relax the policy for the target libraries
     or convert `.xlsm` → `.xlsx` during migration; otherwise those files never arrive.
- Password-protected or corrupt workbooks fail the parse and route to review (must be
  handled explicitly, not thrown as unhandled exceptions).
- Embedded charts, images, and pivot caches are not serialized (values are); acceptable for
  metadata extraction.
- **Taxonomy gap to close:** the taxonomy is PDF-centric. "Financial Analysis" covers pro
  formas, but Excel-native types (rent roll, operating statement / T-12, budget) have no
  distinct home and would currently land in "Financial Analysis" or "Other." Raise in the
  taxonomy sessions (tasks 2.1 / 2.2); may add document types and per-type extraction
  templates (task 2.9).
- **Classification prompt:** Agent 1 examples (task 2.4) should include Excel-serialized
  (markdown-table) samples, which read differently from PDF prose.
- **Open question for SMEs:** are workbooks single-purpose or multi-purpose across tabs, and
  is their structure consistent across deals? One client conversation answers both and
  validates the whole-workbook v1 assumption.
- New unit tests: markdown serialization of a representative multi-sheet `.xlsx`, and the
  password-protected / corrupt failure path.
- **Escalation readiness:** the content-extraction interface should be abstract enough that
  replacing ClosedXML serialization with a Foundry agent call (Alternative C) for
  spreadsheets requires only a new activity implementation behind the same contract —
  not a pipeline redesign. Track spreadsheet confidence scores separately from
  PDF scores to build the data case for when (not if) escalation is needed.
