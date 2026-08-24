targetScope = 'resourceGroup'

@description('Environment name (dev, staging, prod)')
param environmentName string

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Base name for all resources')
param baseName string = 'idv-enrich'

@description('Azure OpenAI GPT-4o deployment name (Agent 2: extraction)')
param openAiDeploymentName string = 'gpt-4o'

@description('Azure OpenAI GPT-4o model name')
param openAiModelName string = 'gpt-4o'

@description('Azure OpenAI GPT-4o model version')
param openAiModelVersion string = '2024-08-06'

@description('Azure OpenAI GPT-4o TPM capacity (in thousands)')
param openAiCapacity int = 60

@description('Azure OpenAI GPT-4o-mini deployment name (Agent 1: classification)')
param openAiMiniDeploymentName string = 'gpt-4o-mini'

@description('Azure OpenAI GPT-4o-mini TPM capacity (in thousands)')
param openAiMiniCapacity int = 60

@description('Owner email tag required by Neudesic policy')
param ownerEmail string = 'Jonathan.Chow@neudesic.com'

// --- Naming ---
var resourceToken = '${baseName}-${environmentName}'
var tags = { Owner: ownerEmail }
var functionAppName = 'func-${resourceToken}'
var storageName = replace('st${resourceToken}', '-', '')
var appInsightsName = 'appi-${resourceToken}'
var keyVaultName = 'kv-${resourceToken}'
var openAiName = 'oai-${resourceToken}'
var docIntelName = 'di-${resourceToken}'
var hostingPlanName = 'plan-${resourceToken}'

// --- Storage Account ---
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: take(storageName, 24)
  location: location
  tags: tags
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
}

resource deploymentPackageContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${take(storageName, 24)}/default/deploymentpackage'
  dependsOn: [storageAccount]
}

resource configContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${take(storageName, 24)}/default/config'
  dependsOn: [storageAccount]
}

resource reportsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${take(storageName, 24)}/default/batch-reports'
  dependsOn: [storageAccount]
}

// --- Log Analytics Workspace (prevents Azure from auto-creating a managed resource group for App Insights) ---
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${resourceToken}'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// --- Application Insights ---
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// --- Key Vault ---
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: take(keyVaultName, 24)
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
  }
}

// --- Azure OpenAI ---
resource openAi 'Microsoft.CognitiveServices/accounts@2024-04-01-preview' = {
  name: openAiName
  location: location
  tags: tags
  kind: 'OpenAI'
  sku: { name: 'S0' }
  properties: {
    customSubDomainName: openAiName
    publicNetworkAccess: 'Enabled'
  }
}

resource openAiDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-04-01-preview' = {
  parent: openAi
  name: openAiDeploymentName
  sku: {
    name: 'GlobalStandard'
    capacity: openAiCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: openAiModelName
      version: openAiModelVersion
    }
  }
}

resource openAiMiniDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-04-01-preview' = {
  parent: openAi
  name: openAiMiniDeploymentName
  dependsOn: [openAiDeployment]
  sku: {
    name: 'GlobalStandard'
    capacity: openAiMiniCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-11-20'
    }
  }
}

// --- Azure AI Document Intelligence ---
resource docIntelligence 'Microsoft.CognitiveServices/accounts@2024-04-01-preview' = {
  name: docIntelName
  location: location
  tags: tags
  kind: 'FormRecognizer'
  sku: { name: 'S0' }
  properties: {
    customSubDomainName: docIntelName
    publicNetworkAccess: 'Enabled'
  }
}

// --- Function App (Flex Consumption — serverless, no VM quota required) ---
resource hostingPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: hostingPlanName
  location: location
  tags: tags
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true // Linux
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storageAccount.properties.primaryEndpoints.blob}deploymentpackage'
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 10
        instanceMemoryMB: 2048
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
    }
    siteConfig: {
      appSettings: [
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'AzureWebJobsStorage__accountName', value: storageAccount.name }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'OpenAiEndpoint', value: openAi.properties.endpoint }
        { name: 'OpenAiDeployment', value: openAiDeploymentName }
        { name: 'OpenAiMiniDeployment', value: openAiMiniDeploymentName }
        { name: 'DocIntelligenceEndpoint', value: docIntelligence.properties.endpoint }
      ]
    }
  }
}

// --- RBAC: Function App → OpenAI (Cognitive Services OpenAI User) ---
resource openAiRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAi.id, functionApp.id, '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
  scope: openAi
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// --- RBAC: Function App → Document Intelligence (Cognitive Services User) ---
resource docIntelRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(docIntelligence.id, functionApp.id, 'a97b65f3-24c7-4388-baec-2e87135dc908')
  scope: docIntelligence
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// --- RBAC: Function App → Key Vault (Key Vault Secrets User) ---
resource kvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, functionApp.id, '4633458b-17de-408a-b874-0445c86b69e6')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// --- RBAC: Function App → Storage (Blob Data Owner) ---
resource storageBlobOwnerRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionApp.id, 'b7e6dc63-2d1d-43c3-bdf9-9b7e1b1e8e11')
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc63-2d1d-43c3-bdf9-9b7e1b1e8e11')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// --- RBAC: Function App → Storage (Queue Data Contributor) ---
resource storageQueueContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionApp.id, '974c5e8b-45b9-4653-ba55-5f855dd0fb88')
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// --- RBAC: Function App → Storage (Table Data Contributor) ---
resource storageTableContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionApp.id, '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// --- Outputs ---
output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output openAiEndpoint string = openAi.properties.endpoint
output docIntelEndpoint string = docIntelligence.properties.endpoint
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output functionAppPrincipalId string = functionApp.identity.principalId
