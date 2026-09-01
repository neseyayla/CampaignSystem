"""
Bağımsız istatistiksel analiz (independent_ranking.json) ile uygulamanın öneri motoru
çıktısını (app_ranking_all.json) karşılaştırır ve karsilastirma-raporu.md yazar.

Önce generate_and_analyze.py çalıştırılmış, veri seti taze bir DB'ye yüklenmiş ve
uygulama o DB'ye karşı çağrılıp app_ranking_all.json kaydedilmiş olmalı.
"""

from __future__ import annotations

import json
import os
import sys

import numpy as np
import pandas as pd
from scipy import stats

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

OUT = os.path.join(os.path.dirname(__file__), "_out")
REPORT = os.path.join(os.path.dirname(__file__), "karsilastirma-raporu.md")

ind = pd.DataFrame(json.load(open(os.path.join(OUT, "independent_ranking.json"), encoding="utf-8")))
app_raw = json.load(open(os.path.join(OUT, "app_ranking_all.json"), encoding="utf-8"))

app = pd.DataFrame([{
    "name": x["merchantCategoryName"],
    "engine_score": x["score"],
    "engine_trend": x["reason"]["trendRatio"],
    "engine_season": x["reason"]["seasonalWeight"],
    "engine_spend": x["reason"]["totalSpend"],
    "engine_gap": x["reason"]["isCoverageGap"],
} for x in app_raw])

df = ind.merge(app, on="name", suffixes=("", "_app"))

# Sıralamaları YALNIZ kapsam boşluğu olan kategoriler üzerinden (operatörün gördüğü liste).
gap = df[df["is_gap"]].copy()
gap["engine_rank"] = gap["engine_score"].rank(ascending=False, method="first").astype(int)
gap["indep_rank"] = gap["composite_score"].rank(ascending=False, method="first").astype(int)
gap["delta"] = gap["engine_rank"] - gap["indep_rank"]

rho_rank, p_rank = stats.spearmanr(gap["engine_rank"], gap["indep_rank"])

# Motor skoru ile tek tek yöntemler arasındaki Spearman (yön uyumu)
def sp(col):
    s = gap[col].astype(float)
    if s.notna().sum() < 4 or s.nunique() < 3:
        return np.nan, np.nan
    r, p = stats.spearmanr(gap["engine_score"], s)
    return r, p

method_corr = {
    "M1 net harcama": sp("net_spend"),
    "M2 iki-yarı oranı": sp("m2_ratio"),
    "M3 OLS eğim (norm.)": sp("m3_slope_norm"),
    "M4 Mann-Kendall tau": sp("m4_tau"),
    "M5 momentum z": sp("m5_z"),
    "M6 ampirik sezon": sp("m6_emp_season"),
    "kompozit": (rho_rank, p_rank),
}

top = gap.sort_values("engine_rank").head(12)

L = []
def w(s=""):
    L.append(s)

w("# Bağımsız Analiz vs. Uygulama Motoru — Karşılaştırma")
w()
w(f"**Veri seti:** `generate_and_analyze.py`, seed 20260901 · "
  f"{int(ind['txn'].sum()):,} işlem (son 90 gün penceresinde) · 22 kategori · "
  f"14 aylık geçmiş.")
w(f"**Uygulama:** `GET /api/campaign-recommendations` taze `CampaignSystem_AnalysisTest` "
  f"DB'sine karşı çalıştırıldı (migration + veri yüklendi).")
w()
w("Karşılaştırma **kapsam boşluğu olan 19 kategori** üzerinden yapılır — kapsanan "
  "3 kategori (Gıda/Market, Akaryakıt, Sağlık) her iki yöntemde de listeden düşer.")
w()

w("## 1. Sıralama uyumu")
w()
w(f"Motor sırası ↔ bağımsız kompozit sıra: **Spearman ρ = {rho_rank:.3f}** "
  f"(p = {p_rank:.4f}).")
w()
w("| Bağımsız yöntem | Motor skoruyla Spearman ρ | p |")
w("|---|---:|---:|")
for name, (r, p) in method_corr.items():
    if np.isnan(r):
        w(f"| {name} | – | – |")
    else:
        w(f"| {name} | {r:+.3f} | {p:.4f} |")
w()
w("Yorum: motor ile en yüksek uyum **M1 (net harcama)** ve **M2 (iki-yarı oranı)** ile — "
  "beklenen, çünkü motorun skoru bu ikisinin ağırlıklı toplamı. OLS eğim / Mann-Kendall / "
  "momentum ile uyum daha düşük: motor trendi tek bir orana indirger, istatistiksel "
  "anlamlılık (p-değeri) kullanmaz.")
w()

w("## 2. İlk 12 kategori — yan yana")
w()
w("| # motor | # bağımsız | Δ | Kategori | Motor skor | Net harcama | Motor trend | "
  "OLS eğim (p) | MK τ (p) | Mom. z | Ampirik sezon | Motor sezon önceli |")
w("|--:|--:|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|")
for r in top.itertuples():
    w(f"| {r.engine_rank} | {r.indep_rank} | {r.delta:+d} | {r.name} | "
      f"{r.engine_score:.2f} | {r.net_spend:,.0f} | "
      f"{'' if r.m2_ratio is None else f'{r.engine_trend:+.2f}'} | "
      f"{r.m3_slope_norm:+.2f} ({r.m3_p:.2f}) | {r.m4_tau:+.2f} ({r.m4_p:.2f}) | "
      f"{r.m5_z:+.2f} | {r.m6_emp_season:.2f} | {r.engine_season:.2f} |")
w()

# Belirgin ayrışmalar
w("## 3. Belirgin ayrışmalar ve kök nedenleri")
w()

big = gap.reindex(gap["delta"].abs().sort_values(ascending=False).index).head(6)
for r in big.itertuples():
    w(f"### {r.name} — motor #{r.engine_rank}, bağımsız #{r.indep_rank} (Δ {r.delta:+d})")
    w()
    w(f"- Net harcama {r.net_spend:,.0f} ₺ · motor trend "
      f"{('yok' if r.m2_ratio is None else f'{r.engine_trend:+.2f}')} · "
      f"OLS eğim {r.m3_slope_norm:+.2f} (p={r.m3_p:.2f}) · MK τ {r.m4_tau:+.2f} "
      f"(p={r.m4_p:.2f}) · momentum z {r.m5_z:+.2f}")
    w(f"- Ampirik sezon {r.m6_emp_season:.2f} · motorun sezon önceli {r.engine_season:.2f}")
    w()

w("## 4. Genel bulgular")
w()
w("Ayrıntılı yorum ve `RecommendationOptions` ayar önerileri ana raporda "
  "(`docs/analysis/veri-seti-ve-karsilastirma-raporu.*`).")
w()

open(REPORT, "w", encoding="utf-8").write("\n".join(L))
print(f"yazıldı: {REPORT}")
print(f"\nSpearman(motor sıra, kompozit sıra) = {rho_rank:.3f}  (p={p_rank:.4f})")
print("\nİlk 12:")
print(top[["engine_rank", "indep_rank", "delta", "name", "engine_score",
           "net_spend", "engine_trend", "m3_slope_norm", "m3_p", "m4_tau",
           "m5_z", "m6_emp_season", "engine_season"]]
      .to_string(index=False))
