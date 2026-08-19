-- CampaignSystem — example campaigns
--
-- Eight campaigns that between them exercise every criterion the system supports: the four
-- junction tables, the two demographic filters on the campaign row, both earning levels,
-- both campaign types, amount bounds and the reward cap. They are meant to be read as much
-- as run — each one shows a different combination, including the rule that a criterion left
-- unset places no restriction at all.
--
-- Spread over three months so the campaign lifecycle is visible: the June and July ones have
-- finished and are ready for the batch, August is still running, and September has not
-- started. Dates are fixed rather than relative, so re-running gives the same picture.
--
-- Run after docs/test-data.sql: the criteria point at the seeded reference data and the
-- periods line up with the transactions that script creates.
--
-- Development only. Safe to run twice — it stops if the campaigns are already there.

USE CampaignSystem;
GO

IF EXISTS (SELECT 1 FROM CAMPAIGN)
BEGIN
    PRINT 'Campaigns are already present. Nothing to do.';
    RETURN;
END

BEGIN TRANSACTION;

-- Reference data ids, fixed by the seed migrations:
--   SEGMENT           1 Öğrenci · 2 Şirket Çalışanı · 3 Çiftçi · 4 Ev Hanımı · 5 Emekli
--   PRODUCT           1 Visa Classic · 2 MC Classic · 3 Visa Gold · 4 MC Gold
--                     5 Platinum Plus · 6 Platinum Plus Metal
--   MERCHANT          1 Grande Cafe · 2 Köfteci Yusuf · 12 Big Chefs
--                     3 Opet · 4 Shell · 5 Petrol Ofisi
--                     6 Migros · 7 BİM · 8 A101
--                     9 Teknosa · 10 Vatan Bilgisayar · 11 LC Waikiki
--   TRANSACTION_CODE  1 Satış · 2 Nakit Avans · 3 Borç Ödeme

DECLARE @Campaign INT;

-- ─────────────────────────────────────────────
-- 1. Haziran — Market Kampanyası
-- ─────────────────────────────────────────────
-- Card based across three grocery chains. No demographic filter, so it reaches the whole
-- portfolio; the narrowing comes from the merchants and the 150 minimum.

INSERT INTO CAMPAIGN
    (Name, Description, CampaignType, StartDate, EndDate, MinimumAmount, MaximumAmount,
     RewardPoint, MaxRewardAmount, EarningType, Gender, CardType, Status, IsActive)
VALUES
    (N'Haziran Market Kampanyası',
     N'Migros, BİM ve A101''de 150 TL üzeri alışverişlerde işlem başına 20 puan.',
     'MASS', '2026-06-01T00:00:00', '2026-06-30T23:59:59',
     150.00, NULL, 20.00, 400.00, 'K', NULL, NULL, 'Ongoing', 1);

SET @Campaign = SCOPE_IDENTITY();
INSERT INTO CAMPAIGN_MERCHANT (CampaignId, MerchantId) VALUES (@Campaign, 6), (@Campaign, 7), (@Campaign, 8);
INSERT INTO CAMPAIGN_TRANSACTION_CODE (CampaignId, TransactionCodeId) VALUES (@Campaign, 1);

-- ─────────────────────────────────────────────
-- 2. Temmuz — Akaryakıt Kampanyası
-- ─────────────────────────────────────────────
-- The worked example from the design document, widened to three fuel brands. Primary cards
-- only, which is what the CardType column is for; gender is left null so it reaches everyone.

INSERT INTO CAMPAIGN
    (Name, Description, CampaignType, StartDate, EndDate, MinimumAmount, MaximumAmount,
     RewardPoint, MaxRewardAmount, EarningType, Gender, CardType, Status, IsActive)
VALUES
    (N'Temmuz Akaryakıt Kampanyası',
     N'Opet, Shell ve Petrol Ofisi''nde 250 TL üzeri satışlarda 50 puan. Gold ve üstü asıl kartlar.',
     'MASS', '2026-07-01T00:00:00', '2026-07-31T23:59:59',
     250.00, NULL, 50.00, 500.00, 'K', NULL, 'A', 'Ongoing', 1);

SET @Campaign = SCOPE_IDENTITY();
INSERT INTO CAMPAIGN_SEGMENT (CampaignId, SegmentId) VALUES (@Campaign, 2), (@Campaign, 3);
INSERT INTO CAMPAIGN_PRODUCT (CampaignId, ProductId) VALUES (@Campaign, 3), (@Campaign, 4), (@Campaign, 5), (@Campaign, 6);
INSERT INTO CAMPAIGN_MERCHANT (CampaignId, MerchantId) VALUES (@Campaign, 3), (@Campaign, 4), (@Campaign, 5);
INSERT INTO CAMPAIGN_TRANSACTION_CODE (CampaignId, TransactionCodeId) VALUES (@Campaign, 1);

-- ─────────────────────────────────────────────
-- 3. Temmuz — Kadınlara Özel Restoran Kampanyası
-- ─────────────────────────────────────────────
-- Customer based, so a customer's cards pool into one reward and the cap applies once per
-- customer rather than once per card. Segment and product are left open.

INSERT INTO CAMPAIGN
    (Name, Description, CampaignType, StartDate, EndDate, MinimumAmount, MaximumAmount,
     RewardPoint, MaxRewardAmount, EarningType, Gender, CardType, Status, IsActive)
VALUES
    (N'Kadınlara Özel Restoran Kampanyası',
     N'Restoranlarda yapılan satışlarda işlem başına 25 puan. Kadın müşteriler.',
     'MASS', '2026-07-01T00:00:00', '2026-07-31T23:59:59',
     100.00, NULL, 25.00, 300.00, 'M', 'K', NULL, 'Ongoing', 1);

SET @Campaign = SCOPE_IDENTITY();
INSERT INTO CAMPAIGN_MERCHANT (CampaignId, MerchantId) VALUES (@Campaign, 1), (@Campaign, 2), (@Campaign, 12);
INSERT INTO CAMPAIGN_TRANSACTION_CODE (CampaignId, TransactionCodeId) VALUES (@Campaign, 1);

-- ─────────────────────────────────────────────
-- 4. Temmuz — Platinum Nakit Avans
-- ─────────────────────────────────────────────
-- Shows MaximumAmount in use: only advances between 500 and 2000 count. No merchant
-- restriction, because a cash advance is not tied to one.

INSERT INTO CAMPAIGN
    (Name, Description, CampaignType, StartDate, EndDate, MinimumAmount, MaximumAmount,
     RewardPoint, MaxRewardAmount, EarningType, Gender, CardType, Status, IsActive)
VALUES
    (N'Platinum Nakit Avans Kampanyası',
     N'500–2000 TL arası nakit avans işlemlerinde 100 puan. Platinum kartlar.',
     'MASS', '2026-07-01T00:00:00', '2026-07-31T23:59:59',
     500.00, 2000.00, 100.00, NULL, 'K', NULL, NULL, 'Ongoing', 1);

SET @Campaign = SCOPE_IDENTITY();
INSERT INTO CAMPAIGN_PRODUCT (CampaignId, ProductId) VALUES (@Campaign, 5), (@Campaign, 6);
INSERT INTO CAMPAIGN_TRANSACTION_CODE (CampaignId, TransactionCodeId) VALUES (@Campaign, 2);

-- ─────────────────────────────────────────────
-- 5. Temmuz — Emeklilere Özel
-- ─────────────────────────────────────────────
-- A single segment and nothing else. Customer based with a low threshold: the point is
-- volume of small purchases, not size.

INSERT INTO CAMPAIGN
    (Name, Description, CampaignType, StartDate, EndDate, MinimumAmount, MaximumAmount,
     RewardPoint, MaxRewardAmount, EarningType, Gender, CardType, Status, IsActive)
VALUES
    (N'Emeklilere Özel Harcama Kampanyası',
     N'Her harcamada 15 puan. Emekli segmentindeki müşteriler.',
     'MASS', '2026-07-01T00:00:00', '2026-07-31T23:59:59',
     NULL, NULL, 15.00, 250.00, 'M', NULL, NULL, 'Ongoing', 1);

SET @Campaign = SCOPE_IDENTITY();
INSERT INTO CAMPAIGN_SEGMENT (CampaignId, SegmentId) VALUES (@Campaign, 5);

-- ─────────────────────────────────────────────
-- 6. Temmuz — Katılımlı Elektronik Kampanyası
-- ─────────────────────────────────────────────
-- CampaignType SI: only customers who sign up take part, so it pays nothing until there are
-- enrolment records. Male primary card holders, electronics retailers.

INSERT INTO CAMPAIGN
    (Name, Description, CampaignType, StartDate, EndDate, MinimumAmount, MaximumAmount,
     RewardPoint, MaxRewardAmount, EarningType, Gender, CardType, Status, IsActive)
VALUES
    (N'Katılımlı Elektronik Kampanyası',
     N'Teknosa ve Vatan''da 1000 TL üzeri alışverişlerde 200 puan. Katılım gerekli.',
     'SI', '2026-07-01T00:00:00', '2026-07-31T23:59:59',
     1000.00, NULL, 200.00, 600.00, 'K', 'E', 'A', 'Ongoing', 1);

SET @Campaign = SCOPE_IDENTITY();
INSERT INTO CAMPAIGN_MERCHANT (CampaignId, MerchantId) VALUES (@Campaign, 9), (@Campaign, 10);
INSERT INTO CAMPAIGN_TRANSACTION_CODE (CampaignId, TransactionCodeId) VALUES (@Campaign, 1);

-- ─────────────────────────────────────────────
-- 7. Ağustos — Yaz İndirimi (hâlâ sürüyor)
-- ─────────────────────────────────────────────
-- Still inside its period, so the batch moves it no further than Ongoing. Useful for looking
-- at the preview endpoint while a campaign is live.

INSERT INTO CAMPAIGN
    (Name, Description, CampaignType, StartDate, EndDate, MinimumAmount, MaximumAmount,
     RewardPoint, MaxRewardAmount, EarningType, Gender, CardType, Status, IsActive)
VALUES
    (N'Yaz İndirimi Giyim Kampanyası',
     N'LC Waikiki''de 200 TL üzeri alışverişlerde 30 puan.',
     'MASS', '2026-08-01T00:00:00', '2026-08-31T23:59:59',
     200.00, NULL, 30.00, 300.00, 'K', NULL, NULL, 'Ongoing', 1);

SET @Campaign = SCOPE_IDENTITY();
INSERT INTO CAMPAIGN_MERCHANT (CampaignId, MerchantId) VALUES (@Campaign, 11);
INSERT INTO CAMPAIGN_TRANSACTION_CODE (CampaignId, TransactionCodeId) VALUES (@Campaign, 1);

-- ─────────────────────────────────────────────
-- 8. Eylül — Okula Dönüş (henüz başlamadı)
-- ─────────────────────────────────────────────
-- Starts in the future, so it sits at Pending and no transaction can reach it yet. Shows the
-- first state of the lifecycle, which the other seven skip past.

INSERT INTO CAMPAIGN
    (Name, Description, CampaignType, StartDate, EndDate, MinimumAmount, MaximumAmount,
     RewardPoint, MaxRewardAmount, EarningType, Gender, CardType, Status, IsActive)
VALUES
    (N'Okula Dönüş Kampanyası',
     N'Öğrenci segmentine kitap ve kırtasiye harcamalarında 40 puan.',
     'MASS', '2026-09-01T00:00:00', '2026-09-30T23:59:59',
     100.00, NULL, 40.00, 400.00, 'K', NULL, NULL, 'Pending', 1);

SET @Campaign = SCOPE_IDENTITY();
INSERT INTO CAMPAIGN_SEGMENT (CampaignId, SegmentId) VALUES (@Campaign, 1);
INSERT INTO CAMPAIGN_TRANSACTION_CODE (CampaignId, TransactionCodeId) VALUES (@Campaign, 1);

COMMIT TRANSACTION;

PRINT 'Example campaigns created.';
GO

-- ─────────────────────────────────────────────
-- What was created
-- ─────────────────────────────────────────────

SELECT c.Id,
       LEFT(c.Name, 38)                                        AS Kampanya,
       c.CampaignType                                          AS Tur,
       c.EarningType                                           AS Seviye,
       ISNULL(c.Gender, '-')                                   AS Cins,
       ISNULL(c.CardType, '-')                                 AS Kart,
       CONVERT(varchar(10), c.StartDate, 104) + ' - ' + CONVERT(varchar(10), c.EndDate, 104) AS Donem,
       (SELECT COUNT(*) FROM CAMPAIGN_SEGMENT s WHERE s.CampaignId = c.Id)          AS Seg,
       (SELECT COUNT(*) FROM CAMPAIGN_PRODUCT p WHERE p.CampaignId = c.Id)          AS Uru,
       (SELECT COUNT(*) FROM CAMPAIGN_MERCHANT m WHERE m.CampaignId = c.Id)         AS Isy,
       (SELECT COUNT(*) FROM CAMPAIGN_TRANSACTION_CODE t WHERE t.CampaignId = c.Id) AS Kod
FROM CAMPAIGN c
ORDER BY c.Id;
