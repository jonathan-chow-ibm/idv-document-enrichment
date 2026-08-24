# Content Type Metadata Fields

> **Status:** Draft for SME review. Grounded in a real client deal folder (`TX DFW Risinger`), not theoretical.
> **Method:** Each content type below was derived by reading actual sample documents from the Risinger
> ground-up industrial development deal (Fort Worth / Tarrant County, TX). Fields marked *grounded* were
> observed in a real document; fields marked *inferred* could not be sample-verified (unreadable format).
> **Supersedes/expands:** the 10-type taxonomy in [taxonomy.yaml](taxonomy.yaml), which is brokerage/investment
> oriented and covers only ~half of a development deal's document set.

---

## 1. How fields are organized

Two classes of metadata, to avoid column sprawl (see the content-type discussion and ADR direction):

- **Shared fields** — extracted for *every* document, stored once, reused across content types:
  - `DealType`, `Submarket`, `Counterparty`, `Confidentiality` (the four universal taxonomy categories)
  - `Property/Address`, `DocumentDate`, `Status` (recurred in nearly every sample — recommend promoting to shared)
- **Type-specific fields** — unique to a content type; only surfaced when a document is that type (via its SharePoint content type).

Where a type-specific field is semantically identical across types (e.g. `TitleCompany`, `GF#`, `RecordingInfo`),
it should reuse a single site column.

---

## 2. Cross-cutting findings (read this first)

These surfaced in **every** analysis group and matter more than any single field list:

1. **🚩 The Submarket taxonomy does not cover this client's deals at all.**
   Every document is **Fort Worth / Tarrant County (DFW)**, but [taxonomy.yaml](taxonomy.yaml) lists only *Houston*
   submarkets. Real submarket values seen in the market comps: *NE Tarrant/Alliance, Meacham Fld/Fossil Cr,
   S Central Tarrant County, Southwest Tarrant, East Fort Worth*. **Submarket will resolve to "Unknown" for the
   entire deal** until the taxonomy is made metro-aware (Metro + Submarket) or a DFW set is added. This is the
   single biggest taxonomy gap.

2. **"Houston red herrings."** The client (IDV) is Houston-based (10375 Richmond Ave, Houston 77042). That Houston
   address appears as the *company* address on many docs (Certificate of Formation registered office, lender letter
   recipient), and one closing letter even mis-references "Harris County." A naive "extract the city" will mis-tag
   these Fort Worth deals as Houston. **Extract the *property/collateral* location, not the sponsor's mailing address.**

3. **Counterparty depends on which side the client is on, and entities multiply.** Across this one deal IDV appears as
   `IDV Development Services, LLC` (LOI purchaser, PSA seller), `IDV Risinger, LLC` (deed grantor, title vested owner,
   JV manager), and `Sealy IDV Risinger, LLC` (the JV vehicle). Counterparty resolution requires knowing IDV's role
   per document — the counterparty is *the other party*, which flips between buyer, seller, lender, and JV partner.

4. **Address / legal description is unstable.** "101 **W.** Risinger" vs "101 **E.** Risinger" vs legal-description-only
   (metes & bounds) vs "Burleson Rd." Several legal descriptions are deliberately *insufficient* ("to be described upon
   receipt of survey"). Recommend normalizing on **parcel/legal reference** (Abstract No. 751; Tarrant parcels
   03955508 / 01341227; deed D226009646) rather than street address for cross-document matching.

5. **DocumentDate is frequently multi-valued.** Contracts/deeds/title carry effective vs executed vs recorded vs issued
   dates. Pick one canonical rule per type (e.g. execution date for agreements, recording date for closing docs).

6. **Many high-value fields resist a single clean value** — tiered commissions, "least-of" loan amounts, financial
   metrics that are table cells, per-row confidentiality in comps. These should be captured as **text/summary**, not
   forced into a number, and will often land below confidence threshold → human review. That is correct behavior.

7. **Format limitations are real and concentrated in value.**
   - `.docx` (Development/JV agreements, many contracts) — **not readable** by the current extraction path; needs a DOCX text extractor. (Note: DI supports DOCX but *not* legacy `.doc`.)
   - `.pptx` (proposals/pitch decks) — not extractable without conversion; image/chart heavy.
   - `.msg` (much correspondence) — not readable; needs MSG→EML/PDF conversion.
   - `.xlsm/.xlsx` (financial models) — handled natively via ClosedXML (good).
   - **Drawings split into two classes:** native-CAD/vector PDFs (survey exhibits, typed plat pages) extract cleanly;
     rasterized/scanned architectural drawings yield only scrambled OCR fragments — enough to *classify* as a drawing,
     too sparse for field extraction → route to review.

8. **Filenames mislead — route on content, not name.** e.g. `Bank Budget.pdf` is actually a **title insurance policy**;
   the "Plat (old)" is a 2003 plat for a *different* tract/owner; the "Encroachment Agreement" file is only the survey
   exhibit, not the signed agreement.

---

## 3. Transaction & Legal

### 3.1 Listing Agreement
*Sample: `08-Listing Agreement\Risinger Listing Agreement - EXECUTED.pdf` (grounded — Newmark TX Exclusive Lease Listing form, DocuSigned)*

| Field | Example value | Kind | Reliability |
|---|---|---|---|
| Owner / Client | IDV Risinger, LLC | Type-specific | high |
| Listing Broker / Brokerage | Newmark Real Estate of Dallas, LLC | Shared → Counterparty | high |
| Assigned Agents | Weston King; Dalton Knipe | Type-specific | med |
| Broker License # | TREC #586696 | Type-specific | high |
| Property/Address | 101 W. Risinger Rd., Fort Worth, TX 76140 | Shared → Property/Address | high |
| County | Tarrant | Type-specific | high |
| Listing Type | Lease (exclusive) — with sale clause | Shared → DealType | med |
| Exclusivity Type | Exclusive Right | Type-specific | high |
| Listing Start / Expiration | 3/30/2026 → 3/30/2027 (1-yr) | Type-specific | high / med (derived) |
| Lease Commission Rate | 6.75% (tiered) | Type-specific | med |
| Sales Commission Rate | 3% of gross sales price | Type-specific | med |
| Protection / Tail Period | 180 days (lease) / 12 months (sale) | Type-specific | med |
| Execution Status | Executed (DocuSign) | Shared → Status | high |

- **Shared resolution:** DealType = Lease; Submarket = Fort Worth *(unmappable — Houston-only taxonomy)*; Counterparty = Newmark; Confidentiality = Confidential (§17).
- **Gotchas:** `ListingType` needs **Lease / Sale / Both** (this form has both); commission is tiered → store as text.

### 3.2 Letter of Intent
*Sample: `03-Acq Docs\25-0603 IDV LOI - W Risinger.pdf` (grounded — non-binding land purchase LOI, DocuSigned)*

| Field | Example value | Kind | Reliability |
|---|---|---|---|
| Property/Address | 101 W Risinger Rd, Tarrant County (±9.427 ac) | Shared → Property/Address | high |
| Purchase Price | ≤$3,700,000 / $392,489.66 per acre / $9.01/SF | Type-specific | high |
| Earnest Money | $40,000 | Type-specific | high |
| Feasibility Period | 60 days | Type-specific | high |
| Closing Timing | 30 days after feasibility | Type-specific | high |
| Title Company | Rattikin Title (Attn: David Bailiff) | Type-specific → shared `TitleCompany` | high |
| Broker / Commission | 3% gross to JLL, paid by Seller | Type-specific | high |
| Purchaser Entity | IDV Development Services, LLC | Type-specific | high |
| Binding Status | Non-binding | Type-specific | high |
| Execution Status | Executed ("Agreed and Accepted", 6/3/2025) | Shared → Status | high |

- **Shared resolution:** DealType = Acquisition; Submarket = Fort Worth *(unmappable)*; Counterparty = Seller side (Westbrook Companies / George Montague; signed Burl Hollingsworth); Confidentiality = none stated → Internal.
- **Gotchas:** Price is **tiered/multi-value** (pick total as canonical). Seller entity name never appears cleanly (addressee ≠ signatory).

### 3.3 Purchase & Sale Agreement
*Sample: `06-Dispo Docs\Bleecker\Contract\CCUP...EXECUTED.pdf` (grounded — TXR-1802 Commercial Contract, Unimproved Property + Special Provisions Addendum, DocuSigned)*

| Field | Example value | Kind | Reliability |
|---|---|---|---|
| Property/Address | ±0.6198 ac, Part of Lot 2 Blk 1 Hollingsworth Addn, Fort Worth/Tarrant | Shared → Property/Address | high |
| Sales Price | $243,432.00 (all cash) | Type-specific | high |
| Earnest Money | $15,000 | Type-specific | high |
| Independent Consideration | $100.00 | Type-specific | high |
| Feasibility Period | 7 days | Type-specific | high |
| Closing Date | January 16, 2026 | Type-specific | high |
| Title Company | Rattikin Title / David Bailiff | Type-specific → shared `TitleCompany` | high |
| Seller / Buyer Entities | Seller: IDV Development Services, LLC; Buyer: 9733 BP South LLC | Type-specific | high |
| Deed Type at Closing | Special Warranty Deed | Type-specific | high |
| Special Contingency | Contingent on Seller's acquisition under 24/7 Storage contract | Type-specific | high |
| Brokers | None used | Type-specific | high |
| Execution Status | Executed (DocuSign, both parties) | Shared → Status | high |

- **Shared resolution:** DealType = Disposition; Submarket = Fort Worth *(unmappable)*; Counterparty = 9733 BP South LLC (buyer; parent Bleecker Partners); Confidentiality = **Confidential** (Addendum §9, incl. buyer identity).
- **Gotchas:** DocumentDate ambiguous (effective §24 blank vs deed-referenced 12/24/2025 vs closing 1/16/2026). Legal description deliberately insufficient (pending survey). Counterparty is two-level (entity / manager / signer).

### 3.4 Development / JV Agreement
*Sample: `03-Acq Docs\JVA Exhibit E - Form Development Agreement - Sealy IDV Risinger...docx` (grounded via XML extraction — Read tool can't open .docx binary)*

| Field | Example value | Kind | Reliability |
|---|---|---|---|
| Property/Address | 8.81-acre parcel, Fort Worth, TX | Shared → Property/Address | high |
| Owner Entity | Sealy IDV Risinger, LLC (a Georgia LLC) | Type-specific | high |
| Developer Entity | IDV Development Services, LLC (Texas) | Type-specific | high |
| Manager / Investor Member | Mgr: IDV Risinger, LLC; Investor: Sealy SIP IV Risinger TRS, LLC | Type-specific | high |
| Development Fee | 5% of Hard Costs + Managed Soft Costs | Type-specific | high |
| Project Scope | One tilt-wall industrial building, ±134,094 SF | Type-specific | high |
| Key Persons (Change of Control) | D. Tyndall Yaap; Jarrad Coulter; Timothy Harrington | Type-specific | high |
| Term | Effective Date → Final Completion + fee reconciliation | Type-specific | med |
| Insurance Requirements | CGL $1M/occ, $2M agg; statutory WC | Type-specific | high |
| Execution Status | **Unexecuted form** (blank "___ day of August, 2026") | Shared → Status | high |

- **Shared resolution:** DealType = Development/JV; Submarket = Fort Worth *(unmappable)*; Counterparty = Sealy IDV Risinger, LLC (JV vehicle; Sealy is capital partner) — IDV is the *developer* here; Confidentiality = Confidential.
- **Gotchas:** Form/template, **not executed** → Status = Draft/Form. Multiple entities → Counterparty resists single value. Three geographies in one doc (property Fort Worth, Owner a Georgia LLC, addresses Houston). Requires a **DOCX extractor**.

### 3.5 Closing Document
*Samples: `06-Dispo Docs\Bleecker\SWD GF#25-2153 - recorded.pdf` (Special Warranty Deed) + `06-Dispo Docs\Closing Statement Items\HUD-1signed FINAL.pdf` (both grounded)*

| Field | Example value | Kind | Reliability |
|---|---|---|---|
| Property/Address | 0.6198 ac (27,000 SF), G. Hamilton Survey Abstract 751, Fort Worth/Tarrant | Shared → Property/Address | high |
| Grantor / Seller | IDV Risinger, LLC (signed Timothy Harrington, Mgr) | Type-specific | high |
| Grantee / Buyer | 9733 BP South LLC (Dallas, TX) | Shared → Counterparty | high |
| Instrument / Recording # | D226009646, recorded 01/16/2026 | Type-specific → shared `RecordingInfo` | high |
| GF# / File # | GF# 25-2153 | Type-specific | high |
| Sales/Contract Price | $243,432.00 | Type-specific | high |
| Cash from Buyer / to Seller | Buyer $239,564.67; Seller $241,082.13 | Type-specific | high |
| Settlement Agent | Rattikin Title Company | Type-specific → shared `TitleCompany` | high |
| Permitted Exceptions | Minerals, utility easement, plat notices, no public street access | Type-specific | med |
| Execution Status | Executed & Recorded | Shared → Status | high |

- **Shared resolution:** DealType = Disposition; Submarket = Fort Worth *(unmappable)*; Counterparty = 9733 BP South LLC (grantee); Confidentiality = **split** — recorded deed is **Public**, HUD-1 is **Confidential**.
- **Gotchas:** One content type spans **two very different sub-docs** (deed vs settlement statement) with different fields *and* different confidentiality — consider sub-types. Grantor entity (IDV Risinger LLC) differs from PSA seller (IDV Development Services LLC) — title flipped between contract and closing. HUD-1 street address literally "TBD". Notary county (Harris) ≠ property county (Tarrant).

### 3.6 Title & Survey
*Sample: `06-Dispo Docs\Bleecker\Title-Survey\Title Commitment(2).pdf` (grounded — Form T-7 Commitment)*

| Field | Example value | Kind | Reliability |
|---|---|---|---|
| GF / Commitment No. | GF/Commitment No. 25-2153 | Type-specific | high |
| Title Insurer / Agent | Chicago Title Insurance Co. (agent: Rattikin Title) | Type-specific → shared `TitleCompany` | high |
| Policy Amount | $243,432.00 (Owner's Policy T-1) | Type-specific | high |
| Premium | $1,588.00 | Type-specific | high |
| Estate / Interest | Fee Simple | Type-specific | high |
| Vested Owner | IDV Risinger, LLC | Type-specific | high |
| Proposed Insured | 9733 BP South LLC | Shared → Counterparty | high |
| Effective / Issued Date | Effective 12/8/2025; Issued 12/30/2025 | Shared → DocumentDate | high |
| Schedule B Exceptions | Minerals, utility easement, area/boundary, no public access | Type-specific | med |
| Schedule C Requirements | Survey; LLC org docs; gap indemnity; executed contract | Type-specific | med |
| Status | Commitment (pre-policy), uncountersigned | Shared → Status | med |

- **Shared resolution:** DealType = Disposition; Submarket = Fort Worth *(unmappable)*; Counterparty = 9733 BP South (proposed insured) *or* IDV Risinger (vested owner) — two candidates; Confidentiality = Confidential/transactional.
- **Gotchas:** Conflates **Title Commitment** (text-rich) with **Survey** (a Kimley-Horn drawing that is essentially unextractable as text) — consider splitting. Legal description intentionally incomplete pending survey. Status = "commitment" (an intermediate state, not Draft/Executed/Final).

---

## 4. Due Diligence & Entitlements

### 4.1 Environmental Report
*Sample: `06-Dispo Docs\Bleecker\War Room\Site Reports\Phase I ESA...pdf` (grounded — 338-page text-based PDF)*

| Field | Example value | Kind | Reliability |
|---|---|---|---|
| Report Type | Phase I Environmental Site Assessment | Type-specific | high |
| ASTM Standard | ASTM E1527-21 | Type-specific | high |
| Property/Address | 101 W. Risinger Road, Fort Worth, TX 76140 | Shared → Property/Address | high |
| Site Acreage | 9.427 ac (title) / 8.89 ac (TCAD) | Type-specific | high |
| Report Date / Effective | July 2, 2025 / eff. June 20, 2025 | Shared → DocumentDate | high |
| Report Expiration | Dec 17, 2025 (eff + 180 days) | Type-specific | high |
| Consultant / Preparer | Rone Engineering Services (a Certerra Company) | Type-specific → shared `Firm/Consultant` | high |
| Project No. | 14-251070-0 | Type-specific | high |
| Conclusion / RECs | "no evidence of RECs... no further assessment warranted" | Type-specific | high |
| Zoning | City of FW: Industrial-I; TCAD: Residential-Ag | Type-specific | high |
| Environmental Professional | Heather Leven | Type-specific | high |

- **Shared resolution:** DealType = Disposition (sell-side data room); Submarket = Fort Worth *(unmappable)*; Counterparty = IDV Development Services (client/orderer); Confidentiality = **Confidential** (explicit cover-letter statement).
- **Gotchas:** Address direction conflict (W vs E Risinger). Acreage inconsistency (9.427 vs 8.89). `Findings/RECs` is free-form → often below threshold.

### 4.2 Plat / Site Plan
*Samples: `04...02-Arch\250822-IDV RIsinger Site Plan.pdf` (OCR-only) + `06-Dispo Docs\Bleecker\War Room\Plat (old)\...Plat Lot 3 Blk 1.pdf` (mixed)*

| Field | Example value | Kind | Reliability |
|---|---|---|---|
| Drawing Title / Subtype | "OVERALL SITE PLAN – OPTION 01" / "FINAL PLAT OF LOT 3 BLOCK 1 HOLLINGSWORTH ADDN" | Type-specific | med / high |
| Property/Address | E Risinger Rd / Burleson Rd, Fort Worth | Shared → Property/Address | med |
| Legal Description | Lot 3 Blk 1 Hollingsworth Addn; G. Hamilton Survey Abstract 751 | Type-specific | high (plat) |
| Recording Ref | D203291093 / Cabinet A Slide 8558, Tarrant County | Type-specific → shared `RecordingInfo` | high (plat) |
| Site Area | ±410,857 SF (9.43 AC) | Type-specific | med (OCR) |
| Building Area / Coverage | 134,125 SF; 33.1% | Type-specific | med |
| Parking | 177 provided / 269 required | Type-specific | med |
| Architect / Surveyor | Powers Brown Architecture / T.D. Disheroon, RPLS | Type-specific | med / high |
| DocumentDate | 07 Aug 2025 (site plan) / Aug 2003 (plat) | Shared → DocumentDate | med / high |

- **Shared resolution:** DealType = Design & Construction; Submarket = Fort Worth *(unmappable)*; Counterparty = **ambiguous by era** (current IDV vs 2003 owner Hollingsworth); Confidentiality = site plan carries architect proprietary notice; recorded plat is Public.
- **Gotchas:** **Architectural site plan is a raster image → OCR fragments only** (classifiable, not field-extractable → review). Same PDF can mix clean typed pages with scanned drawing pages. Pure-raster drawings with no text layer yield nothing.

### 4.3 Permit / Municipal Approval
*Samples: `04...11-Misc\Platting\FS-26-019 Decision Letter.pdf` + `04...11-Misc\Fire Flow Test WFF-25-0284...pdf` (both grounded)*

| Field | Example value | Kind | Reliability |
|---|---|---|---|
| Document Subtype | Plat Decision Letter / Water Flow Report | Type-specific | high |
| Case / Permit Number | FS-26-019 (plat); WFF-25-0284 (fire flow) | Type-specific | high |
| Status | **Conditionally Approved** | Shared → Status | high |
| Issuing Authority | City of Fort Worth (City Plan Commission / Water Dept) | Type-specific | high |
| Property/Address | 101 E Risinger RD, Fort Worth, TX 76140 | Shared → Property/Address | high |
| DocumentDate | 3/12/2026 / 10/27/2025 | Shared → DocumentDate | high |
| Approver / Signatory | Stephen Murray, Executive Secretary, CPC | Type-specific | high |
| Applicant / Engineer | Joshua Kennerly, Kimley-Horn | Shared → Counterparty | high |
| Technical Result (fire flow) | 51 psi residual @ 1500 gpm; 12-inch main | Type-specific | high |

- **Shared resolution:** DealType = Design & Construction/entitlement; Submarket = Fort Worth *(unmappable)*; Counterparty = City of Fort Worth (issuing authority); Confidentiality = Public.
- **Gotchas:** **Status vocabulary needs a "Conditionally Approved" value** (third state). Case-number prefix encodes subtype + year (`FS-26-`, `WFF-25-`) → extract as a first-class field. Fire-flow sheet is arguably a utility/engineering doc (straddles types).

### 4.4 Utility & Easement Agreement
*Sample: `04...13-Utility Agreements\CoFW Encroachment Agreements\...Encroachment Esmt (SS).pdf` (grounded — native-CAD vector exhibit)*

| Field | Example value | Kind | Reliability |
|---|---|---|---|
| Easement Type | 15' Public Sanitary Sewer Easement ("(SS)") | Type-specific | high |
| Easement Area | 225 SF (0.0052 acre) | Type-specific | high |
| Property/Address | G. Hamilton Survey Abstract 751, Fort Worth/Tarrant (no street addr) | Shared → Property/Address | med |
| Grantor / Owner | IDV Risinger, LLC (Deed Inst. D225235312) | Shared → Counterparty | high |
| Adjoining / Related Parties | 24/7 Storage Company; Oncor Electric Delivery | Type-specific | high |
| Surveyor / Preparer | Michael Cleo Billingsley, RPLS #6558, Kimley-Horn | Type-specific | high |
| Recording / Deed Ref | Instrument D225235312, O.P.R.T.C.T. | Type-specific → shared `RecordingInfo` | high |
| DocumentDate | 05/29/2026 (survey date) | Shared → DocumentDate | high |

- **Shared resolution:** DealType = Design & Construction; Submarket = Fort Worth *(unmappable)*; Counterparty = City of Fort Worth (grantee) / IDV (grantor); Confidentiality = non-confidential (recordable land instrument).
- **Gotchas:** File is **only the survey exhibit, not the signed agreement** (no body/signatures/recording). No street address (legal description only). Utility type + counterparty (City) live in the **filename/folder**, not the page. Native-CAD text extracts cleanly (unlike raster drawings).

---

## 5. Financial

### 5.1 Financial Model / Pro Forma
*Sample: `03-Acq Docs\War Room\Title-Survey\REVISED OP Proforma.pdf` (grounded; live models are `.xlsm` via ClosedXML)*

| Field | Example value | Kind | Reliability |
|---|---|---|---|
| Asset Name | IDV – Risinger | Shared → Property/Address | high |
| Total RSF | 134,094 | Type-specific | high |
| Land Acreage / Price | 8.81 AC / $3,766,729 ($9.81/SF) | Type-specific | high |
| Total Project Costs | $17,869,739 ($133.26 PSF) | Type-specific | high |
| Hard Costs | $8,266,686 | Type-specific | high |
| Development Fee | $598,471 | Type-specific | high |
| Capital Stack | Equity $6,701,152 (37.5%) / Debt $11,168,587 (62.5%) | Type-specific | high |
| Stable NOI / Yield | $1,273,893 / 7.13% | Type-specific | high |
| Debt Yield / DSCR | 11.41% / 1.44 | Type-specific | high |
| Cap Rate | 5.50% | Type-specific | high |
| Economic Value | $23,161,691 | Type-specific | high |
| Profit | $4,828,718 | Type-specific | high |

- **Shared resolution:** DealType = Development/ground-up; Submarket = Fort Worth *(unmappable; not on the proforma itself)*; Counterparty = not present; Confidentiality = Internal/Confidential (default).
- **Gotchas:** **No IRR** — returns expressed as Yield/Debt Yield/DSCR/Cap Rate. Every metric is a **table cell** (map by row label + ProForma vs PSF column); "$" sits in a separate column from the number. Exported from `.xlsm`.

### 5.2 Operating Budget
*Sample: `05-Asset Mgmt\2027 Operating Budget - Sealy IDV Risinger - JVA Exhibit C.pdf` (grounded)*

| Field | Example value | Kind | Reliability |
|---|---|---|---|
| Entity / Property | Sealy IDV Risinger, LLC | Shared → Counterparty / Property | high |
| Budget Year | 2027 | Shared → DocumentDate (period) | high |
| Exhibit Reference | JVA Exhibit C | Type-specific | high |
| Occupancy % (by month) | 0% Jan–Oct; 50% Nov–Dec | Type-specific | high |
| Potential Rental Revenue | $743,104 (UW $9.50/SF) | Type-specific | high |
| Property Taxes / Insurance | $145,738 / $53,640 | Type-specific | high |
| Mgmt Fee | $10,500 (greater of 2% gross or $1,500) | Type-specific | high |
| Total OPEX | $253,568 | Type-specific | high |
| NOI | **-$219,266** (lease-up year) | Type-specific | high |

- **Shared resolution:** DealType = Development (lease-up); Submarket = Fort Worth *(unmappable)*; Counterparty = Sealy & Company (JV partner/manager); Confidentiality = Confidential (JV exhibit).
- **Gotchas:** 12-month × line-item grid → target the **Total column** + Notes column for assumptions. NOI negative is expected (lease-up), not an error. Entity "Sealy IDV Risinger" ≠ proforma's "IDV Risinger" — reconcile as same deal.

### 5.3 Lender / Financing Document
*Sample: `Lender Tracker\Lender Term Sheets\Hancock Whitney - Term Sheet...pdf` (grounded)*

| Field | Example value | Kind | Reliability |
|---|---|---|---|
| Lender / Bank | Hancock Whitney Bank | Shared → Counterparty | high |
| Borrower | TBD SPE (IDV-controlled, up to 95% Sealy) | Type-specific | high |
| Facility Type | Senior-secured construction loan | Type-specific | high |
| Facility Amount | $10,617,738 (least of 4 tests) | Type-specific | high |
| Max LTV / LTC | 60% / 60% | Type-specific | high |
| Interest Rate | 1-mo Term SOFR + 2.35% (0% floor) | Type-specific | high |
| Term / Extensions | 36 months + two × 12-mo | Type-specific | high |
| Origination / Extension Fee | 0.60% / 0.15% | Type-specific | high |
| Min DSCR | 1.25x (burn-off), 1.00x (distributions) | Type-specific | high |
| Recourse | 10% limited guaranty + completion/environmental | Type-specific | high |
| Property/Address | Risinger Rd & Old Burleson Rd, Ft Worth; 8.81± ac; 134,094± nrsf | Shared → Property/Address | high |
| DocumentDate | March 31, 2026 | Shared → DocumentDate | high |
| Status | **Proposal — NOT a commitment** (unsigned) | Shared → Status | high |

- **Shared resolution:** DealType = Construction/Development financing; Submarket = **Fort Worth** (explicit in doc; *unmappable in taxonomy*); Counterparty = Hancock Whitney Bank; Confidentiality = **Confidential** (explicit clause).
- **Gotchas:** **Proposal, not executed** (signature block blank) → Status = Proposal/unexecuted. Loan amount is a "least-of-four" definition; several metrics are formula definitions across pages. Letterhead recipient is Houston (IDV office) — use the Fort Worth collateral for Property.

---

## 6. Marketing & Corporate

### 6.1 Marketing Flyer / Brochure
*Sample: `07-Flyers-Marketing\AL_Industrial-101 W Risinger_Brochure v11.pdf` (grounded)*

| Field | Example value | Kind | Reliability |
|---|---|---|---|
| Property/Address | 101 W. Risinger Rd. | Shared → Property/Address | high |
| DealType | "FOR SALE OR LEASE" (dual) | Shared → DealType | high |
| Available SF | 134,094 SF; divisible to 20,000 SF | Type-specific | high |
| Clear Height | 32' clear | Type-specific | high |
| Dock / Drive-in Doors | 38 dock-high; 4 drive-in | Type-specific | high |
| Truck Court / Parking | 130' truck court; 156 car parks | Type-specific | high |
| Power | 1,500 kVA transformer, 3,000-amp | Type-specific | high |
| Broker Contacts | Weston King, Dalton Knipe (@nmrk.com) | Shared → Counterparty | high |
| Driving Distances | Downtown FW 12 mi; DFW Airport 32 mi | Type-specific | med |

- **Shared resolution:** DealType = Sale or Lease; Submarket = Fort Worth / S Central Tarrant *(unmappable)*; Counterparty = Newmark (broker) / IDV (owner); Confidentiality = Public.
- **Gotchas:** Image-heavy (renderings/maps); text often embedded in graphics → OCR fidelity matters. No document date (only "v11").

### 6.2 Proposal / Pitch Deck
*Samples are `.pptx` — **inferred, not sample-grounded** (unreadable format)*

| Field | Example value | Kind | Reliability |
|---|---|---|---|
| Proposal Type | Investment / listing pitch / capital raise | Type-specific | low (inferred) |
| Audience | Investor / seller / lender | Type-specific | low (inferred) |
| Project | Property or fund name | Shared → Property/Address | low (inferred) |
| Key Metrics Summary | IRR, equity multiple, cap rate, cost basis | Type-specific | low (inferred) |
| Presenter | Presenting firm / individual | Shared → Counterparty | low (inferred) |
| DocumentDate | Deck date | Shared → DocumentDate | low (inferred) |

- **Gotchas:** `.pptx` **not readable** by the current pipeline — needs conversion to PDF (or PowerPoint parser). Chart/image heavy even after conversion. All fields above are document-type knowledge, not evidence.

### 6.3 Entity / Corporate Governance
*Sample: `04...11-Misc\IDV Risinger LLC - TX Certificate of Formation (filed).pdf` (grounded)*

| Field | Example value | Kind | Reliability |
|---|---|---|---|
| Entity Name | IDV Risinger, LLC | Type-specific | high |
| Entity Type | Domestic LLC | Type-specific | high |
| Jurisdiction | State of Texas | Type-specific | high |
| Filing / File Number | 806301894 | Type-specific | high |
| DocumentDate | Effective 11/12/2025 (SoS letter 11/13/2025) | Shared → DocumentDate | high |
| Status | Filed | Shared → Status | high |
| Registered Agent | Timothy C. Harrington | Type-specific | high |
| Registered Office | 10375 Richmond Ave Ste 1950, Houston TX 77042 | *(sponsor address — NOT submarket)* | high |
| Managers | Harrington, Yaap, Coulter, Sibley, Shoup | Type-specific | high |
| Filing Counsel | Wilson Cribbs & Goren, P.C. | Shared → Counterparty | high |

- **Shared resolution:** DealType = **N/A / Governance** (not a transaction); Submarket = **N/A** (do NOT infer Houston from registered office); Counterparty = Wilson Cribbs & Goren (counsel) / TX Secretary of State; Confidentiality = Public.
- **Gotchas:** Registered office (Houston) is the **sponsor address, not the deal submarket** — classic red herring. Governance data on pp. 2–4 (form checkboxes) → needs page-level extraction.

### 6.4 Correspondence
*Sample: `06-Dispo Docs\Bleecker\WCG letter.pdf` (grounded — Seller's Closing Instruction Letter)*

| Field | Example value | Kind | Reliability |
|---|---|---|---|
| DocumentDate | January 15, 2026 | Shared → DocumentDate | high |
| Sender | Wilson Cribbs + Goren / Colton D. Kimler | Shared → Counterparty | high |
| Recipient | Rattikin Title / David L. Bailiff | Shared → Counterparty | high |
| Property/Address | 0.6198 ac, Fort Worth, Tarrant County | Shared → Property/Address | high |
| Subject / Re | "Commercial Contract – Unimproved Property, dated 12/24/2025" | Type-specific | high |
| Letter Type | Seller's Closing Instruction Letter | Type-specific | high |
| Parties | Seller: IDV Risinger LLC; Buyer: 9733 BP South LLC | Type-specific | high |
| Enclosures | SWD, Bill of Sale, FIRPTA, Assignment, Settlement Stmt | Type-specific | high |

- **Shared resolution:** DealType = Disposition; Submarket = Fort Worth *(unmappable)*; Counterparty = sender/recipient (WCG → Rattikin), ultimate buyer 9733 BP South; Confidentiality = Confidential (deal-sensitive).
- **Gotchas:** Many correspondence files are **`.msg` Outlook — not readable** (need conversion). Internal inconsistency: letter says "Harris County" (Houston-firm carry-over) while property is Tarrant — don't let it pull submarket to Houston. Handwritten signature block → low OCR reliability.

### 6.5 Market Report
*Sample: `03-Acq Docs\War Room\Market Info\Fort Worth Lease Comps 2024-2025.pdf` (grounded)*

| Field | Example value | Kind | Reliability |
|---|---|---|---|
| Report Title | "Fort Worth Lease Comps | 2024-2025" | Type-specific | high |
| Report Period | 2024–2025 | Shared → DocumentDate (period) | med |
| Submarket(s) | NE Tarrant/Alliance, Meacham/Fossil Cr, S Central Tarrant, SW Tarrant, East FW | Shared → Submarket | high |
| Author Firm | HPI (HPI Fort Worth) | Shared → Counterparty | high |
| Data Schema | Building, Address, City, Submarket, Tenant, SF, Comm Date, Rate, Structure, TI, Escalation, Term, Free, Type, Yr Built, Clear Ht, Owner | Type-specific | high |
| Comp Count | 90+ lease comps | Type-specific | high |
| Rate Range | $4.95–$13.25 NNN | Type-specific | high |
| Notable Tenants | Google, Tesla, Porsche, Frito Lay, Rivian | Type-specific | high |

- **Shared resolution:** DealType = Market Data; Submarket = **multiple Fort Worth submarkets** *(unmappable)*; Counterparty = HPI (authoring brokerage); Confidentiality = **Confidential (mixed)** — rows flagged "CONFIDENTIAL" / "Highly CONFIDENTIAL – Do not share".
- **Gotchas:** Wide landscape table → linearized dump loses column alignment (per-row parsing med reliability). No single publish date. **Per-row confidentiality** — evaluate at row granularity; treat doc as Confidential overall.

---

## 7. Recommendations / next steps

1. **Fix Submarket first — it's the #1 blocker.** Make Submarket metro-aware (Metro + Submarket) and add a DFW/Fort Worth
   submarket set. Without this, every document in DFW deals mis-tags to "Unknown."
2. **Promote `Property/Address`, `DocumentDate`, and `Status` to shared fields** — they recurred in ~every content type.
3. **Add a `Status` vocabulary** covering Draft / Proposal / Executed / Recorded / Commitment / Conditionally Approved —
   several types hinge on it and it's high-value for Copilot ("show me *executed* term sheets").
4. **Store resistant fields as text, not numbers** — tiered commissions, "least-of" loan amounts, financial-metric tables.
5. **Close the extractor format gaps** — add DOCX and (via conversion) PPTX/MSG handling; add the unsupported-format
   guard so `.dwg/.msg/.zip/.pptx` route to review cleanly instead of erroring.
6. **Treat filename/folder as a weak prior, not truth** — content-based routing only (the "Bank Budget = title policy"
   trap). Folder can seed DealType, but verify against content.
7. **Consider sub-types** for Closing Document (deed vs settlement statement) and Title & Survey (commitment vs drawing) —
   each pair has different fields and different confidentiality.
8. **Validate with SMEs** (task 2.1) — this document is the working input for that session, replacing guesswork with
   evidence from a real deal.
