param accountName string
param location string
param modelDeploymentName string
param modelName string
param modelVersion string
param modelSkuName string
param modelCapacity int
param publicNetworkAccess bool
param tags object

resource account 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  //checkov:skip=CKV_AZURE_134:The existing API calls this public endpoint and has no private endpoint or VNet integration.
  //checkov:skip=CKV_AZURE_236:The existing API authenticates with AGENT_API_KEY; disabling local authentication would break the runtime contract.
  name: accountName
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    customSubDomainName: accountName
    publicNetworkAccess: publicNetworkAccess ? 'Enabled' : 'Disabled'
    networkAcls: {
      defaultAction: publicNetworkAccess ? 'Allow' : 'Deny'
    }
  }
  tags: tags
}

resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: account
  name: modelDeploymentName
  sku: {
    name: modelSkuName
    capacity: modelCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: modelName
      version: modelVersion
    }
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
  }
}

output accountName string = account.name
output accountId string = account.id
output endpoint string = account.properties.endpoint
output deploymentName string = modelDeployment.name
