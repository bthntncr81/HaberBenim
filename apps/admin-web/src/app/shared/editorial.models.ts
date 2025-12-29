// Editorial inbox item
export interface EditorialInboxItem {
  id: string;
  publishedAtUtc: string;
  sourceName: string;
  title: string;
  summary: string | null;
  status: string;
  decisionType: string | null;
  decidedAtUtc: string | null;
  scheduledAtUtc: string | null;
  hasDraft: boolean;
}

export interface EditorialInboxResponse {
  items: EditorialInboxItem[];
  total: number;
  page: number;
  pageSize: number;
}

// Editorial item detail
export interface EditorialItem {
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
  decisionType: string | null;
  decisionReason: string | null;
  decidedAtUtc: string | null;
  scheduledAtUtc: string | null;
  trustLevelSnapshot: number | null;
  editorialNote: string | null;
  rejectionReason: string | null;
  lastEditedByUserId: string | null;
  lastEditedAtUtc: string | null;
  draft: EditorialDraft | null;
  media: EditorialMedia[];
  revisions: EditorialRevision[];
}

export interface EditorialDraft {
  id: string;
  xText: string | null;
  webTitle: string | null;
  webBody: string | null;
  mobileSummary: string | null;
  pushTitle: string | null;
  pushBody: string | null;
  hashtagsCsv: string | null;
  mentionsCsv: string | null;
  updatedAtUtc: string;
  updatedByUserId: string | null;
  // Channel toggles (Sprint 6)
  publishToWeb: boolean;
  publishToMobile: boolean;
  publishToX: boolean;
  // Instagram (Sprint 11)
  publishToInstagram: boolean;
  instagramCaptionOverride: string | null;
}

export interface EditorialMedia {
  id: string;
  mediaType: string;
  url: string;
  thumbUrl: string | null;
}

export interface EditorialRevision {
  id: string;
  versionNo: number;
  actionType: string;
  createdAtUtc: string;
  createdByUserId: string | null;
}

// Request payloads
export interface SaveDraftRequest {
  xText?: string | null;
  webTitle?: string | null;
  webBody?: string | null;
  mobileSummary?: string | null;
  pushTitle?: string | null;
  pushBody?: string | null;
  hashtagsCsv?: string | null;
  mentionsCsv?: string | null;
  editorialNote?: string | null;
  // Channel toggles (Sprint 6)
  publishToWeb?: boolean;
  publishToMobile?: boolean;
  publishToX?: boolean;
  // Instagram (Sprint 11)
  publishToInstagram?: boolean;
  instagramCaptionOverride?: string | null;
}

export interface SaveDraftResponse {
  draft: EditorialDraft;
  latestVersionNo: number;
}

export interface RejectRequest {
  reason: string;
}

export interface ScheduleRequest {
  scheduledAtUtc: string;
}

// Inbox query params
export interface EditorialInboxParams {
  status?: string;
  fromUtc?: string;
  toUtc?: string;
  sourceId?: string;
  keyword?: string;
  page?: number;
  pageSize?: number;
}

// Status options for inbox filter
export const EditorialStatusOptions = [
  { value: 'PendingApproval', label: 'Pending Approval' },
  { value: 'ReadyToPublish', label: 'Ready to Publish' },
  { value: 'Rejected', label: 'Rejected' },
  { value: 'Scheduled', label: 'Scheduled' },
  { value: 'AutoReady', label: 'Auto Ready' },
  { value: 'Published', label: 'Published' }
];

// Correction request (Sprint 7)
export interface CorrectionRequest extends SaveDraftRequest {
  correctionNote?: string;
}

// Correction response
export interface CorrectionResponse {
  ok: boolean;
  versionNo: number;
  jobId: string | null;
  error: string | null;
}

