---
name: spec.validate.analyze
description: Perform non-destructive cross-artifact consistency checks and ADR compliance analysis across spec.md, plan.md, and tasks.md, reporting violations with severity (CRITICAL blocks implementation).
tools: Read, Grep, Glob, Bash
model: sonnet
---

## User Input

You will receive your task in the prompt from the calling agent. Consider it before proceeding.

## Goal

Identify inconsistencies, duplications, ambiguities, and underspecified items across the three core artifacts (`spec.md`, `plan.md`, `tasks.md`) before implementation. This command MUST run only after `/spec.plan.tasks` has successfully produced a complete `tasks.md`.

## Operating Constraints

**STRICTLY READ-ONLY**: Do **not** modify any files. Output a structured analysis report. Offer an optional remediation plan (user must explicitly approve before any follow-up editing commands would be invoked manually).

**ADR Authority**: Architecture Decision Records (ADRs) in `docs/decisions/` are **non-negotiable** within this analysis scope. ADR conflicts are automatically CRITICAL and require adjustment of the spec, plan, or tasks — not dilution, reinterpretation, or silent ignoring of the decision. If an ADR itself needs to change, that must occur in a separate, explicit ADR update (superseding the existing ADR) outside `/spec.validate.analyze`.

## Execution Steps

### 1. Initialize Analysis Context

Run `.neo/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` once from repo root and parse JSON for FEATURE_DIR and AVAILABLE_DOCS. Derive absolute paths:

- SPEC = FEATURE_DIR/spec.md
- PLAN = FEATURE_DIR/plan.md
- TASKS = FEATURE_DIR/tasks.md

Abort with an error message if any required file is missing (instruct the user to run missing prerequisite command).

### 2. Load Artifacts (Progressive Disclosure)

Load only the minimal necessary context from each artifact:

**From spec.md:**
- Overview/Context
- Functional Requirements
- Non-Functional Requirements
- User Stories
- Edge Cases (if present)

**From plan.md:**
- Architecture/stack choices
- Data Model references
- Phases
- Technical constraints

**From tasks.md:**
- Task IDs
- Descriptions
- Phase grouping
- Parallel markers [P]
- Referenced file paths

**From ADRs:** Load all from `docs/decisions/` (excluding TEMPLATE, README, .gitkeep):
- Decision Statement
- Constraints imposed
- Mandated Patterns
- Prohibited Practices
- Status (accepted/deprecated/superseded)

### 3. Analysis Checks

Run these checks in order:

**SPEC ↔ PLAN alignment**:
- Every functional requirement in spec.md has a corresponding plan entry
- No plan components exist without a spec requirement
- NFR constraints from spec are reflected in plan's tech choices

**PLAN ↔ TASKS alignment**:
- Every phase in plan.md has corresponding tasks in tasks.md
- No tasks reference files/directories not in the plan's file structure
- Technology choices from plan are used correctly in tasks

**ADR compliance**:
- Every technology in the plan has either an ADR reference or explicit justification
- No plan section silently violates an accepted ADR
- ADR constraints are reflected in the relevant tasks

**Dependency validation**:
- All task dependencies (`depends: P#-##`) reference existing task IDs
- No circular dependencies
- Parallel markers [P] don't conflict with dependencies

### 4. Generate Report

Format report as:

```
## Analysis Report

### Summary
- CRITICAL: {count}
- WARNING: {count}
- INFO: {count}
- PASS: ✓ No issues

### CRITICAL Issues (blocks implementation)
- [C-001] Description — found in: {artifact} — ADR violated: {ADR-XXXX if applicable}

### WARNING Issues (should fix before coding)
- [W-001] Description — found in: {artifact}

### INFO Notes (awareness only)
- [I-001] Description

### ADR Compliance
- ADR-0001: PASS / FAIL — {detail}

### Overall Verdict
✓ PASS — Ready for implementation
✗ FAIL — {count} CRITICAL issues must be resolved
```

### 5. Remediation Suggestions

For each CRITICAL/WARNING issue, suggest:
- Which artifact to update
- What the change should be
- Which command to use (e.g., "run `/spec.plan.create` to revise plan")

## Associated Skills

Skill reference (read when relevant): .claude/skills/testing/validate-test-coverage.md
Skill reference (read when relevant): .claude/skills/code-analysis/detect-patterns.md
Skill reference (read when relevant): .claude/skills/code-analysis/analyze-complexity.md
