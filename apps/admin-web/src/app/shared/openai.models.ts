// OpenAI Configuration Models

export interface OpenAiKeyStatusResponse {
  isConfigured: boolean;
  keyLast4: string | null;
  orgId: string | null;
  projectId: string | null;
  lastTestAtUtc: string | null;
  lastTestOk: boolean | null;
  lastError: string | null;
  sora2Available: boolean | null;
}

export interface SaveOpenAiKeyRequest {
  apiKey: string;
  orgId?: string | null;
  projectId?: string | null;
}

export interface SaveOpenAiKeyResponse {
  success: boolean;
  keyLast4: string | null;
  message: string | null;
}

export interface TestOpenAiResponse {
  success: boolean;
  sora2Available: boolean | null;
  modelCount: number | null;
  error: string | null;
}
