using '../main.bicep'

param namePrefix = 'cpp'
param environment = 'prod'
param location = 'eastus2'

// Reconfirm model lifecycle and quota immediately before deployment.
param modelName = 'gpt-5.4-mini'
param modelVersion = '2026-03-17'
param modelDeploymentName = 'cpp-order-chat'
param modelSkuName = 'GlobalStandard'
param modelCapacity = 30

param apiPrincipalId = ''
param logAnalyticsWorkspaceResourceId = ''

param deployKeyVault = true
// Production private networking is described in README.md; do not set this to
// false until private endpoints and DNS are connected to the App Service.
param publicNetworkAccess = true
param tags = {
  costCenter: 'replace-me'
  owner: 'replace-me'
}
