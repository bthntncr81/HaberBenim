// Media Asset DTO
export interface MediaAsset {
  id: string;
  kind: string;       // "Image" | "Video"
  origin: string;     // "X" | "RSS" | "OG" | "AI" | "Manual"
  sourceUrl: string | null;
  storagePath: string;
  contentType: string;
  sizeBytes: number;
  width: number;
  height: number;
  altText: string | null;
  isPrimary: boolean;
  sortOrder: number;
  publicUrl: string;
  createdAtUtc: string;
}

// Response from refresh-from-source
export interface MediaRefreshResponse {
  success: boolean;
  message: string;
  assetId: string | null;
  publicUrl: string | null;
}

// Response from ensure-primary
export interface MediaEnsureResponse {
  success: boolean;
  message: string;
  assetId: string | null;
  origin: string | null;
  publicUrl: string | null;
}

// Request for AI generation
export interface GenerateImageRequest {
  force?: boolean;
  promptOverride?: string;
  stylePreset?: string;
}

// Response from generate
export interface ImageGenerationResult {
  success: boolean;
  message: string;
  assetId: string | null;
  error: string | null;
  promptUsed: string | null;
  publicUrl: string | null;
  width: number | null;
  height: number | null;
}

// Origin display helpers
export const MediaOriginLabels: Record<string, string> = {
  'X': 'X/Twitter',
  'RSS': 'RSS Feed',
  'OG': 'OpenGraph',
  'AI': 'AI Üretildi',
  'Manual': 'Manuel'
};

export const MediaOriginColors: Record<string, string> = {
  'X': '#1da1f2',
  'RSS': '#ff6b35',
  'OG': '#6366f1',
  'AI': '#8b5cf6',
  'Manual': '#22c55e'
};

