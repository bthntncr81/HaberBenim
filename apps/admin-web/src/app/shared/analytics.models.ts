// Analytics Overview Response
export interface AnalyticsOverview {
  fromUtc: string;
  toUtc: string;
  totalPublished: number;
  byChannel: { [key: string]: number };
  autoVsEditorial: { [key: string]: number };
  topSources: TopSource[];
  totalIngested: number;
  totalPending: number;
  totalRejected: number;
  totalCorrections: number;
}

export interface TopSource {
  sourceName: string;
  count: number;
}

export interface DailyTrend {
  date: string;
  count: number;
}

// Query params
export interface AnalyticsQueryParams {
  fromUtc?: string;
  toUtc?: string;
}

