export interface Quote {
  id: number;
  author: string;
  authorId?: number | null;
  authorEntity?: { id: number; name: string } | null;
  text: string;
  userId?: string;
}

export interface CreateQuoteRequest {
  author: string;
  text: string;
}

export interface PagedQuotesResponse {
  items: Quote[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
