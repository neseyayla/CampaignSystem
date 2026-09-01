"""
Kampanya Öneri Motoru — bağımsız veri seti + istatistiksel analiz.

Ne yapar:
  1. Sıfırdan ~50.000 satırlık, mümkün olduğunca gerçekçi bir kart harcama veri seti üretir
     (14 ay, 22 kategori, kategoriye özgü tutar dağılımları, aylık sezonsallık, ay sonu /
     hafta sonu etkileri, müşteri heterojenliği, ~%1.5 iade, birkaç yapısal trend kırılımı).
  2. Veri setinden BAĞIMSIZ olarak — uygulamanın motoruna bakmadan — hangi kategoride
     kampanya açılmaya değer olduğunu birden çok istatistiksel yöntemle çıkarır:
       M1  net harcama (son 90 gün)
       M2  iki-yarı oranı (motorun yöntemi)
       M3  günlük harcamaya OLS eğim (scipy.linregress) + p-değeri
       M4  haftalık harcamaya Mann-Kendall trend testi (tau + p)
       M5  momentum z-skoru (son 30g ort. vs önceki 60g ort./std)
       M6  ampirik sezonsallık (14 aylık geçmişten ay endeksi) + ufuk projeksiyonu
       M7  kapsam boşluğu (kurulan kampanyalar)
       kompozit  yukarıdakilerin z-normalize ağırlıklı birleşimi
  3. Çıktı:
       _out/dataset.sql              -> taze bir DB'ye yüklenecek (gitignore)
       _out/independent_ranking.json -> bağımsız sıralama
     ve konsola özet tablo.

Sonra uygulama bu veriye karşı çalıştırılıp /api/campaign-recommendations çıktısı
_out/app_ranking.json'a kaydedilir; compare.py ikisini karşılaştırır.

Çalıştır:  python docs/analysis/generate_and_analyze.py
"""

from __future__ import annotations

import json
import math
import os
import sys

try:                                    # Windows konsolu cp1254 olabilir
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
except Exception:
    pass
from dataclasses import dataclass, field
from datetime import date, datetime, timedelta

import numpy as np
import pandas as pd
from scipy import stats

SEED = 20260901
RNG = np.random.default_rng(SEED)

OUT_DIR = os.path.join(os.path.dirname(__file__), "_out")
os.makedirs(OUT_DIR, exist_ok=True)

# "Bugün" — motorun DateTime.Now'una denk. Veri bunun hemen öncesinde biter.
NOW = datetime(2026, 9, 1, 9, 0, 0)
HISTORY_DAYS = 425                       # ~14 ay
START = NOW - timedelta(days=HISTORY_DAYS)
LOOKBACK_DAYS = 90                       # motorun varsayılan penceresi
HORIZON_DAYS = 45                        # motorun varsayılan ufku
TARGET_ROWS = 50_000
N_CUSTOMERS = 450
MERCHANTS_PER_CATEGORY = 4

# Referans veri id'leri migration seed'inden sabit.
SEGMENT_IDS = [1, 2, 3, 4, 5]
PRODUCT_IDS = [1, 2, 3, 4, 5, 6]
SALE_TXN_CODE = 1
ADMIN_PW_HASH = ("AQAAAAIAAYagAAAAEJFQXNYaIC1YsFJirJtMW9NYhciP2xIaiqkgV"
                 "XxvIMOl7UgMCyyHioTSfbubY17Zlw==")


@dataclass
class Category:
    id: int
    name: str
    share: float                # işlem sayısı payı (normalize edilir)
    median: float               # tutar medyanı (lognormal ölçek)
    sigma: float                # lognormal şekil
    season: list[float]         # 12 aylık çarpan, index 0=Ocak
    # opsiyonel yapısal kırılım (sezondan bağımsız gerçek trend), son X günde:
    break_days: int = 0         # kaç gün önce başladı
    break_factor: float = 1.0   # kırılım sonunda ulaşılan çarpan (>1 artış, <1 azalış)


def flat(v: float = 1.0) -> list[float]:
    return [v] * 12


CATS: list[Category] = [
    Category(1,  "Gıda / Market",            0.205, 220,  0.55,
             [1.00, 0.98, 1.08, 1.02, 1.00, 1.00, 1.02, 1.00, 1.00, 1.00, 1.05, 1.12]),
    Category(2,  "Restoran / Yeme-İçme",     0.150, 300,  0.60,
             [0.90, 0.88, 0.95, 1.00, 1.05, 1.15, 1.20, 1.15, 1.00, 1.00, 1.00, 1.20]),
    Category(3,  "Akaryakıt",               0.090, 1100, 0.40,
             [0.85, 0.85, 0.95, 1.00, 1.05, 1.20, 1.35, 1.30, 1.05, 1.00, 0.95, 0.95]),
    Category(4,  "Giyim",                   0.070, 650,  0.70,
             [1.10, 0.90, 1.20, 1.15, 1.00, 0.90, 0.85, 1.00, 1.25, 1.15, 1.20, 1.15]),
    Category(5,  "Ayakkabı & Aksesuar",     0.022, 700,  0.65,
             [1.05, 0.90, 1.15, 1.10, 1.00, 0.90, 0.85, 1.00, 1.25, 1.10, 1.15, 1.10]),
    # Kozmetik: SEASONAL_PATTERN'de HİÇ satırı yok -> motor sezon=1.0 sayar.
    # Buraya sezondan bağımsız GERÇEK bir yükseliş kırılımı koyuyoruz (son 55 gün).
    Category(6,  "Kozmetik",                0.030, 400,  0.60, flat(1.0),
             break_days=55, break_factor=1.9),
    Category(7,  "Elektronik",              0.020, 3800, 0.85,
             [0.80, 0.80, 0.90, 0.95, 0.95, 1.00, 1.00, 1.15, 1.20, 1.05, 1.55, 1.25]),
    Category(8,  "Telekomünikasyon / GSM",  0.045, 320,  0.35, flat(1.0)),
    Category(9,  "Mobilya & Ev Tekstili",   0.013, 2600, 0.80,
             [0.85, 0.85, 1.00, 1.10, 1.25, 1.30, 1.20, 1.05, 1.10, 1.00, 1.15, 1.10]),
    Category(10, "Beyaz Eşya",              0.007, 9500, 0.55,
             [0.80, 0.85, 1.00, 1.10, 1.25, 1.30, 1.15, 1.00, 1.00, 1.05, 1.20, 1.15]),
    Category(11, "Otomotiv & Oto Bakım",    0.020, 1400, 0.75,
             [0.90, 0.90, 1.05, 1.15, 1.15, 1.10, 1.05, 1.00, 1.05, 1.05, 1.00, 0.95]),
    Category(12, "Araç Kiralama",           0.004, 3200, 0.55,
             [0.80, 1.00, 0.95, 1.05, 1.15, 1.30, 1.45, 1.40, 1.10, 1.00, 0.85, 0.95]),
    # Turizm: Eylül ampirik olarak DÜŞÜŞTE (yaz bitti) ama SEASONAL_PATTERN Eyl=1.15 (hafif +).
    # Motorun sezon önceli ile ampirik gerçek çelişsin diye son 45 günde azalış kırılımı.
    Category(13, "Turizm / Seyahat / Otel", 0.014, 5500, 0.80,
             [0.85, 1.15, 0.90, 1.00, 1.10, 1.35, 1.55, 1.50, 1.15, 1.00, 0.80, 0.90],
             break_days=45, break_factor=0.55),
    Category(14, "Havayolları / Ulaşım",    0.010, 3400, 0.65,
             [0.90, 1.15, 1.00, 1.00, 1.05, 1.25, 1.45, 1.40, 1.10, 1.00, 0.85, 1.10]),
    # Eğitim + Kırtasiye: hem sezon (Ağu-Eyl) hem GERÇEK okula dönüş dalgası -> tepe beklenir.
    Category(15, "Eğitim",                  0.020, 2100, 0.70,
             [1.20, 1.25, 1.00, 0.85, 0.85, 1.10, 1.10, 1.45, 1.60, 1.10, 0.90, 0.85],
             break_days=40, break_factor=1.6),
    Category(16, "Sağlık / Eczane / Optik", 0.060, 380,  0.65,
             [1.10, 1.10, 1.05, 1.00, 0.95, 0.90, 0.90, 0.95, 1.00, 1.05, 1.10, 1.15]),
    Category(17, "Sigorta",                 0.012, 2600, 0.45,
             [1.20, 1.10, 1.05, 1.05, 1.00, 1.00, 1.00, 1.00, 1.00, 1.00, 1.00, 1.05]),
    Category(18, "Spor",                    0.020, 550,  0.60,
             [1.45, 1.15, 1.00, 0.95, 0.90, 0.85, 0.80, 0.85, 1.25, 1.10, 1.00, 0.95]),
    Category(19, "Kuyumculuk / Saat",       0.006, 6800, 0.85,
             [0.90, 1.10, 1.05, 1.15, 1.35, 1.30, 1.15, 0.85, 0.90, 1.00, 1.20, 1.25]),
    Category(20, "Kırtasiye / Oyuncak",     0.026, 320,  0.65,
             [1.25, 1.20, 1.00, 1.10, 0.85, 0.80, 0.90, 1.55, 1.60, 0.90, 1.00, 1.30],
             break_days=38, break_factor=1.7),
    Category(21, "Yapı & İnşaat",           0.015, 1500, 0.80,
             [0.75, 0.80, 1.00, 1.20, 1.30, 1.35, 1.30, 1.25, 1.15, 1.05, 0.90, 0.80]),
    Category(22, "Eğlence",                 0.040, 260,  0.60,
             [0.95, 0.90, 1.00, 1.05, 1.10, 1.15, 1.20, 1.15, 1.05, 1.00, 1.00, 1.20]),
]

CAT_BY_ID = {c.id: c for c in CATS}
_share_sum = sum(c.share for c in CATS)
for c in CATS:
    c.share /= _share_sum


# ─────────────────────────────────────────────────────────────────────────────
# 1. Veri üretimi
# ─────────────────────────────────────────────────────────────────────────────
def day_weight(d: datetime) -> float:
    """Ay sonu / maaş günü + hafta sonu etkisi (işlem yoğunluğu çarpanı)."""
    w = 1.0
    if d.day <= 3 or d.day == 15 or d.day >= 28:
        w *= 1.35                       # maaş / ay sonu
    if d.weekday() >= 4:               # Cuma-Cmt-Pazar
        w *= 1.15
    return w


def structural_factor(cat: Category, d: datetime) -> float:
    """Sezondan bağımsız yapısal kırılım (son break_days gün içinde rampalı)."""
    if cat.break_days <= 0:
        return 1.0
    days_from_start = (NOW - d).days
    if days_from_start >= cat.break_days:
        return 1.0
    progress = 1.0 - days_from_start / cat.break_days      # 0..1, sona doğru artar
    return 1.0 + (cat.break_factor - 1.0) * progress


def generate_transactions() -> pd.DataFrame:
    n_days = HISTORY_DAYS
    days = [START + timedelta(days=i) for i in range(n_days)]

    # Müşteri harcama ağırlıkları — Pareto benzeri: az sayıda ağır harcayan.
    cust_weight = RNG.lognormal(mean=0.0, sigma=1.0, size=N_CUSTOMERS)
    cust_weight /= cust_weight.sum()

    # Kategori başına günlük beklenen işlem sayısı taban değeri.
    # 0.80: Poisson toplamı hedefin biraz üstüne çıktığı için kalibrasyon.
    base_daily = TARGET_ROWS / n_days * 0.80
    cat_ids = np.array([c.id for c in CATS])
    cat_share = np.array([c.share for c in CATS])

    rows = []
    rrn = 0
    for d in days:
        dow_w = day_weight(d)
        month_idx = d.month - 1
        # o gün kategori bazında beklenen işlem
        for ci, cat in enumerate(CATS):
            lam = (base_daily * cat_share[ci] * dow_w
                   * cat.season[month_idx]
                   * structural_factor(cat, d))
            k = RNG.poisson(lam)
            if k == 0:
                continue
            custs = RNG.choice(N_CUSTOMERS, size=k, p=cust_weight)
            # lognormal tutar; medyan = exp(mu) -> mu = ln(median)
            mu = math.log(cat.median)
            amounts = RNG.lognormal(mean=mu, sigma=cat.sigma, size=k)
            amounts = np.round(np.clip(amounts, 5, None), 2)
            secs = RNG.integers(0, 86400, size=k)
            for j in range(k):
                rrn += 1
                ts = d + timedelta(seconds=int(secs[j]))
                rows.append((
                    f"AN{rrn:09d}",
                    int(custs[j]),
                    cat.id,
                    float(amounts[j]),
                    ts,
                ))

    df = pd.DataFrame(rows, columns=["rrn", "cust_ix", "category_id", "amount", "ts"])

    # ── İadeler: ~%1.5, yüksek biletli kategorilere eğilimli ──
    hi_ticket = df["category_id"].isin([7, 9, 10, 13, 14, 19, 4])
    p = np.where(hi_ticket, 3.0, 1.0).astype(float)
    p /= p.sum()
    n_ref = int(len(df) * 0.015)
    ref_ix = RNG.choice(df.index.values, size=n_ref, replace=False, p=p)
    refunds = []
    for k, ix in enumerate(ref_ix):
        src = df.loc[ix]
        frac = float(RNG.uniform(0.3, 1.0))
        lag = int(RNG.integers(2, 18))
        refunds.append((
            f"ANR{k:08d}",
            int(src["cust_ix"]),
            int(src["category_id"]),
            -round(float(src["amount"]) * frac, 2),
            src["ts"] + timedelta(days=lag),
            src["rrn"],                       # orijinalin rrn'i (SQL'de join için)
        ))
    ref_df = pd.DataFrame(refunds, columns=["rrn", "cust_ix", "category_id",
                                            "amount", "ts", "orig_rrn"])
    df["orig_rrn"] = None
    full = pd.concat([df, ref_df], ignore_index=True)
    full["is_refund"] = full["orig_rrn"].notna()
    return full


# ─────────────────────────────────────────────────────────────────────────────
# 2. Kapsam: birkaç kategoriye kampanya kur (hem SQL'e hem analize)
# ─────────────────────────────────────────────────────────────────────────────
COVERING_CAMPAIGNS = [
    # (ad, kategori_id, status)
    ("Analiz - Market Sürekli",   1,  "Ongoing"),
    ("Analiz - Akaryakıt Sonbahar", 3, "Ongoing"),
    ("Analiz - Sağlık Yaklaşan",  16, "Pending"),
]
COVERED_CAT_IDS = {c[1] for c in COVERING_CAMPAIGNS}


# ─────────────────────────────────────────────────────────────────────────────
# 3. Bağımsız istatistiksel analiz
# ─────────────────────────────────────────────────────────────────────────────
def mann_kendall(x: np.ndarray) -> tuple[float, float]:
    """Basit Mann-Kendall: (tau, p). scipy.kendalltau zaman-endeksine karşı."""
    n = len(x)
    if n < 4 or np.allclose(x, x[0]):
        return 0.0, 1.0
    tau, p = stats.kendalltau(np.arange(n), x)
    return float(tau), float(p)


def analyse(full: pd.DataFrame) -> pd.DataFrame:
    win_start = NOW - timedelta(days=LOOKBACK_DAYS)
    mid = NOW - timedelta(days=LOOKBACK_DAYS / 2)

    w = full[(full["ts"] >= win_start) & (full["ts"] < NOW)].copy()
    w["day"] = (w["ts"] - win_start).dt.days
    w["week"] = w["day"] // 7

    recs = []
    horizon_months = sorted({((NOW + timedelta(days=k)).month)
                             for k in range(0, HORIZON_DAYS + 1)})

    for cat in CATS:
        cw = w[w["category_id"] == cat.id]
        net = float(cw["amount"].sum())                       # M1 net harcama
        purch = cw[~cw["is_refund"]]
        txn = int(len(purch))

        recent = float(cw.loc[cw["ts"] >= mid, "amount"].sum())
        prior = float(cw.loc[cw["ts"] < mid, "amount"].sum())
        m2_ratio = (recent - prior) / prior if prior > 0 else np.nan   # M2

        # M3 — günlük net harcamaya OLS eğim
        daily = (cw.groupby("day")["amount"].sum()
                 .reindex(range(0, LOOKBACK_DAYS), fill_value=0.0))
        if daily.sum() > 0:
            lr = stats.linregress(daily.index.values, daily.values)
            slope_norm = lr.slope * LOOKBACK_DAYS / (daily.mean() or 1.0)
            m3_slope_norm, m3_p, m3_r2 = float(slope_norm), float(lr.pvalue), float(lr.rvalue ** 2)
        else:
            m3_slope_norm, m3_p, m3_r2 = 0.0, 1.0, 0.0

        # M4 — haftalık net harcamaya Mann-Kendall
        weekly = (cw.groupby("week")["amount"].sum()
                  .reindex(range(0, LOOKBACK_DAYS // 7), fill_value=0.0)).values
        m4_tau, m4_p = mann_kendall(weekly)

        # M5 — momentum z-skoru: son 30g günlük ort. vs önceki 60g ort./std
        last30 = daily.loc[daily.index >= LOOKBACK_DAYS - 30]
        prior60 = daily.loc[daily.index < LOOKBACK_DAYS - 30]
        if prior60.std(ddof=0) > 0:
            m5_z = float((last30.mean() - prior60.mean()) / prior60.std(ddof=0))
        else:
            m5_z = 0.0

        # M6 — ampirik sezonsallık (14 aylık geçmişten ay endeksi)
        h = full[(full["category_id"] == cat.id) & (full["ts"] < NOW)].copy()
        h["ym"] = h["ts"].dt.to_period("M")
        monthly = h.groupby("ym")["amount"].sum()
        # Kısmi ilk/son ayları at (ör. 2026-09 sadece 1 gün) — sezon endeksini bozar.
        full_months = [p for p in monthly.index
                       if p.to_timestamp() >= START.replace(day=1) + pd.offsets.MonthBegin(1)
                       and p.to_timestamp(how="end") < NOW - timedelta(days=1)]
        monthly = monthly.loc[full_months]
        # gün başına normalize (aylar farklı uzunlukta)
        days_in = monthly.index.to_timestamp().days_in_month
        monthly_rate = (monthly.values / days_in)
        mrate_by_month = {}
        for per, r in zip(monthly.index, monthly_rate):
            mrate_by_month.setdefault(per.month, []).append(r)
        overall = np.mean(monthly_rate) if len(monthly_rate) else 1.0
        emp_index = {m: (np.mean(v) / overall if overall else 1.0)
                     for m, v in mrate_by_month.items()}
        m6_emp_season = float(np.mean([emp_index.get(m, 1.0) for m in horizon_months]))

        # ufuk projeksiyonu: son 30g günlük hız * ampirik sezon endeksi
        recent_rate = float(last30.mean())
        m6_forecast = recent_rate * HORIZON_DAYS * m6_emp_season
        m6_uplift = (m6_forecast / (net or 1.0))

        recs.append(dict(
            category_id=cat.id, name=cat.name,
            net_spend=round(net, 2), txn=txn,
            m2_ratio=None if np.isnan(m2_ratio) else round(m2_ratio, 4),
            m3_slope_norm=round(m3_slope_norm, 4), m3_p=round(m3_p, 4), m3_r2=round(m3_r2, 3),
            m4_tau=round(m4_tau, 4), m4_p=round(m4_p, 4),
            m5_z=round(m5_z, 3),
            m6_emp_season=round(m6_emp_season, 4),
            m6_forecast=round(m6_forecast, 2),
            m6_uplift=round(m6_uplift, 3),
            is_gap=cat.id not in COVERED_CAT_IDS,
        ))

    res = pd.DataFrame(recs)

    # ── Kompozit bağımsız skor ──
    # Motordan farkı: trendi tek orana değil, 3 kanıta (OLS eğim + MK tau + momentum z)
    # dayandırır; sezonu ÖNCÜL tablo yerine AMPİRİK endeksten alır.
    def z(s):
        s = s.astype(float)
        sd = s.std(ddof=0)
        return (s - s.mean()) / sd if sd > 0 else s * 0.0

    res["_spend"] = z(np.log1p(res["net_spend"].clip(lower=0)))
    res["_ols"] = z(res["m3_slope_norm"].clip(-1.0, 3.0))
    res["_mk"] = z(res["m4_tau"])
    res["_mom"] = z(res["m5_z"].clip(-3, 6))
    res["_season"] = z((res["m6_emp_season"] - 1.0).clip(-0.6, 1.0))
    trend_block = res[["_ols", "_mk", "_mom"]].mean(axis=1)

    res["composite_raw"] = (0.9 * res["_spend"]
                            + 1.7 * trend_block
                            + 1.1 * res["_season"])
    res["composite_score"] = res["composite_raw"] * np.where(res["is_gap"], 1.75, 1.0)

    vis = res.copy()
    vis.loc[~vis["is_gap"], "composite_score"] = vis.loc[~vis["is_gap"], "composite_score"] - 999
    res["composite_rank"] = (vis["composite_score"].rank(ascending=False, method="first")
                             .astype(int))
    res = res.drop(columns=[c for c in res.columns if c.startswith("_")])
    return res.sort_values("composite_rank").reset_index(drop=True)


# ─────────────────────────────────────────────────────────────────────────────
# 4. SQL üretimi
# ─────────────────────────────────────────────────────────────────────────────
def sql_dt(ts: datetime) -> str:
    return ts.strftime("%Y-%m-%dT%H:%M:%S")


def chunked(seq, n):
    for i in range(0, len(seq), n):
        yield seq[i:i + n]


def write_sql(full: pd.DataFrame, path: str) -> None:
    n_cards = int(N_CUSTOMERS * 1.55)
    lines: list[str] = []
    A = lines.append

    A("SET QUOTED_IDENTIFIER ON;")
    A("SET ANSI_NULLS ON;")
    A("SET XACT_ABORT ON;")
    A("SET NOCOUNT ON;")
    A("GO")
    A("USE CampaignSystem;")
    A("GO")
    A("IF EXISTS (SELECT 1 FROM MERCHANT WHERE MerchantNumber LIKE 'AN%')")
    A("BEGIN PRINT 'Analiz verisi zaten yüklü.'; RETURN; END")
    A(f"DECLARE @Pw nvarchar(200) = '{ADMIN_PW_HASH}';")
    A("DECLARE @cid int;")
    A("BEGIN TRANSACTION;")

    # Müşteriler
    cust_vals = []
    for i in range(N_CUSTOMERS):
        num = 40_000_000 + i
        g = "'E'" if i % 2 == 0 else "'K'"
        seg = SEGMENT_IDS[(i * 3) % len(SEGMENT_IDS)]
        cust_vals.append(f"('{num}',{g},{seg},1,0,@Pw)")
    for ch in chunked(cust_vals, 900):
        A("INSERT INTO CUSTOMER (CustomerNumber,Gender,SegmentId,IsActive,IsAdmin,PasswordHash) VALUES")
        A(",\n".join(ch) + ";")
    A("IF NOT EXISTS (SELECT 1 FROM CUSTOMER WHERE CustomerNumber='29999999')")
    A("  INSERT INTO CUSTOMER (CustomerNumber,Gender,SegmentId,IsActive,IsAdmin,PasswordHash) "
      "VALUES ('29999999','E',2,1,1,@Pw);")

    # Kart id eşlemesi için ordinal tablo
    A("DECLARE @Cust TABLE (Ix int IDENTITY(0,1), Id int);")
    A("INSERT INTO @Cust (Id) SELECT Id FROM CUSTOMER "
      "WHERE CustomerNumber LIKE '4000%' ORDER BY Id;")

    # Kartlar: her müşteriye 1, üçte ikisine 2, dörtte birine 3
    A("DECLARE @CardSeed TABLE (CustIx int, ProductId int, CardType char(1));")
    card_seed = []
    for i in range(N_CUSTOMERS):
        prod = PRODUCT_IDS[(i * 7) % len(PRODUCT_IDS)]
        card_seed.append(f"({i},{prod},'A')")
        if i % 3 != 0:
            card_seed.append(f"({i},{PRODUCT_IDS[(i*5)%len(PRODUCT_IDS)]},'E')")
        if i % 4 == 0:
            card_seed.append(f"({i},1,'E')")
    for ch in chunked(card_seed, 900):
        A("INSERT INTO @CardSeed (CustIx,ProductId,CardType) VALUES")
        A(",\n".join(ch) + ";")
    A("INSERT INTO CARD (CustomerId,ProductId,CardType,IsActive) "
      "SELECT c.Id, s.ProductId, s.CardType, 1 FROM @CardSeed s "
      "JOIN @Cust c ON c.Ix = s.CustIx;")

    A("DECLARE @Card TABLE (Ix int IDENTITY(0,1), Id int, CustomerId int);")
    A("INSERT INTO @Card (Id,CustomerId) SELECT cd.Id, cd.CustomerId FROM CARD cd "
      "JOIN CUSTOMER cu ON cu.Id = cd.CustomerId WHERE cu.CustomerNumber LIKE '4000%' "
      "ORDER BY cd.Id;")
    A("DECLARE @CardCount int = (SELECT COUNT(*) FROM @Card);")

    # Merchant'lar: kategori başına MERCHANTS_PER_CATEGORY
    merch_vals = []
    mno = 0
    cat_merch_first = {}
    for cat in CATS:
        cat_merch_first[cat.id] = mno
        for s in range(MERCHANTS_PER_CATEGORY):
            mno += 1
            merch_vals.append(
                f"('AN{mno:05d}',N'Analiz {cat.name[:20]} {s+1}',1,{cat.id})")
    for ch in chunked(merch_vals, 900):
        A("INSERT INTO MERCHANT (MerchantNumber,MerchantName,IsActive,MerchantCategoryId) VALUES")
        A(",\n".join(ch) + ";")
    A("DECLARE @M TABLE (CatId int, Slot int, Id int);")
    A("INSERT INTO @M (CatId, Slot, Id) "
      "SELECT MerchantCategoryId, "
      "ROW_NUMBER() OVER (PARTITION BY MerchantCategoryId ORDER BY Id)-1, Id "
      "FROM MERCHANT WHERE MerchantNumber LIKE 'AN[0-9]%';")

    # İşlemler
    purch = full[~full["is_refund"]].reset_index(drop=True)
    tx_vals = []
    for r in purch.itertuples(index=False):
        slot = (r.rrn.__hash__() & 0x7fffffff) % MERCHANTS_PER_CATEGORY
        tx_vals.append(
            f"('{r.rrn}',{r.cust_ix},{r.category_id},{slot},"
            f"'{sql_dt(r.ts)}',{r.amount:.2f})")
    A(f"-- {len(tx_vals)} alış işlemi")
    for ch in chunked(tx_vals, 1000):
        A("INSERT INTO [TRANSACTION] (Rrn,CardId,CustomerId,MerchantId,TransactionCodeId,TransactionDate,Amount)")
        A("SELECT v.Rrn, c.Id, c.CustomerId, m.Id, 1, v.Dt, v.Amt")
        A("FROM (VALUES")
        A(",\n".join(ch))
        A(") v(Rrn,CustIx,CatId,Slot,Dt,Amt)")
        A("JOIN @Card c ON c.Ix = v.CustIx % @CardCount")
        A("JOIN @M m ON m.CatId = v.CatId AND m.Slot = v.Slot;")

    # İadeler — orijinali rrn ile bul
    ref = full[full["is_refund"]].reset_index(drop=True)
    ref_vals = [f"('{r.rrn}','{r.orig_rrn}','{sql_dt(r.ts)}',{r.amount:.2f})"
                for r in ref.itertuples(index=False)]
    A(f"-- {len(ref_vals)} iade satırı")
    for ch in chunked(ref_vals, 1000):
        A("INSERT INTO [TRANSACTION] (Rrn,CardId,CustomerId,MerchantId,TransactionCodeId,TransactionDate,Amount,OriginalTransactionId)")
        A("SELECT v.Rrn, p.CardId, p.CustomerId, p.MerchantId, 1, v.Dt, v.Amt, p.Id")
        A("FROM (VALUES")
        A(",\n".join(ch))
        A(") v(Rrn,OrigRrn,Dt,Amt)")
        A("JOIN [TRANSACTION] p ON p.Rrn = v.OrigRrn;")

    # Kapsayan kampanyalar
    for name, cat_id, status in COVERING_CAMPAIGNS:
        A("INSERT INTO CAMPAIGN (Name,Description,CampaignType,EarningType,StartDate,EndDate,"
          "Status,IsActive,RefundClawbackEnabled,UnusedPointsClawbackEnabled,RewardPoint) VALUES")
        start = "DATEADD(DAY,-20,GETDATE())" if status == "Ongoing" else "DATEADD(DAY,5,GETDATE())"
        end = "DATEADD(DAY,25,GETDATE())" if status == "Ongoing" else "DATEADD(DAY,40,GETDATE())"
        A(f"(N'{name}',N'analiz kapsam','MASS','M',{start},{end},'{status}',1,0,0,10);")
        A("SET @cid = SCOPE_IDENTITY();")
        A(f"INSERT INTO CAMPAIGN_MERCHANT (CampaignId,MerchantId) "
          f"SELECT @cid, Id FROM @M WHERE CatId = {cat_id};")

    A("COMMIT TRANSACTION;")
    A("PRINT 'Analiz veri seti yüklendi.';")
    A("GO")

    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))


# ─────────────────────────────────────────────────────────────────────────────
def main() -> None:
    print(f"seed={SEED}  pencere: {START:%Y-%m-%d} … {NOW:%Y-%m-%d}  ({HISTORY_DAYS} gün)")
    full = generate_transactions()
    n_p = int((~full["is_refund"]).sum())
    n_r = int(full["is_refund"].sum())
    print(f"üretilen: {len(full):,} satır  ({n_p:,} alış + {n_r:,} iade)  "
          f"toplam net {full['amount'].sum():,.0f} ₺")

    sql_path = os.path.join(OUT_DIR, "dataset.sql")
    write_sql(full, sql_path)
    print(f"yazıldı: {sql_path}  ({os.path.getsize(sql_path)/1e6:.1f} MB)")

    res = analyse(full)
    cols = ["composite_rank", "name", "net_spend", "txn", "m2_ratio", "m3_slope_norm",
            "m3_p", "m4_tau", "m4_p", "m5_z", "m6_emp_season", "m6_uplift",
            "is_gap", "composite_score"]
    with pd.option_context("display.width", 200, "display.max_columns", 20,
                           "display.float_format", lambda v: f"{v:,.3f}"):
        print("\n=== BAĞIMSIZ ANALİZ (son 90 gün pencere, kompozit sıraya göre) ===")
        print(res[cols].to_string(index=False))

    res.to_json(os.path.join(OUT_DIR, "independent_ranking.json"),
                orient="records", force_ascii=False, indent=2)
    print(f"\nyazıldı: {os.path.join(OUT_DIR, 'independent_ranking.json')}")


if __name__ == "__main__":
    main()
