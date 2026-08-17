# Day 7 - Joins and CTEs at Depth

## Objective

Build a SQL query that returns each author with:
- Their total quote count
- Their most-recent quote

The query uses a Common Table Expression (CTE) instead of a correlated subquery in the SELECT clause.

## Database

This exercise was performed against the Week-1 QuotesApi SQLite database:

`Day-3/Day-3-Task-7/QuotesApi/quotes.db`

The relevant table is `Quotes` with the columns `Id`, `Author`, `Text`, and `UserId`.

Note: The Quotes table does not contain a `CreatedAt` column. Therefore, `Id DESC` is used as the proxy for the most-recent quote because higher IDs represent later inserted records.

## SQL Query

```sql
WITH AuthorQuotes AS
(
    SELECT
        Author,
        Id,
        Text,
        COUNT(*) OVER (PARTITION BY Author) AS QuoteCount,
        ROW_NUMBER() OVER
        (
            PARTITION BY Author
            ORDER BY Id DESC
        ) AS RowNum
    FROM Quotes
)
SELECT
    Author,
    QuoteCount,
    Text AS MostRecentQuote
FROM AuthorQuotes
WHERE RowNum = 1
ORDER BY Author;
```

## Result Set

The query was executed directly against the actual Week-1 Quotes database containing 10 quote rows.

| Author | QuoteCount | MostRecentQuote |
|---|---:|---|
| Albert Einstein | 8 | Imagination is more important than knowledge |
| Mahatma Gandhi | 1 | Be the change you wish to see |
| Steve Jobs | 1 | Stay hungry, stay foolish |

## Result-Set Evidence
The actual SQLite result is captured in `result-set.png` in this folder.

## Why a CTE over a correlated subquery?

A CTE makes the logic clearer and lets us calculate the quote count and rank each author's quotes once, avoiding a correlated subquery that repeatedly executes per author row.

## Concepts Demonstrated

- Common Table Expressions (CTEs)
- Window functions
- COUNT(*) OVER
- ROW_NUMBER() OVER
- PARTITION BY
- ORDER BY
- Aggregation
- Identifying the most-recent row per group
- SQLite SQL
