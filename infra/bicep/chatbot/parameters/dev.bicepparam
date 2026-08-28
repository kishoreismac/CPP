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

// The workflow replaces these values with resources resolved from Azure.
// A valid placeholder workspace ID keeps conditional diagnostics visible to
// static policy analysis; it is never used by workflow deployments.
param apiPrincipalId = ''
param logAnalyticsWorkspaceResourceId = '/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/psrule-dev/providers/Microsoft.OperationalInsights/workspaces/psrule-dev'

param deployKeyVault = true
param publicNetworkAccess = true
param tags = {
  costCenter: 'replace-me'
  owner: 'replace-me'
}
