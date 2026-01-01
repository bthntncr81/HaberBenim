// Source Models - aligned with .NET API SourceModels.cs

export interface SourceListItem {
  id: string;
  name: string;
  type: string;
  identifier: string | null;
  url: string | null;
  category: string;
  trustLevel: number;
  priority: number;
  isActive: boolean;
  defaultBehavior: string;
  updatedAtUtc: string;
}

export interface SourceDetail extends SourceListItem {
  description: string | null;
  group: string | null;
  createdAtUtc: string;
  lastFetchedAtUtc: string | null;
  fetchIntervalMinutes: number;
  xState: XSourceState | null;
  // RSS Full-Text Enrichment (Sprint 12)
  fullTextFetchEnabled: boolean;
  fullTextExtractMode: string;
}

export interface XSourceState {
  id: string;
  xUserId: string | null;
  lastSinceId: string | null;
  lastPolledAtUtc: string | null;
  lastSuccessAtUtc: string | null;
  lastFailureAtUtc: string | null;
  lastError: string | null;
  consecutiveFailures: number;
}

export interface SourceListResponse {
  items: SourceListItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface UpsertSourceRequest {
  name: string;
  type: string;
  identifier?: string | null;
  url?: string | null;
  description?: string | null;
  category: string;
  trustLevel: number;
  priority: number;
  isActive: boolean;
  defaultBehavior: string;
  // RSS Full-Text Enrichment (Sprint 12)
  fullTextFetchEnabled: boolean;
  fullTextExtractMode: string;
}

export interface ToggleActiveRequest {
  isActive: boolean;
}

export interface SourceQueryParams {
  type?: string;
  category?: string;
  isActive?: boolean;
  q?: string;
  page?: number;
  pageSize?: number;
}

export const SourceTypes = [
  { value: 'RSS', label: 'RSS Feed' },
  { value: 'X', label: 'X (Twitter)' },
  { value: 'Manual', label: 'Manual Entry' },
  { value: 'GoogleNews', label: 'Google News' }
];

export const DefaultBehaviors = [
  { value: 'Auto', label: 'Auto (Rule-based)' },
  { value: 'Editorial', label: 'Editorial (Manual Review)' }
];

export const Categories = [
  'Gundem',
  'Spor',
  'Ekonomi',
  'Teknoloji',
  'Magazin',
  'Dunya',
  'Saglik',
  'Kultur'
];

// RSS Full-Text Enrichment extract modes (Sprint 12)
export const FullTextExtractModes = [
  { value: 'Auto', label: 'Auto (Try all methods)' },
  { value: 'JsonLd', label: 'JSON-LD Only' },
  { value: 'Readability', label: 'Readability Only' },
  { value: 'None', label: 'Disabled' }
];
