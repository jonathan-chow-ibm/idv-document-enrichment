using 'main.bicep'

param environmentName = 'dev'
param location = 'eastus'
param baseName = 'idv-doc-enrich'
param ownerEmail = 'jonathan-ibm@idvllc.net'
param openAiDeploymentName = 'gpt-4o'
param openAiModelName = 'gpt-4o'
param openAiModelVersion = '2024-11-20'
param openAiCapacity = 50
param openAiMiniDeploymentName = 'gpt-4o'
param openAiMiniCapacity = 50
param deployModels = false
