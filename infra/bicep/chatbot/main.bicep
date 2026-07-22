targetScope = 'resourceGroup'

@description('Short workload name used in resource names.')
param namePrefix string = 'cpp'

@allowed([
  'dev'
  'test'
  'qa'
  'stage'
  'prod'
])
param environment string

@description('Azure region where the selected model is available and quota exists.')
param location string = resourceGroup().location

@description('Azure OpenAI model name, for example gpt-4.1-mini. Verify regional availability before deployment.')
param modelName string

@description('Exact model version available in the target region.')
param modelVersion string

@description('Logical deployment name consumed by Agent__Deployment.')
param modelDeploymentName string = 'cpp-order-chat'

@description('Model deployment SKU. GlobalStandard is preferred when policy permits it.')
param modelSkuName string = 'GlobalStandard'

@minValue(1)
@description('Model capacity in thousands of tokens per minute for Standard/GlobalStandard deployments.')
param modelCapacity int = 10

@description('Object/principal ID of the existing API App Service system-assigned identity. Leave empty to skip runtime RBAC assignments.')
param apiPrincipalId string = ''

@description('Resource ID of the Terraform-managed Log Analytics workspace. Leave empty to skip AI diagnostics.')
param logAnalyticsWorkspaceResourceId string = ''

@description('Create a Key Vault and store the Azure OpenAI account key for compatibility with the current key-based application.')
param deployKeyVault bool = true

@description('Allow public access to Azure OpenAI and Key Vault. Set false only after private networking is provided.')
param publicNetworkAccess bool = true

param tags object = {}

var suffix = uniqueString(subscription().subscriptionId, resourceGroup().id, namePrefix, environment)
var normalizedPrefix = toLower(replace(namePrefix, '-', ''))
var commonTags = union(tags, {
  application: 'CPP Order Management Portal'
  component: 'chatbot'
  environment: environment
  'managed-by': 'bicep'
})

module ai 'modules/azure-openai.bicep' = {
  name: 'chatbot-ai-${uniqueString(deployment().name)}'
  params: {
    accountName: 'oai-${namePrefix}-${environment}-${suffix}'
    location: location
    modelDeploymentName: modelDeploymentName
    modelName: modelName
    modelVersion: modelVersion
    modelSkuName: modelSkuName
    modelCapacity: modelCapacity
    publicNetworkAccess: publicNetworkAccess
    tags: commonTags
  }
}

module vault 'modules/key-vault.bicep' = if (deployKeyVault) {
  name: 'chatbot-kv-${uniqueString(deployment().name)}'
  params: {
    keyVaultName: take('kv-${normalizedPrefix}-${environment}-${suffix}', 24)
    location: location
    openAiAccountName: ai.outputs.accountName
    apiPrincipalId: apiPrincipalId
    publicNetworkAccess: publicNetworkAccess
    tags: commonTags
  }
}

module runtimeRole 'modules/runtime-role.bicep' = if (!empty(apiPrincipalId)) {
  name: 'chatbot-runtime-role-${uniqueString(deployment().name)}'
  params: {
    cognitiveAccountName: ai.outputs.accountName
    principalId: apiPrincipalId
  }
}

module diagnostics 'modules/diagnostics.bicep' = if (!empty(logAnalyticsWorkspaceResourceId)) {
  name: 'chatbot-diagnostics-${uniqueString(deployment().name)}'
  params: {
    cognitiveAccountName: ai.outputs.accountName
    logAnalyticsWorkspaceResourceId: logAnalyticsWorkspaceResourceId
  }
}

output azureOpenAiAccountName string = ai.outputs.accountName
output azureOpenAiEndpoint string = ai.outputs.endpoint
output agentDeploymentName string = modelDeploymentName
output agentApiVersion string = '2024-10-21'
output keyVaultName string = deployKeyVault ? vault!.outputs.keyVaultName : ''
output agentApiKeyAppSettingValue string = deployKeyVault ? vault!.outputs.appServiceKeyVaultReference : ''
output apiManagedIdentityRoleConfigured bool = !empty(apiPrincipalId)
output diagnosticsConfigured bool = !empty(logAnalyticsWorkspaceResourceId)
