// Audit Log Models (Sprint 8)

export interface AuditLog {
  id: string;
  createdAtUtc: string;
  userId: string | null;
  userEmail: string | null;
  method: string;
  path: string;
  statusCode: number;
  ipAddress: string | null;
  userAgent: string | null;
  durationMs: number;
}

export interface AuditLogListResponse {
  items: AuditLog[];
  total: number;
  page: number;
  pageSize: number;
}

export interface AuditLogQueryParams {
  fromUtc?: string;
  toUtc?: string;
  userEmail?: string;
  path?: string;
  statusCode?: number;
  page?: number;
  pageSize?: number;
}

