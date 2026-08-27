export interface Quote {
  id: number;
  author: string;
  text: string;
  userId?: string;
}

export interface CreateQuoteRequest {
  author: string;
  text: string;
}
