# What You'll Get — Automated Categorization & Tagging

**Companion to the "Document Categories & Tagging — For Your Review" document.** That one lists the
decisions we need from you; this one summarizes what the system will deliver, built from your Metadata
Request and validated against real documents from your Risinger and Flowserve projects.

---

## In plain terms

Every document in your libraries will be automatically **categorized** (what kind of document it is) and
**tagged** (searchable/filterable properties). Tags come from three sources:

- **Your folders** → State, Property, Project — read from the folder path, 100% consistent, no guessing.
- **SharePoint** → Title, Author, Last Modified — already tracked.
- **The document** → Status, Counterparty, dates, parties, prices, property details — read by AI.

The result: you can filter a library ("all executed contracts for Risinger"), find documents by lender or
property, and ask Copilot questions grounded in these tags — without opening files.

---

## What's included

### ✅ Fully supported now

| Capability | Detail |
|---|---|
| **Categorize Contracts** | PSA (Acquisition/Disposition), Lease, Lease Amendment, Vendor, Commission, Loan, JV, Development, LOI, Term Sheet |
| **Categorize Reports** | Closing Statement, Environmental Survey, Geotechnical Report, Easement Document |
| **Categorize Budget Files** | Proforma, Budget/Cost Estimate, Bid Tab, Draw Request, Operating Budget |
| **Location & project tags** | State, Property, Project — automatic from folders |
| **Universal tags** | Document Status (Draft/Executed/Final/Superseded), Counterparty, Transaction Type, key dates |
| **Transaction tags** | Seller, Buyer, Broker, Title Company, Closing Date, Purchase Price, Earnest Money, Contract Value — on the documents that contain them |
| **Property tags** | Address, Parcel ID, County, City/ETJ, Acres, Square Footage, Zoning — from title, survey, plat, and environmental documents |

### ⚠️ Partial — supported with limits

| Capability | Limit |
|---|---|
| **Categorize Drawings** | We reliably tag *what kind* of drawing it is and headline figures from the title block; the engineering *discipline* and fine details are less reliable, and heavily graphical sheets may need a quick manual check. |
| **Property tags on non-DD documents** | Parcel ID, Zoning, Acres, etc. appear on title/survey/plat/environmental documents — they'll be blank on documents that don't contain them (expected). |
| **County / City** | We read State and Property automatically from folders; County and City are read from document content (reliable on title/survey/plat). *This is one item we'd like your input on.* |

### ⛔ Deferred — proposed as a later, validated phase

| Capability | Why deferred |
|---|---|
| **Financial metrics inside pro formas** (IRR, yield-on-cost, NOI, cap rate) | Your financial models differ significantly across projects and vintages — the 2025 Risinger model and the 2014 Flowserve model are structured completely differently and label the same metrics differently. Reliable extraction needs a careful, validated build; a wrong financial figure is worse than none. Pro formas are still **categorized and searchable** — only the internal metrics are deferred. |
| **Legacy / non-text formats** (`.doc`, `.msg` emails, `.dwg` CAD, `.pptx`, `.mpp`) | These formats can't be read directly (~5% of files). They'll be flagged for review or need conversion. |

---

## Two things worth knowing

- **A blank tag is usually correct, not a miss.** A survey has no purchase price; a proforma has no seller.
  The value is that each document carries the tags it *should*, and the folder-based tags (State, Property,
  Project) are consistent everywhere.
- **Categorization is broad and reliable; deep field extraction depends on the document.** Naming the
  document works across almost everything readable. Pulling a specific field works wherever that field
  actually lives in the document.

---

## What this means for you

You get a **fully categorized, searchable, Copilot-ready document library** with consistent location/project
tags and the transaction, property, and status fields from your Metadata Request — delivered first on the
text-rich documents (contracts, reports), with drawings tagged at a useful level and pro forma financial
metrics available as a follow-on if you want them.

*See the companion review document for the handful of decisions we need to finalize this.*
