using '../main.bicep'

param namePrefix = 'cpp'
param environment = 'dev'
param location = 'eastus2'

// Reconfirm model lifecycle and quota immediately before deployment.
param modelName = 'gpt-5.4-mini'
param modelVersion = '2026-03-17'
param modelDeploymentName = 'cpp-order-chat'
param modelSkuName = 'GlobalStandard'
param modelCapacity = 10

// Populate from Terraform outputs/Azure after the app infrastructure exists.
param apiPrincipalId = ''
param logAnalyticsWorkspaceResourceId = ''

param deployKeyVault = true
param publicNetworkAccess = true
param tags = {
  costCenter: 'replace-me'
  owner: 'replace-me'
}
