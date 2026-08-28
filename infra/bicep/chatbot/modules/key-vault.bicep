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
  //checkov:skip=CKV_AZURE_109:The API App Service uses a Key Vault reference without private networking; dev must retain public access until that dependency is redesigned.
  //checkov:skip=CKV_AZURE_42:Purge protection and the maximum 90-day soft-delete retention are explicitly enabled below.
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
  //checkov:skip=CKV_AZURE_41:This provider-managed account key is replaced by deployment and does not have an independently managed expiration date.
  parent: vault
  name: 'azure-openai-api-key'
  properties: {
    contentType: 'Azure OpenAI API key'
    value: openAi.listKeys().key1
  }
}

var keyVaultSecretsUserRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '4633458b-17de-408a-b874-0445c86b69e6'
)

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
