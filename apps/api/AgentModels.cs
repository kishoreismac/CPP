namespace Cpp.Api;

public enum AssistantIntent {
  Greeting,
  CreateOrder,
  FindProduct,
  UpdateOrderLine,
  UpdateShipping,
  ReviewDraft,
  SubmitOrder,
  Unsupported,
  Unknown
}

public enum AssistantStatus {
  Complete,
  NeedsClarification,
  Unsupported,
  Blocked,
  Escalated
}

public record AssistantChatMessage(string Role, string Text);

public record AssistantRequest(
  string Message,
  string? ConversationId,
  string? OrderId,
  List<AssistantChatMessage>? History = null,
  Dictionary<string, string?>? ContextEntities = null
);

public record AssistantMissingField(string Field, string WhyRequired, string ClarificationPrompt);

public record AssistantToolCall(string Name, string Reason, IReadOnlyDictionary<string, string?> Arguments);

public record AssistantGrounding(string Source, string Identifier, string Evidence);

public record AssistantPolicyResult(
  bool PromptInjectionDetected,
  bool UsedApprovedToolsOnly,
  bool TransactionControlKeptInApplication,
  string AuthenticationMode,
  string? BlockReason
);

public record AssistantVersionInfo(string ModelVersion, string PromptVersion, string InstructionVersion);

public record AssistantTrace(
  string TraceId,
  DateTimeOffset StartedAtUtc,
  DateTimeOffset CompletedAtUtc,
  int HistoryMessageCount,
  string InputHash,
  string Outcome
);

public record AssistantResponse(
  string ConversationId,
  AssistantStatus Status,
  AssistantIntent Intent,
  double Confidence,
  string Reply,
  IReadOnlyDictionary<string, string?> Entities,
  IReadOnlyList<AssistantMissingField> MissingFields,
  IReadOnlyList<string> ClarificationQuestions,
  IReadOnlyList<AssistantToolCall> ToolCalls,
  IReadOnlyList<AssistantGrounding> Grounding,
  IReadOnlyList<Product> Products,
  bool Escalate,
  string? EscalationReason,
  string? UnsupportedReason,
  AssistantPolicyResult Policy,
  AssistantVersionInfo Version,
  AssistantTrace Trace
);

public record AssistantHealthResponse(
  string Status,
  DateTimeOffset CheckedAtUtc,
  string Provider,
  string ModelVersion,
  string PromptVersion,
  string InstructionVersion,
  bool Configured,
  bool ConnectivityOk,
  bool SchemaCompatible,
  string? Detail
);
