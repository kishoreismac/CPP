using '../main.bicep'

param namePrefix = 'cpp'
param environment = 'dev'
param location = 'eastus2'

// Confirm the exact model/version and quota in this region before deployment.
param modelName = 'gpt-4.1-mini'
param modelVersion = 'REPLACE_WITH_AVAILABLE_VERSION'
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
