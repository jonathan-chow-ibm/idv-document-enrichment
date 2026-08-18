---
name: conventional-commits
description: Write well-formed Git commit messages and PR titles using Conventional Commits. USE FOR crafting commit messages, amending messages, writing PR titles and descriptions, or setting up commitlint/changeset tooling. Trigger phrases include "commit message", "conventional commit", "commitlint", "semantic release", "PR title", "changelog", "feat:", "fix:", "chore:".
---

# Conventional Commits

Guidelines for Git commit messages and PR titles in this workspace.

## Format

```
<type>(<scope>): <short summary>

<body — optional, wrap at 72 chars>

<footer — optional: BREAKING CHANGE, Refs, Co-authored-by>
```

- **Subject line ≤ 72 chars**, imperative mood ("add", not "added"/"adds").
- **No trailing period** on the subject.
- **Body explains _why_**, not _what_ — the diff shows _what_.
- **Blank line between subject, body, and footer.**

## Types

| Type       | When to use                                                  |
| ---------- | ------------------------------------------------------------ |
| `feat`     | A new user-visible feature                                   |
| `fix`      | A bug fix                                                    |
| `refactor` | Code change that neither fixes a bug nor adds a feature      |
| `perf`     | Performance improvement                                      |
| `test`     | Adding or correcting tests                                   |
| `docs`     | Documentation only                                           |
| `build`    | Build system, dependency, or package manager changes         |
| `ci`       | CI / pipeline changes                                        |
| `chore`    | Routine maintenance (version bumps, non-code housekeeping)   |
| `style`    | Formatting, whitespace — no code meaning changes             |
| `revert`   | Reverts a previous commit (include the reverted SHA in body) |

## Scope

Optional, lowercase, narrow:

- `api`, `web`, `infra`, `ci`, `deps`, or a feature area like `products`, `auth`

## Breaking Changes

Either:

- Append `!` after the type/scope: `feat(api)!: remove v1 endpoints`
- Or add a `BREAKING CHANGE:` footer explaining the impact

Prefer the footer for non-trivial breaks — it gives room to describe migration.

## Examples

```
feat(api): add product search endpoint

Supports fuzzy match on name and SKU. Query is bounded to 100 results
and requires an authenticated user.
```

```
fix(web): debounce search input to avoid request storms

Users typing quickly triggered one request per keystroke. Debounces at
300ms and cancels in-flight requests on change.

Fixes #482
```

```
refactor(infra)!: rename resource group naming scheme

BREAKING CHANGE: Existing deployments must be re-provisioned. The
new scheme uses `rg-<workload>-<env>-<region>`.
```

## PR Titles

PR titles follow the same format. The title becomes the squash-merge commit, so it carries into the changelog.

## Body Checklist

Include in the body when relevant:

- **Motivation** — why this change now
- **Approach** — any non-obvious design decision
- **Alternatives considered** — briefly, if you picked a surprising path
- **Links** — `Refs #123`, `Fixes #456`, related ADRs or docs

Skip the body entirely for trivial changes (typo, formatting, one-liner).

## Rules

- **One logical change per commit.** Mixed commits hurt review and revert.
- **Never commit secrets, generated files, or `.env` content.**
- **`chore(deps)`** for dependency bumps via the package manager; include why if non-trivial.
- **Amend or rebase local commits** to keep history clean before pushing; never rewrite shared history.
- **Co-authors** get a `Co-authored-by: Name <email>` footer line.

## Anti-patterns

- `fix: stuff` / `wip` / `update` — meaningless subjects
- Multi-purpose commits ("add feature + fix unrelated bug + reformat")
- Bodies that just restate the subject
- `BREAKING CHANGE` hidden only in a scope name, not the message
- Commit messages describing the code rather than the intent
