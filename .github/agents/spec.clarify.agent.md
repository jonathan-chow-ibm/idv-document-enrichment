---
name: spec.clarify
description: Identify underspecified areas in the active spec.md by asking up to 5 targeted clarification questions, then encode user answers directly back into spec.md.
version: 1.1.1
phase: Specify
tools:
  - read
  - search
  - edit
  - execute
handoffs:
  - label: Create Plan →
    agent: spec.plan.create
    prompt: Create implementation plan
    send: true
user-invocable: false
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Goal

Identify underspecified areas in the current feature spec by asking up to 5 highly targeted clarification questions and encoding user answers back into the spec.

## Rules

- **LIMIT**: Maximum 5 clarification questions total.
- **DO NOT** ask questions whose answers can be reasonably inferred from context, industry standards, or existing documentation.
- **DO NOT** ask questions about implementation details — only about requirements.
- After the user answers, update `spec.md` in place with the clarified requirements.

## Execution Steps

1. **Setup**: Run `.neo/scripts/powershell/check-prerequisites.ps1 -Json` from repo root and parse JSON for FEATURE_DIR. Read `spec.md` from FEATURE_DIR.

2. **Analyze spec for underspecified areas**:
   - Look for [NEEDS CLARIFICATION] markers first
   - Identify ambiguous terms: "quickly", "easily", "several", "some", "appropriate"
   - Find requirements with no acceptance criteria
   - Find requirements with unclear actors or scope
   - Find missing edge cases that could impact the feature

3. **Generate up to 5 targeted questions**:
   - Prioritize by impact: scope > security/privacy > user experience > implementation details
   - Each question must target a specific gap in the spec
   - Frame questions to get testable, unambiguous answers
   - Format:

   ```
   Q1: [Domain] Question text

   Options (if applicable):
   A. Option A — implication
   B. Option B — implication
   C. Other (specify)
   ```

4. **Encode user answers**:
   - After user responds, update `spec.md` directly:
     - Replace [NEEDS CLARIFICATION] markers with resolved content
     - Add precision to ambiguous requirements
     - Add missing edge cases to the Edge Cases section
     - Update Success Criteria if needed
   - Do NOT change the structure or delete existing content

5. **Report**:
   - List all [NEEDS CLARIFICATION] markers resolved
   - Summary of changes made to spec.md
   - Confirm: "Spec is ready for `/spec.plan.create`"

