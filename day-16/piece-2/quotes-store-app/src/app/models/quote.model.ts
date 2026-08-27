// Mirrors the real Week-1 QuotesApi shape exactly (Models/Quote.cs,
// Models/Dtos/CreateQuoteRequest.cs in day-5/Day-5-Piece-2) — id: int,
// author/text: string, userId: string (set server-side from the JWT, never
// sent by the client).
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
