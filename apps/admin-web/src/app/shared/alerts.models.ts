// Admin Alerts Models (Sprint 8)

export interface AdminAlert {
  id: string;
  createdAtUtc: string;
  type: string;
  severity: string;
  title: string;
  message: string;
  isAcknowledged: boolean;
  acknowledgedAtUtc: string | null;
  acknowledgedByUserEmail: string | null;
  metaJson: string | null;
}

export interface AlertListResponse {
  items: AdminAlert[];
  total: number;
  page: number;
  pageSize: number;
}

export interface AlertQueryParams {
  severity?: string;
  type?: string;
  acknowledged?: boolean;
  fromUtc?: string;
  toUtc?: string;
  page?: number;
  pageSize?: number;
}

export interface AlertAckResponse {
  ok: boolean;
  error: string | null;
}

export interface AlertCountResponse {
  total: number;
  critical: number;
}

export const AlertSeverities = [
  { value: 'Critical', label: 'Critical' },
  { value: 'Warn', label: 'Warning' },
  { value: 'Info', label: 'Info' }
];

export const AlertTypes = [
  { value: 'IngestionDown', label: 'Ingestion Down' },
  { value: 'FailoverActivated', label: 'Failover Activated' },
  { value: 'ComplianceViolation', label: 'Compliance Violation' },
  { value: 'Retract', label: 'Content Retracted' }
];

