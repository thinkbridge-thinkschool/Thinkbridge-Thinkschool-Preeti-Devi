# Day 7 - Window Functions

## Objective

Use SQL window functions to return each quote per author with:
- A running quote count
- The previous quote date
- The gap in days since the previous quote

## SQL Query

```sql
SELECT
    Author,
    Id,
    CreatedAt,
    Text,
    COUNT(*) OVER (
        PARTITION BY Author
        ORDER BY CreatedAt
        ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    ) AS RunningQuoteCount,
    LAG(CreatedAt) OVER (
        PARTITION BY Author
        ORDER BY CreatedAt
    ) AS PreviousQuoteDate,
    CAST(
        julianday(CreatedAt) -
        julianday(
            LAG(CreatedAt) OVER (
                PARTITION BY Author
                ORDER BY CreatedAt
            )
        )
        AS INTEGER
    ) AS GapInDays
FROM Quotes
ORDER BY Author, CreatedAt;
```

## Sample Result

| Author | Id | CreatedAt | RunningQuoteCount | PreviousQuoteDate | GapInDays |
|---|---:|---|---:|---|---:|
| Albert Einstein | 1 | 2026-08-01 | 1 | NULL | NULL |
| Albert Einstein | 2 | 2026-08-03 | 2 | 2026-08-01 | 2 |
| Albert Einstein | 3 | 2026-08-05 | 3 | 2026-08-03 | 2 |
| Albert Einstein | 4 | 2026-08-10 | 4 | 2026-08-05 | 5 |
| Albert Einstein | 5 | 2026-08-12 | 5 | 2026-08-10 | 2 |
| Albert Einstein | 6 | 2026-08-15 | 6 | 2026-08-12 | 3 |
| Albert Einstein | 7 | 2026-08-18 | 7 | 2026-08-15 | 3 |
| Albert Einstein | 8 | 2026-08-25 | 8 | 2026-08-18 | 7 |
| Mahatma Gandhi | 9 | 2026-08-07 | 1 | NULL | NULL |
| Steve Jobs | 10 | 2026-08-20 | 1 | NULL | NULL |

## What This Demonstrates

- `COUNT(*) OVER` for a running count
- `LAG()` to access the previous quote within each author
- `PARTITION BY Author` to calculate independently per author
- `ORDER BY CreatedAt` to establish chronological order
- `julianday()` to calculate the difference between dates
- The first quote for each author has no previous quote, so its gap is `NULL`
