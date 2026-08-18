---
name: spec.utility.githubissues
description: Convert tasks.md entries into GitHub Issues with proper labels, assignees, and milestones, using the GitHub API or gh CLI.
tools: Read, Grep, Glob, Bash, mcp__github
model: sonnet
---

## User Input

You will receive your task in the prompt from the calling agent. Consider it before proceeding.

## Goal

Convert tasks in `tasks.md` into actionable GitHub Issues with proper metadata.

## Execution Steps

1. **Setup**: Run `.neo/scripts/powershell/check-prerequisites.ps1 -Json` from repo root and parse FEATURE_DIR.

2. **Read tasks.md**: Parse the task breakdown. Identify:
   - All tasks with IDs (e.g., P1-01, P2-03)
   - Dependencies between tasks
   - Parallel markers [P]
   - Phase groupings

3. **Determine labels**: Map phases and types to GitHub labels:
   - Phase 1 (Setup) → `type: setup`
   - Phase 2 (Core) → `type: feature`
   - Tests → `type: test`
   - Documentation → `type: docs`
   - Parallel-safe tasks → `parallel-safe`

4. **Create GitHub Issues** using `gh issue create` for each task:
   ```bash
   gh issue create \
     --title "[{TaskID}] {Description}" \
     --body "## Task\n{full task description}\n\n## Files\n{file paths}\n\n## Dependencies\n{depends on: TaskIDs}" \
     --label "{labels}" \
     --milestone "{feature-name}"
   ```

5. **Create milestone** if it doesn't exist:
   ```bash
   gh api repos/{owner}/{repo}/milestones --method POST \
     --field title="{feature-name}" \
     --field description="Feature: {feature description}"
   ```

6. **Set dependencies**: Add "Blocked by #N" references in issue bodies for sequential tasks.

7. **Report**:
   - Total issues created
   - Milestone created/linked
   - Any issues that failed to create (with errors)
   - Labels created
   - URL to the milestone view
