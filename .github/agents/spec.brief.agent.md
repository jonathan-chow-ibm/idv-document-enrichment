---
name: spec.brief
description: Decompose a Feature Brief from a PRD for a single discrete feature with prioritized requirements, testable acceptance criteria, and scope boundaries, producing a feature-brief.md artifact.
version: 1.1.1
phase: Specify
model:
  - Claude Sonnet 5 (copilot)
  - Claude Sonnet 4.5 (copilot)
tools:
  - read
  - search
  - edit
handoffs:
  - label: Capture Feature Spec →
    agent: spec.capture
    prompt: Capture the feature specification
    send: true
user-invocable: true
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Goal

Decompose a specific feature from the PRD into a Feature Brief — a focused, actionable document with prioritized requirements and testable acceptance criteria that can immediately be used by `/spec.capture` to produce a spec.

## Audience

Mixed audience: product managers and engineers. The brief bridges product intent (PRD) and technical specification (spec.md).

## Execution Steps

1. **Setup**: Check if `docs/prd.md` exists. If not, suggest running `/spec.require` first.

2. **Identify the feature**: From `$ARGUMENTS` or through conversation:
   - What is the name of the feature?
   - Which PRD capability domain does it belong to?
   - What is the target user persona?

3. **Extract from PRD**:
   - Relevant user stories for this feature
   - Relevant non-functional requirements
   - Known constraints or dependencies

4. **Generate Feature Brief** at `specs/{feature}/feature-brief.md`:

   ```markdown
   # Feature Brief: {Feature Name}

   ## Summary
   One paragraph: what this feature does, for whom, and why it matters.

   ## Goals
   - Goal 1 (measurable outcome)
   - Goal 2

   ## User Stories (Prioritized)
   - [P1] As a {persona}, I want to {action} so that {benefit}
   - [P2] As a {persona}, I want to {action} so that {benefit}

   ## Acceptance Criteria
   For each user story, listed testable criteria:
   - Story 1: Given/When/Then scenarios
   
   ## Scope
   ### In Scope
   - Feature elements that ARE included

   ### Out of Scope
   - Feature elements explicitly NOT included
   - Defer to a later version

   ## Key Decisions Required
   - Any open questions that `/spec.capture` will need to resolve

   ## Dependencies
   - Other features or systems this depends on

   ## Success Metrics
   - How will we know this feature is successful?
   ```

5. **Report**:
   - Created: `specs/{feature}/feature-brief.md`
   - User stories count: {P1}, {P2}, {P3}
   - Suggested next: `/spec.capture` to create the full specification

