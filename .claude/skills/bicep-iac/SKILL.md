---
name: bicep-iac
description: Write, review, or refactor Azure Infrastructure as Code using Bicep. USE FOR creating Bicep modules, consuming Azure Verified Modules (AVM), structuring main.bicep / main.bicepparam, resource naming, tagging, RBAC, managed identity, Key Vault references, parameter files, what-if validation, and azd-compatible infra layouts. Trigger phrases include "Bicep", "IaC", "infrastructure as code", "Azure resources", "main.bicep", "bicepparam", "AVM", "Azure Verified Module", "deploy to Azure", "azd infra", "what-if".
---

# Bicep Infrastructure as Code

Guidelines for writing maintainable Azure Bicep for this workspace.

## Core Principles

- **Prefer Azure Verified Modules (AVM)** over hand-rolled resource definitions. Only write raw `Microsoft.*/@version` resources when no AVM exists or it doesn't fit the need.
- **One `main.bicep` per environment target**, composed of modules. No 500-line monoliths.
- **Parameters in `main.bicepparam`**, not inline defaults in `main.bicep`.
- **Deterministic naming** via a single `abbreviations.json` + `uniqueString(resourceGroup().id)` pattern — no magic strings sprinkled across modules.
- **Managed Identity over connection strings**. RBAC role assignments are IaC, not a portal click.
- **Tags are mandatory** (owner, environment, costCenter, workload) — centralized once, applied everywhere.

## Layout

```
infra/
  main.bicep              # composition root
  main.bicepparam         # environment parameters
  abbreviations.json      # naming prefixes
  modules/
    app-service.bicep
    sql-server.bicep
    ...
```

For `azd`-backed repos:

- `main.bicep` must output values `azd` consumes (endpoints, names, resource IDs)
- `azure.yaml` maps services to resources

## Finding the Right Module / Schema

Before hand-writing a resource:

1. Search AVM for a module matching the resource type.
2. If writing raw, fetch the schema for the exact API version you intend to use — don't guess property names.

## Template Skeleton

```bicep
targetScope = 'resourceGroup'

@description('Primary location for all resources.')
param location string = resourceGroup().location

@description('Environment name: dev, test, prod.')
@allowed(['dev', 'test', 'prod'])
param environmentName string

@description('Short workload name used in resource naming.')
@minLength(2)
@maxLength(10)
param workloadName string

var tags = {
  environment: environmentName
  workload: workloadName
  'managed-by': 'bicep'
}

var abbrs = loadJsonContent('abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, resourceGroup().id, environmentName))

module appService 'modules/app-service.bicep' = {
  name: 'appService'
  params: {
    name: '${abbrs.webSitesAppService}${workloadName}-${resourceToken}'
    location: location
    tags: tags
  }
}

output appServiceHostName string = appService.outputs.defaultHostName
```

## Parameter Files (`.bicepparam`)

```bicep
using 'main.bicep'

param environmentName = 'dev'
param workloadName = 'sampleapp'
param location = 'eastus2'
```

- One `main.<env>.bicepparam` per environment.
- Never commit secrets — reference Key Vault via `getSecret()` or pipeline variables.

## Rules

- **No hard-coded SKUs** in modules — pass as parameters with sensible defaults.
- **No inline secrets**; use Key Vault references or pipeline-injected parameters.
- **Every module has typed outputs** consumers actually need (IDs, names, endpoints).
- **Every resource is tagged** via the shared `tags` object.
- **Every public resource has `publicNetworkAccess` explicit** — don't rely on the provider default.
- **Role assignments belong in IaC**, not the portal. Use AVM's role-assignment sub-modules or `Microsoft.Authorization/roleAssignments`.
- **Run `az deployment group what-if`** (or the azd equivalent) before any real deployment.

## Common Anti-patterns

- Copy-pasting resource blocks instead of extracting a module
- Hard-coded names that clash across environments
- Secrets in `.bicepparam` files
- Omitting `dependsOn` when you rely on implicit ordering that Bicep can't see
- Writing raw resources when a maintained AVM already covers the case
- Single giant `main.bicep` with every resource inline

## Validation

```bash
az bicep build --file infra/main.bicep
az deployment group what-if \
  --resource-group <rg> \
  --template-file infra/main.bicep \
  --parameters infra/main.dev.bicepparam
```

For azd: `azd provision --preview`.
