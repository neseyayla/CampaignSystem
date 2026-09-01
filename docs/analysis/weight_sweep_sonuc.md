# Ağırlık Taraması — 3 Veri Seti

| Veri seti | Tür | Enjekte kırılım (gerçek trend) |
|---|---|---|
| DS`20260901` | elle kurgulanmış | Kırtasiye ×1.70, Eğitim ×1.60, Kozmetik ×1.90, Turizm ×0.55 |
| DS`40271` | rastgele | Eğitim ×1.28, Araç Kiralama ×0.52, Turizm ×0.75, Mobilya ×1.11, Sigorta ×0.47, Kozmetik ×1.22, Gıda ×1.46 |
| DS`778213` | rastgele | Kozmetik ×1.43, Telekomünikasyon ×0.61, Eğitim ×0.68, Elektronik ×0.61, Spor ×0.68 |

## 1. Motor replikası doğru mu?

Python replikası (mevcut ağırlıklarla) ile gerçek `GET /api/campaign-recommendations` çıktısının karşılaştırması:

| Veri seti | Spearman ρ (replika sırası ↔ uygulama sırası) | Ort. trend farkı |
|---|--:|--:|
| DS`20260901` | 0.995 | 0.010 |
| DS`40271` | 0.999 | 0.015 |
| DS`778213` | 0.999 | 0.008 |

ρ ≈ 1 → replika, motorun kararlarını sadık biçimde yeniden üretiyor; ağırlık taraması gerçek motoru temsil eder.

## 2. Mevcut vs önerilen vs en iyi

- **rho_comp**: motor sırası ↔ bağımsız kompozit sıra (yüksek = daha iyi)
- **rho_truth**: motor skoru ↔ enjekte edilen gerçek trend, yalnız kırılımlı kategoriler (yüksek = daha iyi)

| Ağırlıklar (Ws, Wt, Wse) | rho_comp DS1 / DS2 / DS3 (ort.) | rho_truth DS1 / DS2 / DS3 (ort.) |
|---|--:|--:|
| **MEVCUT** (1.0, 1.5, 1.25) | 0.87 / 0.69 / 0.82  (**0.79**) | -0.20 / 0.37 / 0.70  (**0.29**) |
| **ÖNERİLEN (revize)** (0.85, 2.0, 1.0) | 0.89 / 0.79 / 0.80  (**0.83**) | 0.40 / 0.43 / 0.70  (**0.51**) |
| en iyi rho_comp (1.0, 2.2, 1.0) | 0.90 / 0.79 / 0.81  (**0.84**) | 0.40 / 0.43 / 0.70  (**0.51**) |
| en iyi rho_truth (0.4, 1.0, 1.0) | 0.89 / 0.75 / 0.78  (**0.80**) | 0.40 / 0.43 / 0.70  (**0.51**) |
| en iyi birleşik (1.0, 2.2, 1.0) | 0.90 / 0.79 / 0.81  (**0.84**) | 0.40 / 0.43 / 0.70  (**0.51**) |

## 3. `SpendWeight` × `TrendWeight` etkisi

Her hücre = 3 veri setinde ortalama **birleşik skor** ((rho_comp + rho_truth) / 2).

### Wse = 1.0 (sezon önceli teriminin ağırlığı)

| Ws \ Wt | 1.0 | 1.3 | 1.6 | 1.9 | 2.2 | 2.5 |
|---|--:|--:|--:|--:|--:|--:|
| **0.40** | 0.66 | 0.64 | 0.64 | 0.61 | 0.61 | 0.61 |
| **0.55** | 0.65 | 0.66 | 0.66 | 0.62 | 0.62 | 0.61 |
| **0.70** | 0.54 | 0.65 | 0.67 | 0.66 | 0.62 | 0.62 |
| **0.85** | 0.54 | 0.65 | 0.67 | 0.67 | 0.62 | 0.62 |
| **1.00** | 0.53 | 0.54 | 0.65 | 0.67 | 0.67 | 0.64 |

### Wse = 1.25 (sezon önceli teriminin ağırlığı)

| Ws \ Wt | 1.0 | 1.3 | 1.6 | 1.9 | 2.2 | 2.5 |
|---|--:|--:|--:|--:|--:|--:|
| **0.40** | 0.63 | 0.64 | 0.65 | 0.64 | 0.61 | 0.61 |
| **0.55** | 0.62 | 0.65 | 0.65 | 0.65 | 0.61 | 0.61 |
| **0.70** | 0.53 | 0.65 | 0.67 | 0.66 | 0.65 | 0.62 |
| **0.85** | 0.52 | 0.54 | 0.65 | 0.67 | 0.67 | 0.62 |
| **1.00** | 0.52 | 0.54 | 0.64 | 0.67 | 0.67 | 0.67 |

## 4. Veri seti bazında gerçek-trend uyumu (rho_truth)

| Ağırlıklar | DS1 | DS2 | DS3 |
|---|--:|--:|--:|
| MEVCUT (1.0, 1.5, 1.25) | -0.20 | 0.37 | 0.70 |
| ÖNERİLEN (revize) (0.85, 2.0, 1.0) | 0.40 | 0.43 | 0.70 |
| en iyi birleşik (1.0, 2.2, 1.0) | 0.40 | 0.43 | 0.70 |

## 5. Yorum

Ayrıntılı değerlendirme ana raporda (`docs/kampanya-oneri-motoru-rapor.md`, §B.6).