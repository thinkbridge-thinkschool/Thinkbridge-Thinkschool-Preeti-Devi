export interface Author {
  id: number;
  name: string;
}

export interface Quote {
  id: number;
  author: string;
  authorId: number | null;
  authorEntity: Author | null;
  text: string;
}
