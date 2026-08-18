---
name: system-leverage-point-analysis
description: "Find high-leverage intervention points where small changes produce large systemic effects. Uses Donella Meadows' hierarchy of leverage points. Use when deciding where to intervene in a system, prioritizing improvement efforts, or evaluating why past interventions had limited impact. Produces leverage point analyses in docs/design/."
argument-hint: "Describe the system or problem where you want to find leverage points"
---

# Leverage Point Analysis

## When to Use

- Deciding where to invest limited improvement effort for maximum impact
- Evaluating why a past intervention didn't work as expected
- Shifting the team's attention from parameter tweaking to structural change
- After mapping feedback loops and delays — leverage points emerge from system structure
- When the team is stuck in firefighting mode and needs to find root causes

## Key Concepts

Leverage points are places in a system where a small intervention produces large, lasting change. Donella Meadows identified a hierarchy from least to most effective:

| Level | Type | Description | Difficulty | Impact |
|-------|------|------------|------------|--------|
| 12 | Constants, parameters, numbers | Adjusting thresholds, quotas, budgets | Easy | Low |
| 11 | Buffer sizes | Changing the capacity of stocks | Easy | Low-Med |
| 10 | Stock-and-flow structure | Redesigning physical or informational plumbing | Medium | Medium |
| 9 | Delays | Shortening or lengthening time lags | Medium | Medium |
| 8 | Balancing feedback loops | Strengthening corrective mechanisms | Medium | Medium-High |
| 7 | Reinforcing feedback loops | Weakening vicious cycles or strengthening virtuous ones | Medium | High |
| 6 | Information flows | Making hidden information visible to the right people | Medium | High |
| 5 | Rules | Changing incentives, constraints, permissions, and policies | Hard | High |
| 4 | Self-organization | Enabling the system to evolve its own structure | Hard | Very High |
| 3 | Goals | Changing what the system optimizes for | Very Hard | Very High |
| 2 | Paradigm | Shifting the mindset from which the system arises | Very Hard | Transformative |
| 1 | Transcending paradigms | Recognizing that no paradigm is "true" | Hardest | Transformative |

## Procedure

### 1. Inventory Current Interventions

List what the team is already doing or considering:

| Intervention | Level (1-12) | Expected Effect | Actual Effect | Gap |
|-------------|-------------|----------------|---------------|-----|
| _current or proposed action_ | _Meadows level_ | _what was expected_ | _what actually happened_ | _the difference_ |

Most teams cluster at levels 10–12 (parameters, buffers, structure). Flag this pattern if present.

### 2. Walk the Hierarchy

For the system under study, systematically explore each level. Start from the bottom (parameters) and work upward — this matches how teams naturally think, and each level provides context for the next:

**Levels 12–10: Parameters, Buffers, Structure**
- What numbers are being tuned? (thresholds, headcount, budgets)
- What buffers exist and are they sized appropriately? (queues, inventories, reserves)
- What structural changes to flows are possible? (adding/removing stages, parallel paths)

**Levels 9–7: Delays, Balancing Loops, Reinforcing Loops**
- Where are the critical delays? (use output from `system-delay-analysis`)
- Which balancing loops are weak or missing? (quality checks, governance, feedback)
- Which reinforcing loops are driving unwanted behavior? (escalation, technical debt accumulation)

**Levels 6–5: Information Flows, Rules**
- What information is generated but not visible to decision-makers?
- What rules (incentives, metrics, policies) are driving behavior?
- Are people optimizing for the metric rather than the actual goal?

**Levels 4–3: Self-Organization, Goals**
- Can the system restructure itself in response to change?
- What is the system actually optimizing for (vs. what it claims to optimize for)?
- Are the goals aligned across all actors in the system?

**Levels 2–1: Paradigm, Transcending Paradigms**
- What fundamental belief underlies the current system design?
- What would change if that belief were questioned?
- Are there alternative paradigms that would dissolve the problem entirely?

### 3. Evaluate Candidate Leverage Points

For each identified leverage point:

| Leverage Point | Level | Current State | Proposed Intervention | Expected Effect | Confidence | Reversibility | Risks |
|---------------|-------|--------------|----------------------|----------------|------------|---------------|-------|
| _where in the system_ | _1-12_ | _what exists now_ | _what to change_ | _anticipated outcome_ | Low/Med/High | Easy/Hard | _what could go wrong_ |

### 4. Prioritize

Plot leverage points on an effort-impact matrix:

| Quadrant | Criteria | Action |
|----------|----------|--------|
| High impact, low effort | Quick wins at higher leverage levels | Do first |
| High impact, high effort | Strategic investments | Plan carefully |
| Low impact, low effort | May not be worth the distraction | Deprioritize |
| Low impact, high effort | Traps — avoid | Eliminate |

### 5. Identify Counter-Intuitive Leverage

Meadows emphasized that leverage points are often counter-intuitive — the obvious intervention is frequently at the wrong point or in the wrong direction. Check for:
- **Push in the wrong direction** — strengthening a reinforcing loop that should be weakened
- **Wrong level** — tweaking parameters when the problem is structural
- **Missing feedback** — adding more control when the real issue is missing information
- **Goal displacement** — optimizing metrics instead of questioning whether the goal is correct

### 6. Save the Analysis

Write to `docs/design/system-models/<topic>-leverage-points.md`.

## Output Format

Each leverage point analysis should contain:
1. Current intervention inventory with Meadows-level classification
2. Hierarchy walkthrough findings (organized by level)
3. Candidate leverage point evaluation table
4. Prioritization matrix
5. Counter-intuitive leverage notes
6. Recommendations — top 3 leverage points with rationale

## Rules

- Always classify interventions by Meadows level — this reveals whether the team is aiming high enough
- Higher-level leverage points are harder but more effective — help the team stretch beyond parameter tweaking
- Leverage points are hypotheses — validate before committing significant resources
- Counter-intuitive leverage is common — explicitly check for wrong-direction and wrong-level errors
- Don't dismiss easy wins — a level-6 information flow change can sometimes unlock everything above it
