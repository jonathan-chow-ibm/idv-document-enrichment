# CLAUDE.md

Ambient guidance for Claude Code in this repository. This ports the "Neo" spec-driven
SDLC orchestration guidance (originally authored for GitHub Copilot in
`.github/copilot-instructions.md` and `.github/neo-coding-agent-playbook.md`) into Claude
Code's memory file so it loads automatically every session. The original `.github` files
remain the source of truth for Copilot and are unchanged.

## What this project uses Neo for

Neo is a spec-driven SDLC workflow: **spec → plan → tasks → implement**, with product
planning (PRD → brief → spec), validation gates, and close/archive phases layered around
it. Work is decomposed into feature specs, planned, broken into ordered tasks, then
implemented and verified — coordinated by dedicated agents rather than ad-hoc coding.

## Claude Code equivalents (Copilot → Claude Code)

The Neo mechanics were built on VS Code / Copilot constructs. Translate them as follows:

- **"Copilot agent" / `@workspace /spec.orch`** → the `spec.*` **subagents** (invoke via the
  Agent tool) or the `/spec.*` **slash commands**. Every Neo agent exists BOTH ways.
- **`.github/agents/*.agent.md`** (Copilot agent files) → Claude equivalents are
  `.claude/agents/*.md` (subagents) and `.claude/commands/*.md` (slash commands).
- **`#file:path` skill includes** → read the relevant skill under
  `.claude/skills/<name>/SKILL.md`.
- **`runSubagent('name', prompt)`** → invoke the named `spec.*` subagent via the Agent tool
  (or its `/spec.*` slash command).
- **`.neo/scripts/powershell/*.ps1`** → run via the Bash or PowerShell tool when an
  agent's instructions call for it.
- **Artifact output** → write real files to the filesystem (not fenced code blocks).

## The spec-driven SDLC workflow

Start with the orchestrator — `spec.orch` (subagent) or `/spec.orch` — which routes intent
to the right phase and never implements directly. Scope can be expressed as
`--scope planning` or `--scope full` (equivalently `[scope=planning]` / `[scope=full]`):

- **`[scope=planning]`** → runs `spec.capture` → `spec.plan.create` → `spec.plan.tasks` in
  sequence, then hands off to implementation.
- **`[scope=full]`** → the above plus `spec.validate.analyze` → `spec.build.implement`.
- **No scope** → runs `spec.capture`, then presents step-by-step handoffs.

### Phases

1. **Planning (Spec → Plan → Tasks)** — new feature, no spec yet:
   `spec.capture` (feature → spec.md, checks out feature branch) → `spec.clarify` (optional,
   ≤5 clarifying questions encoded back into spec.md) → `spec.plan.create` (plan.md,
   data-model.md, contracts/) → `spec.plan.tasks` (dependency-ordered tasks.md) →
   `spec.validate.analyze` (cross-artifact consistency + ADR compliance; **required before
   coding**, CRITICAL findings block implementation).
2. **Implement (Execution)** — requires planning complete:
   `spec.utility.checklist` (required gate) → `spec.design.ux` (parallel, if UI) →
   `spec.build.implement` (execute tasks.md phase by phase) → `spec.validate.analyze`
   (post-implementation validation).
3. **Product planning (PRD → Brief → Spec)**:
   `spec.require` (platform PRD) → `spec.brief` (feature brief from PRD) → `spec.capture`.
4. **Architecture decision**: author an ADR interactively (`spec.architecture.adr`).
5. **DevOps / GitHub Issues**: `spec.utility.githubissues` (convert tasks.md to Issues).
6. **Close & archive**: `spec.close` (gate on tasks, summary, CHANGELOG, move to archive).

### Routing cues (for spec.orch)

- "spec", "feature", "requirement" → `spec.capture`
- "plan", "architecture", "design" → `spec.plan.create`
- "tasks", "breakdown" → `spec.plan.tasks`
- "implement", "code", "build" → `spec.build.implement`
- "design", "UI", "UX", "styling" → `spec.design.ux`
- "validate", "check", "consistency" → `spec.validate.analyze`
- "checklist", "review criteria" → `spec.utility.checklist`
- "ADR", "decision", "architecture record" → `spec.architecture.adr`
- "PRD", "product requirements" → `spec.require`
- "brief", "feature brief" → `spec.brief`
- "issues", "GitHub Issues" → `spec.utility.githubissues`
- "close", "archive", "wrap up", "finish", "done with" → `spec.close`

### Context awareness

Before routing, check which artifacts exist: `spec.md` but no `plan.md` → suggest
`spec.plan.create`; `plan.md` but no `tasks.md` → `spec.plan.tasks`; `tasks.md` but no
checklists → `spec.utility.checklist` before implementing; checklists present →
`spec.build.implement`.

### ADR violation recovery

If validation surfaces ADR violations, delegate to `spec.utility.recover` (max 2 correction
rounds). Outcomes: `PROCEED` → continue; `HALT/REPLAN` → re-run `spec.plan.create` with the
relaxed constraint; `HALT/ABANDON` → terminate with summary; `HALT/SECONDARY_ADR_CONFLICT`
→ surface the conflict to the user (do not loop); `HALT/SUB_AGENT_FAILURE` or
`HALT/MISSING_ARTIFACT` → surface the diagnostic and escalate.

## Phase detection (autonomous execution)

When executing a task autonomously (e.g. from an issue-style title), detect the phase:

1. **Title prefix wins**: `spec.capture:`/`capture:` → capture; `spec.plan.create:`/`plan:`
   → plan; `spec.plan.tasks:`/`tasks:` → tasks; `implement:`/`spec.build.implement:` →
   implement; `full:`/`pipeline:` → full pipeline. Strip the prefix to get the feature
   description.
2. **Artifact-state (no prefix)**: match slug in `specs/`. No `spec.md` → capture; no
   `plan.md` → plan; no `tasks.md` → tasks; `tasks.md` exists → implement. No match →
   capture.
3. **Feature number & slug**: `NNN` = max existing + 1 across `specs/` and `specs/archive/`
   (never reuse archived/skipped numbers; zero-padded; start `001`). Slug = lowercase,
   non-alphanumeric → `-`, collapsed, trimmed, ≤40 chars. On collision, stop and report.
4. **Branch**: check out `{NNN}-{slug}` (create if new); all commits target it.

For a `full:`/`pipeline:` run, execute all four phases in sequence, committing and pushing
to `{NNN}-{slug}` after each. Commit format: `feat({NNN}): {phase} — {feature description}`
(unless the agent specifies otherwise). Order: capture → plan.create → plan.tasks →
build.implement → open PR.

## Available spec agents

Each name below is BOTH a slash command (`/spec.*`) AND a subagent (`spec.*`, invoked via
the Agent tool). Prefer the most specific one for the task.

**Workflow entry points (typical order):**

- `spec.orch` — orchestrator: routes intent to the right phase; never implements directly.
- `spec.require` — author/update a platform-level PRD.
- `spec.brief` — decompose a feature brief from a PRD.
- `spec.capture` — feature description → spec.md; checks out the feature branch.
- `spec.clarify` — ≤5 clarifying questions; encodes answers back into spec.md.
- `spec.plan.create` — technical plan (plan.md, data-model.md, contracts/) with ADR checks.
- `spec.plan.tasks` — dependency-ordered tasks.md with phase groupings and parallel markers.
- `spec.validate.analyze` — cross-artifact consistency + ADR compliance (CRITICAL blocks).
- `spec.build.implement` — execute tasks.md phase by phase, marking tasks complete.
- `spec.verify` — build/tests/lint/format quality gates; pass/fail summary.
- `spec.review` — read-only code review vs Clean Code, SOLID, and project skills.
- `spec.close` — gate, summarize, update CHANGELOG, archive the spec.

**Design & systems:**

- `spec.discover` — fuzzy business idea → validated project plan (coordinates coaches).
- `spec.design.thinking` — human-centered design (empathize/define/ideate/prototype/test).
- `spec.design.systems` — systems thinking (feedback loops, stocks/flows, leverage points).
- `spec.design.ux` — UI/UX, component styling, design tokens, WCAG AA accessibility.
- `spec.architecture` — architecture review; distributed event-driven Azure + Aspire.
- `spec.architecture.adr` — interactive ADR authoring in `docs/decisions/`.
- `spec.infrastructure` — Azure infra design/provisioning (Bicep, WAF/CAF).
- `spec.devops` — Bicep IaC, GitHub Actions CI/CD, commit/PR conventions.

**Utility & research:**

- `spec.build.coder` — write/refactor/review app code and tests (.NET, React, EF Core).
- `spec.research` — read-only codebase and external-docs investigation.
- `spec.utility.checklist` — domain requirement-quality checklists (UX/security/a11y/perf).
- `spec.utility.githubissues` — convert tasks.md entries into GitHub Issues.
- `spec.utility.recover` — ADR violation recovery loop (see above).

## Canonical artifact paths

| Artifact | Path |
|---|---|
| Feature directory | `specs/{NNN}-{slug}/` |
| Spec / Plan / Tasks | `specs/{NNN}-{slug}/spec.md` · `plan.md` · `tasks.md` |
| Git branch | `{NNN}-{slug}` |
| ADRs | `docs/decisions/NNNN-{title}.md` |
| Subagents | `.claude/agents/{name}.md` |
| Slash commands | `.claude/commands/{name}.md` |
| Skills | `.claude/skills/{name}/SKILL.md` |
| Neo orchestrator (Copilot source) | `.github/copilot-instructions.md` |
| Coding-agent playbook (Copilot source) | `.github/neo-coding-agent-playbook.md` |
