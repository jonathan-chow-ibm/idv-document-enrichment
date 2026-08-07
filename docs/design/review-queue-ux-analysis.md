# Review Queue UX Analysis — Problem Frame

## Evidence Summary

This problem frame is informed by:

- Architectural design: [human-review-queue.md](../architecture/human-review-queue.md)
- Pipeline design: [pipeline-design.md](../architecture/pipeline-design.md)
- System dynamics: [system-dynamics-analysis.md](system-dynamics-analysis.md)
- Domain context: CRE investment firm, deal-team SMEs (brokers, analysts, asset managers)

No direct user research has been conducted. This analysis synthesizes domain knowledge about CRE professionals and known patterns from review-queue systems to identify UX risks before deployment.

---

## Empathy Evidence: The CRE SME Reviewer

### Who They Are

Deal-team professionals — brokers closing transactions, analysts building financial models, asset managers overseeing portfolios. Their primary job involves deal execution, relationship management, and investment decision-making. They are paid for judgment on deals, not metadata hygiene.

### Empathy Map (Synthesized)

| Dimension | Findings |
|-----------|----------|
| **Think & Feel** | "I have three LOIs to review and a closing next week. Classifying documents is not why I was hired." Frustrated when pulled into administrative tasks. Values their domain expertise but not as a classification engine. |
| **See** | Outlook inbox with 200+ emails. SharePoint libraries they rarely organize. Teams chats from deal partners. A Power Apps form they've never used before. |
| **Say & Do** | Say: "I'll get to it later." Do: Prioritize deal work. Open the queue once, get overwhelmed by 3,750 items, close it. May rubber-stamp a few when reminded. |
| **Hear** | Manager: "We need this metadata cleaned up for reporting." Colleagues: "The AI should just handle this." IT: "We need SMEs to validate the AI." |
| **Pains** | Time is zero-sum — every minute reviewing classifications is a minute not spent on revenue-generating work. No clear incentive. The task feels mechanical and beneath their expertise level. Context-switching between deal work and classification review is cognitively expensive. |
| **Gains** | Would value: finding their documents faster (the outcome of good metadata). Would value: AI handling this without needing them. Would value: being asked only when their specific expertise genuinely matters. |

---

## Candidate POV Statements

| # | POV Statement | Specificity | Need-Based? | Insight Quality | Selected? |
|---|---------------|-------------|-------------|-----------------|-----------|
| 1 | A CRE deal-team analyst needs a way to **contribute domain expertise to AI classification without dedicating blocks of focused time** because their primary value is deal execution, and context-switching to a review queue competes directly with revenue-generating work. | High | Yes | Strong | ✓ |
| 2 | An asset manager needs a way to **trust that document metadata is correct without personally verifying thousands of items** because they rely on accurate search and reporting but cannot scale manual review to the volume of documents produced. | High | Yes | Strong | ✓ |
| 3 | The operations team needs a way to **clear the review backlog predictably** because stale items in the queue mean documents remain unfindable, undermining the entire investment in AI classification. | Medium | Yes | Medium | ✗ |

### Selected Primary POV

> **A CRE deal-team analyst** needs a way to **contribute domain expertise to AI classification without dedicating blocks of focused time** because **their primary value is deal execution, and context-switching to a review queue competes directly with revenue-generating work.**

### Selected Secondary POV

> **An asset manager** needs a way to **trust that document metadata is correct without personally verifying thousands of items** because **they rely on accurate search and reporting but cannot scale manual review to the volume of documents produced.**

---

## How Might We Questions

| # | HMW Question | User Impact (1–5) | Feasibility (1–5) | Evidence Strength (1–5) | Priority |
|---|--------------|-------------------|--------------------|-------------------------|----------|
| 1 | HMW reduce the number of items that need human review in the first place? | 5 | 4 | 5 | **High** |
| 2 | HMW embed review into existing SME workflows (email, Teams, SharePoint) rather than requiring them to visit a separate app? | 5 | 3 | 4 | **High** |
| 3 | HMW make the "usually correct" cases disappear so SMEs only see genuinely ambiguous items? | 5 | 4 | 4 | **High** |
| 4 | HMW make each review decision take under 30 seconds instead of 5 minutes? | 4 | 3 | 4 | **High** |
| 5 | HMW use agreement between multiple quick signals rather than one deep review to build confidence? | 4 | 4 | 3 | **Medium** |
| 6 | HMW give SMEs a reason to review that connects to outcomes they care about (finding their own docs faster)? | 3 | 3 | 3 | **Medium** |
| 7 | HMW handle the initial 3,750-item flood differently from the steady-state trickle? | 4 | 4 | 5 | **Medium** |
| 8 | HMW learn from the first N reviews to auto-resolve remaining similar items? | 5 | 3 | 4 | **High** |

---

## Key Risks in the Current Design

### Risk 1: The Queue Will Never Be Cleared

**Evidence:** 3,750 items ÷ 60/day capacity (2 reviewers × 30/day) = 63 business days. In practice, these are deal-team professionals, not dedicated reviewers. Realistic throughput is likely 10-15 items/day total across all reviewers. At that rate: **250-375 business days** — the queue becomes permanent.

**Impact:** Documents remain unfindable. The feedback loop that improves AI accuracy never fires at scale. Users lose trust in the system.

### Risk 2: Rubber-Stamping Renders Review Meaningless

**Evidence:** If the AI is correct 85%+ of the time (as designed), the reviewer's experience is: Approve. Approve. Approve. Approve. Correct one. Approve. This is a vigilance task — humans are notoriously bad at sustained vigilance when the base rate of "something interesting" is low. Reviewers will learn the AI is usually right and stop reading carefully.

**Impact:** The 15% of items that actually need correction get approved uncritically. The review queue provides the illusion of quality control without the substance.

### Risk 3: Context-Switching Cost Makes 5 Min/Item Unrealistic

**Evidence:** Each review requires: opening a document they've never seen, parsing AI reasoning, understanding which category is uncertain, recalling taxonomy definitions, making a judgment. For a document outside their specific deal area, this could easily be 10-15 minutes. For documents in their area, maybe 2 minutes. The average masks this bimodal distribution.

**Impact:** Throughput projections are optimistic. Reviewers who encounter unfamiliar documents will Skip rather than learn — creating a growing pool of unkillable items.

### Risk 4: Power Apps Is Not Where SMEs Live

**Evidence:** CRE professionals work in Outlook, Teams, and SharePoint document libraries. Power Apps is a separate application with its own authentication flow, learning curve, and URL to remember. There is no natural trigger in their daily workflow that routes them into the review form.

**Impact:** Without workflow integration, review depends entirely on discipline and reminders — both of which decay rapidly after the first week.

### Risk 5: Flood-Then-Trickle Creates Two Unsolved Problems

**Evidence:** After the initial batch, 3,750 items arrive at once. After that, trigger mode produces ~10-20/day (at 15% of ~100-130 docs/day). The batch requires a project-style mobilization. The trickle requires a habit. These are fundamentally different UX and organizational problems.

**Impact:** A design optimized for steady-state trickle cannot handle the initial batch. A design that handles the batch (bulk operations, temporary staff) doesn't sustain as a daily practice.

---

## Alternative Approaches to Human-in-the-Loop Review

### Alternative A: Inline Confirmation (Micro-Review at Point of Use)

**Concept:** Don't ask SMEs to review documents in bulk. Instead, when an SME opens or searches for a document in SharePoint, show a lightweight banner: _"AI classified this as: Lease | NW Houston | CBRE. Correct? [👍] [Edit]"_. Review happens organically when users encounter documents in their actual work.

**Strengths:**
- Zero context-switching — review happens in the document library where SMEs already work
- Each review takes 5 seconds for the common "looks right" case
- Naturally prioritizes documents that people actually use
- Documents nobody ever opens probably don't need perfect metadata

**Weaknesses:**
- Slow to reach full coverage — documents nobody touches remain unreviewed
- Requires SharePoint Framework (SPFx) customization
- Harder to track completion metrics

**Verdict:** Best for steady-state trickle. Doesn't solve the initial batch.

### Alternative B: Active Learning with Minimal Human Input

**Concept:** Instead of reviewing 3,750 items, use a two-round approach:
1. SMEs review a strategically selected sample of ~100-200 items (the most informative items for improving the model — items near decision boundaries, items from underrepresented categories)
2. Use those corrections to re-classify remaining items at higher confidence
3. Only items still below threshold after round 2 go to human review (likely <500)

**Strengths:**
- Reduces human effort by 80-90%
- Provides better training signal (focused on decision boundaries)
- Respects SME time — they're asked for expertise, not endurance
- Enables faster iteration on prompt quality

**Weaknesses:**
- Requires engineering investment in active learning selection
- Adds a processing cycle between review rounds
- SMEs must review the HARDEST items first (higher cognitive load per item)

**Verdict:** Best for the initial batch problem. Dramatically reduces total review burden.

### Alternative C: Peer Agreement (Lightweight Multi-Reviewer)

**Concept:** Instead of one deep review per item, show each document's classification to 3 SMEs as a quick yes/no poll (via Teams Adaptive Card or email). If 2/3 agree "looks right," auto-approve. Only items with disagreement go to a full review. Each "micro-vote" takes 10 seconds.

**Strengths:**
- 10-second effort per person vs. 5-minute deep review
- Statistical confidence through agreement, not individual thoroughness
- Embeds into Teams/Outlook where SMEs already work
- Makes the task social — "others on the team reviewed 12 today"

**Weaknesses:**
- Still requires 3× the number of human touches (though each is trivial)
- May not catch subtle errors that all reviewers miss
- Requires routing logic to match documents to appropriate SMEs

**Verdict:** Best for the steady-state trickle and for the "AI is usually right" case. Catches the obvious errors through volume rather than depth.

---

## Recommendations

### For the Initial Batch (3,750 items)

1. **Do NOT route all 3,750 to the review queue.** Use Alternative B (active learning): have SMEs review ~150-200 strategically selected items, re-run classification with updated prompts, then only route the residual failures (~300-500 items) to full review.
2. **Add bulk operations** to the review form: "Select all items where AI proposed 'Lease' with confidence > 0.80 → Bulk Approve." Pattern-based bulk approval for the initial batch is essential.
3. **Time-box the effort**: frame it as a 2-week project with a defined end, not an open-ended obligation.

### For Steady-State (10-20 items/day)

4. **Deliver review items via Teams Adaptive Cards** — one card per item, inline approve/correct buttons, no app-switching required. Use Alternative C (peer agreement) for items where confidence is 0.70-0.84; reserve the full Power Apps form for items below 0.70.
5. **Implement progressive threshold relaxation**: if the correction rate for a category drops below 5% over 50 reviews, raise the auto-approve threshold for that category. The goal is to shrink the review queue to zero over time.
6. **Route items to the RIGHT SME**: a Houston submarket question should go to someone who works that submarket. A deal-type question should go to someone who does that deal type. Generic round-robin wastes expert attention.

### For Sustainability

7. **Make the outcome visible**: show SMEs how metadata quality improves search results. "Since review started, document search accuracy improved from 60% to 94%." Connect the task to the payoff.
8. **Target queue elimination, not queue management**: the goal of the review system is to make itself unnecessary. Track the trend of review rate over time — it should decrease as the AI improves.

---

## Assumptions to Test

| Assumption | Risk if Wrong | Test Method | Success Signal |
|------------|---------------|-------------|----------------|
| SMEs will dedicate time to review without direct incentive | Queue never clears; metadata stays broken | Pilot with 5 SMEs for 2 weeks; measure actual throughput | >10 items/day sustained across group |
| 5 min/item is realistic for unfamiliar documents | Throughput projections are 3-5× too optimistic | Time 10 SMEs reviewing 10 items each; measure actual duration | Median < 5 min including context acquisition |
| Power Apps form is accessible enough for non-technical SMEs | Adoption fails at the interface layer | Observe 3 SMEs using the form for the first time (no coaching) | Complete 5 reviews without asking for help |
| 85% AI accuracy means reviewers stay engaged | Rubber-stamping makes review theater | Measure correction rate per reviewer over time | Correction rate doesn't drop below actual error rate |
| The initial batch can be cleared in 60 days | Backlog becomes permanent; system trust erodes | Run active learning pilot on 500-doc sample | Sample review reduces remaining items by >75% |
| Teams Adaptive Cards are a viable review channel | Micro-reviews don't provide enough context for good decisions | A/B test: same 50 items reviewed in Power Apps vs. Teams Cards | Agreement rate > 90% between channels |

---

## Handoff

### To System Designer / Architecture

- **Active learning selection** requires an additional orchestration step between batch processing and review routing — the system must identify which items are most informative to review first
- **Teams integration** requires Adaptive Card design and a Bot Framework component or Power Automate connector for delivering review cards and capturing responses
- **Progressive threshold relaxation** requires the confidence routing logic to be configurable per-category and auto-adjustable based on correction rates

### To Product Coach

- **Validate incentive structure**: is metadata quality a measured outcome for deal teams, or only for operations? If SMEs have no stake in the outcome, no amount of UX optimization will sustain review behavior
- **Define acceptable risk**: what's the cost of a misclassified document? If low, reduce review scope aggressively. If high (e.g., confidentiality breaches), invest in review quality over speed
- **Scope the initial batch as a project**: assign it budget, timeline, and potentially temporary reviewers rather than treating it as BAU

### Recommended Next Steps

1. Run `design-assumption-testing` on the top 3 assumptions before committing to the current Power Apps design
2. Prototype Alternative C (Teams Adaptive Cards) for 50 items and compare review quality to the Power Apps form
3. Build the active learning selection logic to reduce initial batch review volume by 75-80%
