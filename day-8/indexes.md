# Day 8 — Index Performance Experiment

## Database

`ThinkSchoolDay8`

## Table

`dbo.IndexTest`

The table contains approximately 100,000 rows.

---

## 1. Baseline — No Supporting Index

### Query

SELECT Id, Author, Category, CreatedAt, QuoteText
FROM dbo.IndexTest
WHERE Author = 'Albert Einstein';

### Result

- Access method: Table Scan
- Logical reads: **1,405**
- Rows returned: **10,000**

The query scanned the table because there was no index supporting the `Author` predicate.

---

## 2. Clustered Index on Id

### DDL

CREATE CLUSTERED INDEX CX_IndexTest_Id
ON dbo.IndexTest (Id);

### Same Query

SELECT Id, Author, Category, CreatedAt, QuoteText
FROM dbo.IndexTest
WHERE Author = 'Albert Einstein';

### Result

- Access method: Scan
- Logical reads: **1,432**
- Rows returned: **10,000**

The clustered index is ordered by `Id`, while the query filters by `Author`. Therefore, the clustered index did not provide an efficient access path for this query.

### Comparison

| State | Logical Reads |
|---|---:|
| No index | 1,405 |
| Clustered index on Id | 1,432 |

---

## 3. Non-Clustered Index on Author

### DDL

CREATE NONCLUSTERED INDEX IX_IndexTest_Author
ON dbo.IndexTest (Author)
INCLUDE (Id, Category, CreatedAt, QuoteText);

### Query

SELECT Id, Author, Category, CreatedAt, QuoteText
FROM dbo.IndexTest
WHERE Author = 'Albert Einstein';

### Result

- Access method: Index Seek (NonClustered)
- Logical reads: **150**
- Rows returned: **10,000**

The `Author` index directly supports the filter condition. The execution plan changed from a table scan to an index seek.

### Comparison

| State | Logical Reads |
|---|---:|
| Before Author index | 1,432 |
| After Author index | 150 |

Logical reads decreased by **1,282**, approximately **89%**.

---

## 4. Category Query — Before Category Index

### Query

SELECT Id, Author, Category, CreatedAt, QuoteText
FROM dbo.IndexTest
WHERE Category = 'technology';

### Result

- Access method: Scan
- Logical reads: **1,397**
- Rows returned: **25,000**

---

## 5. Non-Clustered Index on Category

### DDL

CREATE NONCLUSTERED INDEX IX_IndexTest_Category
ON dbo.IndexTest (Category)
INCLUDE (Id, Author, CreatedAt, QuoteText);

### Same Query After Index

SELECT Id, Author, Category, CreatedAt, QuoteText
FROM dbo.IndexTest
WHERE Category = 'technology';

### Result

- Access method: Index Scan (NonClustered)
- Logical reads: **1,397**
- Rows returned: **25,000**

The index was available, but SQL Server selected an index scan rather than an index seek.

### Comparison

| State | Logical Reads |
|---|---:|
| Before Category index | 1,397 |
| After Category index | 1,397 |

The logical reads did not decrease.

The query returned 25,000 rows, so the predicate was not highly selective. SQL Server determined that scanning the non-clustered index was an appropriate access method.

---

## 6. Write-Side Cost

A test INSERT was performed after all three indexes were created.

The INSERT produced:

- Logical reads: **6**

This demonstrates the write-side cost of indexes: when rows are inserted, SQL Server must maintain the table and the associated index structures.

---

## Final Results

| Test | Access Method | Logical Reads |
|---|---|---:|
| Author query — no index | Table Scan | 1,405 |
| Author query — clustered Id | Scan | 1,432 |
| Author query — Author index | Index Seek | 150 |
| Category query — before index | Scan | 1,397 |
| Category query — Category index | Index Scan | 1,397 |
| Test INSERT — all indexes | DML | 6 |

## Conclusion

The `Author` non-clustered index provided the largest improvement because the query filtered directly on the indexed column and returned a relatively selective result.

The `Category` index did not reduce logical reads because the `technology` predicate returned 25,000 rows. SQL Server chose an index scan instead of an index seek.

The experiment demonstrates that indexes are a trade-off: they can significantly improve suitable read queries, but they also require additional storage and maintenance during write operations.
