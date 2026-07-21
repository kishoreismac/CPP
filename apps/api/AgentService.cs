using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;

namespace Cpp.Api;

public interface IAssistantAgentService
{
    Task<AssistantResponse> ProcessAsync(AssistantRequest request, CancellationToken ct);
    Task<AssistantHealthResponse> CheckHealthAsync(CancellationToken ct);
}

public class AssistantAgentService(AppDb db, IConfiguration config, HttpClient httpClient, IOrderRuleService rules, IFulfillmentGateway fulfillment, IMemoryCache memory) : IAssistantAgentService
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<AssistantResponse> ProcessAsync(AssistantRequest request, CancellationToken ct)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var conversationId = string.IsNullOrWhiteSpace(request.ConversationId) ? Guid.NewGuid().ToString("N") : request.ConversationId.Trim();
        var message = request.Message?.Trim() ?? string.Empty;
        var stateKey = $"assistant-entities:{conversationId}";
        var conversationEntities = memory.Get<Dictionary<string, string?>>(stateKey) is { } saved
          ? new Dictionary<string, string?>(saved, StringComparer.OrdinalIgnoreCase)
          : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in request.ContextEntities ?? new Dictionary<string, string?>())
        {
            if (string.IsNullOrWhiteSpace(entity.Value)) conversationEntities.Remove(entity.Key);
            else conversationEntities[entity.Key] = entity.Value;
        }
        var effectiveRequest = request with { ConversationId = conversationId, ContextEntities = conversationEntities };
        var inputHash = Hash(message);
        var version = GetVersionInfo();
        var approvedTools = GetApprovedTools();

        if (!TryGetAgentApiConfig(out var apiConfig, out var apiConfigError))
        {
            return BuildResponse(
              conversationId,
              AssistantStatus.Escalated,
              AssistantIntent.Unknown,
              0,
              "AI provider is not configured. Set Agent endpoint, deployment, and API key to enable the assistant.",
              new Dictionary<string, string?>(),
              [],
              [],
              [],
              [],
              [],
              true,
              apiConfigError,
              null,
              new AssistantPolicyResult(false, true, true, "api-key", apiConfigError),
              version,
              startedAt,
              request.History?.Count ?? 0,
              inputHash,
              "escalated"
            );
        }

        var contextSnapshot = await BuildContextSnapshotAsync(request.OrderId, approvedTools, ct);
        ModelOutput modelOutput;
        try
        {
            modelOutput = await GetModelDecisionAsync(effectiveRequest, contextSnapshot, apiConfig, ct);
        }
        catch (Exception ex)
        {
            return BuildResponse(
              conversationId,
              AssistantStatus.Escalated,
              AssistantIntent.Unknown,
              0,
              "AI provider call failed. Please retry or escalate to support.",
              new Dictionary<string, string?>(),
              [],
              [],
              [],
              [],
              [],
              true,
              TrimForAudit(ex.Message),
              null,
              new AssistantPolicyResult(false, true, true, "api-key", TrimForAudit(ex.Message)),
              version,
              startedAt,
              request.History?.Count ?? 0,
              inputHash,
              "escalated"
            );
        }

        var modelToolCalls = modelOutput.ToolCalls ?? [];
        if (!modelToolCalls.Any(t => string.Equals(t.Name, "place_order", StringComparison.OrdinalIgnoreCase))
            && ParseIntent(modelOutput.Intent) == AssistantIntent.SubmitOrder
            && modelOutput.Policy?.PromptInjectionDetected != true
            && modelOutput.ReadyForSubmission
            && (modelOutput.MissingFields?.Count ?? 0) == 0)
        {
            modelToolCalls.Add(new ModelToolCall
            {
                Name = "place_order",
                Reason = "The model classified the request as ready for submission with no unresolved fields.",
                Arguments = new Dictionary<string, string?>(modelOutput.Entities ?? new Dictionary<string, string?>(), StringComparer.OrdinalIgnoreCase)
            });
        }
        var acceptedToolCalls = modelToolCalls
          .Where(t => approvedTools.Contains(t.Name, StringComparer.OrdinalIgnoreCase))
          .Select(t => new AssistantToolCall(t.Name, t.Reason ?? "Model-requested tool call", new Dictionary<string, string?>(t.Arguments, StringComparer.OrdinalIgnoreCase)))
          .ToList();

        var toolResults = await ExecuteToolCallsAsync(acceptedToolCalls, ct);

        var grounding = new List<AssistantGrounding>();
        if (modelOutput.Grounding is not null)
        {
            grounding.AddRange(modelOutput.Grounding
              .Where(g => !string.IsNullOrWhiteSpace(g.Source) && !string.IsNullOrWhiteSpace(g.Identifier))
              .Select(g => new AssistantGrounding(g.Source, g.Identifier, g.Evidence ?? string.Empty)));
        }
        grounding.AddRange(toolResults.Grounding);

        var entities = new Dictionary<string, string?>(effectiveRequest.ContextEntities ?? new Dictionary<string, string?>(), StringComparer.OrdinalIgnoreCase);
        foreach (var entity in modelOutput.Entities ?? new Dictionary<string, string?>())
        {
            if (string.IsNullOrWhiteSpace(entity.Value)) entities.Remove(entity.Key);
            else entities[entity.Key] = entity.Value;
        }
        foreach (var call in acceptedToolCalls.Where(c => string.Equals(c.Name, "place_order", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var arg in call.Arguments)
            {
                if (!string.IsNullOrWhiteSpace(arg.Value))
                {
                    entities[arg.Key] = arg.Value;
                }
            }
        }
        var missingFields = (modelOutput.MissingFields ?? [])
          .Where(x => !string.IsNullOrWhiteSpace(x.Field))
          .Select(x => new AssistantMissingField(
            x.Field,
            x.WhyRequired ?? "Required for completing this request.",
            x.ClarificationPrompt ?? "Please provide this value."
          )).ToList();

        var clarifications = (modelOutput.ClarificationQuestions ?? [])
          .Where(x => !string.IsNullOrWhiteSpace(x))
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .ToList();

        var intent = ParseIntent(modelOutput.Intent);
        var status = ParseStatus(modelOutput.Status);
        var confidence = Math.Clamp(modelOutput.Confidence ?? 0.5, 0, 1);

        var usedApprovedToolsOnly = modelToolCalls.Count == acceptedToolCalls.Count;
        var blockedReason = usedApprovedToolsOnly ? null : "One or more unapproved tools were requested by the model and ignored.";
        var policy = new AssistantPolicyResult(
          modelOutput.Policy?.PromptInjectionDetected ?? false,
          usedApprovedToolsOnly,
          true,
          modelOutput.Policy?.AuthenticationMode ?? "api-key",
          modelOutput.Policy?.BlockReason ?? blockedReason
        );

        var reply = string.IsNullOrWhiteSpace(modelOutput.Reply)
          ? "I can help with CPP ordering. Please share your request."
          : modelOutput.Reply;

        var escalate = modelOutput.Escalate || status == AssistantStatus.Escalated;
        var escalationReason = modelOutput.EscalationReason;
        var unsupportedReason = modelOutput.UnsupportedReason;

        if (intent == AssistantIntent.SubmitOrder
          && !acceptedToolCalls.Any(c => string.Equals(c.Name, "place_order", StringComparison.OrdinalIgnoreCase)))
        {
            var inferredArguments = new Dictionary<string, string?>(entities, StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(request.OrderId) && !inferredArguments.ContainsKey("orderId"))
            {
                inferredArguments["orderId"] = request.OrderId;
            }

            acceptedToolCalls.Add(new AssistantToolCall(
              "place_order",
              "Synthesized from SubmitOrder intent",
              inferredArguments
            ));
        }

        var placement = await TryPlaceOrderAsync(modelOutput, entities, acceptedToolCalls, ct);
        if (placement.Executed)
        {
            if (placement.Success)
            {
                status = AssistantStatus.Complete;
                intent = AssistantIntent.SubmitOrder;
                confidence = Math.Max(confidence, 0.9);
                reply = placement.Reply ?? reply;
                escalate = false;
                escalationReason = null;
                unsupportedReason = null;
                missingFields = [];
                clarifications = [];
                if (placement.Grounding.Count > 0) grounding.AddRange(placement.Grounding);
            }
            else
            {
                status = placement.StatusOverride ?? AssistantStatus.NeedsClarification;
                reply = placement.Reply ?? reply;
                escalate = status == AssistantStatus.Escalated;
                escalationReason = placement.ErrorMessage ?? escalationReason;
                if (placement.MissingFields.Count > 0)
                {
                    missingFields = placement.MissingFields;
                    clarifications = placement.MissingFields.Select(x => x.ClarificationPrompt).ToList();
                }
            }
        }

        if (!placement.Success && intent == AssistantIntent.SubmitOrder && status == AssistantStatus.Complete)
        {
            status = AssistantStatus.NeedsClarification;
            if (string.IsNullOrWhiteSpace(reply) || reply.Contains("submitted", StringComparison.OrdinalIgnoreCase) || reply.Contains("placing", StringComparison.OrdinalIgnoreCase))
            {
                reply = "I can submit the order after I have all required details and confirmation. Please confirm product, quantity, account, and delivery location.";
            }
            if (clarifications.Count == 0)
            {
                clarifications = ["Please confirm product, quantity, account, and delivery location so I can submit."];
            }
        }

        await TraceAsync(conversationId, intent, status, reply, ct);

        if (placement.Success && intent == AssistantIntent.SubmitOrder)
        {
            memory.Remove(stateKey);
        }
        else
        {
            memory.Set(
              stateKey,
              new Dictionary<string, string?>(entities, StringComparer.OrdinalIgnoreCase),
              TimeSpan.FromHours(2)
            );
        }

        return BuildResponse(
          conversationId,
          status,
          intent,
          confidence,
          reply,
          entities,
          missingFields,
          clarifications,
          acceptedToolCalls,
          grounding,
          toolResults.Products,
          escalate,
          escalationReason,
          unsupportedReason,
          policy,
          version,
          startedAt,
          request.History?.Count ?? 0,
          inputHash,
          status.ToString().ToLowerInvariant()
        );
    }

    public async Task<AssistantHealthResponse> CheckHealthAsync(CancellationToken ct)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var version = GetVersionInfo();

        if (!TryGetAgentApiConfig(out var apiConfig, out var configError))
        {
            return new AssistantHealthResponse(
              "degraded",
              checkedAt,
              "azure-openai-compatible",
              version.ModelVersion,
              version.PromptVersion,
              version.InstructionVersion,
              false,
              false,
              false,
              configError
            );
        }

        try
        {
            var probeRequest = new AssistantRequest(
              "Health probe: respond with valid schema output only.",
              Guid.NewGuid().ToString("N"),
              null,
              []
            );
            var approvedTools = GetApprovedTools();
            var contextSnapshot = await BuildContextSnapshotAsync(null, approvedTools, ct);
            _ = await GetModelDecisionAsync(probeRequest, contextSnapshot, apiConfig, ct);

            return new AssistantHealthResponse(
              "ok",
              checkedAt,
              "azure-openai-compatible",
              version.ModelVersion,
              version.PromptVersion,
              version.InstructionVersion,
              true,
              true,
              true,
              "Connectivity and schema compatibility checks passed."
            );
        }
        catch (Exception ex)
        {
            return new AssistantHealthResponse(
              "degraded",
              checkedAt,
              "azure-openai-compatible",
              version.ModelVersion,
              version.PromptVersion,
              version.InstructionVersion,
              true,
              false,
              false,
              TrimForAudit(ex.Message)
            );
        }
    }

    bool TryGetAgentApiConfig(out AgentApiConfig cfg, out string? error)
    {
        var endpoint = ResolveConfigValue("Agent:Endpoint", "AGENT__ENDPOINT", "AGENT_ENDPOINT", "AZURE_OPENAI_ENDPOINT")?.Trim();
        var deployment = ResolveConfigValue("Agent:Deployment", "AGENT__DEPLOYMENT", "AGENT_DEPLOYMENT", "AZURE_OPENAI_DEPLOYMENT", "AZURE_OPENAI_CHAT_DEPLOYMENT")?.Trim();
        var apiVersion = ResolveConfigValue("Agent:ApiVersion", "AGENT__APIVERSION", "AGENT_APIVERSION", "AZURE_OPENAI_API_VERSION")?.Trim() ?? "2024-10-21";
        var apiKey = ResolveConfigValue("Agent:ApiKey", "AGENT__APIKEY", "AGENT_API_KEY", "AZURE_OPENAI_API_KEY", "OPENAI_API_KEY")?.Trim();

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment) || string.IsNullOrWhiteSpace(apiKey))
        {
            cfg = default!;
            error = "Missing Agent endpoint/deployment/apiKey configuration.";
            return false;
        }

        cfg = new AgentApiConfig(endpoint.TrimEnd('/'), deployment, apiVersion, apiKey);
        error = null;
        return true;
    }

    string? ResolveConfigValue(string key, params string[] envFallbacks)
    {
        var direct = config[key];
        if (!string.IsNullOrWhiteSpace(direct)) return direct;
        foreach (var envKey in envFallbacks)
        {
            var value = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }

    async Task<string> BuildContextSnapshotAsync(string? orderId, IReadOnlyList<string> approvedTools, CancellationToken ct)
    {
        var accounts = await db.Accounts
          .AsNoTracking()
          .OrderBy(x => x.Name)
          .Select(x => new { x.Id, x.AccountNumber, x.Name, x.RequiresCustomerPo })
          .Take(6)
          .ToListAsync(ct);

        var products = await db.Products
          .AsNoTracking()
          .OrderBy(x => x.Name)
          .Select(x => new { x.Id, x.ItemNumber, x.Name, x.ActiveIngredients, x.Orderable, x.StoplightStatus })
          .Take(12)
          .ToListAsync(ct);

        var deliveryLocations = await db.DeliverToLocations
          .AsNoTracking()
          .OrderBy(x => x.ShipToAccountId)
          .ThenByDescending(x => x.IsDefault)
          .ThenBy(x => x.Name)
          .Select(x => new { x.Id, x.ShipToAccountId, x.Name, x.City, x.IsDefault })
          .ToListAsync(ct);

        object? order = null;
        if (!string.IsNullOrWhiteSpace(orderId))
        {
            order = await db.Orders
              .AsNoTracking()
              .Where(x => x.Id == orderId)
              .Select(x => new
              {
                  x.Id,
                  Status = x.Status.ToString(),
                  x.ShipToAccountId,
                  x.CustomerPo,
                  x.FreightOption,
                  x.ContactEmail,
                  LineCount = x.Lines.Count
              })
              .FirstOrDefaultAsync(ct);
        }

        var snapshot = new
        {
            approvedTools,
            accounts,
            products,
            deliveryLocations,
            order
        };

        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    async Task<ModelOutput> GetModelDecisionAsync(AssistantRequest request, string contextSnapshot, AgentApiConfig apiConfig, CancellationToken ct)
    {
        var endpoint = $"{apiConfig.Endpoint}/openai/deployments/{apiConfig.Deployment}/chat/completions?api-version={apiConfig.ApiVersion}";
        var temperature = config.GetValue<double?>("Agent:Temperature") ?? 0.2;
        var maxTokens = config.GetValue<int?>("Agent:MaxTokens") ?? 1200;

        var systemPrompt = config["Agent:SystemPrompt"] ??
          "You are a CPP ordering assistant. Interpret user intent from the entire conversation, including natural-language confirmations, corrections, and rejections. Always return valid JSON that matches the schema. Keep transaction control in application code. Never claim submission unless you emit place_order in the same response.";
        var instructionSet = config["Agent:InstructionSet"] ??
          "Maintain structured order state across the conversation. Merge contextEntities with facts and corrections from the latest message, return the complete state every turn, and use only canonical keys: productId, itemNumber, productName, quantity, shipToAccountId, shipToAccountName, deliverToId, deliverToName, customerPo, freightOption. Normalize values against contextSnapshot. Resolve pronouns, ordinal choices, abbreviations, partial names, and prior-result references from conversation context. Never discard established facts when the user supplies another field. Ask only for facts unresolved after the merge. Unit of measure comes from the catalog. Decide authorization semantically and emit place_order with the complete state when authorized.";

        var userPayload = new
        {
            message = request.Message,
            conversationId = request.ConversationId,
            orderId = request.OrderId,
            history = request.History,
            contextEntities = request.ContextEntities,
            contextSnapshot
        };

        async Task<(bool Ok, int StatusCode, string Raw)> SendModelRequestAsync(object payload)
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
            };
            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            requestMessage.Headers.Add("api-key", apiConfig.ApiKey);

            using var response = await httpClient.SendAsync(requestMessage, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            return (response.IsSuccessStatusCode, (int)response.StatusCode, raw);
        }

        var baseMessages = new object[] {
      new { role = "system", content = systemPrompt },
      new { role = "system", content = instructionSet },
      new { role = "user", content = JsonSerializer.Serialize(userPayload, JsonOptions) }
    };

        var strictSchemaBody = new
        {
            temperature,
            max_tokens = maxTokens,
            messages = baseMessages,
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "cpp_agent_response",
                    strict = true,
                    schema = ModelOutputJsonSchema.Instance
                }
            }
        };

        var result = await SendModelRequestAsync(strictSchemaBody);
        if (!result.Ok && result.StatusCode == 400 && result.Raw.Contains("Invalid schema for response_format", StringComparison.OrdinalIgnoreCase))
        {
            var jsonObjectBody = new
            {
                temperature,
                max_tokens = maxTokens,
                messages = new object[] {
          new { role = "system", content = systemPrompt },
          new { role = "system", content = instructionSet + " Return only a single valid JSON object with keys: status,intent,confidence,reply,entities,missingFields,clarificationQuestions,toolCalls,grounding,escalate,escalationReason,unsupportedReason,readyForSubmission,policy." },
          new { role = "user", content = JsonSerializer.Serialize(userPayload, JsonOptions) }
        },
                response_format = new { type = "json_object" }
            };
            result = await SendModelRequestAsync(jsonObjectBody);
        }

        if (!result.Ok)
        {
            throw new InvalidOperationException($"Agent AI request failed with {result.StatusCode}: {result.Raw}");
        }

        var content = ExtractAssistantContent(result.Raw);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Agent AI did not return structured content.");
        }

        return ParseModelOutput(content);
    }

    async Task<(List<Product> Products, List<AssistantGrounding> Grounding)> ExecuteToolCallsAsync(IReadOnlyList<AssistantToolCall> calls, CancellationToken ct)
    {
        var products = new List<Product>();
        var grounding = new List<AssistantGrounding>();

        foreach (var call in calls)
        {
            switch (call.Name)
            {
                case "search_products":
                    {
                        var query = call.Arguments.TryGetValue("query", out var q) ? (q ?? string.Empty).Trim() : string.Empty;
                        var data = await db.Products
                          .AsNoTracking()
                          .Where(p => string.IsNullOrWhiteSpace(query)
                            || p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                            || p.ItemNumber.Contains(query, StringComparison.OrdinalIgnoreCase)
                            || p.ActiveIngredients.Contains(query, StringComparison.OrdinalIgnoreCase))
                          .OrderBy(p => p.Name)
                          .Take(5)
                          .ToListAsync(ct);
                        products.AddRange(data);
                        foreach (var product in data)
                        {
                            grounding.Add(new("product", product.Id, $"{product.Name} ({product.ItemNumber})"));
                        }
                        break;
                    }
                case "get_accounts":
                    {
                        var query = call.Arguments.TryGetValue("query", out var q) ? (q ?? string.Empty).Trim() : string.Empty;
                        var accounts = await db.Accounts
                          .AsNoTracking()
                          .Where(a => string.IsNullOrWhiteSpace(query)
                            || a.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                            || a.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                            || a.AccountNumber.Contains(query, StringComparison.OrdinalIgnoreCase))
                          .OrderBy(a => a.Name)
                          .Take(3)
                          .ToListAsync(ct);
                        foreach (var account in accounts)
                        {
                            grounding.Add(new("account", account.Id, $"{account.Name} ({account.AccountNumber})"));
                        }
                        break;
                    }
                case "get_order_summary":
                    {
                        if (!call.Arguments.TryGetValue("orderId", out var orderId) || string.IsNullOrWhiteSpace(orderId)) break;
                        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct);
                        if (order is not null)
                        {
                            grounding.Add(new("order", order.Id, $"Status {order.Status}; {order.Lines.Count} lines."));
                        }
                        break;
                    }
                case "get_deliver_to_locations":
                    {
                        if (!call.Arguments.TryGetValue("shipToAccountId", out var shipTo) || string.IsNullOrWhiteSpace(shipTo)) break;
                        var count = await db.DeliverToLocations.AsNoTracking().CountAsync(x => x.ShipToAccountId == shipTo, ct);
                        grounding.Add(new("deliver-to", shipTo, $"{count} deliver-to locations available."));
                        break;
                    }
            }
        }

        return (products, grounding);
    }

    static AssistantIntent ParseIntent(string? value) => Enum.TryParse<AssistantIntent>(value ?? string.Empty, true, out var parsed)
      ? parsed
      : AssistantIntent.Unknown;

    static AssistantStatus ParseStatus(string? value) => Enum.TryParse<AssistantStatus>(value ?? string.Empty, true, out var parsed)
      ? parsed
      : AssistantStatus.NeedsClarification;

    async Task TraceAsync(string conversationId, AssistantIntent intent, AssistantStatus status, string detail, CancellationToken ct)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            OrderId = conversationId,
            EventType = "AssistantTrace",
            Detail = $"intent={intent};status={status};detail={TrimForAudit(detail)}"
        });
        await db.SaveChangesAsync(ct);
    }

    static string TrimForAudit(string value) => value.Length <= 250 ? value : value[..250];

    AssistantVersionInfo GetVersionInfo()
    {
        return new AssistantVersionInfo(
          config["Agent:ModelVersion"] ?? "model-version-not-set",
          config["Agent:PromptVersion"] ?? "prompt-version-not-set",
          config["Agent:InstructionVersion"] ?? "instruction-version-not-set"
        );
    }

    IReadOnlyList<string> GetApprovedTools()
    {
        return (config.GetSection("Agent:ApprovedTools").Get<string[]>() ?? [])
          .Where(x => !string.IsNullOrWhiteSpace(x))
          .Select(x => x.Trim())
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .ToArray();
    }

    static AssistantResponse BuildResponse(
      string conversationId,
      AssistantStatus status,
      AssistantIntent intent,
      double confidence,
      string reply,
      IReadOnlyDictionary<string, string?> entities,
      IReadOnlyList<AssistantMissingField> missing,
      IReadOnlyList<string> clarifications,
      IReadOnlyList<AssistantToolCall> toolCalls,
      IReadOnlyList<AssistantGrounding> grounding,
      IReadOnlyList<Product> products,
      bool escalate,
      string? escalationReason,
      string? unsupportedReason,
      AssistantPolicyResult policy,
      AssistantVersionInfo version,
      DateTimeOffset startedAt,
      int historyCount,
      string inputHash,
      string outcome
    )
    {
        return new AssistantResponse(
          conversationId,
          status,
          intent,
          Math.Round(confidence, 3),
          reply,
          entities,
          missing,
          clarifications,
          toolCalls,
          grounding,
          products,
          escalate,
          escalationReason,
          unsupportedReason,
          policy,
          version,
          new AssistantTrace(
            Guid.NewGuid().ToString("N"),
            startedAt,
            DateTimeOffset.UtcNow,
            historyCount,
            inputHash,
            outcome
          )
        );
    }

    static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    static string? ExtractAssistantContent(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
        {
            return null;
        }
        var message = choices[0].GetProperty("message");
        if (!message.TryGetProperty("content", out var contentElement))
        {
            return null;
        }

        if (contentElement.ValueKind == JsonValueKind.String)
        {
            return contentElement.GetString();
        }

        if (contentElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in contentElement.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.Object && part.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                {
                    return textEl.GetString();
                }
            }
        }

        return null;
    }

    static ModelOutput ParseModelOutput(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var output = new ModelOutput
        {
            Status = GetString(root, "status"),
            Intent = GetString(root, "intent"),
            Confidence = GetDouble(root, "confidence"),
            Reply = GetString(root, "reply"),
            Escalate = GetBool(root, "escalate"),
            EscalationReason = GetNullableString(root, "escalationReason"),
            UnsupportedReason = GetNullableString(root, "unsupportedReason"),
            ReadyForSubmission = GetBool(root, "readyForSubmission"),
            Entities = ParseEntities(root),
            MissingFields = ParseMissingFields(root),
            ClarificationQuestions = ParseClarificationQuestions(root),
            ToolCalls = ParseToolCalls(root),
            Grounding = ParseGrounding(root),
            Policy = ParsePolicy(root)
        };

        return output;
    }

    static Dictionary<string, string?> ParseEntities(JsonElement root)
    {
        var entities = new Dictionary<string, string?>();
        if (!root.TryGetProperty("entities", out var entityNode) || entityNode.ValueKind != JsonValueKind.Object)
        {
            return entities;
        }

        foreach (var prop in entityNode.EnumerateObject())
        {
            entities[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Null => null,
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => prop.Value.ToString(),
                _ => prop.Value.GetRawText()
            };
        }

        return entities;
    }

    static List<ModelMissingField> ParseMissingFields(JsonElement root)
    {
        var result = new List<ModelMissingField>();
        if (!root.TryGetProperty("missingFields", out var node) || node.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in node.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            result.Add(new ModelMissingField
            {
                Field = GetString(item, "field") ?? string.Empty,
                WhyRequired = GetNullableString(item, "whyRequired"),
                ClarificationPrompt = GetNullableString(item, "clarificationPrompt")
            });
        }

        return result;
    }

    static List<string> ParseClarificationQuestions(JsonElement root)
    {
        var result = new List<string>();
        if (!root.TryGetProperty("clarificationQuestions", out var node))
        {
            return result;
        }

        if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in node.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String) result.Add(item.GetString() ?? string.Empty);
            }
            return result;
        }

        if (node.ValueKind == JsonValueKind.String)
        {
            result.Add(node.GetString() ?? string.Empty);
        }

        return result;
    }

    static List<ModelToolCall> ParseToolCalls(JsonElement root)
    {
        var result = new List<ModelToolCall>();
        if (!root.TryGetProperty("toolCalls", out var node) || node.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in node.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            result.Add(new ModelToolCall
            {
                Name = GetString(item, "name") ?? string.Empty,
                Reason = GetNullableString(item, "reason"),
                Arguments = ParseStringDictionary(item, "arguments")
            });
        }

        return result;
    }

    async Task<OrderPlacementAttempt> TryPlaceOrderAsync(ModelOutput modelOutput, IReadOnlyDictionary<string, string?> entities, IReadOnlyList<AssistantToolCall> toolCalls, CancellationToken ct)
    {
        var toolRequested = toolCalls.Any(t => string.Equals(t.Name, "place_order", StringComparison.OrdinalIgnoreCase));
        if (!toolRequested)
        {
            return OrderPlacementAttempt.NotExecuted;
        }

        var missing = new List<AssistantMissingField>();

        var account = await ResolveAccountAsync(entities, ct);
        if (account is null)
        {
            missing.Add(new AssistantMissingField("shipToAccount", "Ship-To account is required for order submission.", "Which account should be used for this order?"));
        }

        var product = await ResolveProductAsync(entities, ct);
        if (product is null)
        {
            missing.Add(new AssistantMissingField("product", "A product SKU is required for order submission.", "Which product should be ordered?"));
        }

        var quantity = ResolveQuantity(entities);
        if (quantity is null || quantity.Value <= 0)
        {
            missing.Add(new AssistantMissingField("quantity", "A valid quantity is required for order submission.", "How many units should be ordered?"));
        }

        DeliverToLocation? deliverTo = null;
        if (account is not null)
        {
            deliverTo = await ResolveDeliverToAsync(account.Id, entities, ct);
            if (deliverTo is null)
            {
                missing.Add(new AssistantMissingField("deliverTo", "Delivery location is required.", "Which delivery location should be used?"));
            }
        }

        if (missing.Count > 0)
        {
            return new OrderPlacementAttempt(
              true,
              false,
              AssistantStatus.NeedsClarification,
              "I have enough context to place the order once missing fields are provided.",
              null,
              missing,
              []
            );
        }

        try
        {
            var requestedDate = DateTime.UtcNow.Date.AddDays(7);
            var line = new OrderLineDto(product!.Id, quantity!.Value, product.Uom, product.Price, requestedDate, "ASC");
            var customerPo = FirstNonEmpty(entities, "customerPo", "poNumber", "po");
            var freight = FirstNonEmpty(entities, "freightOption") ?? "Standard";

            var request = new OrderRequest(
              null,
              account!.Id,
              deliverTo!.Id,
              customerPo,
              account.ContactEmail,
              account.ShippingInstructions,
              false,
              freight,
              requestedDate,
              [line],
              false,
              null
            );

            var products = new Dictionary<string, Product> { [product.Id] = product };
            var validation = rules.Validate(request, account, products);
            var errors = validation.Where(v => string.Equals(v.Severity, "error", StringComparison.OrdinalIgnoreCase)).ToList();
            if (errors.Count > 0)
            {
                var validationMissing = errors
                  .Select(v => new AssistantMissingField(v.Field ?? "order", v.Message, v.SuggestedResolution ?? v.Message))
                  .ToList();
                return new OrderPlacementAttempt(
                  true,
                  false,
                  AssistantStatus.NeedsClarification,
                  "Order details need corrections before submission.",
                  null,
                  validationMissing,
                  []
                );
            }

            var order = new Order
            {
                ShipToAccountId = request.ShipToAccountId,
                DeliverToAccountId = request.DeliverToAccountId,
                DeliverToAnotherLocation = request.DeliverToAnotherLocation,
                AlternateDelivery = request.AlternateDelivery,
                CustomerPo = request.CustomerPo,
                ContactEmail = request.ContactEmail,
                ShippingInstructions = request.ShippingInstructions,
                CustomerPickup = request.CustomerPickup,
                FreightOption = request.FreightOption,
                RequestedArrivalDate = request.RequestedArrivalDate,
                Lines = request.Lines,
                SoldToName = account.SoldToName,
                UpdatedAt = DateTime.UtcNow,
                Status = OrderState.Draft
            };

            db.Orders.Add(order);
            db.AuditEvents.Add(new AuditEvent { OrderId = order.Id, EventType = "AssistantDraftCreated", Detail = "Draft created by agent" });
            await db.SaveChangesAsync(ct);

            order.Status = OrderState.Submitting;
            await db.SaveChangesAsync(ct);

            var submission = await fulfillment.SubmitOrderAsync(order, ct);
            order.Status = OrderState.Submitted;
            order.WebOrderNumber = submission.WebOrderNumber;
            order.SubmittedAt = DateTime.UtcNow;
            order.FulfillmentOrdersJson = JsonSerializer.Serialize(submission.FulfillmentOrderNumbers);
            db.AuditEvents.Add(new AuditEvent { OrderId = order.Id, EventType = "AssistantSubmissionSucceeded", Detail = submission.WebOrderNumber });
            await db.SaveChangesAsync(ct);

            return new OrderPlacementAttempt(
              true,
              true,
              AssistantStatus.Complete,
              $"Order submitted successfully. Web order number: {submission.WebOrderNumber}.",
              null,
              [],
              [
                new AssistantGrounding("order", order.Id, $"Submitted with web order {submission.WebOrderNumber}"),
          new AssistantGrounding("product", product.Id, $"{product.Name} ({product.ItemNumber}) x {quantity.Value}"),
          new AssistantGrounding("account", account.Id, account.Name)
              ]
            );
        }
        catch (Exception ex)
        {
            return new OrderPlacementAttempt(
              true,
              false,
              AssistantStatus.Escalated,
              "Order placement failed in the application workflow.",
              TrimForAudit(ex.Message),
              [],
              []
            );
        }
    }

    async Task<OrderPlacementAttempt> TrySubmitExistingOrderAsync(string orderId, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null)
        {
            return new OrderPlacementAttempt(
              true,
              false,
              AssistantStatus.Escalated,
              "I could not find that order to submit.",
              "Order not found.",
              [],
              []
            );
        }

        if (order.Status == OrderState.Submitted)
        {
            return new OrderPlacementAttempt(
              true,
              true,
              AssistantStatus.Complete,
              $"That order is already submitted. Web order number: {order.WebOrderNumber}.",
              null,
              [],
              [new AssistantGrounding("order", order.Id, $"Already submitted with web order {order.WebOrderNumber}")]
            );
        }

        var account = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == order.ShipToAccountId, ct);
        var productIds = order.Lines.Select(x => x.ProductId).Distinct().ToArray();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var request = new OrderRequest(
          order.Id,
          order.ShipToAccountId,
          order.DeliverToAccountId,
          order.CustomerPo,
          order.ContactEmail,
          order.ShippingInstructions,
          order.CustomerPickup,
          order.FreightOption,
          order.RequestedArrivalDate,
          order.Lines,
          order.DeliverToAnotherLocation,
          order.AlternateDelivery
        );

        var validation = rules.Validate(request, account, products);
        var errors = validation.Where(v => string.Equals(v.Severity, "error", StringComparison.OrdinalIgnoreCase)).ToList();
        if (errors.Count > 0)
        {
            return new OrderPlacementAttempt(
              true,
              false,
              AssistantStatus.NeedsClarification,
              "I need a few corrections before I can submit this order.",
              null,
              errors.Select(v => new AssistantMissingField(v.Field ?? "order", v.Message, v.SuggestedResolution ?? v.Message)).ToList(),
              []
            );
        }

        try
        {
            order.Status = OrderState.Submitting;
            order.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var submission = await fulfillment.SubmitOrderAsync(order, ct);
            order.Status = OrderState.Submitted;
            order.WebOrderNumber = submission.WebOrderNumber;
            order.SubmittedAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;
            order.FulfillmentOrdersJson = JsonSerializer.Serialize(submission.FulfillmentOrderNumbers);
            db.AuditEvents.Add(new AuditEvent { OrderId = order.Id, EventType = "AssistantSubmissionSucceeded", Detail = submission.WebOrderNumber });
            await db.SaveChangesAsync(ct);

            return new OrderPlacementAttempt(
              true,
              true,
              AssistantStatus.Complete,
              $"Order submitted successfully. Web order number: {submission.WebOrderNumber}.",
              null,
              [],
              [new AssistantGrounding("order", order.Id, $"Submitted with web order {submission.WebOrderNumber}")]
            );
        }
        catch (Exception ex)
        {
            order.Status = OrderState.SubmissionFailed;
            order.UpdatedAt = DateTime.UtcNow;
            db.AuditEvents.Add(new AuditEvent { OrderId = order.Id, EventType = "AssistantSubmissionFailed", Detail = ex.Message });
            await db.SaveChangesAsync(ct);

            return new OrderPlacementAttempt(
              true,
              false,
              AssistantStatus.Escalated,
              "Order placement failed in the application workflow.",
              TrimForAudit(ex.Message),
              [],
              []
            );
        }
    }

    async Task<Account?> ResolveAccountAsync(IReadOnlyDictionary<string, string?> entities, CancellationToken ct)
    {
        var accountId = FirstNonEmpty(entities, "shipToAccountId", "accountId");
        if (!string.IsNullOrWhiteSpace(accountId))
        {
            var exact = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, ct);
            if (exact is not null) return exact;
        }

        var accountName = FirstNonEmpty(entities, "shipToAccountName", "accountName", "account", "shipTo", "location", "city", "deliveryLocation");
        if (!string.IsNullOrWhiteSpace(accountName))
        {
            var normalized = accountName.Trim();
            var pattern = $"%{EscapeLikePattern(normalized)}%";
            return await db.Accounts.AsNoTracking()
              .OrderBy(a => a.Name)
              .FirstOrDefaultAsync(a => EF.Functions.Like(a.Name, pattern, "\\") || EF.Functions.Like(a.Id, pattern, "\\"), ct);
        }

        return null;
    }

    async Task<Product?> ResolveProductAsync(IReadOnlyDictionary<string, string?> entities, CancellationToken ct)
    {
        var productId = FirstNonEmpty(entities, "productId");
        if (!string.IsNullOrWhiteSpace(productId))
        {
            var exact = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, ct);
            if (exact is not null) return exact;
        }

        var itemNumber = FirstNonEmpty(entities, "itemNumber", "sku");
        if (!string.IsNullOrWhiteSpace(itemNumber))
        {
            var byItem = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ItemNumber == itemNumber, ct);
            if (byItem is not null) return byItem;
        }

        var productName = FirstNonEmpty(entities, "productName", "product");
        if (!string.IsNullOrWhiteSpace(productName))
        {
            var pattern = $"%{EscapeLikePattern(productName.Trim())}%";
            return await db.Products.AsNoTracking()
              .OrderBy(p => p.Name)
              .FirstOrDefaultAsync(p => EF.Functions.Like(p.Name, pattern, "\\"), ct);
        }

        return null;
    }

    async Task<DeliverToLocation?> ResolveDeliverToAsync(string shipToAccountId, IReadOnlyDictionary<string, string?> entities, CancellationToken ct)
    {
        var deliverToId = FirstNonEmpty(entities, "deliverToAccountId", "deliverToId");
        if (!string.IsNullOrWhiteSpace(deliverToId))
        {
            var exact = await db.DeliverToLocations.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deliverToId && d.ShipToAccountId == shipToAccountId, ct);
            if (exact is not null) return exact;
        }

        var deliverToName = FirstNonEmpty(entities, "deliverToName", "deliveryLocation", "deliverTo", "facility", "locationName");
        if (!string.IsNullOrWhiteSpace(deliverToName))
        {
            var pattern = $"%{EscapeLikePattern(deliverToName.Trim())}%";
            var byName = await db.DeliverToLocations.AsNoTracking()
              .Where(d => d.ShipToAccountId == shipToAccountId)
              .OrderBy(d => d.Name)
              .FirstOrDefaultAsync(d => EF.Functions.Like(d.Name, pattern, "\\"), ct);
            if (byName is not null) return byName;
        }

        return await db.DeliverToLocations.AsNoTracking()
          .Where(d => d.ShipToAccountId == shipToAccountId)
          .OrderByDescending(d => d.IsDefault)
          .ThenBy(d => d.Name)
          .FirstOrDefaultAsync(ct);
    }

    static int? ResolveQuantity(IReadOnlyDictionary<string, string?> entities)
    {
        var raw = FirstNonEmpty(entities, "quantity", "units", "qty");
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return int.TryParse(raw, out var q) ? q : null;
    }

    static string EscapeLikePattern(string value) => value
      .Replace("\\", "\\\\", StringComparison.Ordinal)
      .Replace("%", "\\%", StringComparison.Ordinal)
      .Replace("_", "\\_", StringComparison.Ordinal);

    static string? FirstNonEmpty(IReadOnlyDictionary<string, string?> entities, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (entities.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }

    static List<ModelGrounding> ParseGrounding(JsonElement root)
    {
        var result = new List<ModelGrounding>();
        if (!root.TryGetProperty("grounding", out var node))
        {
            return result;
        }

        if (node.ValueKind == JsonValueKind.Object)
        {
            result.Add(new ModelGrounding
            {
                Source = GetString(node, "source") ?? string.Empty,
                Identifier = GetString(node, "identifier") ?? string.Empty,
                Evidence = GetNullableString(node, "evidence")
            });
            return result;
        }

        if (node.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in node.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            result.Add(new ModelGrounding
            {
                Source = GetString(item, "source") ?? string.Empty,
                Identifier = GetString(item, "identifier") ?? string.Empty,
                Evidence = GetNullableString(item, "evidence")
            });
        }

        return result;
    }

    static ModelPolicy? ParsePolicy(JsonElement root)
    {
        if (!root.TryGetProperty("policy", out var node) || node.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new ModelPolicy
        {
            PromptInjectionDetected = GetBool(node, "promptInjectionDetected"),
            AuthenticationMode = GetNullableString(node, "authenticationMode"),
            BlockReason = GetNullableString(node, "blockReason")
        };
    }

    static string? GetString(JsonElement node, string propertyName)
    {
        if (!node.TryGetProperty(propertyName, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
            _ => null
        };
    }

    static string? GetNullableString(JsonElement node, string propertyName)
    {
        if (!node.TryGetProperty(propertyName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Null) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    static Dictionary<string, string?> ParseStringDictionary(JsonElement node, string propertyName)
    {
        var result = new Dictionary<string, string?>();
        if (!node.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var prop in value.EnumerateObject())
        {
            result[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Null => null,
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => prop.Value.ToString(),
                _ => prop.Value.GetRawText()
            };
        }

        return result;
    }

    static double? GetDouble(JsonElement node, string propertyName)
    {
        if (!node.TryGetProperty(propertyName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d)) return d;
        if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out var s)) return s;
        return null;
    }

    static bool GetBool(JsonElement node, string propertyName)
    {
        if (!node.TryGetProperty(propertyName, out var value)) return false;
        if (value.ValueKind == JsonValueKind.True) return true;
        if (value.ValueKind == JsonValueKind.False) return false;
        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed)) return parsed;
        return false;
    }

    readonly record struct AgentApiConfig(string Endpoint, string Deployment, string ApiVersion, string ApiKey);

    sealed class ModelOutput
    {
        public string? Status { get; set; }
        public string? Intent { get; set; }
        public double? Confidence { get; set; }
        public string? Reply { get; set; }
        public Dictionary<string, string?>? Entities { get; set; }
        public List<ModelMissingField>? MissingFields { get; set; }
        public List<string>? ClarificationQuestions { get; set; }
        public List<ModelToolCall>? ToolCalls { get; set; }
        public List<ModelGrounding>? Grounding { get; set; }
        public bool Escalate { get; set; }
        public string? EscalationReason { get; set; }
        public string? UnsupportedReason { get; set; }
        public bool ReadyForSubmission { get; set; }
        public ModelPolicy? Policy { get; set; }
    }

    readonly record struct OrderPlacementAttempt(
      bool Executed,
      bool Success,
      AssistantStatus? StatusOverride,
      string? Reply,
      string? ErrorMessage,
      List<AssistantMissingField> MissingFields,
      List<AssistantGrounding> Grounding
    )
    {
        public static readonly OrderPlacementAttempt NotExecuted = new(false, false, null, null, null, [], []);
    }

    sealed class ModelMissingField
    {
        public string Field { get; set; } = string.Empty;
        public string? WhyRequired { get; set; }
        public string? ClarificationPrompt { get; set; }
    }

    sealed class ModelToolCall
    {
        public string Name { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public Dictionary<string, string?> Arguments { get; set; } = new();
    }

    sealed class ModelGrounding
    {
        public string Source { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public string? Evidence { get; set; }
    }

    sealed class ModelPolicy
    {
        public bool PromptInjectionDetected { get; set; }
        public string? AuthenticationMode { get; set; }
        public string? BlockReason { get; set; }
    }

    static class ModelOutputJsonSchema
    {
        public static readonly object Instance = new
        {
            type = "object",
            additionalProperties = false,
            required = new[] {
        "status","intent","confidence","reply","entities","missingFields","clarificationQuestions","toolCalls","grounding","escalate","escalationReason","unsupportedReason","readyForSubmission","policy"
      },
            properties = new
            {
                status = new { type = "string", @enum = new[] { "Complete", "NeedsClarification", "Unsupported", "Blocked", "Escalated" } },
                intent = new { type = "string", @enum = new[] { "Greeting", "CreateOrder", "FindProduct", "UpdateOrderLine", "UpdateShipping", "ReviewDraft", "SubmitOrder", "Unsupported", "Unknown" } },
                confidence = new { type = "number", minimum = 0, maximum = 1 },
                reply = new { type = "string" },
                entities = new
                {
                    type = "object",
                    additionalProperties = new { type = new[] { "string", "null" } }
                },
                missingFields = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        required = new[] { "field", "whyRequired", "clarificationPrompt" },
                        properties = new
                        {
                            field = new { type = "string" },
                            whyRequired = new { type = "string" },
                            clarificationPrompt = new { type = "string" }
                        }
                    }
                },
                clarificationQuestions = new
                {
                    type = "array",
                    items = new { type = "string" }
                },
                toolCalls = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        required = new[] { "name", "reason", "arguments" },
                        properties = new
                        {
                            name = new { type = "string" },
                            reason = new { type = "string" },
                            arguments = new
                            {
                                type = "object",
                                additionalProperties = new { type = new[] { "string", "null" } }
                            }
                        }
                    }
                },
                grounding = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        required = new[] { "source", "identifier", "evidence" },
                        properties = new
                        {
                            source = new { type = "string" },
                            identifier = new { type = "string" },
                            evidence = new { type = "string" }
                        }
                    }
                },
                escalate = new { type = "boolean" },
                escalationReason = new { type = new[] { "string", "null" } },
                unsupportedReason = new { type = new[] { "string", "null" } },
                readyForSubmission = new { type = "boolean" },
                policy = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "promptInjectionDetected", "authenticationMode", "blockReason" },
                    properties = new
                    {
                        promptInjectionDetected = new { type = "boolean" },
                        authenticationMode = new { type = "string" },
                        blockReason = new { type = new[] { "string", "null" } }
                    }
                }
            }
        };
    }
}
