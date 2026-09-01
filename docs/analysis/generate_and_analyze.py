"""
Kampanya Öneri Motoru — sentetik veri seti + bağımsız istatistiksel analiz.

Kullanım:
    python generate_and_analyze.py [SEED] [--randomize]

  SEED yoksa 20260901 (DS1 — elle kurgulanmış referans veri seti).
  --randomize : kategori payları, aylık sezon eğrileri ve yapısal kırılımlar da
                rastgele üretilir (DS2, DS3 için).

Çıktı (_out/):
    dataset_<seed>.sql              -> taze DB'ye yüklenir (gitignore)
    independent_ranking_<seed>.json -> 6 yöntem + kompozit sıralama
    aggregates_<seed>.json          -> ağırlık taraması için kategori bazında ham değerler
                                       (net harcama, iki yarı, sezon önceli/ampirik,
                                        enjekte edilen GERÇEK trend = break_factor)

Bağımsız yöntemler (son 90 gün penceresi, kategori bazında):
    M1 net harcama · M2 iki-yarı oranı (motorun yöntemi) · M3 OLS eğim + p ·
    M4 Mann-Kendall tau + p · M5 momentum z · M6 ampirik ay-endeksi + ufuk projeksiyonu ·
    M7 kapsam · kompozit (z-normalize ağırlıklı; trend = 3 yöntemin ortalaması,
    sezon = ampirik)
"""

from __future__ import annotations

import json
import math
import os
import sys
from dataclasses import dataclass

import numpy as np
import pandas as pd
from scipy import stats

try:
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
except Exception:
    pass

from datetime import datetime, timedelta

OUT_DIR = os.path.join(os.path.dirname(__file__), "_out")
os.makedirs(OUT_DIR, exist_ok=True)

NOW = datetime(2026, 9, 1, 9, 0, 0)
HISTORY_DAYS = 425
START = NOW - timedelta(days=HISTORY_DAYS)
LOOKBACK_DAYS = 90
HORIZON_DAYS = 45
TARGET_ROWS = 50_000
N_CUSTOMERS = 450
MERCHANTS_PER_CATEGORY = 4

SEGMENT_IDS = [1, 2, 3, 4, 5]
PRODUCT_IDS = [1, 2, 3, 4, 5, 6]
ADMIN_PW_HASH = ("AQAAAAIAAYagAAAAEJFQXNYaIC1YsFJirJtMW9NYhciP2xIaiqkgV"
                 "XxvIMOl7UgMCyyHioTSfbubY17Zlw==")

# ── Motorun SEASONAL_PATTERN öncül tablosu (SeasonalPatternConfiguration.BuildSeed) ──
# {kategori_id: {ay: ağırlık}}. Ufuk ayları [9, 10] için önceli hesaplanır.
_PRIOR = {
    3:  {1: .85, 2: .85, 6: 1.20, 7: 1.35, 8: 1.30, 9: 1.05, 12: .95},
    4:  {1: 1.10, 2: .90, 3: 1.20, 4: 1.15, 7: .85, 9: 1.25, 10: 1.15, 11: 1.20, 12: 1.15},
    5:  {3: 1.15, 4: 1.10, 7: .85, 9: 1.25, 10: 1.10, 11: 1.15, 12: 1.10},
    7:  {1: .80, 2: .80, 3: .90, 8: 1.15, 9: 1.20, 11: 1.55, 12: 1.25},
    9:  {1: .85, 2: .85, 5: 1.25, 6: 1.30, 7: 1.20, 9: 1.10, 11: 1.15},
    10: {1: .80, 2: .85, 5: 1.25, 6: 1.30, 7: 1.15, 11: 1.20, 12: 1.15},
    13: {1: .85, 2: 1.15, 3: .90, 6: 1.35, 7: 1.55, 8: 1.50, 9: 1.15, 11: .80, 12: .90},
    14: {1: .90, 2: 1.15, 6: 1.25, 7: 1.45, 8: 1.40, 11: .85, 12: 1.10},
    15: {1: 1.20, 2: 1.25, 4: .85, 5: .85, 6: 1.10, 7: 1.10, 8: 1.45, 9: 1.60, 10: 1.10, 11: .90, 12: .85},
    18: {1: 1.45, 2: 1.15, 6: .85, 7: .80, 8: .85, 9: 1.25, 10: 1.10},
    19: {2: 1.10, 4: 1.15, 5: 1.35, 6: 1.30, 7: 1.15, 8: .85, 9: .90, 11: 1.20, 12: 1.25},
    20: {1: 1.25, 2: 1.20, 4: 1.10, 5: .85, 6: .80, 7: .90, 8: 1.55, 9: 1.60, 10: .90, 12: 1.30},
    21: {1: .75, 2: .80, 4: 1.20, 5: 1.30, 6: 1.35, 7: 1.30, 8: 1.25, 9: 1.15, 12: .80},
}
HORIZON_MONTHS = sorted({(NOW + timedelta(days=k)).month for k in range(0, HORIZON_DAYS + 1)})


def prior_season(cat_id: int) -> float:
    d = _PRIOR.get(cat_id, {})
    return float(np.mean([d.get(m, 1.0) for m in HORIZON_MONTHS]))


@dataclass
class Category:
    id: int
    name: str
    share: float
    median: float
    sigma: float
    season: list[float]          # 12 aylık çarpan, index 0=Ocak
    break_days: int = 0
    break_factor: float = 1.0    # enjekte edilen GERÇEK trend (>1 artış, <1 azalış)


# DS1 için elle kurgulanmış tablo (referans / tuzaklı testler).
_BASE = [
    (1,  "Gıda / Market",            0.205, 220,  0.55,
     [1.00, 0.98, 1.08, 1.02, 1.00, 1.00, 1.02, 1.00, 1.00, 1.00, 1.05, 1.12], 0, 1.0),
    (2,  "Restoran / Yeme-İçme",     0.150, 300,  0.60,
     [0.90, 0.88, 0.95, 1.00, 1.05, 1.15, 1.20, 1.15, 1.00, 1.00, 1.00, 1.20], 0, 1.0),
    (3,  "Akaryakıt",               0.090, 1100, 0.40,
     [0.85, 0.85, 0.95, 1.00, 1.05, 1.20, 1.35, 1.30, 1.05, 1.00, 0.95, 0.95], 0, 1.0),
    (4,  "Giyim",                   0.070, 650,  0.70,
     [1.10, 0.90, 1.20, 1.15, 1.00, 0.90, 0.85, 1.00, 1.25, 1.15, 1.20, 1.15], 0, 1.0),
    (5,  "Ayakkabı & Aksesuar",     0.022, 700,  0.65,
     [1.05, 0.90, 1.15, 1.10, 1.00, 0.90, 0.85, 1.00, 1.25, 1.10, 1.15, 1.10], 0, 1.0),
    (6,  "Kozmetik",                0.030, 400,  0.60, [1.0] * 12, 55, 1.9),
    (7,  "Elektronik",              0.020, 3800, 0.85,
     [0.80, 0.80, 0.90, 0.95, 0.95, 1.00, 1.00, 1.15, 1.20, 1.05, 1.55, 1.25], 0, 1.0),
    (8,  "Telekomünikasyon / GSM",  0.045, 320,  0.35, [1.0] * 12, 0, 1.0),
    (9,  "Mobilya & Ev Tekstili",   0.013, 2600, 0.80,
     [0.85, 0.85, 1.00, 1.10, 1.25, 1.30, 1.20, 1.05, 1.10, 1.00, 1.15, 1.10], 0, 1.0),
    (10, "Beyaz Eşya",              0.007, 9500, 0.55,
     [0.80, 0.85, 1.00, 1.10, 1.25, 1.30, 1.15, 1.00, 1.00, 1.05, 1.20, 1.15], 0, 1.0),
    (11, "Otomotiv & Oto Bakım",    0.020, 1400, 0.75,
     [0.90, 0.90, 1.05, 1.15, 1.15, 1.10, 1.05, 1.00, 1.05, 1.05, 1.00, 0.95], 0, 1.0),
    (12, "Araç Kiralama",           0.004, 3200, 0.55,
     [0.80, 1.00, 0.95, 1.05, 1.15, 1.30, 1.45, 1.40, 1.10, 1.00, 0.85, 0.95], 0, 1.0),
    (13, "Turizm / Seyahat / Otel", 0.014, 5500, 0.80,
     [0.85, 1.15, 0.90, 1.00, 1.10, 1.35, 1.55, 1.50, 1.15, 1.00, 0.80, 0.90], 45, 0.55),
    (14, "Havayolları / Ulaşım",    0.010, 3400, 0.65,
     [0.90, 1.15, 1.00, 1.00, 1.05, 1.25, 1.45, 1.40, 1.10, 1.00, 0.85, 1.10], 0, 1.0),
    (15, "Eğitim",                  0.020, 2100, 0.70,
     [1.20, 1.25, 1.00, 0.85, 0.85, 1.10, 1.10, 1.45, 1.60, 1.10, 0.90, 0.85], 40, 1.6),
    (16, "Sağlık / Eczane / Optik", 0.060, 380,  0.65,
     [1.10, 1.10, 1.05, 1.00, 0.95, 0.90, 0.90, 0.95, 1.00, 1.05, 1.10, 1.15], 0, 1.0),
    (17, "Sigorta",                 0.012, 2600, 0.45,
     [1.20, 1.10, 1.05, 1.05, 1.00, 1.00, 1.00, 1.00, 1.00, 1.00, 1.00, 1.05], 0, 1.0),
    (18, "Spor",                    0.020, 550,  0.60,
     [1.45, 1.15, 1.00, 0.95, 0.90, 0.85, 0.80, 0.85, 1.25, 1.10, 1.00, 0.95], 0, 1.0),
    (19, "Kuyumculuk / Saat",       0.006, 6800, 0.85,
     [0.90, 1.10, 1.05, 1.15, 1.35, 1.30, 1.15, 0.85, 0.90, 1.00, 1.20, 1.25], 0, 1.0),
    (20, "Kırtasiye / Oyuncak",     0.026, 320,  0.65,
     [1.25, 1.20, 1.00, 1.10, 0.85, 0.80, 0.90, 1.55, 1.60, 0.90, 1.00, 1.30], 38, 1.7),
    (21, "Yapı & İnşaat",           0.015, 1500, 0.80,
     [0.75, 0.80, 1.00, 1.20, 1.30, 1.35, 1.30, 1.25, 1.15, 1.05, 0.90, 0.80], 0, 1.0),
    (22, "Eğlence",                 0.040, 260,  0.60,
     [0.95, 0.90, 1.00, 1.05, 1.10, 1.15, 1.20, 1.15, 1.05, 1.00, 1.00, 1.20], 0, 1.0),
]


def build_categories(rng: np.random.Generator, randomize: bool) -> list[Category]:
    cats: list[Category] = []
    for (cid, name, share, median, sigma, season, bd, bf) in _BASE:
        if randomize:
            # pay: lognormal gürültü ile çarp
            share = share * float(rng.lognormal(0.0, 0.45))
            median = median * float(rng.lognormal(0.0, 0.25))
            sigma = float(np.clip(sigma * rng.lognormal(0.0, 0.15), 0.3, 1.0))
            # sezon eğrisi: 1.0 + rastgele fazlı/genlikli sinüs + gürültü
            amp = float(rng.uniform(0.05, 0.35))
            phase = float(rng.uniform(0, 2 * math.pi))
            season = [float(np.clip(1.0 + amp * math.sin(2 * math.pi * m / 12 + phase)
                                    + rng.normal(0, 0.04), 0.65, 1.65))
                      for m in range(12)]
            bd, bf = 0, 1.0  # kırılımlar aşağıda rastgele atanır
        cats.append(Category(cid, name, share, median, sigma, season, bd, bf))

    if randomize:
        # 5–7 kategoriye rastgele yapısal kırılım (log-uniform: artış ve azalış eşit olası)
        k = int(rng.integers(5, 8))
        chosen = rng.choice(len(cats), size=k, replace=False)
        for ix in chosen:
            cats[ix].break_days = int(rng.integers(25, 70))
            cats[ix].break_factor = float(np.exp(rng.uniform(math.log(0.40), math.log(2.30))))

    tot = sum(c.share for c in cats)
    for c in cats:
        c.share /= tot
    return cats


# ─────────────────────────────────────────────────────────────────────────────
def day_weight(d: datetime) -> float:
    w = 1.0
    if d.day <= 3 or d.day == 15 or d.day >= 28:
        w *= 1.35
    if d.weekday() >= 4:
        w *= 1.15
    return w


def structural_factor(cat: Category, d: datetime) -> float:
    if cat.break_days <= 0:
        return 1.0
    days_from = (NOW - d).days
    if days_from >= cat.break_days:
        return 1.0
    progress = 1.0 - days_from / cat.break_days
    return 1.0 + (cat.break_factor - 1.0) * progress


def generate_transactions(rng: np.random.Generator, cats: list[Category]) -> pd.DataFrame:
    n_days = HISTORY_DAYS
    days = [START + timedelta(days=i) for i in range(n_days)]

    cust_weight = rng.lognormal(0.0, 1.0, size=N_CUSTOMERS)
    cust_weight /= cust_weight.sum()

    base_daily = TARGET_ROWS / n_days * 0.80
    shares = np.array([c.share for c in cats])

    rows, rrn = [], 0
    for d in days:
        dow_w = day_weight(d)
        mi = d.month - 1
        for ci, cat in enumerate(cats):
            lam = (base_daily * shares[ci] * dow_w
                   * cat.season[mi] * structural_factor(cat, d))
            kk = rng.poisson(lam)
            if kk == 0:
                continue
            custs = rng.choice(N_CUSTOMERS, size=kk, p=cust_weight)
            amounts = np.round(np.clip(
                rng.lognormal(math.log(cat.median), cat.sigma, size=kk), 5, None), 2)
            secs = rng.integers(0, 86400, size=kk)
            for j in range(kk):
                rrn += 1
                rows.append((f"AN{rrn:09d}", int(custs[j]), cat.id,
                             float(amounts[j]), d + timedelta(seconds=int(secs[j]))))

    df = pd.DataFrame(rows, columns=["rrn", "cust_ix", "category_id", "amount", "ts"])

    hi = df["category_id"].isin([7, 9, 10, 13, 14, 19, 4])
    p = np.where(hi, 3.0, 1.0).astype(float)
    p /= p.sum()
    n_ref = int(len(df) * 0.015)
    ref_ix = rng.choice(df.index.values, size=n_ref, replace=False, p=p)
    refunds = []
    for kk, ix in enumerate(ref_ix):
        src = df.loc[ix]
        frac = float(rng.uniform(0.3, 1.0))
        lag = int(rng.integers(2, 18))
        refunds.append((f"ANR{kk:08d}", int(src["cust_ix"]), int(src["category_id"]),
                        -round(float(src["amount"]) * frac, 2),
                        src["ts"] + timedelta(days=lag), src["rrn"]))
    ref_df = pd.DataFrame(refunds, columns=["rrn", "cust_ix", "category_id",
                                            "amount", "ts", "orig_rrn"])
    df["orig_rrn"] = None
    full = pd.concat([df, ref_df], ignore_index=True)
    full["is_refund"] = full["orig_rrn"].notna()
    return full


COVERING_CAMPAIGNS = [
    ("Analiz - Market Sürekli",     1,  "Ongoing"),
    ("Analiz - Akaryakıt Sonbahar", 3,  "Ongoing"),
    ("Analiz - Sağlık Yaklaşan",    16, "Pending"),
]
COVERED_CAT_IDS = {c[1] for c in COVERING_CAMPAIGNS}


# ─────────────────────────────────────────────────────────────────────────────
def mann_kendall(x: np.ndarray) -> tuple[float, float]:
    n = len(x)
    if n < 4 or np.allclose(x, x[0]):
        return 0.0, 1.0
    tau, p = stats.kendalltau(np.arange(n), x)
    return float(tau), float(p)


def analyse(full: pd.DataFrame, cats: list[Category]) -> pd.DataFrame:
    win_start = NOW - timedelta(days=LOOKBACK_DAYS)
    mid = NOW - timedelta(days=LOOKBACK_DAYS / 2)
    w = full[(full["ts"] >= win_start) & (full["ts"] < NOW)].copy()
    w["day"] = (w["ts"] - win_start).dt.days
    w["week"] = w["day"] // 7

    recs = []
    for cat in cats:
        cw = w[w["category_id"] == cat.id]
        net = float(cw["amount"].sum())
        purch = cw[~cw["is_refund"]]
        recent = float(cw.loc[cw["ts"] >= mid, "amount"].sum())
        prior = float(cw.loc[cw["ts"] < mid, "amount"].sum())
        m2 = (recent - prior) / prior if prior > 0 else np.nan

        daily = (cw.groupby("day")["amount"].sum()
                 .reindex(range(0, LOOKBACK_DAYS), fill_value=0.0))
        if daily.sum() > 0:
            lr = stats.linregress(daily.index.values, daily.values)
            m3 = float(lr.slope * LOOKBACK_DAYS / (daily.mean() or 1.0))
            m3p, m3r2 = float(lr.pvalue), float(lr.rvalue ** 2)
        else:
            m3, m3p, m3r2 = 0.0, 1.0, 0.0

        weekly = (cw.groupby("week")["amount"].sum()
                  .reindex(range(0, LOOKBACK_DAYS // 7), fill_value=0.0)).values
        m4, m4p = mann_kendall(weekly)

        last30 = daily.loc[daily.index >= LOOKBACK_DAYS - 30]
        prior60 = daily.loc[daily.index < LOOKBACK_DAYS - 30]
        m5 = float((last30.mean() - prior60.mean()) / prior60.std(ddof=0)) \
            if prior60.std(ddof=0) > 0 else 0.0

        h = full[(full["category_id"] == cat.id) & (full["ts"] < NOW)].copy()
        h["ym"] = h["ts"].dt.to_period("M")
        monthly = h.groupby("ym")["amount"].sum()
        full_months = [pp for pp in monthly.index
                       if pp.to_timestamp() >= START.replace(day=1) + pd.offsets.MonthBegin(1)
                       and pp.to_timestamp(how="end") < NOW - timedelta(days=1)]
        monthly = monthly.loc[full_months]
        if len(monthly):
            mrate = monthly.values / monthly.index.to_timestamp().days_in_month
            by_m = {}
            for per, r in zip(monthly.index, mrate):
                by_m.setdefault(per.month, []).append(r)
            overall = np.mean(mrate)
            emp = {m: (np.mean(v) / overall if overall else 1.0) for m, v in by_m.items()}
            m6_season = float(np.mean([emp.get(m, 1.0) for m in HORIZON_MONTHS]))
        else:
            m6_season = 1.0

        recs.append(dict(
            category_id=cat.id, name=cat.name,
            net_spend=round(net, 2), recent_spend=round(recent, 2),
            prior_spend=round(prior, 2), txn=int(len(purch)),
            m2_ratio=None if np.isnan(m2) else round(m2, 4),
            m3_slope_norm=round(m3, 4), m3_p=round(m3p, 4), m3_r2=round(m3r2, 3),
            m4_tau=round(m4, 4), m4_p=round(m4p, 4), m5_z=round(m5, 3),
            m6_emp_season=round(m6_season, 4),
            prior_season=round(prior_season(cat.id), 4),
            is_gap=cat.id not in COVERED_CAT_IDS,
            true_break_factor=round(cat.break_factor, 3),
            true_break_days=cat.break_days,
        ))

    res = pd.DataFrame(recs)

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
    res["composite_raw"] = 0.9 * res["_spend"] + 1.7 * trend_block + 1.1 * res["_season"]
    res["composite_score"] = res["composite_raw"] * np.where(res["is_gap"], 1.75, 1.0)
    vis = res["composite_score"] - np.where(res["is_gap"], 0, 999)
    res["composite_rank"] = vis.rank(ascending=False, method="first").astype(int)
    return res.drop(columns=[c for c in res.columns if c.startswith("_")]) \
        .sort_values("composite_rank").reset_index(drop=True)


# ─────────────────────────────────────────────────────────────────────────────
def sql_dt(ts):
    return ts.strftime("%Y-%m-%dT%H:%M:%S")


def chunked(seq, n):
    for i in range(0, len(seq), n):
        yield seq[i:i + n]


def write_sql(full: pd.DataFrame, cats: list[Category], path: str, db_name: str) -> None:
    L = []
    A = L.append
    A("SET QUOTED_IDENTIFIER ON;\nSET ANSI_NULLS ON;\nSET XACT_ABORT ON;\nSET NOCOUNT ON;\nGO")
    A(f"USE {db_name};\nGO")
    A("IF EXISTS (SELECT 1 FROM MERCHANT WHERE MerchantNumber LIKE 'AN%')")
    A("BEGIN PRINT 'Analiz verisi zaten yüklü.'; RETURN; END")
    A(f"DECLARE @Pw nvarchar(200) = '{ADMIN_PW_HASH}';")
    A("DECLARE @cid int;")
    A("BEGIN TRANSACTION;")

    cust = []
    for i in range(N_CUSTOMERS):
        g = "'E'" if i % 2 == 0 else "'K'"
        cust.append(f"('{40_000_000 + i}',{g},{SEGMENT_IDS[(i*3)%5]},1,0,@Pw)")
    for ch in chunked(cust, 900):
        A("INSERT INTO CUSTOMER (CustomerNumber,Gender,SegmentId,IsActive,IsAdmin,PasswordHash) VALUES")
        A(",\n".join(ch) + ";")
    A("IF NOT EXISTS (SELECT 1 FROM CUSTOMER WHERE CustomerNumber='29999999')")
    A("  INSERT INTO CUSTOMER (CustomerNumber,Gender,SegmentId,IsActive,IsAdmin,PasswordHash) "
      "VALUES ('29999999','E',2,1,1,@Pw);")
    A("DECLARE @Cust TABLE (Ix int IDENTITY(0,1), Id int);")
    A("INSERT INTO @Cust (Id) SELECT Id FROM CUSTOMER WHERE CustomerNumber LIKE '4000%' ORDER BY Id;")

    cs = []
    for i in range(N_CUSTOMERS):
        cs.append(f"({i},{PRODUCT_IDS[(i*7)%6]},'A')")
        if i % 3 != 0:
            cs.append(f"({i},{PRODUCT_IDS[(i*5)%6]},'E')")
        if i % 4 == 0:
            cs.append(f"({i},1,'E')")
    A("DECLARE @CardSeed TABLE (CustIx int, ProductId int, CardType char(1));")
    for ch in chunked(cs, 900):
        A("INSERT INTO @CardSeed (CustIx,ProductId,CardType) VALUES")
        A(",\n".join(ch) + ";")
    A("INSERT INTO CARD (CustomerId,ProductId,CardType,IsActive) "
      "SELECT c.Id,s.ProductId,s.CardType,1 FROM @CardSeed s JOIN @Cust c ON c.Ix=s.CustIx;")
    A("DECLARE @Card TABLE (Ix int IDENTITY(0,1), Id int, CustomerId int);")
    A("INSERT INTO @Card (Id,CustomerId) SELECT cd.Id,cd.CustomerId FROM CARD cd "
      "JOIN CUSTOMER cu ON cu.Id=cd.CustomerId WHERE cu.CustomerNumber LIKE '4000%' ORDER BY cd.Id;")
    A("DECLARE @CardCount int = (SELECT COUNT(*) FROM @Card);")

    mv, mno = [], 0
    for cat in cats:
        for s in range(MERCHANTS_PER_CATEGORY):
            mno += 1
            mv.append(f"('AN{mno:05d}',N'Analiz {cat.name[:18]} {s+1}',1,{cat.id})")
    for ch in chunked(mv, 900):
        A("INSERT INTO MERCHANT (MerchantNumber,MerchantName,IsActive,MerchantCategoryId) VALUES")
        A(",\n".join(ch) + ";")
    A("DECLARE @M TABLE (CatId int, Slot int, Id int);")
    A("INSERT INTO @M (CatId,Slot,Id) SELECT MerchantCategoryId,"
      "ROW_NUMBER() OVER (PARTITION BY MerchantCategoryId ORDER BY Id)-1,Id "
      "FROM MERCHANT WHERE MerchantNumber LIKE 'AN[0-9]%';")

    purch = full[~full["is_refund"]].reset_index(drop=True)
    tv = [f"('{r.rrn}',{r.cust_ix},{r.category_id},"
          f"{(hash(r.rrn) & 0x7fffffff) % MERCHANTS_PER_CATEGORY},'{sql_dt(r.ts)}',{r.amount:.2f})"
          for r in purch.itertuples(index=False)]
    A(f"-- {len(tv)} alış")
    for ch in chunked(tv, 1000):
        A("INSERT INTO [TRANSACTION] (Rrn,CardId,CustomerId,MerchantId,TransactionCodeId,TransactionDate,Amount)")
        A("SELECT v.Rrn,c.Id,c.CustomerId,m.Id,1,v.Dt,v.Amt FROM (VALUES")
        A(",\n".join(ch))
        A(") v(Rrn,CustIx,CatId,Slot,Dt,Amt) JOIN @Card c ON c.Ix=v.CustIx % @CardCount "
          "JOIN @M m ON m.CatId=v.CatId AND m.Slot=v.Slot;")

    ref = full[full["is_refund"]].reset_index(drop=True)
    rv = [f"('{r.rrn}','{r.orig_rrn}','{sql_dt(r.ts)}',{r.amount:.2f})"
          for r in ref.itertuples(index=False)]
    A(f"-- {len(rv)} iade")
    for ch in chunked(rv, 1000):
        A("INSERT INTO [TRANSACTION] (Rrn,CardId,CustomerId,MerchantId,TransactionCodeId,TransactionDate,Amount,OriginalTransactionId)")
        A("SELECT v.Rrn,p.CardId,p.CustomerId,p.MerchantId,1,v.Dt,v.Amt,p.Id FROM (VALUES")
        A(",\n".join(ch))
        A(") v(Rrn,OrigRrn,Dt,Amt) JOIN [TRANSACTION] p ON p.Rrn=v.OrigRrn;")

    for name, cid, status in COVERING_CAMPAIGNS:
        st = "DATEADD(DAY,-20,GETDATE())" if status == "Ongoing" else "DATEADD(DAY,5,GETDATE())"
        en = "DATEADD(DAY,25,GETDATE())" if status == "Ongoing" else "DATEADD(DAY,40,GETDATE())"
        A("INSERT INTO CAMPAIGN (Name,Description,CampaignType,EarningType,StartDate,EndDate,"
          "Status,IsActive,RefundClawbackEnabled,UnusedPointsClawbackEnabled,RewardPoint) VALUES")
        A(f"(N'{name}',N'analiz','MASS','M',{st},{en},'{status}',1,0,0,10);")
        A("SET @cid = SCOPE_IDENTITY();")
        A(f"INSERT INTO CAMPAIGN_MERCHANT (CampaignId,MerchantId) SELECT @cid,Id FROM @M WHERE CatId={cid};")

    A("COMMIT TRANSACTION;\nPRINT 'Analiz veri seti yüklendi.';\nGO")
    open(path, "w", encoding="utf-8").write("\n".join(L))


def main() -> None:
    args = [a for a in sys.argv[1:]]
    randomize = "--randomize" in args
    seed_args = [a for a in args if a.isdigit()]
    seed = int(seed_args[0]) if seed_args else 20260901

    rng = np.random.default_rng(seed)
    cats = build_categories(rng, randomize)
    tag = "rastgele" if randomize else "elle kurgulanmış"
    print(f"seed={seed}  ({tag})  pencere {START:%Y-%m-%d} … {NOW:%Y-%m-%d}")
    if randomize:
        brk = [(c.name, round(c.break_factor, 2), c.break_days) for c in cats if c.break_days]
        print("  enjekte kırılımlar (kategori, gerçek çarpan, gün):")
        for b in brk:
            print(f"    {b[0]:26} ×{b[1]:<5} ({b[2]} gün)")

    full = generate_transactions(rng, cats)
    n_p = int((~full["is_refund"]).sum())
    n_r = int(full["is_refund"].sum())
    print(f"  üretilen: {len(full):,} satır ({n_p:,} alış + {n_r:,} iade), "
          f"net {full['amount'].sum():,.0f} ₺")

    db = f"CampaignSystem_DS{seed}"
    write_sql(full, cats, os.path.join(OUT_DIR, f"dataset_{seed}.sql"), db)

    res = analyse(full, cats)
    res.to_json(os.path.join(OUT_DIR, f"independent_ranking_{seed}.json"),
                orient="records", force_ascii=False, indent=2)

    # Ağırlık taraması için ham değerler
    agg = res[["category_id", "name", "net_spend", "recent_spend", "prior_spend",
               "m2_ratio", "m3_slope_norm", "m3_p", "m4_tau", "m5_z",
               "m6_emp_season", "prior_season", "is_gap", "composite_rank",
               "true_break_factor", "true_break_days"]].copy()
    agg.to_json(os.path.join(OUT_DIR, f"aggregates_{seed}.json"),
                orient="records", force_ascii=False, indent=2)

    cols = ["composite_rank", "name", "net_spend", "m2_ratio", "m3_slope_norm",
            "m3_p", "m4_tau", "m5_z", "m6_emp_season", "prior_season",
            "is_gap", "true_break_factor"]
    with pd.option_context("display.width", 200, "display.max_columns", 20,
                           "display.float_format", lambda v: f"{v:,.3f}"):
        print(res.sort_values("composite_rank")[cols].to_string(index=False))
    print(f"  -> _out/dataset_{seed}.sql, independent_ranking_{seed}.json, aggregates_{seed}.json")


if __name__ == "__main__":
    main()
