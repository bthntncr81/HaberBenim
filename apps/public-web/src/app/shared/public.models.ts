export interface PublicArticleListItem {
  id: string;
  webTitle: string;
  excerpt: string;
  publishedAtUtc: string;
  path: string;
  canonicalUrl: string | null;
  sourceName: string | null;
  categoryOrGroup: string | null;
}

export interface PublicArticle {
  id: string;
  webTitle: string;
  webBody: string;
  publishedAtUtc: string;
  path: string;
  slug: string;
  canonicalUrl: string | null;
  sourceName: string | null;
  categoryOrGroup: string | null;
}

export interface PublicArticleListResponse {
  items: PublicArticleListItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface PublicQueryParams {
  q?: string;
  fromUtc?: string;
  toUtc?: string;
  page?: number;
  pageSize?: number;
}

