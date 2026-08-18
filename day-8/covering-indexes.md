# Day 8 — Covering Indexes + Included Columns

## Objective

Demonstrate how a covering index can eliminate a Key Lookup by including the columns required by a query.

## Query

SELECT Id, Author, Category, CreatedAt, QuoteText
FROM dbo.IndexTest
WHERE Author = 'Albert Einstein';

## Before — Narrow Index

### Index DDL

CREATE NONCLUSTERED INDEX IX_IndexTest_Author_Narrow
ON dbo.IndexTest (Author);

### Execution Plan

The narrow index produced:

Index Seek (NonClustered)
→ Key Lookup (Clustered)

The Key Lookup occurred because the index contained only the `Author` key, while the query also required `Id`, `Category`, `CreatedAt`, and `QuoteText`.

### Before Logical Reads

**43,900 logical reads**

## After — Covering Index

### Index DDL

CREATE NONCLUSTERED INDEX IX_IndexTest_Author_Covering
ON dbo.IndexTest (Author)
INCLUDE (Id, Category, CreatedAt, QuoteText);

### Execution Plan

The covering index produced:

Index Seek (NonClustered)

The Key Lookup was eliminated because all columns required by the query are available directly from the index.

### After Logical Reads

**150 logical reads**

## Logical Reads Delta

| Stage | Logical Reads |
|---|---:|
| Before — Key Lookup | 43,900 |
| After — Covering Index | 150 |
| Reduction | 43,750 |

Logical reads were reduced by approximately **99.66%**.

## Conclusion

Adding the required columns with `INCLUDE` turned the non-clustered index into a covering index. SQL Server could satisfy the query directly from the index, eliminating the Key Lookup and significantly reducing logical reads.
