# CampaignSystem — proje devri

Bankacılık kredi kartı kampanya sistemi geliştiriyorum. Önceki sohbetin devamı.

## Proje
- Yol: C:\Users\neşe\source\repos\CampaignSystem
- Repo: github.com/neseyayla/CampaignSystem
- ASP.NET Core Web API, .NET 10, controller tabanlı, OpenAPI + Swagger UI kurulu
- ORM: EF Core (Code First + migration)
- Veritabanı: MSSQL
- Yapı: tek proje, klasörlü (Entities/, Enums/, Data/, Services/, Controllers/)
  Ayrı class library projeleri YOK, bilinçli tercih.

## Git stratejisi
main <- dev <- feature/*
Her adım dev'den açılan kısa ömürlü feature dalında yapılır, GitHub'da PR ile dev'e
merge edilir. Commit mesajları Conventional Commits (feat:, chore:, docs:).
Git komutlarını BEN çalıştırıyorum — bana komutları ver, sen çalıştırma.

Şu an: feature/entities dalındayım. dev'de son commit 5eef83d (PR #1 merge).

## Şema
docs/schema.dbml        -> tek doğruluk kaynağı, dbdiagram.io kaynağı
docs/database-design.md -> tasarım dokümanı (mermaid ER diyagramı gömülü)

14 tablo: SEGMENT, PRODUCT, MERCHANT, TRANSACTION_CODE, CUSTOMER, CARD, CAMPAIGN,
CAMPAIGN_SEGMENT, CAMPAIGN_PRODUCT, CAMPAIGN_MERCHANT, CAMPAIGN_TRANSACTION_CODE,
CAMPAIGN_PARTICIPATION, TRANSACTION, CAMPAIGN_REWARD

Veritabanı adları UPPER_SNAKE_CASE, C# sınıfları PascalCase.
Eşleştirme Fluent API ile yapılacak (ToTable).

## Tamamlanan
1. Şema dokümantasyonu — PR #1 ile dev'e merge edildi
2. Entity ve enum sınıfları yazıldı, proje derleniyor (0 hata)
   - CampaignSystem/Entities/ — 14 sınıf
   - CampaignSystem/Enums/   — 6 enum
   - HENÜZ COMMIT EDİLMEDİ

## Entity yazım kuralları (kurulmuş, devam ettirilecek)
- Saf POCO — hiçbirinde using Microsoft.EntityFrameworkCore yok
- Data Annotations attribute'u YOK, tüm kurallar Fluent API'ye gidecek
  (sebep: composite PK, filtered unique index, 3 kolonlu unique constraint
   attribute ile yazılamaz)
- Zorunlu string alanlar: = null!;   Opsiyonel: string?
- Koleksiyonlar: ICollection<T> ... = [];
- Her ilişki iki uçtan da yazıldı (FK property + navigation property)
- bigint -> long, decimal para alanları (asla double)
- Durum alanları enum, veritabanına string olarak gidecek (value converter ile)
- Kod, yorum ve dokümanlar İNGİLİZCE yazılıyor

## Enum semantiği — DİKKAT
EarningType, kampanyanın kart bazlı mı müşteri bazlı mı olduğunu belirtir:
  CardBased = 1      (K) birikim kart bazında, CampaignReward.CardId dolu
  CustomerBased = 2  (M) birikim müşteri bazında, CardId null

Bu alan eskiden "Tek Kazanım / Sürekli Kazanım" (TK/SK) anlamındaydı ve ayrı bir
Level alanı vardı. Level kaldırıldı, anlamı EarningType'a taşındı.
Enums/EarningType.cs güncellendi AMA DOKÜMANLAR HÂLÂ ESKİ ANLAMI YAZIYOR.

## Yapılacak ilk iş
1. docs/schema.dbml (73. satır) ve docs/database-design.md (250, 359-373, 434.
   satırlar) içindeki "TK = one-time, SK = recurring" ifadelerini yeni anlama göre
   düzelt. Ödül hesaplama bölümü EarningType'a göre GROUP BY CardId / CustomerId
   şeklinde olmalı.
2. Entity + enum + doküman değişikliklerini commit et, PR ile dev'e merge et.

## Sonraki adımlar
3. Veri katmanı: EF Core paketleri, CampaignDbContext, Data/Configurations/ altında
   Fluent API konfigürasyonları, connection string (User Secrets ile —
   appsettings.Development.json git'te takip ediliyor, oraya YAZMA), DI kaydı
4. İlk migration + MSSQL'de veritabanı oluşturma + seed data
5. Repository / servis metotları
6. Controller ve endpoint'ler

## Bilinen açık konular
- TRANSACTION tablosunda IsReversed alanı yok — iade edilen işlem ödüle dahil oluyor
- CAMPAIGN_REWARD tablosunda Status alanı yok — ödülün yüklendiği izlenemiyor
- "Ödül bir kez mi, her işlemde mi" sorusunun ayrı alanı yok; MaxRewardAmount =
  RewardPoint yaparak tek kazanım ifade edilebilir (doğrulanmadı)
- warning NU1903: Microsoft.OpenApi 2.0.0 güvenlik açığı (Microsoft.AspNetCore.OpenApi
  dolaylı bağımlılığı, baştan beri var, henüz ele alınmadı)

## Çalışma tarzım
- Benimle TÜRKÇE konuş, kodu ve dokümanları İngilizce yaz
- Dosya değiştirmeden ÖNCE ne yapacağını anlat, onayımı al
- Git komutlarını bana ver, sen çalıştırma
- Adım adım ilerleyelim, her adımı açıklayarak

## Çalışma dizinindeki commit'lenmemiş değişiklikler
 M CampaignSystem/Controllers/WeatherForecastController.cs   (benim değişikliğim)
 M CampaignSystem/Program.cs                                  (benim değişikliğim)
?? CampaignSystem/Entities/
?? CampaignSystem/Enums/

Önce bu dosyaları oku, durumu doğrula, sonra yukarıdaki 1. maddeden başla.
