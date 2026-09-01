# docs/analysis — öneri motoru doğrulama araçları

Bu klasör, Kampanya Öneri Motoru'nu sentetik veriyle test etmek için kullanılan
yeniden üretilebilir betikleri içerir. **Bulguların anlatımı ana rapordadır:**
[`../kampanya-oneri-motoru-rapor.md`](../kampanya-oneri-motoru-rapor.md) (Bölüm B).

| Dosya | Ne yapar |
|---|---|
| `generate_and_analyze.py` | Sıfırdan ~50.000 satırlık gerçekçi harcama veri seti üretir (`_out/dataset.sql`) ve aynı veriden 6 istatistiksel yöntemle bağımsız bir kampanya sıralaması çıkarır (`_out/independent_ranking.json`). Seed `20260901` — deterministik. |
| `compare.py` | Bağımsız sıralamayı, uygulamanın `GET /api/campaign-recommendations` çıktısıyla (`_out/app_ranking_all.json`) karşılaştırır; Spearman korelasyonu + yan yana tablo → `karsilastirma-raporu.md`. |
| `karsilastirma-raporu.md` | `compare.py`'nin ürettiği özet tablo (otomatik). |
| `_out/` | Üretilen çıktılar — gitignore. `python generate_and_analyze.py` ile yeniden oluşur. |

Çalıştırma adımları ana raporun **§B.8** bölümünde.
