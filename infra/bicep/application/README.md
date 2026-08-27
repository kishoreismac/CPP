# Application Bicep infrastructure

This subscription-scope stack creates the shared CPP Order Management Portal infrastructure. The independently deployed chatbot Bicep stack remains read-only and owns its Azure OpenAI and Key Vault resources.

## Resources

| Bicep declaration | Azure resource | Key configuration | Dependencies |
|---|---|---|---|
| `applicationResourceGroup` | Resource group | Deterministic environment-specific name, location, shared tags | Subscription, prefix, environment |
| `registry` | Azure Container Registry | Basic SKU, admin disabled, managed-identity image pulls | Resource group |
| `workspace` | Log Analytics workspace | `PerGB2018`, configurable retention | Resource group |
| `insights` | Application Insights | Web application type, workspace-based ingestion | Log Analytics |
| `plan` | Linux App Service plan | Environment-configurable SKU | Resource group |
| `web`, `webLogs` | Web Linux Web App | System identity, HTTPS, TLS 1.2, FTPS disabled, health check, container, settings and logs | Plan, ACR |
| `api`, `apiLogs` | API Linux Web App | System identity, HTTPS, TLS 1.2, FTPS disabled, health check, persistent SQLite, CORS, App Insights and logs | Plan, ACR, web hostname, App Insights |
| `apiAcrPull`, `webAcrPull` | ACR role assignments | Built-in `AcrPull` role at ACR scope | Web App identities, ACR |
| `apiDiagnostics`, `webDiagnostics` | Diagnostic settings | App Service logs and metrics sent to Log Analytics | Web Apps, workspace |

Outputs expose the resource group, ACR name/login server, API and web URLs, workspace ID, and secure Application Insights connection string.

## Validation and deployment

The `Azure Infrastructure (Bicep)` workflow runs these gates in order:

1. Bicep format check.
2. Bicep build, including every environment parameter file.
3. Bicep lint.
4. Checkov security scanning with SARIF upload.
5. PSRule for Azure policy analysis.
6. Azure provider validation.
7. Azure what-if for preview and deployment actions.
8. Deployment only after every applicable gate succeeds.

The stack supports `dev`, `test`, `qa`, and `prod`. Resource names use a stable six-character `uniqueString` suffix derived from the subscription, workload, environment, and Bicep namespace.

Local commands:

```powershell
az bicep format --file infra/bicep/application/main.bicep --stdout
az bicep build --file infra/bicep/application/main.bicep
az bicep lint --file infra/bicep/application/main.bicep
checkov --directory infra/bicep/application --framework bicep
az deployment sub validate --location centralindia --template-file infra/bicep/application/main.bicep --parameters infra/bicep/application/parameters/dev.bicepparam
az deployment sub what-if --location centralindia --template-file infra/bicep/application/main.bicep --parameters infra/bicep/application/parameters/dev.bicepparam
```

Destroy requires the explicit `confirm_destroy=DESTROY` workflow input and targets only the Bicep-tagged resource group for the selected environment.

## Ownership boundary

This stack owns shared application hosting, monitoring, identities, and registry resources. It does not declare Azure OpenAI accounts, model deployments, chatbot Key Vault resources, chatbot RBAC, or Azure OpenAI diagnostic settings.
