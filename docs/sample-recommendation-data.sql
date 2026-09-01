-- CampaignSystem — sample data for the campaign recommendation screen
--
-- Fills a fresh database with enough transaction history that GET /api/campaign-recommendations
-- returns an interesting, easy-to-read list:
--
--   * a dedicated cohort of 24 customers + cards, and a known admin (29999999 / 123456)
--   * sample merchants across eight categories (numbers start with "ORN")
--   * ~2.200 transactions over the last ~85 days, shaped so the trend and the season carry
--     the ranking rather than raw volume:
--       Kırtasiye, Eğitim  — heavy and steeply rising in the recent half (and September is
--                            their seasonal peak) -> top of the list
--       Giyim              — rising, mild seasonal lift
--       Elektronik         — the biggest tickets but a flat/declining trend -> mid
--       Akaryakıt          — declining -> low
--       Market             — high volume but COVERED by the campaign below -> hidden unless
--                            "Kapsananları da göster" is on
--   * ~30 refund rows on large Elektronik purchases, so the "spend nets out refunds" rule
--     is visible
--   * one Ongoing campaign that targets the sample Market merchants
--
-- This is for the DOCKER database (compose sets Database=CampaignSystem). Dates are relative
-- to GETDATE(), so it stays meaningful whenever it is run.
--
-- Development only. Safe to run twice — it stops if the sample merchants are already there.
--
-- Load it:
--   docker compose exec -T db /opt/mssql-tools18/bin/sqlcmd \
--     -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d CampaignSystem \
--     -i /dev/stdin < docs/sample-recommendation-data.sql

-- sqlcmd runs with QUOTED_IDENTIFIER OFF by default; the TRANSACTION table has a filtered
-- unique index, so an insert into it needs these on. XACT_ABORT rolls the whole thing back
-- on any error rather than leaving a half-written transaction open.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

USE CampaignSystem;
GO

IF EXISTS (SELECT 1 FROM MERCHANT WHERE MerchantNumber LIKE 'ORN%')
BEGIN
    PRINT 'Örnek veri zaten yüklü. Bir şey yapılmadı.';
    RETURN;
END

DECLARE @Pw nvarchar(200) =
    'AQAAAAIAAYagAAAAEJFQXNYaIC1YsFJirJtMW9NYhciP2xIaiqkgVXxvIMOl7UgMCyyHioTSfbubY17Zlw==';

BEGIN TRANSACTION;

-- ─────────────────────────────────────────────
-- Numbers
-- ─────────────────────────────────────────────
;WITH Numbers AS (
    SELECT TOP (4000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
)
SELECT n INTO #N FROM Numbers;

-- ─────────────────────────────────────────────
-- Sample merchants  (MerchantNumber = 'ORN' + <3-letter category tag> + <slot>)
-- ─────────────────────────────────────────────
-- Category ids come from MerchantCategoryConfiguration.
DECLARE @mseed TABLE (CatTag varchar(4), CatId int, Slot int, Nm nvarchar(200));
INSERT INTO @mseed (CatTag, CatId, Slot, Nm) VALUES
    ('KRT', 20, 1, N'Örnek Kırtasiye A'), ('KRT', 20, 2, N'Örnek Kırtasiye B'),
    ('EGT', 15, 1, N'Örnek Eğitim A'),    ('EGT', 15, 2, N'Örnek Kurs B'),
    ('GYM',  4, 1, N'Örnek Giyim A'),     ('GYM',  4, 2, N'Örnek Giyim B'),
    ('ELK',  7, 1, N'Örnek Elektronik A'),('ELK',  7, 2, N'Örnek Elektronik B'),
    ('AKY',  3, 1, N'Örnek Akaryakıt A'), ('AKY',  3, 2, N'Örnek Akaryakıt B'),
    ('TUR', 13, 1, N'Örnek Seyahat A'),
    ('GDA',  1, 1, N'Örnek Market A'),    ('GDA',  1, 2, N'Örnek Market B'),
    ('RST',  2, 1, N'Örnek Restoran A');

INSERT INTO MERCHANT (MerchantNumber, MerchantName, IsActive, MerchantCategoryId)
SELECT CONCAT('ORN', CatTag, Slot), Nm, 1, CatId FROM @mseed;

DECLARE @M TABLE (CatTag varchar(4), Slot int, MerchantId int);
INSERT INTO @M (CatTag, Slot, MerchantId)
SELECT SUBSTRING(MerchantNumber, 4, 3),
       CAST(SUBSTRING(MerchantNumber, 7, 1) AS int),
       Id
FROM MERCHANT
WHERE MerchantNumber LIKE 'ORN%' AND LEN(MerchantNumber) = 7;

-- ─────────────────────────────────────────────
-- Customer cohort  (CustomerNumber 20000001..20000024) + a known admin
-- ─────────────────────────────────────────────
INSERT INTO CUSTOMER (CustomerNumber, Gender, SegmentId, IsActive, IsAdmin, PasswordHash)
SELECT CAST(20000000 + n AS varchar(20)),
       CASE WHEN n % 2 = 0 THEN 'E' ELSE 'K' END,
       ((n * 3) % 5) + 1,
       1, 0, @Pw
FROM #N
WHERE n <= 24;

IF NOT EXISTS (SELECT 1 FROM CUSTOMER WHERE CustomerNumber = '29999999')
    INSERT INTO CUSTOMER (CustomerNumber, Gender, SegmentId, IsActive, IsAdmin, PasswordHash)
    VALUES ('29999999', 'E', 2, 1, 1, @Pw);

-- One primary card each, a light product mix.
INSERT INTO CARD (CustomerId, ProductId, CardType, IsActive)
SELECT c.Id,
       CASE WHEN (c.Id % 5) < 3 THEN 1 WHEN (c.Id % 5) < 4 THEN 3 ELSE 5 END,
       'A', 1
FROM CUSTOMER c
WHERE c.CustomerNumber LIKE '2000000%' OR c.CustomerNumber = '29999999';

DECLARE @CardList TABLE (Ordinal int IDENTITY(0, 1), CardId int, CustomerId int);
INSERT INTO @CardList (CardId, CustomerId)
SELECT cd.Id, cd.CustomerId
FROM CARD cd
JOIN CUSTOMER c ON c.Id = cd.CustomerId
WHERE c.CustomerNumber LIKE '2000000%' OR c.CustomerNumber = '29999999';

DECLARE @CardCount int = (SELECT COUNT(*) FROM @CardList);

-- ─────────────────────────────────────────────
-- Transactions
-- ─────────────────────────────────────────────
-- d  = day offset back from today, 1..84  (d < 42 is the "recent half")
-- b  = a 0..99 bucket that picks the category; the split differs by half, which is what
--      creates each category's trend.
INSERT INTO [TRANSACTION]
    (Rrn, CardId, CustomerId, MerchantId, TransactionCodeId, TransactionDate, Amount)
SELECT
    CONCAT('ORN', RIGHT('000000000' + CAST(n AS varchar(9)), 9)),
    cl.CardId,
    cl.CustomerId,
    mm.MerchantId,
    1,
    DATEADD(SECOND, (n * 997) % 86400,
        DATEADD(DAY, -((n * 61) % 84) - 1, CAST(CAST(GETDATE() AS date) AS datetime2(0)))),
    CAST(amt.Amt AS decimal(18, 2))
FROM #N
CROSS APPLY (SELECT ((n * 61) % 84) AS d, ((n * 37) % 100) AS b) p
CROSS APPLY (SELECT CASE WHEN p.d < 42 THEN 1 ELSE 0 END AS recent) r
CROSS APPLY (SELECT CASE
        WHEN r.recent = 1 THEN
            CASE
                WHEN p.b < 24 THEN 'KRT'
                WHEN p.b < 42 THEN 'EGT'
                WHEN p.b < 57 THEN 'GYM'
                WHEN p.b < 72 THEN 'ELK'
                WHEN p.b < 80 THEN 'AKY'
                WHEN p.b < 91 THEN 'GDA'
                WHEN p.b < 97 THEN 'RST'
                ELSE 'TUR'
            END
        ELSE
            CASE
                WHEN p.b < 6  THEN 'KRT'
                WHEN p.b < 12 THEN 'EGT'
                WHEN p.b < 22 THEN 'GYM'
                WHEN p.b < 45 THEN 'ELK'
                WHEN p.b < 62 THEN 'AKY'
                WHEN p.b < 85 THEN 'GDA'
                WHEN p.b < 94 THEN 'RST'
                ELSE 'TUR'
            END
    END AS CatTag) c
CROSS APPLY (SELECT CASE c.CatTag
        WHEN 'KRT' THEN  200 + ((n * 37) % 1300)
        WHEN 'EGT' THEN  500 + ((n * 53) % 4000)
        WHEN 'GYM' THEN  300 + ((n * 41) % 2200)
        WHEN 'ELK' THEN  800 + ((n * 71) % 8200)
        WHEN 'AKY' THEN  400 + ((n * 29) % 2100)
        WHEN 'GDA' THEN   80 + ((n * 17) % 820)
        WHEN 'RST' THEN  120 + ((n * 23) % 1080)
        ELSE            2000 + ((n * 91) % 16000)
    END AS Amt) amt
CROSS APPLY (SELECT TOP 1 MerchantId FROM @M
             WHERE CatTag = c.CatTag
               AND Slot = CASE WHEN c.CatTag IN ('TUR', 'RST') THEN 1 ELSE (n % 2) + 1 END) mm
CROSS APPLY (SELECT CardId, CustomerId FROM @CardList WHERE Ordinal = n % @CardCount) cl
WHERE n <= 2200;

-- ─────────────────────────────────────────────
-- Refunds  (negative rows pointing at large Elektronik purchases)
-- ─────────────────────────────────────────────
;WITH BigElk AS (
    SELECT t.Id, t.CardId, t.CustomerId, t.MerchantId, t.TransactionDate, t.Amount,
           ROW_NUMBER() OVER (ORDER BY t.Id) AS rk
    FROM [TRANSACTION] t
    JOIN @M m ON m.MerchantId = t.MerchantId AND m.CatTag = 'ELK'
    WHERE t.Rrn LIKE 'ORN%' AND t.OriginalTransactionId IS NULL AND t.Amount > 3500
)
INSERT INTO [TRANSACTION]
    (Rrn, CardId, CustomerId, MerchantId, TransactionCodeId, TransactionDate, Amount, OriginalTransactionId)
SELECT CONCAT('ORNR', RIGHT('00000000' + CAST(rk AS varchar(8)), 8)),
       CardId, CustomerId, MerchantId, 1,
       DATEADD(DAY, 4, TransactionDate),
       -1 * CAST(Amount * 0.4 AS decimal(18, 2)),
       Id
FROM BigElk
WHERE rk <= 30;

-- ─────────────────────────────────────────────
-- A campaign that covers the Market category
-- ─────────────────────────────────────────────
INSERT INTO CAMPAIGN
    (Name, Description, CampaignType, EarningType, StartDate, EndDate, Status, IsActive,
     RefundClawbackEnabled, UnusedPointsClawbackEnabled, RewardPoint)
VALUES
    (N'Market Sonbahar - Örnek',
     N'Örnek veri: Market kategorisini kapsayan aktif kampanya.',
     'MASS', 'M',
     DATEADD(DAY, -25, GETDATE()), DATEADD(DAY, 20, GETDATE()),
     'Ongoing', 1, 0, 0, 10);

DECLARE @cid int = SCOPE_IDENTITY();

INSERT INTO CAMPAIGN_MERCHANT (CampaignId, MerchantId)
SELECT @cid, MerchantId FROM @M WHERE CatTag = 'GDA';

DROP TABLE #N;

COMMIT TRANSACTION;

PRINT 'Örnek öneri verisi oluşturuldu.';
GO

-- ─────────────────────────────────────────────
-- What was created
-- ─────────────────────────────────────────────
SELECT mc.CategoryName                                   AS Kategori,
       COUNT(*)                                          AS Islem,
       CAST(SUM(t.Amount) AS decimal(18, 2))             AS NetHarcama,
       SUM(CASE WHEN t.TransactionDate >= DATEADD(DAY, -42, GETDATE()) THEN 1 ELSE 0 END) AS SonYariAdet,
       SUM(CASE WHEN t.TransactionDate <  DATEADD(DAY, -42, GETDATE()) THEN 1 ELSE 0 END) AS OncekiYariAdet
FROM [TRANSACTION] t
JOIN MERCHANT m         ON m.Id = t.MerchantId
JOIN MERCHANT_CATEGORY mc ON mc.Id = m.MerchantCategoryId
WHERE t.Rrn LIKE 'ORN%'
GROUP BY mc.CategoryName
ORDER BY NetHarcama DESC;

PRINT 'Admin girişi: 29999999 / 123456';
GO
