# Review Queue Accountability Analysis

**Document type:** Design Thinking — Stakeholder Mapping + Value Proposition Analysis  
**Date:** 2026-08-18  
**Status:** Ready for product-coach and SOW review  
**Related artifacts:**  
- [reviewer-persona.md](reviewer-persona.md)  
- [review-queue-ux-analysis.md](review-queue-ux-analysis.md)  
- [system-dynamics-analysis.md](system-dynamics-analysis.md)  
- [ADR-006](../decisions/adr-006-inline-review.md)  
- [human-review-queue.md](../architecture/human-review-queue.md) (historical)

---

## 1. Stakeholder Map

### Stakeholder Groups

| Stakeholder | Role in This System | Influence | Interest |
|-------------|---------------------|-----------|----------|
| **Jordan** (Deal-support SME / reviewer) | Performs the review decisions that generate correction data | Low — individual contributor | High operational impact; daily work is affected by metadata quality |
| **Jordan's manager** (Team lead / director of brokerage operations) | Controls Jordan's time allocation; decides whether review is "real work" | High — time and priority setter | Medium — accountable for team productivity metrics, not AI accuracy |
| **Brokerage leadership** (Managing partner / COO equivalent) | Approved the AI enrichment investment; owns the ROI narrative | High — budget and mandate | Medium — cares about outcomes, not mechanics |
| **Deal brokers** (Active deal principals) | Primary consumers of classified metadata for deal search and reporting | Low over the system | Very high — wrong metadata wastes their time directly |
| **The implementation team** (vendor / integrator) | Responsible under the SOW for three rounds of prompt/taxonomy tuning | Medium — technical authority during engagement | High — correction data is the raw material they need to do the contracted tuning work |
| **IT / SharePoint admin** | Manages the SharePoint environment, column schema, and access | Medium — controls the environment | Low — execution role, not outcomes |
| **Copilot for M365 queries** (downstream AI consumer) | Uses SharePoint metadata as grounding for Copilot search and Q&A | None — software | Indirect but real: accurate metadata = better Copilot answers |

### Influence / Interest Matrix

```
HIGH INFLUENCE
    │
    │  Jordan's Manager          Brokerage Leadership
    │  [Time gatekeeper]         [Investment owner]
    │
    │
    │  IT Admin                  Implementation Team
    │  [Environment control]     [SOW accountability]
    │
LOW INFLUENCE
    └─────────────────────────────────────────────
       LOW INTEREST               HIGH INTEREST

                    Jordan (SME)
                    [Reviewer]
                                    Deal Brokers
                                    [Metadata consumers]
```

---

## 2. Who Wins If the Review Queue Is Used Consistently

### Jordan (SME reviewer)

Jordan wins in two ways, but neither is automatic.

The first payoff is personal findability. If metadata is accurate, Jordan stops being the human search engine that brokers call when they cannot find a file. The persona research shows Jordan is interrupted regularly by "Where's the LOI for Greenway?" queries. Accurate metadata routes those queries to SharePoint search, not to Jordan. This is a real and personal payoff — but Jordan will not experience it until weeks after sustained review effort, which is too long a delay to sustain behavior without other reinforcement.

The second payoff is being asked only for genuine expertise. The system is designed so that high-confidence items never reach Jordan. If the queue routes only genuinely uncertain documents, each review decision feels like expert consultation rather than data entry. This respects Jordan's professional identity as a domain expert rather than a metadata cleaner. Whether the current queue design actually achieves this depends on the routing logic — see blocking questions.

### Jordan's Manager

The manager wins a reduction in operational friction: fewer broker complaints about missing documents, faster document retrieval during client calls, and a metadata layer that supports Q3 reporting without manual re-tagging sprints. The manager also wins the ability to show brokerage leadership a functioning AI investment.

Critically, the manager's win is conditional: they only see the benefit if the initial batch is cleared and the prompt tuning iterations fire. If the queue stagnates, the manager inherits a liability — a system the firm paid for that does not improve.

### Brokerage Leadership

Leadership wins the Copilot M365 story. The business case for the AI enrichment pipeline was almost certainly framed around Copilot search quality — "ask Copilot where the NW Houston lease agreements are and get a real answer." That story depends entirely on metadata being accurate. The review queue is the mechanism that makes early metadata accurate enough to trust. Leadership's ROI calculation has a hidden dependency on Jordan's review throughput.

### Deal Brokers

Brokers win search precision. A broker on a site visit who can say "show me all confidential lease agreements in the Katy submarket from the last 18 months" and get a reliable answer is more effective than one who has to call Jordan or browse folders. Brokers are the downstream beneficiaries with the highest visible interest — but they have no direct influence over whether the review queue gets used.

### The Implementation Team

The team wins the ability to fulfill the contracted deliverable. The SOW commits to three rounds of prompt and taxonomy tuning after the initial batch. Those rounds require correction data. Without it, the team is obligated to deliver tuning iterations but has no signal to tune against. The review queue is not a nice-to-have for the team — it is the upstream dependency for their deliverable.

---

## 3. Who Loses If the Review Queue Is Abandoned

### Jordan (eventually)

Jordan does not lose immediately — if the queue is abandoned, Jordan's life gets simpler in the short term (no extra task). The loss is delayed: within 4–8 weeks, deal teams notice that metadata is wrong or incomplete. Copilot answers degrade. Brokers resume calling Jordan for document locations. Jordan absorbs the failure state without understanding its cause, because the connection between "I didn't review the queue" and "metadata is now unreliable" is not visible to them.

### Jordan's Manager

The manager loses credibility. They oversaw an AI implementation that was supposed to reduce operational overhead. Within a quarter, they are facing a stale document library, broker complaints, and a vendor who cannot deliver the contracted tuning rounds because no correction data exists. The manager cannot claim the ROI that justified the investment.

### Brokerage Leadership

Leadership loses the Copilot investment. Microsoft 365 Copilot search quality is directly correlated with the accuracy of SharePoint metadata. If metadata stagnates at initial-batch accuracy (before any prompt tuning), Copilot answers will be unreliable for the categories that matter most: submarket, deal type, and confidentiality. Leadership will observe that Copilot "doesn't work well for real estate questions" — and attribute it to the AI technology, not to the unmaintained metadata it depends on.

### The Implementation Team

The team loses the ability to demonstrate the system's value and fulfill the SOW. Three promised tuning iterations cannot be delivered without correction data. The team faces a choice: deliver tuning iterations without real data (producing performance theater), ask to re-scope the deliverable, or surface the organizational failure to the client. None of these are good outcomes. The team's reputation is collateral damage from an organizational gap in the client's operations.

### The System Itself (Long-Term)

The system dynamics analysis identifies this scenario as the "Review Queue Death Spiral" (R2 loop): queue depth grows, reviewers disengage, throughput drops, corrections stop accumulating, prompt tuning never fires, AI accuracy stagnates, user trust erodes, metadata is ignored, and the entire classification investment produces no lasting value. This is not a hypothetical — it is the documented attractor state for review queue systems when no accountability structure exists.

---

## 4. Minimum Accountability Structure for Self-Sustaining Queue

A review queue is self-sustaining when it has three structural properties: a clear owner, a visible metric, and a consequence for non-performance. Without all three, queue maintenance degrades from organized effort to individual goodwill to abandonment.

### 4.1 Clear Owner

The current design has no named owner for the review queue. ADR-006 identifies the filtered library view as the mechanism, and the reviewer persona identifies Jordan as the actor — but no single person is accountable for the queue being cleared on schedule.

A sustainable queue requires a named role: **Queue Owner**. This is not Jordan (a reviewer) — it is Jordan's manager or a designated operations lead. The Queue Owner is accountable for throughput, not for performing reviews. Their responsibility is to ensure reviews happen, not to do the reviewing. In a brokerage context, this might be the Director of Transaction Services, the Operations Manager, or the Practice Lead for a deal type.

### 4.2 A Visible Metric with a Target

The queue must have a defined operational threshold and a visible dashboard. Based on the architecture and the SOW structure, the minimum viable metric set is:

| Metric | Target | Source |
|--------|--------|--------|
| Queue depth (pending items) | < 100 items at any time after initial batch | SharePoint filtered view count |
| Weekly throughput | >= 60 reviews/week across all reviewers | Reviewed items with ReviewedDate this week |
| Correction rate | > 5% (proxy for genuine engagement, not rubber-stamping) | Corrected / (Approved + Corrected) |
| Time to first prompt iteration | <= 8 weeks after initial batch ships | SOW milestone |

These are not monitoring-only metrics — they need to be surfaced to the Queue Owner on a weekly basis. A Power BI dashboard or even a SharePoint list view with count summaries is sufficient. Invisible queues do not get cleared.

### 4.3 A Consequence for Non-Performance

This is the hardest property to establish — and it is the one most likely to be absent.

The current design relies on voluntary participation. Jordan reviews when asked, when reminded, when it is convenient. This is not an accountability structure; it is a request. Voluntary participation decays reliably over 2–4 weeks for tasks with no direct incentive and delayed consequences (the persona research documents exactly this behavior).

The minimum viable consequence structure is one of the following:

**Option A: SOW milestone gate.** The SOW's prompt tuning iterations are tied to a correction data threshold (e.g., "Round 2 tuning begins when 400 corrections have been collected and reviewed"). This makes correction data collection a contractual gate, not a background activity. The client must maintain the queue to receive the next SOW deliverable.

**Option B: Quarterly operations review inclusion.** The Queue Owner presents queue metrics at the quarterly operations review. Pending item count and correction rate appear alongside deal pipeline metrics. This makes metadata quality a visible management metric, not a technical one.

**Option C: Reviewer time formal allocation.** Each named reviewer has a weekly time allocation for review (e.g., 30 minutes / 15 items per week) written into their operational responsibilities. This converts the task from "extra work" to "part of the role."

Any one of these creates a structural consequence. None of them require technical changes — they are organizational and contractual decisions.

---

## 5. Is the 63-Day Backlog a Design Problem or a People Problem?

The 63-day projection (3,750 items / 60 per day / 2 SMEs / 30 per day each) is optimistic. The system dynamics analysis estimates realistic throughput is closer to 10–15 items/day total across all reviewers when review is a secondary duty, which produces a 250–375 day drain time. At that rate, the queue becomes psychologically permanent — a wall of work no one believes they can finish.

The answer is: it is both, and the distinction matters because each requires a different intervention.

### The Design Problem Component

The current design routes all low-confidence items to the same queue with no differentiation. Three structural design changes reduce the volume problem without adding reviewers:

**1. Phased batch execution.** Rather than running all 25,000 documents through the pipeline and depositing 3,750 items into the queue at once, run the batch in 5,000-document waves. Only proceed to the next wave after the review queue drains below 50 items. The system dynamics analysis identifies this as Leverage Point 2 — restructuring the stock-and-flow relationship rather than adding capacity. This converts a 3,750-item wall into five manageable 750-item sprints.

**2. Active learning sample selection.** Rather than reviewing items randomly, the first 150–200 items reviewed should be the most informative ones — documents near classification decision boundaries, underrepresented categories, cases where Agent 1 and Agent 2 disagree. Corrections from this sample are used to retune prompts before the next wave. The review-queue-ux-analysis estimates this approach reduces total review volume by 75–80%, turning 3,750 items into approximately 700–900.

**3. Bulk approval for high-confidence clusters.** Documents with only a single category below threshold, at confidence 0.72–0.74 (just below the threshold), in a homogeneous group (same document type, same deal type) can be reviewed as a batch rather than individually. A SME who can confirm "yes, all 40 of these are Lease Agreements in the Katy submarket" completes 40 reviews in 5 minutes, not 200.

These three interventions together can reduce the effective review burden from 3,750 items to under 800 — a 4-week effort at 60 reviews/day rather than a 63-day obligation.

### The People Problem Component

Even at 800 items, the queue will not be cleared without organizational commitment. The design problem component addresses the volume. The people problem component requires:

- A time-bounded framing: "We have a 3-week review sprint to clear the initial batch" rather than "the queue needs ongoing maintenance." Jordan and other reviewers will commit to a sprint. They will not commit to an indefinite obligation.
- Named reviewers with explicit week-by-week targets during the sprint.
- A visible completion state: Jordan and colleagues can see the queue shrinking toward zero, not just shrinking.
- A clear handoff: after the sprint, review volume drops to steady-state (10–20 items/week in trigger mode), which is manageable as a 15-minute weekly task if the queue is handed off cleanly.

The 63-day risk is a design problem that can be reduced to a 4-week sprint with the interventions above, and then a people problem in that a 4-week sprint still requires deliberate organizational mobilization. The intervention for the people problem is framing and accountability, not more UX work.

---

## 6. Value Proposition for Jordan's Manager

The audience for this section is the brokerage operations leader who controls Jordan's time — the person who must decide whether review work is a real priority or a background nice-to-have.

### The Problem They Currently Have

The brokerage's SharePoint libraries are an unreliable search surface. Documents are named inconsistently, filed by feel, and metadata columns are empty or filled manually by whoever last touched the file. When a broker needs a document on a call, they ask Jordan, Jordan finds it by folder memory, and the knowledge stays implicit. Every new hire or associate who joins the team cannot find documents without asking someone who "knows the system."

The current state is not neutral — it costs real time, creates real risk (incorrect confidentiality labels on sensitive documents), and scales poorly as headcount grows.

### What the AI Pipeline Delivers (If the Queue Works)

The pipeline deposits accurate, consistent metadata on every document: deal type, submarket, counterparty, document classification, and confidentiality level. With those five fields populated consistently:

- SharePoint search becomes reliable. A broker can filter the library by "Lease, NW Houston, Confidential" and get a valid result set.
- Copilot for M365 can answer deal-specific questions. "What's the status of our active lease deals in Katy?" becomes a question Copilot can answer from metadata, not from someone's memory.
- Reporting becomes automatic. Quarterly deal summaries by submarket or deal type can be generated from the library, not assembled manually.
- Jordan stops being the library's memory.

None of this works at initial AI accuracy alone. The three rounds of prompt tuning in the SOW exist precisely because initial accuracy (estimated 80–85% on well-represented categories; lower on edge cases) is not sufficient for Copilot reliability. Every percentage point of accuracy improvement requires correction data. The review queue is the only source of that data.

### The Ask, in Manager Terms

The manager needs to authorize two things:

1. A 3-week sprint where Jordan and one other named SME spend 30 minutes per day clearing the initial batch queue. Total time commitment: approximately 9 hours per person over 3 weeks — less than one day of work per person, spread across daily tasks.

2. An ongoing weekly habit where Jordan spends 15 minutes per week reviewing the 10–20 new items generated by trigger mode. This is sustainable indefinitely and falls well within Jordan's SharePoint workflow.

In exchange, the manager gets: a document library that supports reliable search and Copilot queries, a metadata layer that requires no ongoing manual maintenance after the initial sprint, and the ability to demonstrate an AI investment that produced a measurable operational improvement.

The alternative — letting the queue stagnate — costs the same 9 hours but delivers none of the improvement, because the tuning iterations that require correction data never fire.

---

## 7. Top 3 Organizational Risks If Queue Maintenance Is Not Locked In

### Risk 1: The SOW Deliverables Cannot Be Fulfilled

The three prompt tuning iterations are the highest-value items in the SOW for long-term accuracy. Each iteration requires a minimum correction dataset — the analysis suggests approximately 200 quality corrections per iteration to produce statistically meaningful signal. If the queue is abandoned, the implementation team has no data. They face a binary choice: deliver tuning iterations with synthetic or fabricated data (which does not improve real-world performance) or surface the delivery failure to the client. Either outcome damages the engagement.

**Probability if queue is not locked in:** High. The system dynamics analysis identifies queue abandonment as the default attractor state without explicit intervention.

**Likelihood of detection before it is too late:** Low. The correction data gap only becomes visible when the first tuning iteration is due — weeks after the queue has been neglected.

### Risk 2: Metadata Trust Erodes Before It Is Established

The window for establishing user trust in AI-assigned metadata is narrow. Users will form their assessment of the system in the first 4–6 weeks after the initial batch ships. If they encounter visibly wrong classifications during that window — a lease filed as a market report, a confidential document tagged Internal — they will generalize that the AI "doesn't work" and stop relying on the metadata columns.

Once that narrative is established in a deal team, it is extremely difficult to reverse. Users will continue opening documents manually, calling Jordan, and bypassing the metadata entirely — even after prompt tuning has improved accuracy to 95%. They will not re-evaluate the system; they will remember their first experience.

The review queue's function during the initial batch is not just to provide correction data — it is to catch the most visible errors before they create a first-impression failure. Without timely review, the system's first impression is whatever the initial AI accuracy produces, including its worst failures.

**Probability if queue is not locked in:** High for the initial batch period. Metadata quality is directly correlated with queue throughput during weeks 1–8.

**Likelihood of detection:** Moderate. Visible misclassifications surface quickly if deal teams are using the library. Less visible if adoption is low at launch.

### Risk 3: Confidentiality Misclassification Generates Compliance Exposure

ADR-003 sets a confidentiality threshold of 0.85 — the highest in the taxonomy — because the consequences of a misclassification are not a search quality problem; they are a data governance problem. A document tagged "Internal" that should be "Highly Confidential" is accessible to people who should not have it.

Fifteen percent of documents go to review. In a batch of 25,000 documents, approximately 3,750 are below threshold on at least one category. Some fraction of those will have their primary uncertainty in the Confidentiality category. If those items sit in the review queue unreviewed for 60+ days, they have live metadata — "Under Review" status does not prevent access. They are accessible to anyone with library permissions, with AI-proposed confidentiality labels that may be wrong.

The review queue was designed as a quality gate. If it is not staffed, it is not a gate — it is a waiting room with no one checking credentials.

**Probability if queue is not locked in:** Medium. Depends on the proportion of confidentiality-uncertain documents and the client's actual access control model.

**Likelihood of detection:** Low until an incident occurs. Confidentiality misclassifications are not discoverable through normal usage — they require an explicit audit.

---

## 8. One Structural Change That Most Reduces Queue Abandonment Risk

### Recommendation: Tie the Prompt Tuning Iterations to Correction Volume in the SOW

Of all the options available — UX improvements, organizational framing, dashboard visibility, reviewer accountability models — the single most durable structural change is to make correction data a contractual gate in the SOW, not a background operational assumption.

**The change:** Add a clause to the SOW (or an addendum to the delivery schedule) that states:

> Each prompt tuning iteration will be initiated when a minimum of [200] reviewed and corrected items are available in the corrections dataset. The implementation team will notify the client when this threshold is reached. If the threshold is not reached within [6 weeks] of the previous iteration, the implementation team will notify the client's Queue Owner and escalate to the engagement sponsor.

**Why this is the highest-leverage intervention:**

It converts the review queue from a design feature into a contractual dependency. Jordan's manager — who controls Jordan's time — now has a direct line of sight between "Jordan reviews the queue" and "we receive the next SOW deliverable." The manager's decision about time allocation changes character: it is no longer a question of whether metadata maintenance is worth the time, but whether the firm wants to receive what it contracted for.

It removes the implementation team from the uncomfortable position of watching the queue stagnate while being unable to escalate without creating a client relationship problem. The clause creates a shared protocol for escalation: after 6 weeks without reaching threshold, the team has contractual grounds to notify the sponsor directly.

It is SOW-side, not code-side. It requires no technical changes, no UX changes, and no organizational restructuring beyond naming a Queue Owner and including queue metrics in the delivery tracking process. It can be agreed in a single conversation with the client's project sponsor.

**What it does not solve:**

This intervention does not solve the 63-day volume problem — that requires the phased batch and active learning approaches described in Section 5. It does not solve the routing and expertise-matching problem. It does not guarantee Jordan will do high-quality reviews rather than rubber-stamping. But it creates the organizational pressure that makes those other problems solvable, because without it, the motivation to invest in any of those improvements simply does not exist.

---

## 9. Blocking Questions Before Committing to Final Design

The following questions cannot be resolved through design or architecture. They require answers from the client before the review queue design should be considered final.

| Question | Why It Blocks | Who Can Answer |
|----------|--------------|----------------|
| Has the client named a Queue Owner — a single person accountable for queue clearance, not for performing reviews? | Without a named owner, no accountability structure is possible. UX and SOW changes are wasted without this. | Engagement sponsor / COO equivalent |
| Is review work formally allocated in any reviewer's time or responsibilities? | If review is genuinely optional from the manager's perspective, voluntary participation will decay within 2–3 weeks regardless of design quality. | Jordan's manager |
| Is the SOW amendable to include a correction-volume gate on prompt tuning iterations? | This is the single highest-leverage structural change. If the client will not accept it, the team needs an alternative accountability mechanism before proceeding. | Account lead + client project sponsor |
| Are the 3,750 review items distributed across the deal team's knowledge areas, or concentrated among a few specialists? | If concentrated, a general "all SMEs review" approach wastes generalist time on specialized questions and produces low-quality corrections. Routing by expertise becomes mandatory, not optional. | Deal team lead + roster of active deal areas |
| Does the client have the operational will to phase the initial batch into waves (5K-doc increments) rather than running all 25K at once? | Phased batch execution is the primary mitigation for the 63-day backlog risk. If the client requires a single-shot batch for reporting or timeline reasons, the team must plan for a 3,750-item queue and staff accordingly. | Operations lead / engagement sponsor |
| Can Jordan and at least one other named SME commit to 30 minutes per day for 3 weeks during the initial batch sprint? | The 4-week sprint framing is only valid if the reviewers can actually commit. If they cannot, the framing fails and a different model (temporary reviewers, bulk approval, expanded team) is needed. | Jordan's manager |

---

## 10. Handoff

### To the Product Coach

The core finding is that the review queue's business case is sound but organizationally orphaned. The value is real, the design is appropriate, and the implementation is feasible — but none of it delivers lasting value without a Queue Owner, a correction-volume gate in the SOW, and a time-bounded framing for the initial batch sprint. These are not design questions. They are product and commercial questions that must be resolved before the next phase of implementation proceeds.

The top recommendation is to add a correction-volume gate to the SOW as described in Section 8. This is the one change that creates durable organizational pressure without requiring new technology, new processes, or new staff.

### To the System Designer / Architecture

The 63-day backlog risk is partially a design problem. The three interventions that address it technically are: (1) phased batch execution with a queue-depth gate between waves, (2) active learning sample selection to reduce total review volume by 75–80%, and (3) bulk approval for high-confidence clusters. The system dynamics analysis already identifies phased execution as Leverage Point 2. These changes should be evaluated for inclusion in the batch orchestration design before the initial batch runs.

The inline filtered library view (ADR-006) remains the correct UX choice given what we know. The outstanding question is whether the AIOriginalClassification diff mechanism works end-to-end when corrections are made via inline grid editing — this must be validated in a sandbox before the review phase begins.
