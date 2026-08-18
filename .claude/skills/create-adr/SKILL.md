---
name: create-adr
description: "Create a new Architectural Decision Record (ADR). Use when recording architecture decisions, proposing design changes, documenting technical choices, or adding a new ADR to docs/decisions/."
argument-hint: "Describe the architectural decision to record"
---

# Create Architectural Decision Record

## When to Use

- Recording a significant architectural or design decision
- Proposing a new technology, pattern, or structural change
- Documenting trade-offs between alternatives

## Procedure

1. **Determine the next ADR number**
   - List existing files in `docs/decisions/` and find the highest `NNN-` prefix
   - Increment by 1, zero-padded to 3 digits

2. **Generate the ADR from the template**
   - Use the template in [./assets/adr-template.md](./assets/adr-template.md)
   - Fill in all sections based on the user's description
   - File naming: `NNN-short-description.md` (kebab-case)

3. **Write the file**
   - Save to `docs/decisions/NNN-short-description.md`

4. **Update the ADR index**
   - Add an entry to the `## Index of ADRs` section in `docs/decisions/README.md`
   - make sure to include the new ADR number, title, and status

5. **Link from AGENTS.md if significant**
   - If this is a key project decision, add a row to the `## Key Decisions` table in the root `AGENTS.md`

## Rules

- ADRs are **immutable** once accepted — supersede with a new ADR rather than editing
- Status must be one of: `Proposed`, `Accepted`, `Deprecated`, `Superseded`
- Keep the context and rationale sections substantive — these are the most valuable parts
- List real alternatives that were considered, not strawmen
