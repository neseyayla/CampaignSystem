-- CampaignSystem — development data set
--
-- Fifty customers, roughly ninety cards and fifteen hundred transactions across June to
-- August 2026. Large enough that a campaign's criteria visibly exclude people, small enough
-- that the script runs in a second and a reward total can still be checked by hand.
--
-- Identifiers follow the shapes the real systems use rather than obvious placeholders:
-- eight digit customer numbers and twelve digit retrieval reference numbers. Values are
-- derived from the row number instead of being randomised, so every run produces the same
-- data and a figure can be reproduced.
--
-- Development only. Never run against a real database.
-- Safe to run twice: it stops if the data is already there.
--
-- Usage: open in SSMS against the CampaignSystem database and execute.

USE CampaignSystem;
GO

IF EXISTS (SELECT 1 FROM CUSTOMER)
BEGIN
    PRINT 'Customer data is already present. Nothing to do.';
    RETURN;
END

BEGIN TRANSACTION;

-- ─────────────────────────────────────────────
-- Numbers
-- ─────────────────────────────────────────────
-- A plain sequence the rest of the script derives everything from.

WITH Numbers AS (
    SELECT TOP (2000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
)
SELECT n INTO #N FROM Numbers;

-- ─────────────────────────────────────────────
-- Customers
-- ─────────────────────────────────────────────
-- Segments: 1 Öğrenci · 2 Şirket Çalışanı · 3 Çiftçi · 4 Ev Hanımı · 5 Emekli
--
-- The multiplier scatters the customer numbers so they do not read as a counter, while
-- staying reproducible.
--
-- Gender is taken modulo 2 and the segment modulo 5. Those two are independent, so no
-- segment ends up all male or all female — which would quietly turn a gender filter into a
-- segment filter and make any figure drawn from this data misleading.

INSERT INTO CUSTOMER (CustomerNumber, Gender, SegmentId, IsActive)
SELECT
    CAST(10000000 + ((n * 7919) % 8999999) AS varchar(20)),
    CASE WHEN n % 2 = 0 THEN 'E' ELSE 'K' END,
    ((n * 3) % 5) + 1,
    1
FROM #N
WHERE n <= 50;

-- ─────────────────────────────────────────────
-- Cards
-- ─────────────────────────────────────────────
-- Products: 1 Visa Classic · 2 MC Classic · 3 Visa Gold · 4 MC Gold
--           5 Platinum Plus · 6 Platinum Plus Metal
--
-- One card for everyone, a second for two thirds, a third for a quarter — about ninety in
-- all. The first is always the primary card and the rest are supplementary, which is what
-- the CardType criterion filters on.
--
-- Products are weighted the way a card portfolio actually looks: Classic common, Gold less
-- so, Platinum rare. A campaign restricted to Platinum then reaches a genuinely small group.

DECLARE @Cards TABLE (CustomerId int, ProductId int, CardType char(1));

-- Primary card
INSERT INTO @Cards (CustomerId, ProductId, CardType)
SELECT c.Id,
       CASE
           WHEN (n * 11) % 100 < 45 THEN 1 + ((n * 7) % 2)   -- Classic, 45%
           WHEN (n * 11) % 100 < 80 THEN 3 + ((n * 7) % 2)   -- Gold, 35%
           ELSE                          5 + ((n * 7) % 2)   -- Platinum, 20%
       END,
       'A'
FROM #N
JOIN CUSTOMER c ON c.Id = (SELECT MIN(Id) FROM CUSTOMER) + n - 1
WHERE n <= 50;

-- Second card
INSERT INTO @Cards (CustomerId, ProductId, CardType)
SELECT c.Id,
       CASE WHEN (n * 13) % 100 < 60 THEN 1 + ((n * 5) % 2) ELSE 3 + ((n * 5) % 2) END,
       'E'
FROM #N
JOIN CUSTOMER c ON c.Id = (SELECT MIN(Id) FROM CUSTOMER) + n - 1
WHERE n <= 50 AND n % 3 <> 0;

-- Third card
INSERT INTO @Cards (CustomerId, ProductId, CardType)
SELECT c.Id, 1, 'E'
FROM #N
JOIN CUSTOMER c ON c.Id = (SELECT MIN(Id) FROM CUSTOMER) + n - 1
WHERE n <= 50 AND n % 4 = 0;

INSERT INTO CARD (CustomerId, ProductId, CardType, IsActive)
SELECT CustomerId, ProductId, CardType, 1 FROM @Cards;

-- ─────────────────────────────────────────────
-- Transactions
-- ─────────────────────────────────────────────
-- Fifteen hundred rows over the first of June to the end of August.
--
-- Amounts are deliberately lopsided rather than evenly spread: most card spending is small
-- and the large purchases are rare. An evenly spread set would make a 250 minimum look like
-- it excludes half the table, when in a real portfolio it excludes far more.
--
--   60%  40 – 300      coffee, groceries, fuel
--   28%  300 – 1 000   weekly shop, clothing
--   10%  1 000 – 4 000 electronics, travel
--    2%  4 000 – 15 000 white goods, furniture
--
-- Merchants: 1,2,12 restaurant · 3,4,5 fuel · 6,7,8 grocery · 9,10 electronics · 11 clothing
-- Codes:     1 Satış · 2 Nakit Avans · 3 Borç Ödeme — sales dominate, as they do in life.

DECLARE @CardList TABLE (Ordinal int, CardId int, CustomerId int);

INSERT INTO @CardList (Ordinal, CardId, CustomerId)
SELECT ROW_NUMBER() OVER (ORDER BY Id) - 1, Id, CustomerId FROM CARD;

DECLARE @CardCount int = (SELECT COUNT(*) FROM @CardList);

INSERT INTO [TRANSACTION]
    (Rrn, CardId, CustomerId, MerchantId, TransactionCodeId, TransactionDate, Amount)
SELECT
    -- Twelve digits, the shape a retrieval reference number takes.
    CAST(600000000000 + (n * 137) AS varchar(24)),
    cd.CardId,
    cd.CustomerId,
    CASE
        WHEN (n * 17) % 100 < 22 THEN 1 + ((n * 3) % 2)          -- restoran
        WHEN (n * 17) % 100 < 30 THEN 12
        WHEN (n * 17) % 100 < 55 THEN 3 + ((n * 5) % 3)          -- akaryakıt
        WHEN (n * 17) % 100 < 85 THEN 6 + ((n * 7) % 3)          -- market
        WHEN (n * 17) % 100 < 95 THEN 9 + ((n * 11) % 2)         -- elektronik
        ELSE 11                                                   -- giyim
    END,
    CASE
        WHEN (n * 23) % 100 < 88 THEN 1                          -- satış
        WHEN (n * 23) % 100 < 96 THEN 2                          -- nakit avans
        ELSE 3                                                    -- borç ödeme
    END,
    DATEADD(MINUTE, (n * 883) % 132480, '2026-06-01T00:00:00'),
    CAST(
        CASE
            WHEN (n * 29) % 100 < 60 THEN   40 + ((n * 37) % 261)
            WHEN (n * 29) % 100 < 88 THEN  300 + ((n * 53) % 701)
            WHEN (n * 29) % 100 < 98 THEN 1000 + ((n * 71) % 3001)
            ELSE                          4000 + ((n * 91) % 11001)
        END AS decimal(18, 2))
FROM #N
CROSS APPLY (SELECT CardId, CustomerId FROM @CardList WHERE Ordinal = n % @CardCount) cd
WHERE n <= 1500;

-- ─────────────────────────────────────────────
-- Passwords
-- ─────────────────────────────────────────────
-- Every customer here signs in with 123456.
--
-- DEVELOPMENT ONLY. One literal hash is reused for all fifty rows, which means they share a
-- salt — acceptable for a set of throwaway records, and never how a real password is stored.
-- In use, a password is set one customer at a time through
--   PUT /api/customers/{id}/password
-- which hashes it with its own salt. The clear value is never written anywhere.

UPDATE CUSTOMER
SET PasswordHash = 'AQAAAAIAAYagAAAAEJFQXNYaIC1YsFJirJtMW9NYhciP2xIaiqkgVXxvIMOl7UgMCyyHioTSfbubY17Zlw==';

DROP TABLE #N;

COMMIT TRANSACTION;

PRINT 'Development data created.';
GO

-- ─────────────────────────────────────────────
-- What was created
-- ─────────────────────────────────────────────

SELECT 'Müşteri' AS Tablo, COUNT(*) AS Adet FROM CUSTOMER
UNION ALL SELECT 'Kart', COUNT(*) FROM CARD
UNION ALL SELECT 'İşlem', COUNT(*) FROM [TRANSACTION];

SELECT s.SegmentName,
       COUNT(*)                                              AS Musteri,
       SUM(CASE WHEN c.Gender = 'E' THEN 1 ELSE 0 END)       AS Erkek,
       SUM(CASE WHEN c.Gender = 'K' THEN 1 ELSE 0 END)       AS Kadin
FROM CUSTOMER c
JOIN SEGMENT s ON s.Id = c.SegmentId
GROUP BY s.SegmentName
ORDER BY s.SegmentName;

SELECT p.ProductName, COUNT(*) AS Kart
FROM CARD cd JOIN PRODUCT p ON p.Id = cd.ProductId
GROUP BY p.ProductName
ORDER BY COUNT(*) DESC;

SELECT CASE
           WHEN Amount <  300 THEN '1. 40 – 300'
           WHEN Amount < 1000 THEN '2. 300 – 1.000'
           WHEN Amount < 4000 THEN '3. 1.000 – 4.000'
           ELSE                    '4. 4.000 +'
       END                              AS TutarAraligi,
       COUNT(*)                         AS Islem,
       CAST(SUM(Amount) AS decimal(18,2)) AS Toplam
FROM [TRANSACTION]
GROUP BY CASE
             WHEN Amount <  300 THEN '1. 40 – 300'
             WHEN Amount < 1000 THEN '2. 300 – 1.000'
             WHEN Amount < 4000 THEN '3. 1.000 – 4.000'
             ELSE                    '4. 4.000 +'
         END
ORDER BY 1;
