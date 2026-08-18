# Day 8 — Clustered vs Non-Clustered Indexes

## Objective

Compare clustered and non-clustered indexes using a 100,000-row SQL Server table, SET STATISTICS IO, and actual execution plans.

## Indexes Created

### Clustered Index

CREATE CLUSTERED INDEX CX_IndexTest_Id
ON dbo.IndexTest (Id);

### Non-Clustered Index — Author

CREATE NONCLUSTERED INDEX IX_IndexTest_Author
ON dbo.IndexTest (Author)
INCLUDE (Id, Category, CreatedAt, QuoteText);

### Non-Clustered Index — Category

CREATE NONCLUSTERED INDEX IX_IndexTest_Category
ON dbo.IndexTest (Category)
INCLUDE (Id, Author, CreatedAt, QuoteText);

## Results

| Query | Index State | Access Method | Logical Reads |
|---|---|---|---:|
| Author = Albert Einstein | No index | Table Scan | 1,405 |
| Author = Albert Einstein | Clustered Id | Scan | 1,432 |
| Author = Albert Einstein | Non-clustered Author | Index Seek | 150 |
| Category = technology | Before Category index | Scan | 1,397 |
| Category = technology | Non-clustered Category | Index Scan | 1,397 |

## Write-Side Cost

The test INSERT with the clustered index and two non-clustered indexes incurred 6 logical reads, demonstrating that indexes add maintenance work to writes.

## Key Learning

Indexes can significantly improve selective read queries, but they also consume storage and add write-maintenance cost. SQL Server chooses the access method based on the estimated cost of the query.

## Covering Indexes + Included Columns

A narrow `Author` index produced an `Index Seek` followed by a `Key Lookup (Clustered)` because the query required columns that were not stored in the index.

A covering index was then created using `INCLUDE`:

CREATE NONCLUSTERED INDEX IX_IndexTest_Author_Covering
ON dbo.IndexTest (Author)
INCLUDE (Id, Category, CreatedAt, QuoteText);

The covering index eliminated the Key Lookup.

### Logical Reads

| Stage | Logical Reads |
|---|---:|
| Before — Key Lookup | 43,900 |
| After — Covering Index | 150 |
| Reduction | 43,750 |

This represents approximately a **99.66% reduction** in logical reads.
