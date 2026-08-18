---
name: scaffold-bicep-module
description: "Scaffold a new Bicep module with standardized file and parameter structure. Use when creating a new Azure resource module, adding a Bicep module to infra/, or scaffolding infrastructure for a new service."
argument-hint: "Name of the Azure resource to scaffold (e.g., service-bus, storage-account, cosmos-db)"
---

# Scaffold Bicep Module

## When to Use

- Adding a new Azure resource type to the infrastructure
- Creating a standardized Bicep module under `infra/modules/`
- Bootstrapping infrastructure for a new service in the claims processing system

## Procedure

1. **Determine the module name**
   - Use the user's argument or derive from the Azure resource type
   - Apply kebab-case naming: `service-bus`, `storage-account`, `app-configuration`

2. **Check for conflicts**
   - Verify `infra/modules/<module-name>.bicep` does not already exist
   - If it exists, ask the user whether to update or create a variant

3. **Scaffold the module file**
   - Use the template in [./assets/module-template.bicep](./assets/module-template.bicep)
   - Replace all `{{PLACEHOLDERS}}` with actual values
   - Fill in the resource definition based on the Azure resource type
   - Follow all conventions from `.github/instructions/bicep.instructions.md`

4. **Create or update the parameter file**
   - If `infra/env/dev.bicepparam` exists, add the new module's parameters
   - Otherwise, note that parameter files should be created per environment

5. **Wire into main.bicep**
   - Add a module reference in `infra/main.bicep` pointing to the new module
   - If `main.bicep` does not exist, scaffold it with the new module as the first entry

6. **Validate**
   - Run `az bicep lint --file infra/modules/<module-name>.bicep` to check for errors
   - Run `az bicep build --file infra/modules/<module-name>.bicep` to verify compilation

## Rules

- Every module must accept `location`, `environmentName`, and `tags` parameters at minimum
- Every resource must include the standard tag set (see Bicep conventions)
- Use `@description()` on all parameters and outputs
- Use `@secure()` for any secret parameters
- Prefer managed identities over keys or connection strings
- One resource type per module — do not bundle unrelated resources
