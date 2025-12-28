// Daily Report Generate Response
export interface DailyReportGenerateResponse {
  success: boolean;
  reportDate: string;
  filePath: string | null;
  reportRunId: string | null;
  itemCount: number;
  error: string | null;
}

// Daily Report Run DTO
export interface DailyReportRun {
  id: string;
  reportDateLocal: string;
  createdAtUtc: string;
  createdBy: string | null;
  status: string;
  error: string | null;
}

// Query params
export interface ReportRunsQueryParams {
  from?: string;
  to?: string;
}

