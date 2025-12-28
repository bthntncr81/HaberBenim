export interface FeedItem {
  id: string;
  publishedAtUtc: string;
  sourceId: string;
  sourceName: string;
  title: string;
  summary: string | null;
  canonicalUrl: string | null;
  status: string;
  duplicateCount: number;
  // Decision fields
  decisionType: string | null;
  decidedByRuleId: string | null;
  decisionReason: string | null;
  decidedAtUtc: string | null;
  scheduledAtUtc: string | null;
  trustLevelSnapshot: number | null;
}

export interface FeedItemDetail {
  id: string;
  publishedAtUtc: string;
  ingestedAtUtc: string;
  sourceId: string;
  sourceName: string;
  title: string;
  summary: string | null;
  bodyText: string;
  originalText: string | null;
  canonicalUrl: string | null;
  language: string | null;
  status: string;
  duplicateCount: number;
  // Decision fields
  decisionType: string | null;
  decidedByRuleId: string | null;
  decisionReason: string | null;
  decidedAtUtc: string | null;
  scheduledAtUtc: string | null;
  trustLevelSnapshot: number | null;
  media: MediaItem[];
}

export interface MediaItem {
  id: string;
  mediaType: string;
  url: string;
  thumbUrl: string | null;
}

export interface FeedResponse {
  items: FeedItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface FeedQueryParams {
  fromUtc?: string;
  toUtc?: string;
  sourceId?: string;
  keyword?: string;
  status?: string;
  decisionType?: string;
  page?: number;
  pageSize?: number;
}

export interface Source {
  id: string;
  name: string;
  type: string;
  url: string | null;
  description: string | null;
  group: string | null;
  trustLevel: number;
  priority: number;
  isActive: boolean;
  createdAtUtc: string;
  lastFetchedAtUtc: string | null;
}

export interface IngestionResult {
  ok: boolean;
  sourcesProcessed: number;
  itemsInserted: number;
  duplicates: number;
  errors: string[] | null;
  byDecisionTypeCounts: { [key: string]: number } | null;
}
