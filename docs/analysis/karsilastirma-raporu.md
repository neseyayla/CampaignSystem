# Bağımsız Analiz vs. Uygulama Motoru — Karşılaştırma

**Veri seti:** `generate_and_analyze.py`, seed 20260901 · 10,735 işlem (son 90 gün penceresinde) · 22 kategori · 14 aylık geçmiş.
**Uygulama:** `GET /api/campaign-recommendations` taze `CampaignSystem_AnalysisTest` DB'sine karşı çalıştırıldı (migration + veri yüklendi).

Karşılaştırma **kapsam boşluğu olan 19 kategori** üzerinden yapılır — kapsanan 3 kategori (Gıda/Market, Akaryakıt, Sağlık) her iki yöntemde de listeden düşer.

## 1. Sıralama uyumu

Motor sırası ↔ bağımsız kompozit sıra: **Spearman ρ = 0.872** (p = 0.0000).

| Bağımsız yöntem | Motor skoruyla Spearman ρ | p |
|---|---:|---:|
| M1 net harcama | +0.255 | 0.2924 |
| M2 iki-yarı oranı | +0.750 | 0.0002 |
| M3 OLS eğim (norm.) | +0.717 | 0.0006 |
| M4 Mann-Kendall tau | +0.665 | 0.0019 |
| M5 momentum z | +0.750 | 0.0002 |
| M6 ampirik sezon | +0.313 | 0.1922 |
| kompozit | +0.872 | 0.0000 |

Yorum: motor ile en yüksek uyum **M1 (net harcama)** ve **M2 (iki-yarı oranı)** ile — beklenen, çünkü motorun skoru bu ikisinin ağırlıklı toplamı. OLS eğim / Mann-Kendall / momentum ile uyum daha düşük: motor trendi tek bir orana indirger, istatistiksel anlamlılık (p-değeri) kullanmaz.

## 2. İlk 12 kategori — yan yana

| # motor | # bağımsız | Δ | Kategori | Motor skor | Net harcama | Motor trend | OLS eğim (p) | MK τ (p) | Mom. z | Ampirik sezon | Motor sezon önceli |
|--:|--:|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|
| 1 | 1 | +0 | Kırtasiye / Oyuncak | 4.32 | 163,058 | +1.37 | +1.89 (0.00) | +0.67 (0.00) | +2.45 | 1.11 | 1.25 |
| 2 | 2 | +0 | Eğitim | 2.63 | 874,471 | +0.33 | +0.53 (0.07) | +0.24 (0.31) | +0.84 | 1.26 | 1.35 |
| 3 | 3 | +0 | Giyim | 1.86 | 562,924 | +0.30 | +0.41 (0.01) | +0.27 (0.25) | +0.46 | 1.19 | 1.20 |
| 4 | 4 | +0 | Havayolları / Ulaşım | 1.75 | 566,717 | +0.42 | +0.62 (0.07) | +0.48 (0.03) | +0.42 | 1.04 | 1.00 |
| 5 | 8 | -3 | Elektronik | 1.43 | 1,149,373 | -0.04 | -0.02 (0.97) | -0.09 (0.74) | +0.22 | 0.98 | 1.10 |
| 6 | 13 | -7 | Mobilya & Ev Tekstili | 1.39 | 621,311 | +0.22 | +0.18 (0.68) | +0.09 (0.74) | +0.04 | 0.92 | 1.05 |
| 7 | 5 | +2 | Ayakkabı & Aksesuar | 1.30 | 166,843 | +0.28 | +0.27 (0.40) | +0.18 (0.46) | -0.02 | 1.18 | 1.18 |
| 8 | 12 | -4 | Yapı & İnşaat | 1.24 | 498,080 | +0.20 | +0.14 (0.68) | +0.09 (0.74) | -0.04 | 0.97 | 1.07 |
| 9 | 9 | +0 | Turizm / Seyahat / Otel | 1.23 | 1,542,283 | -0.26 | -0.28 (0.38) | -0.36 (0.12) | -0.26 | 1.05 | 1.07 |
| 10 | 6 | +4 | Kozmetik | 1.22 | 183,098 | +0.38 | +0.51 (0.03) | +0.24 (0.31) | +0.51 | 1.07 | 1.00 |
| 11 | 7 | +4 | Restoran / Yeme-İçme | 0.98 | 687,893 | +0.08 | +0.24 (0.03) | +0.33 (0.15) | +0.40 | 0.93 | 1.00 |
| 12 | 11 | +1 | Eğlence | 0.81 | 138,660 | +0.25 | +0.31 (0.13) | +0.36 (0.12) | +0.35 | 1.01 | 1.00 |

## 3. Belirgin ayrışmalar ve kök nedenleri

### Mobilya & Ev Tekstili — motor #6, bağımsız #13 (Δ -7)

- Net harcama 621,311 ₺ · motor trend +0.22 · OLS eğim +0.18 (p=0.68) · MK τ +0.09 (p=0.74) · momentum z +0.04
- Ampirik sezon 0.92 · motorun sezon önceli 1.05

### Restoran / Yeme-İçme — motor #11, bağımsız #7 (Δ +4)

- Net harcama 687,893 ₺ · motor trend +0.08 · OLS eğim +0.24 (p=0.03) · MK τ +0.33 (p=0.15) · momentum z +0.40
- Ampirik sezon 0.93 · motorun sezon önceli 1.00

### Yapı & İnşaat — motor #8, bağımsız #12 (Δ -4)

- Net harcama 498,080 ₺ · motor trend +0.20 · OLS eğim +0.14 (p=0.68) · MK τ +0.09 (p=0.74) · momentum z -0.04
- Ampirik sezon 0.97 · motorun sezon önceli 1.07

### Kuyumculuk / Saat — motor #15, bağımsız #19 (Δ -4)

- Net harcama 739,514 ₺ · motor trend -0.06 · OLS eğim -0.53 (p=0.38) · MK τ -0.27 (p=0.25) · momentum z -0.33
- Ampirik sezon 0.71 · motorun sezon önceli 0.95

### Kozmetik — motor #10, bağımsız #6 (Δ +4)

- Net harcama 183,098 ₺ · motor trend +0.38 · OLS eğim +0.51 (p=0.03) · MK τ +0.24 (p=0.31) · momentum z +0.51
- Ampirik sezon 1.07 · motorun sezon önceli 1.00

### Elektronik — motor #5, bağımsız #8 (Δ -3)

- Net harcama 1,149,373 ₺ · motor trend -0.04 · OLS eğim -0.02 (p=0.97) · MK τ -0.09 (p=0.74) · momentum z +0.22
- Ampirik sezon 0.98 · motorun sezon önceli 1.10

## 4. Genel bulgular

Ayrıntılı yorum ve `RecommendationOptions` ayar önerileri ana raporda (`docs/analysis/veri-seti-ve-karsilastirma-raporu.*`).
