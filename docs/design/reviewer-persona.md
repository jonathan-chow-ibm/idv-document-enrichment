# Reviewer Persona: CRE Deal-Support SME

**Document type:** Design Thinking — Empathize + Define  
**Date:** 2026-08-18  
**Status:** Synthesized from domain knowledge (no direct user research conducted — see Assumptions section)  
**Related artifacts:** [ADR-006](../decisions/adr-006-inline-review.md), [review-queue-ux-analysis.md](review-queue-ux-analysis.md)

---

## 1. Persona Profile

### Primary Persona: The Deals Desk Generalist

**Name (archetype):** Jordan  
**Role:** Deal coordinator / transaction analyst at a mid-size Houston CRE brokerage  
**Experience level:** 3–7 years in commercial real estate; comfortable with Microsoft Office and SharePoint; no experience with enterprise software governance, Power Apps, or workflow automation tools

### Core Goals

- Close deals and support the deal team with timely, accurate document access
- Find the right document quickly when a broker calls during a site visit
- Keep the shared library organized enough that colleagues stop asking Jordan where things are
- Not be blamed when a document is misfiled or a client gets the wrong version

### Behavioral Profile

| Dimension | Detail |
|-----------|--------|
| Primary tools | Outlook (email), Teams (chat + calls), SharePoint document libraries (browse and open) |
| Document habits | Browses by folder/library rather than searching; opens documents directly to scan; saves files by dragging to a library |
| Decision-making style | Context-dependent — comfortable making quick calls on familiar document types; slow and uncertain on edge cases |
| Attitude toward AI tools | Curious but skeptical; will try something once and abandon it if it requires effort or causes confusion |
| Attitude toward metadata work | Sees it as maintenance work, not their primary job; does it when asked by a manager, not proactively |
| Digital comfort level | Intermediate Microsoft 365 user; knows SharePoint views, basic column filtering; has never opened Power Apps |
| Mobile use | Checks Teams and Outlook on mobile; never uses SharePoint on mobile for document management |

### Frustrations

- Spending time on "admin" work that takes them away from deal support
- Being asked to review something without knowing how urgent it is or what decision is being made
- Receiving vague instructions: "review the AI queue" — what does that mean?
- Finding out a document was miscategorized because someone approved it without actually reading it
- Having to remember a URL or navigate to an unfamiliar tool to complete a task
- Not knowing whether their input mattered — did the correction get applied?

### What They Value

- Staying in the tools they already have open (Outlook, Teams, SharePoint)
- Knowing exactly what they're being asked to do and how long it will take
- Seeing that their corrections make a difference
- Being asked about documents they actually know — not random files from a deal area they've never touched

---

## 2. Empathy Map

The scenario is: Jordan's organization has deployed the AI classification pipeline. Some documents have been flagged for human review. Jordan has been told they are one of the SMEs who should "review the flagged documents."

| Dimension | What Jordan Experiences |
|-----------|------------------------|
| **Thinks** | "How many is there? Is this going to take an hour?" / "I already know what most of these are — why is the AI uncertain about this one?" / "If I get it wrong, is someone going to blame me?" / "Nobody told me the deadline for this." |
| **Feels** | Mildly anxious when the queue is large and undefined. Neutral-to-mild-positive when they understand the scope. Frustrated when forced to switch to an unfamiliar interface. Confused when the AI's reasoning doesn't match what they see in the document. |
| **Sees** | A SharePoint library with document names they recognize. Columns they do not know the meaning of (TypeConfidence, AIReasoning). A Teams message asking them to "review the queue." An Outlook email with a link they're not sure where it goes. |
| **Says & Does** | Says: "I'll get to it this afternoon." Does: opens Teams, gets pulled into something else, forgets. When they do sit down: opens the document first, reads the top page, then checks what the AI said. If it matches their reading, approves quickly. If it doesn't, reads more carefully and may ask a colleague. |
| **Hears** | From their manager: "The client wants the library cleaned up before Q3 reporting." From a colleague: "I just approved everything in there, took me 5 minutes." From IT: "You need to go to this URL to do the reviews." From another colleague: "I have no idea what they want me to do." |
| **Pains** | No natural trigger in their daily workflow routes them to the review task. The queue is invisible unless they navigate to it deliberately. Context switching from deal work is cognitively expensive. Documents outside their deal area require research they don't have time to do. Instructions are assumed, not explained. |
| **Gains** | If the metadata is right, Jordan stops getting asked "where's that lease for the Westheimer project?" Finding their own documents faster is a direct personal payoff. Being asked only when their expertise is genuinely needed (not rubber-stamping 80% AI-correct items) feels like their time is respected. |

### Key Insight from Empathy Mapping

Jordan does not have a "review queue workflow." They have a **document library workflow**. Every time they interact with documents — opening a file, renaming something, sharing a link with a client — they are already in SharePoint doing exactly the kind of contextual document judgment that review requires. The problem is that the review task has been designed as a separate destination, not an overlay on the workflow they already use.

---

## 3. Current-State Journey: Reviewing a Flagged Document (Without Our System)

This describes how Jordan currently handles a document classification or filing question when it arises organically — before any AI review system exists.

```
TRIGGER                    ACTION                      EMOTION
────────                   ──────                      ───────

Broker asks:               Jordan opens Teams           Mildly interrupted
"Where's the LOI           and searches for             but familiar — this
for Greenway?"             the document name            is normal work

                           Document not found           Mild frustration —
                           by name search;              search is unreliable
                           browses by folder            here

                           Opens the folder,            Focused; scanning
                           identifies the file          familiar document
                           by name pattern              names
                           (date, deal name)

                           Opens the document           Confident — reading
                           to confirm it's              is their primary
                           the right one                verification method

                           Shares the link              Task complete;
                           with the broker              switches back to
                                                        deal work

NO TAGGING HAPPENS.        The document remains         Jordan never thought
The judgment Jordan         untagged. The next          about classification.
just made — "yes,          person who needs it          The judgment was
this is the LOI for        will repeat this             ephemeral.
Greenway" — is lost.       process.
```

**What this tells us:** Jordan makes document classification judgments constantly in their normal workflow. The problem is not capability or willingness — it is that the judgment is made in passing and never captured. A review system that catches Jordan at the moment they are already thinking about a document has a far higher chance of capturing a useful signal than one that asks them to go somewhere else to make the same judgment in isolation.

---

## 4. Point of View Statement

> **Jordan, a CRE deal-support SME,** needs a way to **contribute accurate document classification decisions without leaving their existing SharePoint workflow** because **every moment they spend navigating to a separate tool is a moment taken from deal support, and context-switching to an unfamiliar interface produces either abandonment or rubber-stamping — neither of which serves the accuracy goal.**

### Supporting Evidence

- The client organization has explicitly been flagged as inexperienced with enterprise software governance — there is no established habit of navigating to separate review applications
- Jordan's primary tools are Outlook, Teams, and SharePoint document libraries — not Power Apps or any dedicated review UI
- The most natural unit of review for a CRE professional is a document they are already looking at, not a metadata record in a list
- The alternative-approaches analysis and review-queue-ux-analysis both independently identified app-switching as the top adoption risk
- ADR-006 was accepted precisely because SMEs "work in SharePoint document libraries all day — they don't want another app"

---

## 5. How Might We Questions

### HMW 1

**How might we make the review action appear exactly where Jordan is already working — in the document library — so that contributing a classification correction costs no more than a column edit?**

This question points toward Design B (inline library view): the review surfaces as a filtered view of the same library Jordan uses daily, with an editable column grid. No navigation, no new app, no mental model shift. The question also points toward the Teams Adaptive Card concept for steady-state, where review appears in a tool Jordan has open all day.

### HMW 2

**How might we show Jordan only the documents where their specific expertise is genuinely needed, so that reviewing never feels like an endurance task?**

This question points toward the routing logic that connects a document to the right reviewer — someone who works that submarket, that deal type. It also points toward the active-learning approach: review the most informative 150–200 items first, not all 3,750. If Jordan is only ever shown documents where the AI is genuinely uncertain and where Jordan's domain knowledge is relevant, the task feels like expert consultation rather than data entry.

---

## 6. Assumptions We Are Making vs. Validated Needs

### Validated Needs (grounded in observable behavior or stated architecture)

| Need | Evidence |
|------|----------|
| Review must live inside SharePoint | Explicitly stated by client: primary tools are SharePoint, Outlook, Teams. ADR-006 accepted on this basis. |
| Review must require minimal navigation | CRE professionals' primary cognitive context is deal execution. Any navigation cost compounds abandonment. |
| Each review decision must be completable in under 2 minutes | At higher time costs, deal-team professionals cannot sustain any review throughput. |
| Jordan needs to know the scope before starting | Undefined queues ("go review the queue") are the most common cause of deferral. |
| Jordan reads the document before deciding | Observed behavior: CRE professionals verify by opening and reading. They do not trust metadata alone. |

### Assumptions We Have Not Validated

| Assumption | Risk if Wrong | What Would Confirm or Deny It |
|------------|---------------|-------------------------------|
| Jordan is willing to edit columns directly in SharePoint grid view | Grid editing is a standard SharePoint capability, but many users are unaware of it or find it non-obvious. If Jordan does not know how to trigger edit mode on a library view, Design B fails silently. | Observe 3 uncoached users performing a column edit in a shared library. If any cannot find the edit mode, the interaction design needs explicit affordance. |
| A filtered library view is visually distinct enough that Jordan notices it is a "review task" rather than just a document list | Without a clear visual cue that this view represents documents needing attention, it may just look like another folder. | Show 5 users the filtered view with no explanation and ask: "What would you do here?" If they cannot identify the review intent, the visual design needs work. |
| Jordan will notice the AIProcessingStatus = "Under Review" column and understand what it means | SharePoint columns are not self-explanatory to non-technical users. | Ask 3 users: "What does 'Under Review' mean for this document?" If they interpret it as "someone else is already reviewing it" (not "needs my review"), the label must change. |
| Jordan has sufficient domain knowledge to correct any document in the library | Deal-team generalists may be strong on deal types they work frequently but uncertain about submarket boundaries, counterparty names for deals they didn't touch, or confidentiality classifications for documents from other teams. | Map the 3,750 review items against deal team member portfolios. If >30% of items fall outside any individual's expertise area, intelligent routing is a prerequisite, not a nice-to-have. |
| Jordan will not simply approve everything without reading it | The rubber-stamping risk is well-documented in the review-queue-ux-analysis. This is the most dangerous assumption because it produces an outcome that looks like success (queue cleared) while delivering failure (low-quality corrections). | Compare correction rates between reviewers who spend <30 seconds/item vs. >2 minutes/item. If they produce identical correction rates, either the AI is accurate enough that review adds no value, or everyone is rubber-stamping. |
| Inline column editing in a library view produces corrections that are captured by the AIOriginalClassification diff mechanism | ADR-006 specifies that corrections are detected by diffing current column values against the AIOriginalClassification JSON snapshot. This only works if Jordan edits the same columns the AI wrote to — not if they edit a different column or use a different field name. | Verify end-to-end in a sandbox: make a manual column edit, run analyze_corrections.py, confirm the correction is detected. This is a technical assumption, not a UX one, but it must be validated before the correction feedback loop is trusted. |

---

## 7. Design Recommendation

### Which Design Fits Jordan's Natural Habitat?

**Design B (inline filtered library view) is the stronger fit — with qualifications.**

Design A (separate SharePoint list + Power Apps form) requires Jordan to:
1. Remember or receive a link to a different list
2. Open a Power Apps form they have never used
3. Operate in an interface with no visual or contextual relationship to the document library they know
4. Trust that the form is connected to the real documents (this is non-obvious)

Every one of these steps is a drop-off point. For a non-technical client who has been flagged as inexperienced with enterprise software governance, each unfamiliar step roughly doubles the abandonment probability.

Design B requires Jordan to:
1. Navigate to a view they may or may not know exists (this is a real friction point — see assumptions above)
2. Filter or open a pre-filtered view they have been given a link to
3. Edit a column in a library they already use

Steps 2 and 3 are within Jordan's existing SharePoint mental model. Step 1 is solvable with a pinned link in Teams or a pinned view in the library.

### Qualifications and Remaining Risks

**Design B is not complete without the following:**

1. A clear, prominent entry point. A Teams channel message with a direct link to the filtered view, sent on a cadence (e.g., "You have 12 documents to review this week — [open review view]"), is the trigger that Design B lacks in its current form. Without this, the filtered view exists but Jordan never navigates to it.

2. Column labels that communicate intent. "AIProcessingStatus = Under Review" is engineer-readable, not SME-readable. The column header should communicate action, not state: "Needs Your Review" or a visual badge in the document name column.

3. Explicit scope communication. Jordan needs to see "12 items need your review" before they open the view — not discover a wall of documents and estimate the work themselves.

4. The Teams Adaptive Card path for steady-state. Once the initial batch is cleared, ADR-006's inline view is appropriate for the trickle (10–20 items/day). But a micro-review in Teams ("This lease was classified as NW Houston — does that look right? [Yes] [No, correct it]") may be even lower-friction for steady-state. This was flagged as "deferred" in ADR-006 and is worth revisiting once the batch phase is complete.

### Unknowns That Block a Final Recommendation

1. **Can Jordan find and activate column editing in a SharePoint library view without coaching?** If the answer is no for more than one in three users, the interaction design for Design B needs a guided affordance (a "Review" button that opens the item form), not just the raw grid view.

2. **Are the 3,750 batch items distributed across the deal team's knowledge areas, or concentrated in a few specialists?** If concentrated, intelligent routing is mandatory before either design can work at the batch scale. If broadly distributed, either design can send a general "please review" prompt.

3. **Has the client defined who is responsible for clearing the queue, and is it in anyone's performance objectives?** The best UX in the world will not sustain review behavior if no one is accountable for queue clearance. This is a product-coach question, not a design question — but it is a prerequisite for any design recommendation to hold.

---

## 8. Handoff

### To System Designer / Architecture

- ADR-006 (Design B) is the correct architecture given what we know about this persona. Retain it.
- The correction detection mechanism (AIOriginalClassification diff) must be validated end-to-end in a sandbox before the review phase begins. A silent failure here means corrections are not captured at all.
- Consider a lightweight notification mechanism (Power Automate sending a weekly Teams message with a direct link to the filtered view and a count of pending items) as the trigger that makes Design B discoverable. Without it, the filtered view is a destination with no path to it.
- For steady-state, revisit Teams Adaptive Cards as the delivery channel. The inline library view serves the initial batch; micro-review in Teams may serve the trickle better.

### To Product Coach

- Validate whether any deal-team member's objectives include a metadata quality metric. If not, voluntary review will decay after the first 2–3 weeks regardless of design quality.
- Define "done" for the initial batch review explicitly: a time-boxed 2-week sprint with named reviewers and a target item count, not an open-ended obligation. Jordan needs scope, not a queue.
- Confirm whether the client has the organizational will to route specific documents to specific reviewers by deal area. Without that routing, a generalist reviewer will encounter unfamiliar documents and skip rather than correct — defeating the purpose.

### Recommended Next Steps Before Committing to Final Review UX

1. Observe 3 uncoached users performing a column edit in a SharePoint library view (15-minute session). This resolves the most critical unknown about Design B's interaction model.
2. Map the 3,750 review items against deal team portfolios to determine whether intelligent routing is a prerequisite or a nice-to-have.
3. Validate the AIOriginalClassification diff mechanism end-to-end in a sandbox.
4. Confirm with the product coach whether review accountability is established before investing further in the UX.
