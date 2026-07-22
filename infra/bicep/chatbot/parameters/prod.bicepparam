using '../main.bicep'

param namePrefix = 'cpp'
param environment = 'prod'
param location = 'eastus2'

// Confirm the exact model/version and quota in this region before deployment.
param modelName = 'gpt-4.1-mini'
param modelVersion = 'REPLACE_WITH_AVAILABLE_VERSION'
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
