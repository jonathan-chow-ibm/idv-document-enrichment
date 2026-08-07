---
name: github-actions
description: Write, review, or refactor GitHub Actions workflows for CI/CD. USE FOR creating workflow YAML, reusable workflows, composite actions, matrix builds, caching, concurrency control, environments and approvals, OIDC federated authentication to Azure (passwordless), and deploying with azd or Bicep. Trigger phrases include "GitHub Actions", "workflow", "CI/CD pipeline", "ci.yml", "deploy workflow", "reusable workflow", "OIDC to Azure", "azure/login", "actions cache", "matrix build".
---

# GitHub Actions (Pipelines as Code)

Guidelines for writing maintainable, secure GitHub Actions workflows.

## Core Principles

- **OIDC only for cloud auth** — no long-lived secrets. Use `azure/login@v2` with `client-id` + `tenant-id` + `subscription-id` and `permissions: id-token: write`.
- **Pin actions by major version** (`@v4`) for internal/first-party, by SHA for third-party.
- **Least privilege `permissions:`** at the workflow and job level — default to `contents: read` and add more only as needed.
- **Small, reusable workflows** (`workflow_call`) over one mega-workflow with dozens of `if:` branches.
- **Concurrency groups** prevent duplicate runs on the same branch/PR.
- **Every workflow is self-documenting** via `name:` on the workflow, each job, and each step.

## Layout

```
.github/
  workflows/
    ci.yml                 # build + test on PR and main
    deploy-dev.yml         # deploy to dev on merge to main
    deploy-prod.yml        # deploy to prod on tag / manual
    reusable-build.yml     # shared build steps
    reusable-deploy.yml    # shared deploy steps
```

## CI Workflow Skeleton

```yaml
name: CI

on:
  pull_request:
    branches: [main]
  push:
    branches: [main]

permissions:
  contents: read

concurrency:
  group: ci-${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true

jobs:
  backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.x"
      - run: dotnet restore
        working-directory: backend
      - run: dotnet build --no-restore --configuration Release
        working-directory: backend
      - run: dotnet test --no-build --configuration Release --logger trx
        working-directory: backend

  frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: "22"
          cache: "npm"
          cache-dependency-path: frontend/package-lock.json
      - run: npm ci
        working-directory: frontend
      - run: npm run lint
        working-directory: frontend
      - run: npm run typecheck
        working-directory: frontend
      - run: npm test -- --run
        working-directory: frontend
      - run: npm run build
        working-directory: frontend
```

## Deploy Workflow (OIDC to Azure)

```yaml
name: Deploy Dev

on:
  push:
    branches: [main]
  workflow_dispatch:

permissions:
  id-token: write # required for OIDC
  contents: read

concurrency:
  group: deploy-dev
  cancel-in-progress: false # never cancel a deploy mid-flight

jobs:
  deploy:
    runs-on: ubuntu-latest
    environment: dev # protections, reviewers, env vars
    steps:
      - uses: actions/checkout@v4
      - uses: azure/login@v2
        with:
          client-id: ${{ vars.AZURE_CLIENT_ID }}
          tenant-id: ${{ vars.AZURE_TENANT_ID }}
          subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}
      - name: Provision infra
        run: |
          az deployment group create \
            --resource-group ${{ vars.AZURE_RESOURCE_GROUP }} \
            --template-file infra/main.bicep \
            --parameters infra/main.dev.bicepparam
```

For azd-based repos, use `azure/setup-azd@v2` and `azd provision` / `azd deploy`.

## Rules

- **`permissions:` is explicit** at workflow scope; never rely on the default.
- **`concurrency:` on every workflow** — CI cancels prior runs; deploys do not.
- **Environments for anything non-dev** with required reviewers and scoped secrets/vars.
- **Secrets in GitHub Environments**, not repo-level, for deploy credentials.
- **`id-token: write` is only on jobs that need it.**
- **Never `curl | bash`** — pin tool installs to a version; prefer official setup actions.
- **Cache deps** (`actions/setup-node` with `cache:`, `actions/cache` for NuGet, etc.) — don't rebuild the world on every run.
- **`workflow_dispatch`** on deploys so they're manually runnable for recovery.
- **Fail fast on security findings** — integrate CodeQL, dependency review, secret scanning as required status checks.

## Reusable Workflows

When two workflows share >30% of their steps, extract:

```yaml
# .github/workflows/reusable-deploy.yml
on:
  workflow_call:
    inputs:
      environment: { required: true, type: string }
      parameter-file: { required: true, type: string }
```

Call with:

```yaml
jobs:
  deploy:
    uses: ./.github/workflows/reusable-deploy.yml
    with:
      environment: prod
      parameter-file: infra/main.prod.bicepparam
```

## Common Anti-patterns

- Storing client secrets in repo or org secrets instead of using OIDC
- `permissions: write-all` or no `permissions:` block at all
- One 400-line workflow with a dozen `if:` guards
- Hard-coded resource group / subscription in YAML (use `vars` or environment)
- `continue-on-error: true` to paper over flaky steps
- Actions pinned to `@main` or `@latest`
- No concurrency, so deploys race each other
