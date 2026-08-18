---
name: system-archetype-recognition
description: "Identify common system archetypes (fixes that fail, shifting the burden, limits to growth, tragedy of the commons, etc.) in the system under study. Use when diagnosing recurring problems, explaining why standard fixes don't work, or accelerating system analysis by matching observed behavior to known patterns. Produces archetype analyses in docs/design/."
argument-hint: "Describe the recurring problem or behavior pattern you want to diagnose"
---

# System Archetype Recognition

## When to Use

- A problem keeps recurring despite repeated fixes
- The team is stuck in a pattern they can't explain
- Accelerating analysis by matching observed behavior to known structural patterns
- After mapping feedback loops — archetypes are common loop configurations
- When explaining system behavior to stakeholders who aren't systems thinkers

## Key Concepts

System archetypes are recurring structural patterns that produce predictable behavior. Recognizing an archetype accelerates diagnosis because the structure, behavior, and effective interventions are already known. The archetype is a hypothesis — validate it against the specific system before acting.

## Common Archetypes

### 1. Fixes That Fail

**Pattern:** A quick fix addresses a symptom but creates side effects that make the original problem worse over time.

**Structure:**
- Problem → Quick fix → Symptom relief (balancing loop)
- Quick fix → Unintended consequence → Problem worsens (reinforcing loop with delay)

**Behavior:** Oscillation with worsening trend. Each fix helps briefly, then the problem returns worse.

**Signal phrases:** "We keep fixing this but it keeps coming back." "Every time we solve it, something else breaks."

**Leverage:** Address the root cause instead of the symptom. Anticipate side effects before applying the fix.

---

### 2. Shifting the Burden

**Pattern:** A symptomatic solution is used instead of a fundamental solution. Over time, dependence on the symptomatic solution grows while the capability to apply the fundamental solution atrophies.

**Structure:**
- Problem → Symptomatic solution → Relief (balancing loop)
- Problem → Fundamental solution → Relief (balancing loop, slower)
- Symptomatic solution → Side effect → Fundamental capability erodes (reinforcing loop with delay)

**Behavior:** Increasing dependence on the quick fix. The fundamental solution becomes harder to implement over time.

**Signal phrases:** "We can't stop doing this even though we know it's wrong." "We've become dependent on the workaround."

**Leverage:** Strengthen the fundamental solution. Weaken the symptomatic solution. Invest in capability building.

---

### 3. Limits to Growth

**Pattern:** A reinforcing process drives growth, but eventually hits a constraint that slows or stops it.

**Structure:**
- Growth action → Performance → More growth action (reinforcing loop)
- Performance → Constraint pressure → Slowing action (balancing loop, delayed)

**Behavior:** S-curve — initial exponential growth that plateaus or reverses.

**Signal phrases:** "We used to grow fast but now we've hit a wall." "We're doing the same things but getting diminishing returns."

**Leverage:** Identify and address the constraint before it binds. Don't push harder on the growth engine — relieve the constraint.

---

### 4. Tragedy of the Commons

**Pattern:** Multiple actors share a common resource. Each actor's individual rational behavior depletes the shared resource.

**Structure:**
- Actor A uses resource → A's gain (reinforcing loop)
- Actor B uses resource → B's gain (reinforcing loop)
- Total usage → Resource depletion → Reduced availability for all (balancing loop with delay)

**Behavior:** Resource works well initially, then degrades as usage grows. Each actor blames others.

**Signal phrases:** "Everyone is using it and nobody is maintaining it." "It worked fine until it got popular."

**Leverage:** Make total resource usage visible. Establish governance, quotas, or shared investment rules.

---

### 5. Eroding Goals

**Pattern:** When there's a gap between a goal and actual performance, the goal is lowered instead of performance being raised.

**Structure:**
- Gap between goal and performance → Pressure to improve (balancing loop)
- Gap → Pressure to lower goal → Reduced gap (balancing loop, easier path)

**Behavior:** Standards gradually decline. "Good enough" keeps getting worse.

**Signal phrases:** "We used to aim higher." "The definition of done keeps changing." "Expectations have shifted."

**Leverage:** Hold the goal firm. Make the gap visible. Invest in the corrective action rather than adjusting the standard.

---

### 6. Escalation

**Pattern:** Two parties each respond to the other's actions with increasing intensity, creating an arms race.

**Structure:**
- A acts → B feels threatened → B escalates → A feels threatened → A escalates (reinforcing loop)

**Behavior:** Mutual escalation until one party exhausts resources or both collapse.

**Signal phrases:** "They did X so we had to do Y." "It's an arms race." "Neither side can back down."

**Leverage:** Unilateral de-escalation. Shared goals. Third-party mediation. Changing the rules of engagement.

---

### 7. Success to the Successful

**Pattern:** Resources are allocated to the more successful of two competing activities, starving the other.

**Structure:**
- Activity A succeeds → More resources to A → A succeeds more (reinforcing loop)
- Resources to A → Fewer resources for B → B underperforms → Even fewer resources for B (reinforcing loop)

**Behavior:** Winner-take-all dynamics. The rich get richer.

**Signal phrases:** "The team that ships gets more funding." "We always invest in what's already working."

**Leverage:** Equalize opportunity, not just outcomes. Protect investment in early-stage or experimental initiatives.

---

### 8. Growth and Underinvestment

**Pattern:** Growth approaches a limit that could be raised by investment, but the investment is not made (or not made in time) because performance standards erode.

**Structure:**
- Growth → Demand > Capacity → Performance drops (balancing loop)
- Performance drops → Lower standards → Less perceived need to invest → Capacity stays flat (reinforcing loop)
- Investment → Capacity increase → Performance maintained (balancing loop, delayed)

**Behavior:** Growth stalls, quality degrades, and nobody invests because "that's just how things are."

**Signal phrases:** "We can't grow because we can't keep up." "Quality always suffers when we scale." "Why invest when performance is already 'acceptable'?"

**Leverage:** Maintain standards. Invest ahead of demand. Make the cost of not investing visible.

## Procedure

### 1. Describe the Observed Behavior

Document the pattern the team is seeing:
- What keeps happening?
- How has it evolved over time?
- What has been tried and what was the result?

### 2. Match Against Archetypes

Compare the observed behavior to the archetype catalog:

| Archetype | Match Indicators | Match Strength |
|-----------|-----------------|----------------|
| _archetype name_ | _which signals are present_ | Strong / Moderate / Weak |

Multiple archetypes can be active simultaneously. Rank by match strength.

### 3. Validate the Match

For the strongest-matching archetype(s):
- Map the specific system variables onto the archetype structure
- Identify which loops are present and which are hypothesized
- Check for the archetype's predicted behavior — does it match what's observed?

| Archetype Element | Mapped to System | Evidence | Confidence |
|------------------|-----------------|----------|------------|
| _loop or variable from archetype_ | _specific system element_ | _data or observation_ | High/Med/Low |

### 4. Apply Archetype Leverage

Use the archetype's known intervention points:

| Archetype | Known Leverage | Applied to This System | Expected Effect |
|-----------|---------------|----------------------|----------------|
| _archetype_ | _generic leverage_ | _specific intervention_ | _predicted outcome_ |

### 5. Save the Analysis

Write to `docs/design/system-models/<topic>-archetypes.md`.

## Output Format

Each archetype analysis should contain:
1. Observed behavior description
2. Archetype matching table
3. Validation mapping for top matches
4. Archetype-specific leverage points applied to this system
5. Risks — where the archetype match may be imperfect

## Rules

- Archetypes are diagnostic tools, not labels — validate the match against actual system structure
- Multiple archetypes can coexist — systems are rarely explained by a single pattern
- The value is in the leverage points — knowing "it's a shifting the burden" is only useful if you act on the fundamental solution
- Don't force a match — if no archetype fits well, use first-principles loop analysis instead
