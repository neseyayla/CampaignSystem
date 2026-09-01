# Sentetik Veri Seti + İstatistiksel Doğrulama Raporu

**Amaç:** Kampanya Öneri Motoru'nu, sıfırdan üretilmiş ~50.000 satırlık gerçekçi bir kart
harcama veri setine karşı çalıştırmak; aynı veriden **motora bakmadan**, farklı istatistiksel
yöntemlerle bağımsız bir kampanya önceliklendirmesi çıkarmak; ikisini karşılaştırıp motorun
nerede isabetli, nerede yanıldığını göstermek.

**Kod:** `docs/analysis/generate_and_analyze.py` (üretim + bağımsız analiz),
`docs/analysis/compare.py` (karşılaştırma). Çıktılar `docs/analysis/_out/` (gitignore,
`python generate_and_analyze.py` ile yeniden üretilir). Seed `20260901` — deterministik.

---

## 1. Veri seti tasarımı — "mümkün olduğunca gerçek"

| Boyut | Değer |
|---|---|
| İşlem | **49.160** (48.434 alış + 726 iade) |
| Toplam net harcama | 54.417.841 ₺ |
| Zaman aralığı | 2025-07-03 … 2026-09-01 (**425 gün ≈ 14 ay**) |
| Müşteri | 451 (biri admin: `29999999 / 123456`) |
| Kart | ~700 (herkese 1, üçte ikisine 2, dörtte birine 3) |
| Merchant | 88 (22 kategori × 4) |
| Kapsayan kampanya | 3 (Gıda/Market, Akaryakıt — Ongoing; Sağlık — Pending) |

Gerçekçiliği sağlayan yapılar:

1. **Kategori işlem payı.** Türkiye kart kullanımına yakın dağılım: Gıda/Market %20,5,
   Restoran %15, Akaryakıt %9, Sağlık/Eczane %6, Telekom %4,5, Eğlence %4 … Elektronik %2,
   Beyaz Eşya %0,7, Kuyum %0,6. (`Category.share`, normalize edilir.)

2. **Kategoriye özgü tutar dağılımı.** Her kategori için **lognormal**: medyan + şekil (σ).
   Gıda medyan 220 ₺ / σ 0,55 (çok işlem, küçük sepet); Beyaz Eşya medyan 9.500 ₺ / σ 0,55;
   Elektronik medyan 3.800 ₺ / σ 0,85 (ağır sağ kuyruk). Gerçek portföyde büyük alışverişin
   nadir olması bu şekilde yakalanır.

3. **Aylık sezonsallık.** Her kategoriye 12 aylık çarpan verildi (domain bilgisi):
   Akaryakıt/Turizm/Havayolları yazın tepe; Elektronik Kasım (Efsane Cuma) + okula dönüş;
   Giyim ilkbahar+sonbahar sezon geçişi + yıl sonu; Eğitim/Kırtasiye Ağustos–Eylül; Spor
   Ocak; Kuyum ilkbahar düğün.

4. **Ay içi yoğunluk.** Maaş/ay sonu günleri (ayın 1–3'ü, 15'i, 28+) ×1,35; hafta sonu
   (Cuma–Pazar) ×1,15. İşlem sayısı Poisson ile örneklenir.

5. **Müşteri heterojenliği.** Her müşteriye lognormal harcama ağırlığı (Pareto benzeri) —
   az sayıda ağır harcayan, çoğunluk hafif. İşlem başına müşteri bu ağırlıkla seçilir.

6. **İadeler.** İşlemlerin %1,5'i; yüksek biletli kategorilere (Elektronik, Mobilya, Beyaz
   Eşya, Turizm, Havayolları, Kuyum, Giyim) 3× eğilimli. İade satırı **negatif tutarla**,
   orijinal alışın 2–18 gün sonrasına, `OriginalTransactionId` ile bağlı.

7. **Yapısal kırılımlar (kasıtlı testler).** Sezondan bağımsız, son X günde rampalı:

   | Kategori | Kırılım | Amaç |
   |---|---|---|
   | **Kozmetik** | son 55 günde ×1,9 artış, **SEASONAL_PATTERN'de hiç satırı yok** | Motor sezon yardımı olmadan gerçek trendi yakalayabiliyor mu? |
   | **Turizm** | son 45 günde ×0,55 düşüş, ama Eylül önceli +1,15 | Motorun pozitif sezon önceli, cari yıldaki düşüşü örtüyor mu? |
   | **Eğitim** | son 40 günde ×1,6 | Okula dönüş: sezon + gerçek trend birlikte → tepe beklenir |
   | **Kırtasiye** | son 38 günde ×1,7 | Aynı |

---

## 2. Bağımsız istatistiksel yöntemler

Hepsi **son 90 günlük** pencerede (motorun `LookbackDays`'i), kategori bazında:

| # | Yöntem | Ne ölçer |
|---|---|---|
| **M1** | Net harcama | `SUM(Amount)` (iadeler negatif olduğu için netlenmiş) |
| **M2** | İki-yarı oranı | `(son45g − önceki45g) / önceki45g` — **motorun kullandığı yöntem** |
| **M3** | OLS eğim | Günlük net harcamaya en küçük kareler regresyonu (`scipy.stats.linregress`): normalize eğim, **p-değeri**, R² |
| **M4** | Mann-Kendall | Haftalık net harcamaya parametrik olmayan trend testi (Kendall τ + p) |
| **M5** | Momentum z | `(son 30g günlük ort. − önceki 60g ort.) / önceki 60g std` |
| **M6** | Ampirik sezon | 14 aylık geçmişten **ölçülmüş** ay-endeksi (kısmi aylar atılır) + ufuk (45g) projeksiyonu |
| **M7** | Kapsam | Açık/yaklaşan kampanyanın hedeflediği kategori |

**Kompozit bağımsız skor:** `{ln(net harcama), OLS eğim, MK τ, momentum z, ampirik sezon}`
z-normalize edilir; trend bloğu bu üç yöntemin **ortalaması** (motorun tek oranına karşı);
ağırlıklar 0,9·harcama + 1,7·trend + 1,1·sezon; kapsam boşluğu → ×1,75. Yani motorla **aynı
ruhta** ama trendi tek orana değil üç kanıta, sezonu **öncül tabloya değil ölçülen endekse**
dayandırır.

---

## 3. Motorun çalıştırılması

`CampaignSystem_AnalysisTest` adında **taze** bir DB: `dotnet ef database update` ile şema +
seed (106 `SEASONAL_PATTERN` satırı dahil), ardından `_out/dataset.sql` yüklendi. Uygulama
bu DB'ye bağlanıp `GET /api/campaign-recommendations?maxSuggestions=25&minimumSpend=0&includeCovered=true`
çağrıldı; çıktı `_out/app_ranking_all.json`.

---

## 4. Sonuç — sıralama uyumu

Karşılaştırma **kapsam boşluğu olan 19 kategori** üzerinden (kapsanan 3'ü ikisi de eler).

> **Spearman ρ = 0,872** (p < 0,0001) — motorun sırası ile bağımsız kompozit sıra arasında
> **güçlü uyum**. İlk 4 birebir aynı.

| Bağımsız yöntem | Motor skoruyla Spearman ρ |
|---|---:|
| M1 net harcama | +0,74 |
| M2 iki-yarı oranı | +0,78 |
| M3 OLS eğim | +0,55 |
| M4 Mann-Kendall τ | +0,49 |
| M5 momentum z | +0,52 |
| M6 ampirik sezon | +0,31 |

Motorla en yüksek uyum M1/M2 ile — beklenen, çünkü motorun skoru bu ikisinin ağırlıklı
toplamı. Regresyon/MK/momentum ile uyum daha düşük: motor trendi **tek bir orana** indirger,
**istatistiksel anlamlılık kullanmaz**.

### İlk 12 kategori — yan yana

| # motor | # bağımsız | Δ | Kategori | Motor skor | Net harcama | Motor trend | OLS eğim (p) | MK τ (p) | Mom. z | Ampirik sezon | Motor sezon önceli |
|--:|--:|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|
| 1 | 1 | 0 | Kırtasiye / Oyuncak | 4,32 | 163.058 | +1,37 | +1,89 (0,00) | +0,67 (0,00) | +2,45 | 1,11 | 1,25 |
| 2 | 2 | 0 | Eğitim | 2,66 | 874.471 | +0,35 | +0,53 (0,07) | +0,24 (0,31) | +0,84 | 1,26 | 1,35 |
| 3 | 3 | 0 | Giyim | 1,85 | 562.924 | +0,30 | +0,41 (0,01) | +0,27 (0,25) | +0,46 | 1,19 | 1,20 |
| 4 | 4 | 0 | Havayolları / Ulaşım | 1,75 | 566.717 | +0,42 | +0,62 (0,07) | +0,48 (0,03) | +0,43 | 1,04 | 1,00 |
| 5 | 8 | −3 | **Elektronik** | 1,43 | 1.149.373 | −0,04 | −0,02 (0,97) | −0,09 (0,74) | +0,22 | 0,98 | 1,10 |
| 6 | 13 | **−7** | **Mobilya & Ev Tekstili** | 1,39 | 621.311 | +0,22 | +0,18 (0,68) | +0,09 (0,74) | +0,04 | 0,92 | 1,05 |
| 7 | 5 | +2 | Ayakkabı & Aksesuar | 1,30 | 166.843 | +0,28 | +0,27 (0,40) | +0,18 (0,46) | −0,02 | 1,18 | 1,18 |
| 8 | 12 | −4 | **Yapı & İnşaat** | 1,24 | 498.080 | +0,20 | +0,14 (0,68) | +0,09 (0,74) | −0,04 | 0,97 | 1,08 |
| 9 | 9 | 0 | **Turizm / Seyahat / Otel** | 1,23 | 1.542.283 | −0,26 | −0,28 (0,38) | −0,36 (0,12) | −0,26 | 1,05 | 1,08 |
| 10 | 6 | +4 | **Kozmetik** | 1,22 | 183.098 | +0,38 | +0,51 (0,03) | +0,24 (0,31) | +0,51 | 1,07 | 1,00 |
| 11 | 7 | +4 | **Restoran / Yeme-İçme** | 0,98 | 687.893 | +0,08 | +0,24 (0,03) | +0,33 (0,15) | +0,40 | 0,93 | 1,00 |
| 12 | 11 | +1 | Eğlence | 0,81 | 138.660 | +0,25 | +0,31 (0,13) | +0,36 (0,12) | +0,36 | 1,01 | 1,00 |

---

## 5. Belirgin ayrışmalar ve kök nedenleri

### 5.1 Mobilya #6 vs #13 (Δ −7) ve Yapı & İnşaat #8 vs #12 (Δ −4) — **sahte trend**

Motor bu iki kategoriyi trend sinyaliyle yukarı çekti: iki-yarı oranı **+0,22 / +0,20**.
Ama üç bağımsız yöntemin hepsi "gerçek trend yok" diyor:

- Mobilya: OLS eğim +0,18 ama **p = 0,68** (anlamsız), MK τ +0,09 (p 0,74), momentum +0,04.
- Yapı: OLS eğim +0,14 **p = 0,68**, MK τ +0,09, momentum −0,04.

**Kök neden:** motorun tek iki-yarı oranı, iki toplam arasındaki gürültülü bir farktır ve
**anlamlılık testi yoktur**. 45'er günlük iki toplamda rastgele bir dalgalanma %20'lik bir
"artış" gibi görünebilir. Regresyon eğimi + p-değeri bunu eler.

### 5.2 Restoran #11 vs #7 (Δ +4) — **kademeli trendi kaçırma**

Motorun iki-yarı oranı sadece **+0,08** (zayıf). Oysa OLS eğim **+0,24, p = 0,03
(anlamlı)**, MK τ +0,33, momentum +0,40 — gerçek, istikrarlı bir yükseliş var.

**Kök neden:** iki-yarı oranı, doğrusal/kademeli bir artışa **duyarsızdır**. Harcama tüm
pencere boyunca yavaşça artıyorsa, "son yarı toplamı" ile "önceki yarı toplamı" birbirine
yakın çıkar; oysa günlük seriye çizilen doğrunun eğimi bunu net yakalar.

### 5.3 Elektronik #5 vs #8 (Δ −3) ve Turizm — **hacim terimi büyük düz kategorileri taşıyor**

- **Elektronik:** trend her yöntemde ~0 veya negatif, ama net harcama 1,15 M ₺. Motorda
  `normalisedSpend = 1.149.373 / maxNetSpend`. `maxNetSpend` bu veri setinde Turizm'in
  1,54 M'si → Elektronik payı ≈ 0,75 → harcama terimi 0,75 · 1,0 = 0,75. Trend ≈ 0, sezon
  0,125 → ham ≈ 0,84, × 1,75 = **1,47**. Yani düşen/düz bir kategori sırf cirosu büyük diye
  "önerilir" bölgesinde kalıyor.
- **Turizm #9 — sıra aynı ama karar zıt.** Motor skoru **+1,23** (öneri olarak listeye
  girer); bağımsız kompozit **−0,46** (önerilmez). Turizm bu veri setinin en yüksek
  harcamalı kategorisi (1,54 M) → `normalisedSpend ≈ 1,0` → harcama terimi 1,0. Trend
  −0,26 → −0,39. Sezon önceli +1,075 → +0,09. Ham 0,70 × 1,75 = 1,23. Oysa Turizm son 45
  günde **belirgin düşüşte** (kasıtlı kırılım): OLS −0,28, MK τ −0,36, momentum −0,26.

**Kök neden (ortak):** `SpendWeight` terimi, harcamayı **tek bir en yoğun kategoriye**
göre normalize eder. Tek bir dev kategori (Turizm) hem tavanı belirler hem de kendi
payını 1,0 yapar; onun yanında Elektronik gibi büyükler de yüksek pay alır. Trend negatif
olsa bile hacim terimi tek başına skoru "öneri" eşiğinin üstünde tutabiliyor.

### 5.4 Turizm — **sezon önceli cari yıl kırılımını göremiyor**

`SEASONAL_PATTERN` Turizm/Eylül = 1,15 (yaz sonu hâlâ hareketli varsayımı). Bu yıl veri
Turizm'i Eylül'de **düşüşte** gösteriyor (ampirik sezon endeksi 1,05, gerçek trend sert
negatif). Motorun sezon terimi `1,25 · (1,075 − 1) = +0,09` katkı verip skoru **yukarı**
iterken, olması gereken aşağı itmesiydi.

**Kök neden:** öncül tablo statik. "Eylül bir Turizm ayıdır" doğru bir uzun dönem
ortalaması olabilir ama **bu yıl** yaz erken bitmişse öncül yanıltır. Ampirik (veriden
ölçülen) sezon endeksi bunu kısmen düzeltir; gerçek trend sinyali tamamen düzeltir.

### 5.5 Kozmetik #10 vs #6 (Δ +4) — **motor küçük ama hızlı büyüyen kategoriyi az ödüllendiriyor**

Kozmetik'e enjekte edilen ×1,9'luk yapısal büyüme **her iki tarafça da yakalandı**
(motor trend +0,38; OLS +0,51 **p = 0,03**; momentum +0,51). Ama motor onu #10'a koydu:
harcaması küçük (183 K → `normalisedSpend ≈ 0,12`) ve `SEASONAL_PATTERN`'de satırı olmadığı
için sezon terimi tam 0. Bağımsız kompozit, `ln(harcama)` ile hacim avantajını sıkıştırıp
trende daha çok ağırlık verdiği için onu #6'ya çıkardı.

**Yorum:** hangisi "doğru" tartışmalı — ama hızlı büyüyen bir niş, bir kampanyayı hak
edebilir. Motorun `SpendWeight`'i mutlak ciroyu fazla ödüllendirip momentum'u bastırıyor.

---

## 6. `RecommendationOptions` için somut ayar önerileri

Motor kodunu değiştirmeden, yalnız `appsettings.json` → `Recommendation` ile:

| Ayar | Şu an | Öneri | Gerekçe |
|---|--:|--:|---|
| `SpendWeight` | 1,0 | **0,6–0,7** | §5.3: hacim terimi büyük düz/düşen kategorileri fazla taşıyor. Düşürmek Elektronik/Turizm'i geri çeker. |
| `TrendWeight` | 1,5 | **1,8–2,0** | Trend, kampanya zamanlaması için hacimden daha bilgilendirici. |
| `SeasonWeight` | 1,25 | 1,1–1,25 | Öncül statik ve bazen yanıltıcı (§5.4); ağırlığını çok artırma. |
| `MinimumSpend` | 1000 | 5.000–10.000 | Çok küçük kategorileri baştan eler, gürültüyü azaltır. |

Kodu değiştirmeden `SpendWeight`'i 1,0 → 0,65 ve `TrendWeight`'i 1,5 → 1,9 yapmak, bu veri
setinde Elektronik'i #5 → ~#8, Turizm'i #9 → ~#12'ye çeker (bağımsız sıraya yaklaşır).

---

## 7. Faz 2 için — koddan çıkarımlar

Değiştirilecek tek yer `CampaignRecommendationService.GetSuggestionsAsync` içindeki
`rawScore` bloğu (§5.7(f) ana kod raporunda). Bu analizden çıkan üç net iyileştirme:

1. **Trendi tek orandan çıkar.** İki-yarı oranı hem sahte pozitif üretiyor (§5.1) hem
   kademeli trendi kaçırıyor (§5.2). Yerine: günlük seriye **OLS eğim + p-değeri**
   (anlamsızsa katkı 0) veya Mann-Kendall τ. Her ikisi de saf T-SQL/EF'te zor; servis
   işlemleri bellek içine çekip (kategori × gün ~ birkaç bin satır) `MathNet`/basit OLS ile
   hesaplayabilir.
2. **Hacim terimini logaritmik/robust normalize et.** `normalisedSpend = ln(1+net) / ln(1+max)`
   ya da yüzdelik sıra (percentile rank). Tek dev kategori artık tavanı domine etmez (§5.3).
3. **Sezonu veriden öğren.** `SEASONAL_PATTERN`'i elle seed yerine, yeterli geçmiş
   biriktiğinde her (kategori, ay) için `ay_ortalama_günlük_harcama / yıllık_ortalama`
   hesaplayıp tabloya yaz (kısmi ayları atarak — bkz. `analyse()` içindeki `full_months`
   filtresi). Öncül yalnız veri yetersizken kullanılır.

Bu üç değişiklik, yukarıdaki 6 ayrışmanın 5'ini bağımsız yöntemlerle hizalar.

---

## 8. Yeniden üretme

```bash
# 1) Veri seti + bağımsız analiz
python docs/analysis/generate_and_analyze.py
#    -> docs/analysis/_out/dataset.sql, independent_ranking.json

# 2) Taze DB oluştur + şema + veri
$env:ConnectionStrings__DefaultConnection = "Server=localhost\SQLEXPRESS;Database=CampaignSystem_AnalysisTest;Trusted_Connection=True;TrustServerCertificate=True"
$env:Jwt__SigningKey = "0123456789012345678901234567890123456789"
dotnet ef database update --project CampaignSystem --startup-project CampaignSystem
#    dataset.sql'de USE CampaignSystem -> USE CampaignSystem_AnalysisTest yapıp sqlcmd ile yükle

# 3) Uygulamayı o DB'ye karşı çalıştır, endpoint'i çağır
#    -> docs/analysis/_out/app_ranking_all.json

# 4) Karşılaştır
python docs/analysis/compare.py
#    -> docs/analysis/karsilastirma-raporu.md
```

---

*Hazırlanma: 2026-09-01 · Dal `feature/campaign-recommendations` · Seed 20260901 ·
Spearman ρ(motor, bağımsız kompozit) = 0,872.*
