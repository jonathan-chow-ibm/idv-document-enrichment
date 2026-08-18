---
description: Guide interactive creation of Architecture Decision Records (ADRs) in docs/decisions/, including status, context, decision, consequences, alternatives, and implementation notes.
model: sonnet
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Overview

This command guides you through creating a new Architecture Decision Record (ADR) for the project. ADRs document significant architectural decisions in a structured, discoverable format.

## Execution Flow

### 1. Load Context

First, load the necessary context:

- Read the ADR template: `.neo/templates/decision-file-template.md`
- Read the ADR index: `docs/decisions/README.md`
- Review existing ADRs in `docs/decisions/` to understand current decisions and avoid duplication
- Determine the next sequential ADR number (e.g., if last ADR is 0005, next is 0006)

### 2. Gather Decision Information

Collect the following information through conversation with the user (if not provided in $ARGUMENTS):

**Decision Title**: A concise, descriptive title (e.g., "Adopt Microservices Architecture", "Use PostgreSQL for Primary Database")

**Context**: Why is this decision needed? What problem does it solve? What forces are at play?
- Current situation
- Business drivers
- Technical constraints
- Team considerations

**Decision Details**: What exactly is being decided?
- The chosen approach/technology/pattern
- Key implementation details
- Scope and boundaries
- Affected components/systems

**Consequences**: What are the impacts of this decision?

Positive consequences (benefits, improvements, capabilities enabled):
- POS-001: [First positive outcome]
- POS-002: [Second positive outcome]

Negative consequences (costs, risks, limitations):
- NEG-001: [First negative impact]
- NEG-002: [Second negative impact]

**Alternatives Considered**: What other options were evaluated and why were they rejected?
- ALT-001: Description — Rejection reason
- ALT-002: Description — Rejection reason

**Implementation Notes**: Key considerations for people implementing this decision.
- IMP-001: [First implementation note]

### 3. Validate Uniqueness

Before creating the ADR:
- Search existing ADRs for similar decisions
- If a similar ADR exists, ask if this supersedes it or is a new distinct decision
- If superseding, reference the superseded ADR in the new one

### 4. Create the ADR

Write the ADR to `docs/decisions/{NNNN}-{slug}.md`:

```markdown
# ADR-{NNNN}: {Title}

## Status

**Accepted**

## Context

{context paragraph}

## Decision

{decision paragraph}

## Consequences

### Positive
- **POS-001**: {outcome}

### Negative
- **NEG-001**: {impact}

## Alternatives Considered

### {Alternative Name}
- **ALT-001**: **Description**: {description}
- **ALT-002**: **Rejection Reason**: {reason}

## Implementation Notes
- **IMP-001**: {note}

## References
- **REF-001**: {reference}
```

### 5. Update ADR Index

Add the new ADR to `docs/decisions/README.md` with:
- Number, title, status, one-sentence summary

### 6. Report

- Created: `docs/decisions/{NNNN}-{slug}.md`
- Updated: `docs/decisions/README.md`
- ADR number: {NNNN}
- Suggested next: `/spec.plan.create` to incorporate the ADR constraint into the implementation plan

## Associated Skills

Skill reference (read when relevant): .claude/skills/code-analysis/detect-patterns.md
