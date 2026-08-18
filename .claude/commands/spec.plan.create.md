---
description: Produce the technical implementation plan (plan.md, data-model.md, contracts/) from a validated spec.md, including ADR compliance validation during planning.
model: sonnet
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Outline

1. **Setup**: Run `.neo/scripts/powershell/setup-plan.ps1 -Json` from repo root and parse JSON for FEATURE_SPEC, IMPL_PLAN, SPECS_DIR, BRANCH. For single quotes in args like "I'm Groot", use escape syntax: e.g `'I'\''m Groot'` (or double-quote if possible: `"I'm Groot"`).

2. **Load context**: Read FEATURE_SPEC and ADRs from `docs/decisions/` (excluding template and README). Load IMPL_PLAN template (already copied).

3. **Execute plan workflow**: Follow the structure in IMPL_PLAN template to:
   - Fill Technical Context (mark unknowns as "NEEDS CLARIFICATION")
   - Fill Architecture Check section for architecture decisions and ADRs
   - Evaluate gates (ERROR if violations unjustified)
   - Phase 0: Generate research.md (resolve all NEEDS CLARIFICATION)
   - Phase 1: Generate data-model.md, contracts/, quickstart.md
   - Phase 1: Update agent context by running the agent script
   - Re-evaluate Architecture Check post-design

4. **Stop and report**: Command ends after Phase 2 planning. Report branch, IMPL_PLAN path, and generated artifacts.

## Phases

### Phase 0: Outline & Research

1. **Extract unknowns from Technical Context**:
   - For each NEEDS CLARIFICATION → research task
   - For each dependency → best practices task
   - For each integration → patterns task

2. **Generate and dispatch research tasks**:

   ```text
   For each unknown in Technical Context:
     Task: "Research {unknown} for {feature context}"
   For each technology choice:
     Task: "Find best practices for {tech} in {domain}"
   ```

3. **Consolidate findings** in `research.md` using format:
   - Decision: [what was chosen]
   - Rationale: [why chosen]
   - Alternatives considered: [what else evaluated]

**Output**: research.md with all NEEDS CLARIFICATION resolved

### Phase 1: Design & Contracts

**Prerequisites:** `research.md` complete

1. **Extract entities from feature spec** → `data-model.md`:
   - Entity name, fields, relationships
   - Validation rules from requirements
   - State transitions if applicable

2. **Generate API contracts** from functional requirements:
   - For each user action → endpoint
   - Use standard REST/GraphQL patterns
   - Output OpenAPI/GraphQL schema to `/contracts/`

3. **Agent context update**:
   - Run `.neo/scripts/powershell/update-agent-context.ps1 -AgentType copilot`
   - These scripts detect which AI agent is in use
   - Update the appropriate agent-specific context file

### Phase 2: Implementation Planning

**Prerequisites:** Phase 1 complete, research.md with zero unresolved items

Fill IMPL_PLAN template sections:
- **Summary**: One paragraph describing the approach
- **Architecture Overview**: System design, key components
- **Tech Stack**: With ADR references for each choice
- **Complete File Structure**: All files that will be created/modified
- **Component Design**: For each major component, describe its responsibility
- **Implementation Phases**: Phase breakdown with dependencies (mirrors tasks.md structure)
- **ADR Notes**: All architecture decisions with numbered references

## Architecture Check

Before finalizing, validate:
- Every technology choice has an ADR reference or justification
- No ADR constraints are violated
- If an ADR would be violated, either:
  - Adjust the plan to comply
  - Document why a new ADR is needed (do NOT silently violate)

## Output

Report:
- Branch: BRANCH_NAME
- Plan: IMPL_PLAN path
- Artifacts generated: data-model.md, contracts/, research.md
- ADR compliance: PASS/FAIL with details
- Suggested next: `/spec.plan.tasks`

## Associated Skills

Skill reference (read when relevant): .claude/skills/code-analysis/detect-patterns.md
Skill reference (read when relevant): .claude/skills/code-analysis/analyze-complexity.md

## Next Steps (Handoffs)
- **Create Tasks →** → run `/spec.plan.tasks`: Break the plan into tasks
