# CPP chatbot infrastructure (Bicep)

This deployment adds the Azure resources used specifically by the CPP ordering chatbot. The existing Terraform stack remains the owner of the resource group, App Services, container registry, Application Insights, and Log Analytics workspace.

## What is deployed

- Azure OpenAI account (`Microsoft.CognitiveServices/accounts`, `kind: OpenAI`).
- One chat-model deployment with parameterized model, version, SKU, and capacity.
- Azure Key Vault with purge protection and RBAC, enabled by default because the current API requires an `api-key` header.
- A versioned Key Vault secret containing the generated Azure OpenAI account key.
- Optional `Cognitive Services OpenAI User` and `Key Vault Secrets User` assignments for the existing API App Service identity.
- Optional Azure OpenAI diagnostic logs and metrics routed to the Terraform-managed Log Analytics workspace.

The templates deliberately do not recreate the App Service, Application Insights, Log Analytics, database, or networking already owned by Terraform.

## Required inputs

Before deploying, replace `REPLACE_WITH_AVAILABLE_VERSION` and verify that the selected model, version, SKU, and quota exist in the target region. Capacity and model availability are subscription- and region-specific.

Obtain integration values after the Terraform deployment:

```powershell
$rg = '<terraform-resource-group>'
$api = '<terraform-api-web-app-name>'
$apiPrincipalId = az webapp identity show --resource-group $rg --name $api --query principalId -o tsv
$workspaceId = az monitor log-analytics workspace show --resource-group $rg --workspace-name '<workspace-name>' --query id -o tsv
```

Put those values in a local parameter file or pass them at deployment time. Do not commit credentials.

## Validate and deploy

```powershell
az bicep build --file infra/bicep/chatbot/main.bicep
az deployment group what-if `
  --resource-group <terraform-resource-group> `
  --template-file infra/bicep/chatbot/main.bicep `
  --parameters infra/bicep/chatbot/parameters/dev.bicepparam `
  --parameters apiPrincipalId=$apiPrincipalId logAnalyticsWorkspaceResourceId=$workspaceId
az deployment group create `
  --name cpp-chatbot-dev `
  --resource-group <terraform-resource-group> `
  --template-file infra/bicep/chatbot/main.bicep `
  --parameters infra/bicep/chatbot/parameters/dev.bicepparam `
  --parameters apiPrincipalId=$apiPrincipalId logAnalyticsWorkspaceResourceId=$workspaceId
```

This change does not include a deployment workflow and does not deploy resources.

## Connect the existing API

Read the deployment outputs and set these values on the Terraform-managed API App Service:

| App setting | Bicep output |
|---|---|
| `Agent__Endpoint` | `azureOpenAiEndpoint` |
| `Agent__Deployment` | `agentDeploymentName` |
| `Agent__ApiVersion` | `agentApiVersion` |
| `Agent__ApiKey` | `agentApiKeyAppSettingValue` |

`Agent__ApiKey` should contain the App Service Key Vault reference output, not the key itself. Because Terraform owns the Web App's complete `app_settings` map, add these settings in Terraform or the app deployment workflow; managing the same settings from Bicep would create competing ownership and possible configuration loss.

The Bicep role assignment also prepares managed-identity access. The current C# implementation still requires an API key; changing it to request an Entra token for `https://cognitiveservices.azure.com/.default` is an application change and should be tested separately before disabling local authentication.

## Production hardening options

The baseline permits public endpoints so it works with the current public App Service networking. A hardened production topology can additionally use:

- App Service regional VNet integration.
- Private endpoints for Azure OpenAI and Key Vault.
- Private DNS zones for `privatelink.openai.azure.com` and `privatelink.vaultcore.azure.net`.
- Azure Monitor alert rules and an action group for throttling, model errors, latency, and Key Vault access failures.
- A durable/distributed conversation cache if the API scales beyond one instance.

These are omitted from the baseline because they require network/address-space, DNS-link, alert-recipient, and scaling decisions that are not present in the repository.
