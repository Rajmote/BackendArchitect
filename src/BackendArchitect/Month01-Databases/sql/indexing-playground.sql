-- Month 1 · Indexing & query-plan playground (SQL Server / T-SQL)
-- Run block by block in SSMS or Azure Data Studio with "Include Actual Execution Plan" on.
-- Postgres users: swap SET STATISTICS IO for EXPLAIN (ANALYZE, BUFFERS).

------------------------------------------------------------
-- 0. A table with enough rows to make scan vs seek visible
------------------------------------------------------------
DROP TABLE IF EXISTS dbo.Orders;
CREATE TABLE dbo.Orders
(
    Id          INT IDENTITY PRIMARY KEY,   -- clustered index (the row order itself)
    CustomerId  INT           NOT NULL,
    OrderDate   DATE          NOT NULL,
    Email       VARCHAR(200)  NOT NULL,
    Amount      DECIMAL(10,2) NOT NULL
);

-- 200k rows
INSERT INTO dbo.Orders (CustomerId, OrderDate, Email, Amount)
SELECT TOP (200000)
       ABS(CHECKSUM(NEWID())) % 5000,
       DATEADD(DAY, -(ABS(CHECKSUM(NEWID())) % 900), CAST('2026-07-20' AS DATE)),
       CONCAT('user', ABS(CHECKSUM(NEWID())) % 5000, '@example.com'),
       (ABS(CHECKSUM(NEWID())) % 10000) / 100.0
FROM sys.all_objects a CROSS JOIN sys.all_objects b;

SET STATISTICS IO ON;

------------------------------------------------------------
-- 1. No usable index yet -> expect a TABLE/CLUSTERED-INDEX SCAN + high logical reads
------------------------------------------------------------
SELECT * FROM dbo.Orders WHERE CustomerId = 42;

------------------------------------------------------------
-- 2. Add a non-clustered index -> re-run #1 -> expect an INDEX SEEK, few reads
------------------------------------------------------------
CREATE INDEX IX_Orders_CustomerId_OrderDate ON dbo.Orders (CustomerId, OrderDate);
SELECT * FROM dbo.Orders WHERE CustomerId = 42;                       -- SEEK (leftmost col)

------------------------------------------------------------
-- 3. Leftmost-prefix rule
------------------------------------------------------------
SELECT * FROM dbo.Orders WHERE OrderDate > '2026-01-01';             -- SCAN (date isn't leftmost)
SELECT * FROM dbo.Orders
 WHERE CustomerId = 42 AND OrderDate > '2026-01-01';                 -- SEEK + range walk (best)

------------------------------------------------------------
-- 4. Function on the column kills the seek
------------------------------------------------------------
SELECT * FROM dbo.Orders WHERE YEAR(OrderDate) = 2026;              -- SCAN
SELECT * FROM dbo.Orders
 WHERE OrderDate >= '2026-01-01' AND OrderDate < '2027-01-01';       -- SEEK (column left bare)

------------------------------------------------------------
-- 5. Covering index: INCLUDE the columns the query returns -> no key lookup
------------------------------------------------------------
CREATE INDEX IX_Orders_Customer_Covering
    ON dbo.Orders (CustomerId) INCLUDE (OrderDate, Amount);
SELECT CustomerId, OrderDate, Amount
  FROM dbo.Orders WHERE CustomerId = 42;                             -- SEEK, no lookup

-- Compare the "logical reads" printed in the Messages tab across each step.
