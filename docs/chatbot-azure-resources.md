# Azure resource analysis for the CPP ordering chatbot

## Application findings

The chatbot is implemented inside the ASP.NET Core API, not as an Azure AI Foundry hosted agent. `AgentController` exposes `POST /api/agent/messages` and `GET /api/agent/health`. `AssistantAgentService` calls an Azure OpenAI-compatible chat-completions endpoint, requires structured JSON output, executes only approved application tools, and keeps order submission under deterministic application control.

The approved tools query the existing EF Core data and application services for products, accounts, delivery locations, order summaries, and order placement. Short-lived conversational entities and order lines are stored in ASP.NET `IMemoryCache`; the client also sends conversation history. The current API authenticates to the model using an `api-key` header.

## Complete resource decision

| Resource | Baseline status | Purpose / decision |
|---|---:|---|
| Azure OpenAI account | Required, new Bicep | Provides the `*.openai.azure.com` endpoint expected by `AgentService`. |
| Azure OpenAI model deployment | Required, new Bicep | Hosts the chat model. Model/version/SKU/capacity stay parameterized because regional availability and subscription quota vary. |
| Azure Key Vault | Required for current code, new Bicep | Stores the account key without putting it in source, parameters, or ordinary App Service configuration. |
| Key Vault secret | Required for current code, new Bicep | Holds the Azure OpenAI key and is consumed through an App Service Key Vault reference. |
| Runtime RBAC assignments | Recommended, new Bicep | Lets the existing API identity resolve the secret and prepares least-privilege Azure OpenAI access for a later managed-identity code migration. |
| Azure Monitor diagnostic setting | Recommended, new Bicep | Sends AI resource logs/metrics to the existing workspace. |
| API App Service + managed identity | Required, already Terraform-managed | Runs `AssistantAgentService`; must not be duplicated in Bicep. |
| Log Analytics workspace | Required for centralized logs, already Terraform-managed | Receives App Service and Azure OpenAI telemetry. |
| Application Insights | Required for app APM, already Terraform-managed | Observes the API; no chatbot-specific duplicate is needed. |
| Resource group | Required, already Terraform-managed | Bicep is intentionally resource-group scoped and deploys into it. |
| Container registry/service plan/web app | Required for the full application, already Terraform-managed | Application hosting concerns, not chatbot-specific resources. |
| Existing application database | Required, already part of the application | Product/account/order grounding and persistence. The chatbot does not use a vector index. |

## Resources not currently required

| Resource | Why it is not in the baseline |
|---|---|
| Azure AI Foundry project/hub/hosted agent | The repository owns orchestration, tool execution, schema validation, and policy checks in C#. |
| Azure AI Search | Retrieval is structured SQL/EF Core catalog and account lookup, not document/vector RAG. |
| Cosmos DB | Orders/accounts use the existing application store; no independent chatbot document store exists. |
| Azure Cache for Redis | `IMemoryCache` is sufficient for the current single-instance behavior. Redis becomes necessary for durable shared state across scaled API instances. |
| Storage account | The chatbot has no document ingestion, transcript archive, or model file requirement. |
| Standalone Azure AI Content Safety | Azure OpenAI has built-in safety controls; add a separate service only for an explicit custom moderation workflow. |
| API Management | Useful for enterprise gateway policy and external consumers, but the SPA currently calls its own backend API. |
| Service Bus/Event Grid | The order-assistant path is synchronous; no async chatbot workload is implemented. |

## Runtime flow

1. The React client sends a message, history, and structured context to the API.
2. The API loads database context and calls the configured Azure OpenAI deployment.
3. The model returns schema-constrained intent, entities, policy data, and proposed tool calls.
4. The API rejects unapproved tools and executes approved catalog/account/order operations locally.
5. For lookups, grounded database results are sent to the model for the final response.
6. Order submission occurs only after application validation and explicit confirmation.
7. API telemetry goes to the existing Application Insights/Log Analytics stack; Azure OpenAI diagnostics can go to the same workspace.

## Ownership boundary

Terraform continues to own shared application infrastructure. Bicep owns only resources under `infra/bicep/chatbot`. Cross-stack values are explicit inputs (`apiPrincipalId`, `logAnalyticsWorkspaceResourceId`) and outputs (endpoint, deployment, API version, Key Vault reference), preventing two infrastructure engines from managing the same Web App or monitoring workspace.

## Security and scale gaps discovered

- Account-key authentication is functional but managed identity is the better end state. The role is provisioned, but C# must be changed before the key can be removed.
- Public network access is the deployable baseline. Private endpoints require an agreed VNet/subnet/DNS design and App Service VNet integration.
- `IMemoryCache` is instance-local. Multiple API instances can lose conversational continuity unless the request carries all state or a distributed cache is introduced.
- The SQLite database on App Service storage is not an enterprise scale-out data tier. A managed database is a broader application-infrastructure decision, not a chatbot-only dependency.
- Model quota and available versions must be checked immediately before deployment; the template deliberately does not promise that a model version is available in every region.

See `infra/bicep/chatbot/README.md` for validation, deployment, and App Service integration instructions.
