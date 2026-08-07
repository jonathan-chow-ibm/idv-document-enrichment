---
name: spec.utility.checklist
description: Generate domain-specific requirement-quality checklists (UX, security, accessibility, performance) that validate requirements are complete and clear, not that code is correct.
version: 1.1.1
phase: Utility
tools:
  - read
  - search
  - edit
user-invocable: true
---

## Checklist Purpose: "Unit Tests for English"

**CRITICAL CONCEPT**: Checklists are **UNIT TESTS FOR REQUIREMENTS WRITING** — they validate the quality, clarity, and completeness of requirements in a given domain.

**NOT for verification/testing**:
- ❌ NOT "Verify the button clicks correctly"
- ❌ NOT "Test error handling works"
- ❌ NOT "Confirm the API returns 200"
- ❌ NOT checking if code/implementation matches the spec

**FOR requirements quality validation**:
- ✅ "Are visual hierarchy requirements defined for all card types?" (completeness)
- ✅ "Is 'prominent display' quantified with specific sizing/positioning?" (clarity)
- ✅ "Are hover state requirements consistent across all interactive elements?" (consistency)
- ✅ "Are accessibility requirements defined for keyboard navigation?" (coverage)
- ✅ "Does the spec define what happens when logo image fails to load?" (edge cases)

**Metaphor**: If your spec is code written in English, the checklist is its unit test suite. You're testing whether the requirements are well-written, complete, unambiguous, and ready for implementation — NOT whether the implementation works.

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Execution Steps

1. **Setup**: Run `.neo/scripts/powershell/check-prerequisites.ps1 -Json` from repo root and parse JSON for FEATURE_DIR and AVAILABLE_DOCS list.

2. **Clarify intent**: Ask up to THREE targeted questions about:
   - Checklist domain (security, UX, accessibility, performance, idempotency, cross-platform, etc.)
   - Depth: lightweight pre-commit list or formal release gate?
   - Audience: author, peer reviewer, QA team?

3. **Load feature context**: Read spec.md, plan.md (if exists) from FEATURE_DIR.

4. **Generate checklist**:
   - Create `FEATURE_DIR/checklists/<domain>.md`
   - Each item must be a YES/NO question about a requirement
   - Group items by sub-domain (e.g., Authentication, Authorization, Input Validation)
   - 15–30 items per domain checklist

5. **Checklist item format**:
   ```
   - [ ] Clear, testable requirement question about the spec/plan?
   ```

6. **Domain coverage** (generate for the requested domain):
   - **UX**: Visual hierarchy, interaction states, empty states, error messages, loading indicators
   - **Security**: Authentication, authorization, input validation, output encoding, secrets handling
   - **Accessibility**: WCAG AA compliance, keyboard nav, screen reader, color contrast
   - **Performance**: Load time budgets, pagination, caching, query optimization
   - **Idempotency**: Re-run safety, atomic writes, conflict detection
   - **Cross-platform**: Behavior consistency across environments/platforms

7. **Report**:
   - Created: `checklists/<domain>.md`
   - Item count
   - Suggested domains not yet covered
   - Suggested next: `/spec.validate.analyze`

## Associated Skills

#file: skills/testing/validate-test-coverage.md

