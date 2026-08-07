# Neo Coding Agent Playbook
> **Who reads this**: The GitHub Copilot Coding Agent, assigned a GitHub issue in a Neo-enabled repository. **When**: Start of every autonomous session. **What it enables**: Neo SDLC workflow (spec → plan → tasks → implement) via file read/write, git operations, and `pwsh` for PS1 scripts called by agent files.

---

## Section 1 — Purpose & Scope

This playbook routes phase execution: detect phase → map to agent file → translate VS Code constructs → execute. Per-phase instructions come from agent files only.

**Scope**: Produce/modify files in `specs/{NNN}-{slug}/` and `.github/`; git branch/commit/PR; run `.neo/scripts/powershell/*.ps1` via `pwsh` when called for.

---

## Section 2 — Phase Detection

**Step 1 – Title prefix** (takes priority): strip prefix → get feature description → run phase:

| Prefix | Phase |
|---|---|
| `spec.capture:` / `capture:` | spec.capture |
| `spec.plan.create:` / `plan:` | spec.plan.create |
| `spec.plan.tasks:` / `tasks:` | spec.plan.tasks |
| `implement:` / `spec.build.implement:` | spec.build.implement |
| `full:` / `pipeline:` | full pipeline |

**Step 2 – Artifact-state** (no prefix): match slug in `specs/` dirs. No `spec.md` → spec.capture; no `plan.md` → spec.plan.create; no `tasks.md` → spec.plan.tasks; `tasks.md` exists → spec.build.implement. No match → spec.capture.

**Step 3 – Feature number & slug**: NNN = max existing + 1 across `specs/` and `specs/archive/` (so archived and skipped numbers are never reused; zero-padded; start `001` if none). Slug = lowercase, non-alphanumeric → `-`, collapse, trim, max 40 chars. Collision → comment and stop.

**Step 4 – Branch**: `git checkout {NNN}-{slug}` (branch exists) or `git checkout -b {NNN}-{slug}` (new). All commits target this branch.

---

## Section 3 — Agent File Execution

### Phase-to-file mapping

| Phase | Agent file |
|---|---|
| spec.capture | `.github/agents/spec.capture.agent.md` |
| spec.plan.create | `.github/agents/spec.plan.create.agent.md` |
| spec.plan.tasks | `.github/agents/spec.plan.tasks.agent.md` |
| spec.build.implement | `.github/agents/spec.build.implement.agent.md` |

### Execution rules

Read the mapped agent file; execute its full body as your workflow instructions. Translation rules:

1. **YAML frontmatter** (`---` … `---` at top): skip.
2. **`handoffs:` blocks**: skip — phase routing is Section 2 only.
3. **`$ARGUMENTS`**: substitute with the feature description stripped from the issue title.
4. **`runSubagent('name', prompt)`**: look up `name` in the mapping table, read and execute that file inline; apply all rules recursively (max 2 levels deep).
5. **`#file:path`**: read the file at `path` directly from the repository.
6. **`<!-- NEO:SKILLS:BEGIN -->` … `<!-- NEO:SKILLS:END -->`**: skip.
7. **`.neo/scripts/powershell/*.ps1`**: run via `pwsh`; interpret output per the calling agent file's own instructions.
8. **Artifact output**: write files to the filesystem — not as fenced code blocks in a response.

**Missing agent file**: report expected path and stop. **Missing `#file:` target**: warn with path and skip.

### `full:` / `pipeline:` execution

Run all four phases in sequence. After each phase commit and push to `{NNN}-{slug}` before starting the next. Commit format: `feat({NNN}): {phase} — {feature description}` (unless the agent file specifies one). Order: spec.capture → spec.plan.create → spec.plan.tasks → spec.build.implement → open PR.

---

## Section 4 — PR Description Template

```markdown
## Neo SDLC: {Phase} — {Feature Name}

**Feature**: `specs/{NNN}-{slug}/`  **Branch**: `{NNN}-{slug}`
**Phase completed**: {spec.capture | spec.plan.create | spec.plan.tasks | spec.build.implement | full pipeline}

### Artifacts
| File | Status |
|------|--------|
| {path} | {Created / Updated} |

### Next phase
{spec.capture done} → `/spec.plan.create`: "Create a plan for [feature]. Spec at specs/{NNN}-{slug}/spec.md."
{spec.plan.create done} → `/spec.plan.tasks`: "Generate tasks for [feature]. Plan at specs/{NNN}-{slug}/plan.md."
{spec.plan.tasks done} → `/spec.build.implement`: "Execute tasks for [feature]. Tasks at specs/{NNN}-{slug}/tasks.md."
{implement done} → all phases complete. Request human review.

### Manual prerequisites
{Commands not run by Coding Agent, e.g., `pnpm install`}
```

---

## Section 5 — Canonical Artifact Paths

| Artifact | Path |
|---|---|
| Feature directory | `specs/{NNN}-{slug}/` |
| Spec / Plan / Tasks | `specs/{NNN}-{slug}/spec.md` · `plan.md` · `tasks.md` |
| Git branch | `{NNN}-{slug}` |
| Agent files | `.github/agents/{name}.agent.md` |
| ADRs | `docs/decisions/NNNN-{title}.md` |
| Neo orchestrator | `.github/copilot-instructions.md` |
