export const API = import.meta.env.VITE_API_URL ?? "http://localhost:5090/api";
export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const r = await fetch(`${API}${path}`, {
    ...init,
    headers: { "Content-Type": "application/json", ...init?.headers },
  });
  if (!r.ok) {
    const e = await r.json().catch(() => ({ title: r.statusText }));
    throw new Error(e.detail ?? e.title ?? "Request failed");
  }
  return r.json();
}
export type Account = {
  id: string;
  accountNumber: string;
  name: string;
  address: string;
  city: string;
  state: string;
  postalCode: string;
  soldToName: string;
  contactEmail: string;
  shippingInstructions: string;
  requiresCustomerPo: boolean;
};
export type DeliverTo = {
  id: string;
  accountNumber: string;
  name: string;
  addressLine1: string;
  addressLine2: string;
  city: string;
  state: string;
  postalCode: string;
  contactName: string;
  contactPhone: string;
  isDefault: boolean;
  shipToAccountId: string;
};
export type AlternateDelivery = {
  locationName: string;
  addressLine1: string;
  addressLine2: string;
  city: string;
  state: string;
  postalCode: string;
  contactName: string;
  contactPhone: string;
};
export type Product = {
  id: string;
  itemNumber: string;
  name: string;
  supplier: string;
  category: string;
  productLine: string;
  activeIngredients: string;
  gtin: string;
  packageSize: string;
  uom: string;
  price: number;
  priceUom: string;
  leadTimeText: string;
  minimumQuantity: number;
  maximumQuantity: number;
  quantityIncrement: number;
  favorite: boolean;
  restrictions: string;
  orderable: boolean;
  availableInventory: number;
  stoplightStatus: string;
};
export type Line = {
  productId: string;
  quantity: number;
  uom: string;
  unitPrice: number;
  requestedArrivalDate: string;
  inventorySource: string;
};
export type Order = {
  id: string;
  webOrderNumber?: string;
  status: string;
  shipToAccountId: string;
  soldToName: string;
  deliverToAccountId?: string;
  deliverToAnotherLocation: boolean;
  alternateDelivery?: AlternateDelivery;
  customerPo?: string;
  contactEmail: string;
  shippingInstructions: string;
  customerPickup: boolean;
  freightOption: string;
  requestedArrivalDate: string;
  lines: Line[];
  account?: Account;
  createdAt: string;
  updatedAt: string;
};
export type Validation = {
  severity: "error" | "warning" | "information";
  code: string;
  message: string;
  field?: string;
  suggestedResolution?: string;
};

export type AssistantChatMessage = {
  role: "assistant" | "user";
  text: string;
};

export type AssistantRequest = {
  message: string;
  conversationId?: string;
  orderId?: string;
  history?: AssistantChatMessage[];
  contextEntities?: Record<string, string | null>;
};

export type AssistantMissingField = {
  field: string;
  whyRequired: string;
  clarificationPrompt: string;
};

export type AssistantToolCall = {
  name: string;
  reason: string;
  arguments: Record<string, string | null>;
};

export type AssistantGrounding = {
  source: string;
  identifier: string;
  evidence: string;
};

export type AssistantResponse = {
  conversationId: string;
  status: "Complete" | "NeedsClarification" | "Unsupported" | "Blocked" | "Escalated";
  intent: string;
  confidence: number;
  reply: string;
  entities: Record<string, string | null>;
  missingFields: AssistantMissingField[];
  clarificationQuestions: string[];
  toolCalls: AssistantToolCall[];
  grounding: AssistantGrounding[];
  products: Product[];
  escalate: boolean;
  escalationReason?: string;
  unsupportedReason?: string;
  policy: {
    promptInjectionDetected: boolean;
    usedApprovedToolsOnly: boolean;
    transactionControlKeptInApplication: boolean;
    authenticationMode: string;
    blockReason?: string;
  };
  version: {
    modelVersion: string;
    promptVersion: string;
    instructionVersion: string;
  };
  trace: {
    traceId: string;
    startedAtUtc: string;
    completedAtUtc: string;
    historyMessageCount: number;
    inputHash: string;
    outcome: string;
  };
};
