// Breaking News Models (Sprint 8)

export interface MarkBreakingRequest {
  note?: string;
  priority?: number;
  pushRequired?: boolean;
}

export interface MarkBreakingResponse {
  ok: boolean;
  versionNo: number;
  jobId: string | null;
  error: string | null;
}

export interface BreakingInboxItem {
  id: string;
  title: string;
  summary: string | null;
  sourceName: string;
  status: string;
  breakingAtUtc: string;
  breakingNote: string | null;
  breakingPriority: number;
  breakingPushRequired: boolean;
  breakingByUserId: string | null;
  publishedAtUtc: string;
  hasDraft: boolean;
}

export interface BreakingInboxResponse {
  items: BreakingInboxItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface BreakingInboxParams {
  status?: string;
  page?: number;
  pageSize?: number;
}

