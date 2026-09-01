# docs/analysis — öneri motoru doğrulama araçları

Bu klasör, Kampanya Öneri Motoru'nu sentetik veriyle test etmek için kullanılan
yeniden üretilebilir betikleri içerir. **Bulguların anlatımı ana rapordadır:**
[`../kampanya-oneri-motoru-rapor.md`](../kampanya-oneri-motoru-rapor.md) (Bölüm B).

| Dosya | Ne yapar |
|---|---|
| `generate_and_analyze.py` | `python generate_and_analyze.py [SEED] [--randomize]` — sıfırdan ~50.000 satırlık gerçekçi harcama veri seti üretir (`_out/dataset_<seed>.sql`) ve aynı veriden 6 istatistiksel yöntemle bağımsız bir kampanya sıralaması çıkarır (`_out/independent_ranking_<seed>.json`, `_out/aggregates_<seed>.json`). `--randomize`: kategori payları, sezon eğrileri ve yapısal kırılımlar da rastgele. |
| `compare.py` | DS1 için bağımsız sıralamayı, uygulamanın `GET /api/campaign-recommendations` çıktısıyla (`_out/app_ranking_<seed>.json`) karşılaştırır → `karsilastirma-raporu.md`. |
| `weight_sweep.py` | Motorun skor formülünü Python'da taklit eder, gerçek uygulamayla doğrular, sonra 3 veri setinde ağırlık ızgarasını tarar (iki ölçüt: bağımsız sıraya uyum + enjekte edilen gerçek trende uyum) → `weight_sweep_sonuc.md`. |
| `karsilastirma-raporu.md`, `weight_sweep_sonuc.md` | Betiklerin ürettiği özet tablolar (otomatik). |
| `_out/` | Üretilen çıktılar (dataset SQL, JSON) — gitignore. |

Kullanılan veri setleri: **DS1** `20260901` (elle kurgulanmış), **DS2** `40271 --randomize`,
**DS3** `778213 --randomize`. Çalıştırma adımları ana raporun **§B.8** bölümünde.
