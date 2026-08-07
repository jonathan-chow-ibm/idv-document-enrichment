// ============================================================================
// Module: {{MODULE_NAME}}
// Description: {{DESCRIPTION}}
// ============================================================================

targetScope = 'resourceGroup'

// --------------------------------------------------------------------------
// Parameters
// --------------------------------------------------------------------------

@description('Azure region for the resource.')
param location string

@description('Environment name (dev, test, prod).')
param environmentName string

@description('Resource tags.')
param tags object

@description('Name of the {{RESOURCE_FRIENDLY_NAME}}.')
param {{RESOURCE_PARAM_NAME}} string

// --------------------------------------------------------------------------
// Resources
// --------------------------------------------------------------------------

resource {{RESOURCE_SYMBOLIC_NAME}} '{{RESOURCE_TYPE}}@{{API_VERSION}}' = {
  name: {{RESOURCE_PARAM_NAME}}
  location: location
  tags: tags
  properties: {
    // TODO: configure resource properties
  }
}

// --------------------------------------------------------------------------
// Outputs
// --------------------------------------------------------------------------

@description('Resource ID of the {{RESOURCE_FRIENDLY_NAME}}.')
output id string = {{RESOURCE_SYMBOLIC_NAME}}.id

@description('Name of the {{RESOURCE_FRIENDLY_NAME}}.')
output name string = {{RESOURCE_SYMBOLIC_NAME}}.name
