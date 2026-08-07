# Human Review Queue — Deep Dive

## 1. Purpose

The human review queue is the **quality gate** between AI classification and production metadata. It serves three functions:

1. **Quality assurance** — SMEs validate low-confidence classifications before they're written to SharePoint
2. **Feedback loop** — corrections provide data for prompt tuning iterations
3. **Audit trail** — every classification that was reviewed has a documented decision

## 2. Queue Design

### SharePoint List Schema: "AI Classification Review"

| Column | Internal Name | Type | Required | Description |
|--------|--------------|------|----------|-------------|
| Title | Title | Single line of text | Yes | File name (auto-populated) |
| Document Link | DocumentLink | Hyperlink | Yes | URL to original document in SharePoint |
| Document ID | DocumentId | Single line of text | Yes | SharePoint item ID for write-back |
| Site ID | SiteId | Single line of text | Yes | SharePoint site ID |
| Drive ID | DriveId | Single line of text | Yes | SharePoint drive ID |
| Proposed Deal Type | ProposedDealType | Choice | Yes | AI-proposed value |
| Proposed Submarket | ProposedSubmarket | Choice | Yes | AI-proposed value |
| Proposed Counterparty | ProposedCounterparty | Single line of text | Yes | AI-proposed value |
| Proposed Classification | ProposedClassification | Choice | Yes | Agent 1 primary document type classification |
| Proposed Confidentiality | ProposedConfidentiality | Choice | Yes | AI-proposed confidentiality |
| Document Type | DocumentType | Choice | Yes | Agent 1 classification result (Lease Agreement, Offer Memorandum, Market Report, etc.) |
| Type Confidence | TypeConfidence | Number | Yes | Agent 1's confidence score for document type classification |
| Type-Specific Fields | TypeSpecificFields | Multi-line text | Yes | JSON of type-specific extracted fields from Agent 2 |
| Suggested Fields | SuggestedFields | Multi-line text | No | JSON of additional fields Agent 2 discovered beyond the expected schema |
| Confidence Scores | ConfidenceScores | Multi-line text | Yes | JSON: per-category confidence |
| Low Confidence Categories | LowConfidenceCategories | Multi-line text | Yes | Categories that triggered review — includes Agent 1 type confidence and Agent 2 per-field confidence |
| AI Reasoning | AIReasoning | Multi-line text | Yes | AI's reasoning per category |
| Extracted Text Snippet | TextSnippet | Multi-line text | No | First 500 chars of extracted text for context |
| Review Status | ReviewStatus | Choice | Yes | Pending / Approved / Corrected / Rejected / Skipped |
| Reviewed By | ReviewedBy | Person | No | SME who reviewed |
| Reviewed Date | ReviewedDate | Date/Time | No | When reviewed |
| Correction Notes | CorrectionNotes | Multi-line text | No | What was changed and why |
| Batch ID | BatchId | Single line of text | No | Batch ID if from batch processing |
| Processing Date | ProcessingDate | Date/Time | Yes | When AI processed the document |

### Choice Column Values

**ProposedDealType:** Lease, Sale, Development, Acquisition, Disposition, Financing, Other

**ProposedSubmarket:** Northwest Houston, North Houston, Northeast Houston, Katy/West Houston, Southwest Houston, Southeast Houston, Central Houston, Multiple, Unknown

**ProposedClassification:** Offer Memorandum, Market Report, Lease Agreement, Purchase Agreement, Letter of Intent, Financial Analysis, Due Diligence, Correspondence, Presentation, Other

**ProposedConfidentiality:** Public, Internal, Confidential, Highly Confidential

**ReviewStatus:** Pending, Approved, Corrected, Rejected, Skipped

## 3. Review Interface (Power Apps)

### Layout

```
┌──────────────────────────────────────────────────────────────────┐
│  AI Classification Review                                        │
│  ═══════════════════════                                        │
│                                                                  │
│  Document: [CBRE_NW_Houston_Q2_2024.pdf] 📄 Open Document       │
│  Processed: 2026-07-29 14:30 UTC                                │
│  ─────────────────────────────────────────────────────────────── │
│                                                                  │
│  ┌──────────────────────────┐  ┌──────────────────────────────┐ │
│  │ 🏷️ Document Type (Agent 1)│  │ Document Preview              │ │
│  │                          │  │                              │ │
│  │ [Lease Agreement ▼]      │  │ ┌────────────────────────┐  │ │
│  │        ✅ 0.94 confidence │  │ │                        │  │ │
│  │                          │  │ │   (Embedded PDF or      │  │ │
│  │ ── Common Fields ──────  │  │ │    first page preview)  │  │ │
│  │                          │  │ │                        │  │ │
│  │ Deal Type:               │  │ │                        │  │ │
│  │ [Lease        ▼] ✅ 0.92 │  │ │                        │  │ │
│  │                          │  │ └────────────────────────┘  │ │
│  │ Submarket:               │  │                              │ │
│  │ [NW Houston   ▼] ⚠️ 0.68│  │ AI Reasoning:               │ │
│  │                          │  │ "Agent 1: Identified as      │ │
│  │ Counterparty:            │  │  lease agreement based on    │ │
│  │ [CBRE         ] ✅ 0.95  │  │  legal structure and terms.  │ │
│  │                          │  │  Agent 2: Extracted lease-   │ │
│  │ Confidentiality:         │  │  specific fields. Submarket  │ │
│  │ [Internal     ▼] ⚠️ 0.72│  │  uncertain — references both │ │
│  │                          │  │  NW and North Houston."      │ │
│  │ ── Type-Specific Fields ─│  │                              │ │
│  │  (Lease Agreement)       │  └──────────────────────────────┘ │
│  │                          │                                   │
│  │ Tenant:                  │                                   │
│  │ [Acme Corp    ] ✅ 0.97  │                                   │
│  │                          │                                   │
│  │ Landlord:                │                                   │
│  │ [Hines REIT   ] ✅ 0.93  │                                   │
│  │                          │                                   │
│  │ Lease Term:              │                                   │
│  │ [60 months    ] ✅ 0.91  │                                   │
│  │                          │                                   │
│  │ Rental Rate:             │                                   │
│  │ [$24.50/SF NNN] ⚠️ 0.74 │                                   │
│  │                          │                                   │
│  │ ── Suggested Fields ──── │                                   │
│  │  (Agent 2 discovered)    │                                   │
│  │                          │                                   │
│  │ ☐ Escalation Rate: 3%/yr │                                   │
│  │ ☐ TI Allowance: $45/SF   │                                   │
│  │ ☐ Commencement: 2026-09  │                                   │
│  └──────────────────────────┘                                   │
│                                                                  │
│  ⚠️ Review needed: Submarket, Confidentiality, Rental Rate      │
│                                                                  │
│  Correction Notes:                                               │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ Changed submarket to NW Houston — primary focus is 290   │   │
│  │ corridor despite brief mention of I-45 North.            │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│  [✅ Approve]  [✏️ Correct & Approve]  [❌ Reject]  [⏭️ Skip]    │
│                                                                  │
│  ◀ Previous (3 of 47 pending)                    Next ▶          │
└──────────────────────────────────────────────────────────────────┘
```

### Power Apps Implementation Notes

| Feature | Implementation |
|---------|---------------|
| Document preview | Use SharePoint file viewer embed URL (`/_layouts/15/WopiFrame.aspx`) |
| Confidence indicators | Conditional formatting: ✅ green (≥0.85), ⚠️ yellow (0.70-0.84), 🔴 red (<0.70) |
| Category dropdowns | Bound to SharePoint Choice columns; values match taxonomy |
| Type-specific panel | Dynamically renders fields from TypeSpecificFields JSON based on DocumentType |
| Suggested fields | Rendered from SuggestedFields JSON with accept/reject checkboxes |
| Approve button | Sets ReviewStatus="Approved", ReviewedBy=current user, ReviewedDate=now |
| Correct button | Sets ReviewStatus="Corrected", captures original vs. corrected values in CorrectionNotes |
| Reject button | Sets ReviewStatus="Rejected", requires notes |
| Navigation | Gallery filtered on ReviewStatus="Pending", sorted by ProcessingDate |
| Batch filter | Optional filter by BatchId to review batch results separately |

## 4. Review Queue Metrics & Monitoring

### Key Metrics

| Metric | Source | Alert Threshold | Action |
|--------|--------|----------------|--------|
| Queue depth (pending items) | SharePoint list count where ReviewStatus="Pending" | >100 items | Notify SME team; consider adjusting thresholds |
| Review throughput (items/day) | Count of ReviewedDate = today | <10/day when queue >50 | SMEs may need more time allocation |
| Average review latency | ReviewedDate - ProcessingDate | >3 business days | Escalate; documents have stale metadata |
| Correction rate | Corrected / (Approved + Corrected) | >30% | Prompts need tuning; schedule iteration |
| Rejection rate | Rejected / total reviewed | >10% | Investigate — may indicate extraction failures |
| Category-specific correction rate | Corrections per category | Any category >40% | Category-specific prompt tuning needed |
| Type classification correction rate | DocumentType corrections / total reviewed | >15% | Agent 1 classification prompts need tuning |

### Monitoring Dashboard (Power BI or SharePoint List Views)

```
Review Queue Dashboard
═══════════════════════

📊 Current Status          📈 Trends (Last 30 Days)
─────────────────          ──────────────────────────
Pending:    47             [Sparkline: queue depth over time]
In Review:   3             
Completed: 892             Review Throughput: 15/day avg
                           Avg Latency: 1.2 business days

🎯 Accuracy by Category    🔍 Top Correction Patterns
─────────────────────       ────────────────────────────
Deal Type:       94%        Submarket confusion:
Submarket:       81% ⚠️      NW Houston ↔ North Houston (12x)
Counterparty:    96%        Doc Classification:
Doc Type:        89%          LOI ↔ Correspondence (8x)
Confidentiality: 85%        Confidentiality:
                              Internal ↔ Confidential (6x)
```

## 5. Correction Data → Prompt Tuning Feedback Loop

### Corrections List Schema

When a reviewer selects "Correct & Approve", a separate **Corrections Log** list captures:

| Column | Type | Description |
|--------|------|-------------|
| DocumentId | Text | Reference to original document |
| FileName | Text | For context |
| CorrectionType | Choice | Which agent/layer was corrected (see correction types below) |
| Category | Choice | Which category was corrected |
| AIProposedValue | Text | What the AI suggested |
| CorrectedValue | Text | What the SME chose |
| AIConfidence | Number | AI's confidence for this category |
| AIReasoning | Multi-line text | AI's reasoning |
| CorrectorNotes | Multi-line text | Why the SME disagreed |
| CorrectedBy | Person | Who corrected |
| CorrectedDate | DateTime | When corrected |
| PromptTuningIteration | Number | Which iteration consumed this correction (null = unconsumed) |

### Correction Types

Corrections fall into four categories, each feeding back to a different part of the two-agent pipeline:

1. **Document type** (Agent 1) — the classification was wrong, which cascades to an incorrect extraction schema being used by Agent 2
2. **Common metadata fields** (Agent 2 common fields) — deal type, submarket, counterparty, or confidentiality were wrong
3. **Type-specific fields** (Agent 2 type-specific fields) — fields specific to the document type (e.g., tenant, lease term) were wrong or missing
4. **Suggested fields** (Agent 2 discovered) — reviewer accepts, rejects, or edits additional fields that Agent 2 surfaced beyond the expected schema

### Using Corrections for Prompt Tuning

```python
# scripts/analyze_corrections.py

"""Analyze correction patterns to inform prompt tuning iterations."""

def analyze_corrections(corrections: list[dict]) -> dict:
    """Produce a correction analysis report for prompt tuning."""
    
    analysis = {
        "total_corrections": len(corrections),
        "by_category": {},
        "confusion_matrix": {},
        "recommended_actions": [],
    }
    
    for category in ["documentType", "dealType", "submarket", "counterparty", 
                     "confidentiality", "typeSpecificFields", "suggestedFields"]:
        cat_corrections = [c for c in corrections if c["category"] == category]
        
        if not cat_corrections:
            continue
        
        # Build confusion pairs
        confusion = {}
        for c in cat_corrections:
            pair = f"{c['ai_proposed']} → {c['corrected']}"
            confusion[pair] = confusion.get(pair, 0) + 1
        
        # Sort by frequency
        sorted_confusion = sorted(confusion.items(), key=lambda x: -x[1])
        
        analysis["by_category"][category] = {
            "correction_count": len(cat_corrections),
            "top_confusions": sorted_confusion[:5],
            "avg_ai_confidence_on_errors": (
                sum(c["ai_confidence"] for c in cat_corrections) / len(cat_corrections)
            ),
        }
        
        # Generate recommendations
        for pair, count in sorted_confusion[:3]:
            if count >= 3:
                from_val, to_val = pair.split(" → ")
                analysis["recommended_actions"].append({
                    "category": category,
                    "action": f"Add decision boundary between '{from_val}' and '{to_val}' — confused {count} times",
                    "priority": "high" if count >= 5 else "medium",
                    "suggestion": f"Add few-shot example showing why '{to_val}' is correct when [describe distinguishing feature]",
                })
    
    return analysis
```

## 6. Scaling the Review Queue

### Scenario Planning

| Batch Size | Estimated Review Rate (15% low-confidence) | SME Capacity (2 reviewers, 30/day each) | Days to Clear | Risk |
|-----------|---------------------------------------------|----------------------------------------|--------------|------|
| 5,000 | ~750 items | 60/day | ~13 days | Manageable |
| 15,000 | ~2,250 items | 60/day | ~38 days | Significant backlog |
| 25,000 | ~3,750 items | 60/day | ~63 days | Unsustainable |

### Mitigation Strategies

1. **Tune thresholds before batch** — run on 500-doc sample, adjust until review rate < 10%
2. **Priority tiers** — review high-value documents first (by deal type or folder)
3. **Bulk approve** — if a batch of similar documents all have the same classification at 0.75+ confidence, allow bulk approval
4. **Temporary reviewers** — bring in additional SMEs during batch processing window
5. **Progressive relaxation** — after reviewing 100 items with 95% approve rate, relax threshold for remaining items
