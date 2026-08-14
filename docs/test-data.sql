-- CampaignSystem — development test data
--
-- Creates five customers, ten cards and two hundred transactions in July 2026, so that
-- reward calculation can be exercised against data that is deliberately mixed: some of it
-- matches a typical campaign's criteria and some of it does not. Data that matched
-- everything would prove nothing about the filtering.
--
-- July is chosen because rewards are calculated once a campaign has ended. Transactions
-- dated in the future would leave the campaign running and nothing to evaluate. It also
-- lines up with the sample July campaign in database-design.md.
--
-- Development only. Never run against a real database.
--
-- Safe to run twice: it stops if the test customers are already there.
--
-- Usage: open in SSMS against the CampaignSystem database and execute.

USE CampaignSystem;
GO

IF EXISTS (SELECT 1 FROM CUSTOMER WHERE CustomerNumber LIKE 'TEST%')
BEGIN
    PRINT 'Test data is already present. Nothing to do.';
    RETURN;
END

BEGIN TRANSACTION;

-- ─────────────────────────────────────────────
-- Customers — spread across segments on purpose
-- ─────────────────────────────────────────────
-- Segments: 1 = Student, 2 = Company Employee, 3 = Farmer, 4 = Homemaker, 5 = Retiree

INSERT INTO CUSTOMER (CustomerNumber, Gender, SegmentId, IsActive)
VALUES
    ('TEST0001', 'K', 3, 1),   -- Farmer
    ('TEST0002', 'E', 2, 1),   -- Company Employee
    ('TEST0003', 'K', 1, 1),   -- Student
    ('TEST0004', 'E', 5, 1),   -- Retiree
    ('TEST0005', 'K', 4, 1);   -- Homemaker

-- ─────────────────────────────────────────────
-- Cards — two each, products deliberately mixed
-- ─────────────────────────────────────────────
-- Products: 1 = Visa Classic, 2 = MC Classic, 3 = Visa Gold,
--           4 = MC Gold, 5 = Platinum Plus, 6 = Platinum Plus Metal
--
-- A campaign restricted to Gold and above will therefore reach one card of some
-- customers and both cards of others. Card types: A = primary, E = supplementary.

INSERT INTO CARD (CustomerId, ProductId, CardType, IsActive)
SELECT c.Id, v.ProductId, v.CardType, 1
FROM (VALUES
    ('TEST0001', 3, 'A'),   -- Visa Gold
    ('TEST0001', 4, 'E'),   -- MC Gold          → both cards qualify
    ('TEST0002', 5, 'A'),   -- Platinum Plus
    ('TEST0002', 1, 'E'),   -- Visa Classic     → only one card qualifies
    ('TEST0003', 1, 'A'),   -- Visa Classic
    ('TEST0003', 2, 'E'),   -- MC Classic       → no card qualifies
    ('TEST0004', 4, 'A'),   -- MC Gold
    ('TEST0004', 3, 'E'),   -- Visa Gold        → cards qualify, segment does not
    ('TEST0005', 6, 'A'),   -- Platinum Plus Metal
    ('TEST0005', 1, 'E')    -- Visa Classic
) AS v (CustomerNumber, ProductId, CardType)
JOIN CUSTOMER c ON c.CustomerNumber = v.CustomerNumber;

-- ─────────────────────────────────────────────
-- Transactions
-- ─────────────────────────────────────────────
-- Two hundred rows spread over July 2026. Values are derived from the row number
-- rather than randomised, so the same script always produces the same data and a reward
-- total can be checked by hand.
--
-- Cards        cycle through all ten
-- Merchants    1 = Grande Cafe, 2 = Köfteci Yusuf, 3 = Opet
-- Codes        1 = Sale, 2 = Cash Advance, 3 = Debt Payment — mostly sales
-- Amounts      50.00 to 2000.00, so a 250.00 minimum excludes a real share of them

DECLARE @Cards TABLE (Ordinal int, CardId int, CustomerId int);

INSERT INTO @Cards (Ordinal, CardId, CustomerId)
SELECT ROW_NUMBER() OVER (ORDER BY cd.Id) - 1, cd.Id, cd.CustomerId
FROM CARD cd
JOIN CUSTOMER c ON c.Id = cd.CustomerId
WHERE c.CustomerNumber LIKE 'TEST%';

WITH Numbers AS (
    SELECT TOP (200) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
)
INSERT INTO [TRANSACTION]
    (Rrn, CardId, CustomerId, MerchantId, TransactionCodeId, TransactionDate, Amount)
SELECT
    'TEST' + RIGHT('0000000' + CAST(n.n AS varchar(10)), 7),
    cd.CardId,
    cd.CustomerId,
    (n.n % 3) + 1,
    CASE
        WHEN n.n % 11 = 0 THEN 2   -- cash advance
        WHEN n.n % 17 = 0 THEN 3   -- debt payment
        ELSE 1                     -- sale
    END,
    DATEADD(HOUR, (n.n * 13) % 719, '2026-07-01T00:00:00'),
    CAST(50 + ((n.n * 37) % 1951) AS decimal(18, 2))
FROM Numbers n
JOIN @Cards cd ON cd.Ordinal = n.n % 10;

COMMIT TRANSACTION;

PRINT 'Test data created.';
GO

-- ─────────────────────────────────────────────
-- What was created
-- ─────────────────────────────────────────────

SELECT c.CustomerNumber,
       s.SegmentName,
       COUNT(DISTINCT cd.Id)  AS Cards,
       COUNT(t.Id)            AS Transactions,
       SUM(t.Amount)          AS TotalAmount
FROM CUSTOMER c
LEFT JOIN SEGMENT s      ON s.Id = c.SegmentId
LEFT JOIN CARD cd        ON cd.CustomerId = c.Id
LEFT JOIN [TRANSACTION] t ON t.CardId = cd.Id
WHERE c.CustomerNumber LIKE 'TEST%'
GROUP BY c.CustomerNumber, s.SegmentName
ORDER BY c.CustomerNumber;

-- Transactions that would qualify for the sample campaign in database-design.md:
-- sales over 250.00 at Opet, on Gold or better cards, for Farmers and Company Employees.

SELECT COUNT(*) AS QualifyingTransactions
FROM [TRANSACTION] t
JOIN CARD cd     ON cd.Id = t.CardId
JOIN CUSTOMER c  ON c.Id = t.CustomerId
WHERE t.Amount >= 250.00
  AND t.MerchantId = 3            -- Opet
  AND t.TransactionCodeId = 1     -- sale
  AND cd.ProductId IN (3, 4, 5, 6)
  AND c.SegmentId IN (2, 3)
  AND t.TransactionDate >= '2026-07-01'
  AND t.TransactionDate <  '2026-08-01';
