---
name: spec.require
description: Guide a product owner through authoring or updating a platform-level PRD with capability domains, users, requirements, and scope boundaries, producing a prd.md artifact.
version: 1.1.1
phase: Specify
model:
  - Claude Sonnet 4.6 (copilot)
  - Claude Sonnet 4.5 (copilot)
  - Claude Sonnet 4 (copilot)
tools:
  - read
  - search
  - edit
handoffs:
  - label: Decompose Feature Brief →
    agent: spec.brief
    prompt: Decompose a feature brief from this PRD
    send: true
user-invocable: true
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Goal

Author or update a platform-level Product Requirements Document (PRD) that defines the capability vision, user segments, capability requirements, and scope for the product or platform.

## Audience

This PRD is for non-technical stakeholders. Write in clear, plain English. No code, no technical architecture, no implementation details.

## Execution Steps

1. **Setup**: Check if `docs/prd.md` already exists.
   - If yes: load it to understand existing structure before updating
   - If no: create it from the PRD template

2. **Load context**:
   - Read any existing ADRs from `docs/decisions/` for product constraints
   - Read `$ARGUMENTS` for the specific area to document

3. **Gather PRD content** through conversation:

   **Platform Overview**:
   - What is this product/platform?
   - Who are the primary users? (personas)
   - What problem does it solve?

   **Capability Domains**: Groups of related capabilities
   - Domain name
   - User value: what users can do in this domain
   - Key requirements (NOT implementation details)

   **User Stories** (structured format):
   - As a [persona], I want to [action] so that [benefit]
   - Priority: P1/P2/P3

   **Non-Functional Requirements**:
   - Performance: load times, throughput
   - Security: compliance, data handling
   - Accessibility: WCAG level
   - Availability: uptime, disaster recovery

   **Out of Scope**: Explicitly list what is NOT included

4. **Write PRD** to `docs/prd.md`:
   - Follow the PRD template structure
   - Technology-agnostic language throughout
   - Each requirement must be testable without implementation details

5. **Report**:
   - Created/updated: `docs/prd.md`
   - Capability domains covered
   - Total user stories
   - Suggested next: `/spec.brief` to decompose a specific feature

## Associated Skills

#file: skills/analysis/youtube-transcript-knowledge.md

