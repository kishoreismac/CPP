# Azure deployment with Terraform

## 1. Purpose and scope

This guide deploys the CPP Order Management Portal as two Linux custom-container Azure Web Apps. It reflects the application's current implementation rather than assuming capabilities it does not have. The React site is public, the ASP.NET Core API is public over HTTPS, images are stored in Azure Container Registry (ACR), SQLite is stored beneath the API Web App's persistent `/home` directory, and platform logs are sent to Log Analytics.

This topology is appropriate for a demonstration, QA environment, or low-volume single-instance workload. It is not horizontally scalable because SQLite is a single-file embedded database and the application seeds and writes it directly. See **Production evolution** before treating this as a production design.

## 2. Application analysis that drives the design

| Application behavior | Infrastructure consequence |
|---|---|
| React/Vite is compiled into static files and served by Nginx on port 80 | A Linux Web App runs the `cpp-web` container with `WEBSITES_PORT=80` |
| ASP.NET Core listens on port 8080 in its Dockerfile | A second Linux Web App runs `cpp-api` with `WEBSITES_PORT=8080` |
| The browser calls an absolute `VITE_API_URL` baked into the web bundle | The web image must be built after the API hostname is known |
| API CORS previously allowed only localhost | `Cors__AllowedOrigins__0` supplies the deployed web origin; `Program.cs` now reads configurable origins |
| EF Core uses SQLite and calls `EnsureCreated()` at startup | The connection string points to `/home/cpp.db`, and App Service persistent storage is enabled |
| There is no external identity, cache, queue, or real JDE integration | No Entra app registration, Redis, Service Bus, or private integration resource is provisioned |
| API exposes `/health` and web root returns the SPA | App Service health checks use `/health` and `/` respectively |
| The API emits console logs and correlation response headers | App Service diagnostic logs and metrics are routed to Log Analytics |
| No Application Insights SDK is installed | The connection string is supplied for future instrumentation, while current useful telemetry is primarily platform logs/metrics |

## 3. Provisioned resources

Terraform creates:

1. **Resource group** — lifecycle boundary for the workload environment.
2. **Azure Container Registry Basic** — private storage for API and web images. Admin credentials are disabled.
3. **Linux App Service Plan** — shared compute for both containers. `B1` is the inexpensive default; use `P1v3` or better for production-like workloads.
4. **API Linux Web App** — runs ASP.NET Core, enables persistent `/home`, configures SQLite, CORS, JDE simulation, HTTPS, TLS 1.2, logging, and health checks.
5. **Web Linux Web App** — runs the Nginx-hosted React bundle with HTTPS and health checks.
6. **System-assigned managed identities** — one for each Web App.
7. **ACR Pull role assignments** — allow each Web App identity to pull images without registry passwords.
8. **Log Analytics workspace** — centralized HTTP, console, application, and metric data.
9. **Workspace-based Application Insights resource** — reserved for application telemetry and supplied to the API through its connection string.
10. **Diagnostic settings** — send supported App Service logs and metrics to Log Analytics.

No passwords or application secrets are required by the current POC. Therefore, Key Vault is intentionally not provisioned. Add it when real fulfillment credentials, database credentials, identity secrets, certificates, or other secrets exist.

## 4. Files

| File | Purpose |
|---|---|
| `infra/terraform/versions.tf` | Terraform/provider constraints and Azure provider setup |
| `infra/terraform/variables.tf` | Typed inputs and defaults |
| `infra/terraform/main.tf` | Azure resources, identities, settings, and diagnostics |
| `infra/terraform/outputs.tf` | Registry, resource group, and application endpoints |
| `infra/terraform/terraform.tfvars.example` | Safe configuration template |
| `infra/terraform/backend.hcl.example` | Optional local remote-state configuration template |
| `infra/terraform/.gitignore` | Prevents local state and variable files being committed |

## 5. Prerequisites

- Azure subscription and permission to create resource groups, role assignments, Web Apps, ACR, Log Analytics, and Application Insights.
- Azure CLI authenticated with `az login`.
- Terraform 1.6 or newer.
- Docker with Linux container support, or use `az acr build` as shown below.
- A unique, lowercase workload prefix. A random suffix is added automatically.

Set the subscription explicitly:

```powershell
az account set --subscription "<subscription-id>"
az account show --query "{name:name,id:id,tenantId:tenantId}"
```

## 6. Terraform state

The configuration declares an AzureRM backend without embedded environment values. CI bootstraps its state storage and passes backend settings during `terraform init`. For shared local use, create a dedicated storage account and blob container, copy `backend.hcl.example` to `backend.hcl`, update its literal values, and run `terraform init -backend-config=backend.hcl`.

Recommended state controls:

- Azure AD authentication rather than storage keys.
- Blob versioning and soft delete.
- A separate state key for each environment.
- Restricted RBAC for the state container.
- Never commit `terraform.tfstate` or `terraform.tfvars`.

## 7. Configure the environment

```powershell
cd C:\CPP\infra\terraform
Copy-Item terraform.tfvars.example terraform.tfvars
```

Edit `terraform.tfvars` and set at least `subscription_id`. Resource names include a stable random suffix stored in Terraform state, so retain the state file.

Initialize and inspect:

```powershell
terraform init
terraform fmt -check -recursive
terraform validate
terraform plan -out cpp.tfplan
```

## 8. Bootstrap ACR and obtain hostnames

The private registry must exist before application images can be pushed. Provision it and the supporting base resources first:

```powershell
terraform apply -target=azurerm_container_registry.main
```

Then run the complete apply once to create the Web Apps and produce their stable hostnames. They may temporarily report an image-pull error until images are pushed:

```powershell
terraform apply
$acr = terraform output -raw container_registry_name
$apiUrl = terraform output -raw api_url
```

## 9. Build and push containers

The preferred deployment path is `.github/workflows/app-azure.yml`. It validates both projects, builds immutable API and web images using ACR Tasks, updates both Web Apps, restarts them, and performs live health/API/web smoke tests. Run **Build and Deploy Application** with the target environment after infrastructure has been applied.

Use an immutable tag such as a commit SHA or release number. The web image requires the final API URL at build time because Vite substitutes it into the browser bundle.

Using ACR Tasks avoids requiring a local Docker daemon:

```powershell
$tag = "2026.07.20.1"
az acr build --registry $acr --image "cpp-api:$tag" C:\CPP\apps\api
az acr build --registry $acr --image "cpp-web:$tag" --build-arg "VITE_API_URL=$apiUrl/api" C:\CPP\apps\web
```

Update `api_image_tag` and `web_image_tag` in `terraform.tfvars`, then apply:

```powershell
terraform plan -out cpp.tfplan
terraform apply cpp.tfplan
```

If an app attempted to pull before its `AcrPull` role became effective, restart it after RBAC propagation:

```powershell
$rg = terraform output -raw resource_group_name
az webapp restart --resource-group $rg --name ((terraform output -raw api_url) -replace 'https://','' -replace '\.azurewebsites\.net','')
az webapp restart --resource-group $rg --name ((terraform output -raw web_url) -replace 'https://','' -replace '\.azurewebsites\.net','')
```

## 10. Validate the deployment

```powershell
$webUrl = terraform output -raw web_url
$apiUrl = terraform output -raw api_url
Invoke-WebRequest "$apiUrl/health"
Invoke-WebRequest $webUrl
```

Expected results:

- API health returns HTTP 200 and `{ "status": "Healthy" }`.
- Web root returns HTTP 200.
- The browser can load accounts and orders without a CORS error.
- Creating a draft creates `/home/cpp.db` on the API Web App.

OpenAPI is currently mapped only in the ASP.NET `Development` environment, so `/openapi/v1.json` is intentionally unavailable in this deployment.

## 11. Configuration reference

| Terraform variable | Default | Notes |
|---|---:|---|
| `subscription_id` | required | Target Azure subscription |
| `location` | `centralindia` | Keep App Service, ACR, and monitoring in one region |
| `environment` | `dev` | `dev`, `test`, `qa`, or `prod` |
| `name_prefix` | `cpp-order` | Used in all resource names |
| `app_service_sku` | `B1` | Shared plan SKU; `P1v3` is a better production baseline |
| `api_image_tag` | `latest` | Prefer immutable version or commit SHA |
| `web_image_tag` | `latest` | Must correspond to the correct baked-in API URL |
| `mock_jde_fail_submissions` | `false` | Demonstrates downstream failure recovery when true |
| `log_retention_days` | `30` | Workspace retention |
| `tags` | `{}` | Owner, cost center, data classification, etc. |

Key runtime settings applied to the API:

| Setting | Value/purpose |
|---|---|
| `WEBSITES_PORT` | `8080` |
| `WEBSITES_ENABLE_APP_SERVICE_STORAGE` | `true`, required for SQLite persistence |
| `ConnectionStrings__Cpp` | `Data Source=/home/cpp.db` |
| `Cors__AllowedOrigins__0` | HTTPS hostname of the web app |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `MockJde__FailSubmissions` | Failure simulation switch |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Future SDK telemetry integration |

## 12. Operations

### Logs

Use Log Analytics for durable platform logs. For immediate container startup diagnosis:

```powershell
az webapp log tail --resource-group $rg --name <api-app-name>
az webapp log tail --resource-group $rg --name <web-app-name>
```

Useful Log Analytics queries include:

```kusto
AppServiceHTTPLogs
| where TimeGenerated > ago(1h)
| summarize Requests=count(), Failures=countif(ScStatus >= 500) by CsHost
```

```kusto
AppServiceConsoleLogs
| where TimeGenerated > ago(1h)
| where ResultDescription has_any ("error", "exception", "failed")
| project TimeGenerated, Host, ResultDescription
| order by TimeGenerated desc
```

Diagnostic table names can vary with Azure diagnostic mode and provider behavior; inspect the workspace schema after the first records arrive.

### SQLite backup and recovery

The database is stored on persistent App Service storage, but persistence is not a backup strategy. Before meaningful use:

- Enable scheduled App Service backups to a separate storage account, or implement SQLite-aware backups.
- Include `/home/cpp.db` in the backup scope.
- Define retention and test restoration into a separate environment.
- Never copy a live SQLite file naively while writes are occurring; use SQLite's backup mechanism or a coordinated application stop/checkpoint.
- Treat the application as recovery-sensitive because orders and audit events share the same file.

### Scaling

Keep the API at exactly one instance while it uses SQLite. Multiple API instances would have unsafe or inconsistent file access semantics and application startup seeding is not designed for concurrent replicas. The web app is stateless, but because both apps share one App Service Plan, scaling the plan scales both applications together. Do not enable scale-out until the database is replaced.

### Deployment rollback

Use immutable tags. To roll back, set `api_image_tag` and `web_image_tag` to the previous known-good tags and apply Terraform. Because the web bundle contains the API URL, keep environment-specific web images or adopt runtime configuration in a future revision.

## 13. Security posture

The stack provides:

- HTTPS-only endpoints.
- Minimum TLS 1.2.
- Disabled FTPS.
- Private ACR with disabled admin credentials.
- Managed-identity image pulls.
- No secrets committed in Terraform.
- Central diagnostics.

The application itself still lacks authentication and authorization. Both endpoints are publicly reachable. For a production system, add one of these patterns:

1. Entra ID authentication on both Web Apps, with API audience validation and app roles.
2. Azure Front Door Premium with WAF in front of the web app, and private endpoints for origins.
3. API Management for external API governance, throttling, tokens, and contract control.

Do not simply make the API private without changing the frontend architecture: the React SPA calls the API directly from the user's browser, so the browser must have a reachable API endpoint.

## 14. Production evolution

Before production, replace or add the following:

1. **Managed relational database** — migrate SQLite and JSON order-line columns to Azure SQL or PostgreSQL Flexible Server with migrations, connection resiliency, backups, private networking, and normalized order-line tables.
2. **Authentication/authorization** — integrate Entra ID and enforce account entitlements and API permissions server-side.
3. **Durable idempotency** — persist `Idempotency-Key` and fulfillment outcomes transactionally to prevent duplicate external orders.
4. **Real fulfillment integration** — store credentials in Key Vault and use private connectivity, retries, timeouts, circuit breaking, and a durable queue/outbox.
5. **Application telemetry** — install the Application Insights/OpenTelemetry SDK, include correlation IDs in structured logs, and trace database and fulfillment operations.
6. **Network/security edge** — Front Door Premium/WAF, custom domains, managed certificates, private endpoints, and access restrictions as required.
7. **Independent compute plans** — separate web and API plans when independent scaling or fault isolation is needed.
8. **Deployment slots** — deploy to staging slots, run smoke tests, then swap.
9. **Alerts** — action group plus alerts for 5xx rate, latency, availability, CPU/memory, restarts, and failed health checks. Alert recipients must be supplied by the owning team.
10. **CI/CD federation** — use GitHub Actions or Azure DevOps workload identity federation, build immutable images, scan them, push to ACR, run Terraform plan with approval, apply, and smoke-test.
11. **Data protection** — formal backup/restore, retention, classification, encryption requirements, and recovery objectives.
12. **Contract and UI corrections** — connect favorite mutations, add server pagination, remove the unused legacy editor, generate typed clients from OpenAPI, and expand automated submission/failure tests.

## 15. Destruction

For an ephemeral environment:

```powershell
terraform plan -destroy -out destroy.tfplan
terraform apply destroy.tfplan
```

Destroying the resource group deletes the Web Apps, registry images, logs, and the SQLite order database. Export or back up required data first. Terraform state should be retained according to the organization's audit policy.
