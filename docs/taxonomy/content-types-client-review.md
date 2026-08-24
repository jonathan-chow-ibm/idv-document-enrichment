# Document Categories & Tagging — For Your Review

**Purpose:** This confirms how we will automatically categorize and tag your documents in SharePoint so
they're easy to filter, search, and use with Microsoft 365 Copilot. It is built **directly from the
"Metadata Request" file your team sent us**, using real documents from your Risinger (Fort Worth) deal
so the examples are familiar. The questions at the end are the decisions we need from you to finalize it.

---

## How to read this

Every document gets a set of tags (a "tag" is a column in SharePoint you can filter and search on). The
tags come from three places, and knowing which matters because it affects accuracy and cost:

| Source | How it's populated | Examples |
|---|---|---|
| **Your folders** | Read automatically from the folder path — no guessing | State, Property, Project |
| **SharePoint** | Already tracked by the system | Title, Author, Last Modified |
| **The document** | Read from the document content by the AI | Status, Counterparty, dates, prices, property details |

This split keeps location/project tags 100% consistent (they come from your folder structure), and reserves
the AI for what only reading the document can tell us.

---

## 1. Document categories

These mirror the groups in your Metadata Request:

**Contracts:** PSA – Acquisition · PSA – Disposition · Lease · Lease Amendment · Vendor Contract ·
Commission Agreement · Loan Agreement · JV Agreement · Development Agreement · Letter of Intent · Term Sheet

**Drawing Files:** Survey · Plat · Design Drawing *(with discipline: Architectural, Civil, Landscape,
Structural, Mechanical, Electrical, Plumbing)*

**Reports:** Closing Statement · Environmental Survey · Geotechnical Report · Easement Document

**Budget Files:** Proforma · Budget / Cost Estimate · Bid Tab · Draw Request · Operating Budget

**Other:** anything that doesn't fit the above (see Question 1).

> **Scope note — pro formas & financial models.** Pro formas and budget spreadsheets will be **categorized
> and searchable** like any other document. Automatically extracting the financial *metrics inside* them
> (IRR, yield-on-cost, NOI, cap rate) is **not** part of this pass: these models vary significantly across
> projects and vintages, and a wrong financial figure is worse than none. We propose metric extraction as a
> validated later enhancement — see Question 6.

> **Scope note — design drawings.** Drawings are image/CAD-based, so we capture *what kind* of drawing it is
> plus headline figures from the title block — not every detail. See Question 3.

---

## 2. The metadata we'll capture

One consistent set applied across all documents (not every field applies to every document — a purchase
price won't appear on a survey, and that's expected).

**From your folders (automatic):** State · Property Name · Project / Development Name

**From SharePoint (automatic):** Document Title · Author · Last Modified

**Read from the document by AI:**
- *Every document:* Document Status (Draft / Executed / Final / Superseded) · Counterparty · Transaction
  Type · Execution / Effective / Expiration dates
- *Property documents:* Property Address · Parcel ID · County · City / ETJ · Acres · Square Footage ·
  Land Use · Zoning · Opportunity Zone
- *Transaction documents:* Seller · Buyer · Broker · Title Company · Closing Date · Purchase Price ·
  Earnest Money · Deposit · Contract Value
- *Ownership (if present):* Entity Name · Investor / Fund

**Transaction Type values** (from your file): Acquisition · Disposition · Lease · Easement ·
Development Agreement · Loan · Construction Contract

**How you'll use it:** filter a library to "all Executed contracts for Risinger," find every financing
document by lender, or ask Copilot "which Fort Worth properties are in an Opportunity Zone" — without
opening files.

---

## 3. Decisions we need from you

1. **Categories you didn't list, but that are in your files.** Your Risinger folder also contains
   **marketing flyers, proposals/pitch decks, correspondence (emails), permits, entity/formation documents,
   and market reports.** They aren't in the Metadata Request. Do you want these tracked as their own
   categories, or left under "Other" for now?

2. **Location tagging.** Your file lists **County, City/ETJ, State** (not "submarket"). We'll pull **State
   and Property from your folder names** automatically, and read **County / City** from the documents.
   Please confirm that County + City + State is the right way to tag location for your team.

3. **Design drawings are tagged, not deeply read.** Drawings (site plans, CAD, discipline sets) are
   image-based, so we reliably capture *what kind* of drawing it is and headline figures from the title
   block, but not every detail. Highly graphical sheets may need a quick manual confirmation. Is that
   acceptable?

4. **Confidentiality.** Your list doesn't include a confidentiality tag. We recommend handling sensitivity
   with Microsoft's built-in sensitivity labels rather than having the system guess a level on every file.
   Confirm, or tell us if you want a confidentiality tag.

5. **Priority / first pass.** We suggest starting with **Contracts and Reports** (text-rich, cleanest
   results), then classifying Budget Files and Drawings — with Budget Files categorized and searchable but
   their internal financial metrics deferred (see scope note above), and Drawings tag-only. Does that match
   where the value is for you?

6. **Pro forma metrics as a later enhancement.** Do you want automated extraction of the financial metrics
   *inside* pro formas/models (IRR, yield-on-cost, NOI, cap rate) added as a scoped fast-follow — and at what
   priority? We've confirmed your models differ across projects and vintages, so this needs a careful,
   validated build rather than a quick pass.

---

*Prepared as a working proposal for confirmation. Once approved, these categories and tags become the basis
for automatic tagging across your document libraries.*
