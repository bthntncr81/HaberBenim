// Publish job DTO
export interface PublishJob {
  id: string;
  contentItemId: string;
  contentTitle: string | null;
  scheduledAtUtc: string;
  status: string;
  attemptCount: number;
  lastAttemptAtUtc: string | null;
  nextRetryAtUtc: string | null;
  lastError: string | null;
  createdAtUtc: string;
}

export interface PublishJobListResponse {
  items: PublishJob[];
  total: number;
  page: number;
  pageSize: number;
}

// Channel publish log DTO
export interface ChannelPublishLog {
  id: string;
  channel: string;
  status: string;
  createdAtUtc: string;
  requestJson: string | null;
  responseJson: string | null;
  error: string | null;
  externalPostId: string | null; // For X: Tweet ID
}

// Enqueue response
export interface EnqueueResponse {
  success: boolean;
  alreadyQueued: boolean;
  jobId: string | null;
  scheduledAtUtc: string | null;
  message: string | null;
  error: string | null;
}

// Query params for jobs
export interface PublishJobQueryParams {
  status?: string;
  fromUtc?: string;
  toUtc?: string;
  page?: number;
  pageSize?: number;
}

// Job status options
export const JobStatusOptions = [
  { value: '', label: 'All Statuses' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Running', label: 'Running' },
  { value: 'Succeeded', label: 'Succeeded' },
  { value: 'Failed', label: 'Failed' },
  { value: 'Cancelled', label: 'Cancelled' }
];

