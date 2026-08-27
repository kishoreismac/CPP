targetScope = 'subscription'

@description('Azure region for all application resources.')
param location string = 'centralindia'

@allowed([
  'dev'
  'test'
  'qa'
  'prod'
])
param environment string = 'dev'

@minLength(2)
@maxLength(24)
@description('Lowercase workload prefix used in resource names.')
param namePrefix string = 'cpp-order'

@description('Linux App Service Plan SKU. Use at least P1v3 for production.')
param appServiceSku string = 'B1'

param apiImageTag string = 'latest'
param webImageTag string = 'latest'
param mockJdeFailSubmissions bool = false

@minValue(30)
param logRetentionDays int = 30

param tags object = {}

var suffix = take(uniqueString(subscription().subscriptionId, namePrefix, environment, 'bicep'), 6)
var baseName = '${namePrefix}-${environment}-${suffix}'
var resourceGroupName = 'rg-${baseName}'
var commonTags = union(tags, {
  application: 'CPP Order Management Portal'
  environment: environment
  'managed-by': 'bicep'
})

resource applicationResourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: commonTags
}

module application 'modules/application.bicep' = {
  name: 'cpp-application-${environment}'
  scope: applicationResourceGroup
  params: {
    location: location
    baseName: baseName
    appServiceSku: appServiceSku
    apiImageTag: apiImageTag
    webImageTag: webImageTag
    mockJdeFailSubmissions: mockJdeFailSubmissions
    logRetentionDays: logRetentionDays
    tags: commonTags
  }
}

output resourceGroupName string = applicationResourceGroup.name
output containerRegistryName string = application.outputs.containerRegistryName
output containerRegistryLoginServer string = application.outputs.containerRegistryLoginServer
output apiUrl string = application.outputs.apiUrl
output webUrl string = application.outputs.webUrl
output logAnalyticsWorkspaceId string = application.outputs.logAnalyticsWorkspaceId
@secure()
output applicationInsightsConnectionString string = application.outputs.applicationInsightsConnectionString
