Day 9 - Isolation Levels + Read Anomalies

Objective

Reproduce dirty reads, non-repeatable reads, and phantom reads using two SQL Server sessions and identify the lowest isolation level that prevents each anomaly.

Test Database

'ThinkSchoolDay8'

Test Table

sql
CREATE TABLE dbo.IsolationTest
(
    Id INT NOT NULL PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    Amount INT NOT NULL
);

INSERT INTO dbo.IsolationTest (Id, Name, Amount)
VALUES
(1, 'Alice', 100),
(2, 'Bob', 200),
(3, 'Charlie', 300);

1. Dirty Read

Session 1

sql
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
GO

BEGIN TRANSACTION;

UPDATE dbo.IsolationTest
SET Amount = 999
WHERE Id = 1;

Session 2

sql
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
GO

SELECT Id, Name, Amount
FROM dbo.IsolationTest
WHERE Id = 1;

Observed result:

1 | Alice | 999

Session 2 read the uncommitted value from Session 1.

Anomaly: Dirty read

Prevented by: READ COMMITTED

Cleanup

sql
ROLLBACK;

2. Non-Repeatable Read

Session 1

sql
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
GO

BEGIN TRANSACTION;

SELECT Amount
FROM dbo.IsolationTest
WHERE Id = 1;

First read: 100

Session 2

sql
UPDATE dbo.IsolationTest
SET Amount = 600
WHERE Id = 1;

COMMIT;

Session 1

sql
SELECT Amount
FROM dbo.IsolationTest
WHERE Id = 1;

Second read: 600

The same row returned different values within the same transaction.

Anomaly: Non-repeatable read

Prevented by: REPEATABLE READ

Cleanup

sql
COMMIT;

3. Phantom Read

Session 1

sql
SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;
GO

BEGIN TRANSACTION;

SELECT Id, Name, Amount
FROM dbo.IsolationTest
WHERE Amount >= 200
ORDER BY Id;

Initial matching rows:

2 | Bob | 200
3 | Charlie | 300

Session 2

sql
INSERT INTO dbo.IsolationTest (Id, Name, Amount)
VALUES (4, 'David', 250);

COMMIT;

Session 1

sql
SELECT Id, Name, Amount
FROM dbo.IsolationTest
WHERE Amount >= 200
ORDER BY Id;

The new matching row appears:

4 | David | 250

Anomaly: Phantom read

Prevented by: SERIALIZABLE

Cleanup

sql
COMMIT;

4. Preventing Phantom Reads with SERIALIZABLE

Session 1

sql
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
GO

BEGIN TRANSACTION;

SELECT Id, Name, Amount
FROM dbo.IsolationTest
WHERE Amount >= 200
ORDER BY Id;

Session 2

sql
INSERT INTO dbo.IsolationTest (Id, Name, Amount)
VALUES (4, 'David', 250);

The insert was blocked while Session 1 held the SERIALIZABLE transaction.

Session 1

sql
COMMIT;

Isolation Level Summary

| Anomaly | Lowest Isolation Level That Prevents It |
|---|---|
| Dirty read | READ COMMITTED |
| Non-repeatable read | REPEATABLE READ |
| Phantom read | SERIALIZABLE |

Key Learning

Higher isolation levels provide stronger consistency by restricting concurrent access, but they can also increase blocking and reduce concurrency.