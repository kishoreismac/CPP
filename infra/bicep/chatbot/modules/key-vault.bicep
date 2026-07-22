param keyVaultName string
param location string
param openAiAccountName string
param apiPrincipalId string
param publicNetworkAccess bool
param tags object

resource openAi 'Microsoft.CognitiveServices/accounts@2023-05-01' existing = {
  name: openAiAccountName
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enablePurgeProtection: true
    softDeleteRetentionInDays: 90
    publicNetworkAccess: publicNetworkAccess ? 'Enabled' : 'Disabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: publicNetworkAccess ? 'Allow' : 'Deny'
    }
  }
  tags: tags
}

resource openAiApiKey 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'azure-openai-api-key'
  properties: {
    value: openAi.listKeys().key1
  }
}

var keyVaultSecretsUserRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')

resource apiSecretReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(apiPrincipalId)) {
  name: guid(vault.id, apiPrincipalId, keyVaultSecretsUserRoleId)
  scope: vault
  properties: {
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleId
  }
}

output keyVaultName string = vault.name
output secretUri string = openAiApiKey.properties.secretUriWithVersion
output appServiceKeyVaultReference string = '@Microsoft.KeyVault(SecretUri=${openAiApiKey.properties.secretUriWithVersion})'
