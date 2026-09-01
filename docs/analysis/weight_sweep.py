"""
Ağırlık taraması — 3 veri setinde birden.

Motorun skor formülünü Python'da birebir taklit eder; önce gerçek uygulama çıktısıyla
doğrular; sonra ağırlık ızgarasında her kombinasyonu iki ölçütle değerlendirir:

  rho_comp  : motor sırası ile BAĞIMSIZ kompozit sıra arasında Spearman ρ
              (bağımsız yöntem OLS eğim + Mann-Kendall + momentum + ampirik sezon kullanır)
  rho_truth : motor skoru ile ENJEKTE EDİLEN GERÇEK trend (ln break_factor) arasında Spearman ρ
              (yalnız gerçek kırılımı olan kategoriler)

Girdi: _out/aggregates_<seed>.json, _out/app_ranking_<seed>.json  (generate_and_analyze.py
+ uygulamanın çalıştırılmasıyla üretilir).
Çıktı: weight_sweep_sonuc.md + konsol.
"""

from __future__ import annotations

import glob
import json
import math
import os
import re
import sys

import numpy as np
import pandas as pd
from scipy import stats

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

OUT = os.path.join(os.path.dirname(__file__), "_out")
REPORT = os.path.join(os.path.dirname(__file__), "weight_sweep_sonuc.md")

BOOST = 1.75                      # CoverageGapBoost — sabit tutuluyor
CURRENT = (1.0, 1.5, 1.25)       # (SpendWeight, TrendWeight, SeasonWeight) — mevcut
PROPOSED = (0.85, 2.0, 1.0)      # 3 veri seti taramasının işaret ettiği revize öneri

SEEDS = []
DS = {}
for path in sorted(glob.glob(os.path.join(OUT, "aggregates_*.json"))):
    seed = re.search(r"aggregates_(\d+)\.json", path).group(1)
    agg = pd.DataFrame(json.load(open(path, encoding="utf-8")))
    app_path = os.path.join(OUT, f"app_ranking_{seed}.json")
    app = None
    if os.path.exists(app_path):
        raw = json.load(open(app_path, encoding="utf-8"))
        app = pd.DataFrame([{"category_id": x["merchantCategoryId"],
                             "app_score": x["score"],
                             "app_trend": x["reason"]["trendRatio"]} for x in raw])
    SEEDS.append(seed)
    DS[seed] = (agg, app)


def engine_score(agg: pd.DataFrame, ws: float, wt: float, wse: float,
                 season_col: str = "prior_season") -> pd.Series:
    max_net = agg["net_spend"].clip(lower=0).max()
    norm_spend = (agg["net_spend"] / max_net).clip(lower=0)
    trend = (agg["recent_spend"] - agg["prior_spend"]) / agg["prior_spend"].where(agg["prior_spend"] > 0)
    trend = trend.fillna(0.0).clip(-1.0, 3.0)
    raw = ws * norm_spend + wt * trend + wse * (agg[season_col] - 1.0)
    score = raw.clip(lower=0.01) * np.where(agg["is_gap"], BOOST, 1.0)
    return score


def gap_ranks(score: pd.Series, agg: pd.DataFrame) -> pd.Series:
    s = score[agg["is_gap"].values]
    return s.rank(ascending=False, method="first")


def eval_weights(ws, wt, wse):
    rc, rt = [], []
    for seed in SEEDS:
        agg, _ = DS[seed]
        sc = engine_score(agg, ws, wt, wse)
        g = agg[agg["is_gap"]].copy()
        g["eng"] = gap_ranks(sc, agg).values
        # rho_comp: motor sırası vs bağımsız kompozit sıra
        rho_c, _ = stats.spearmanr(g["eng"], g["composite_rank"])
        rc.append(rho_c)
        # rho_truth: yalnız gerçek kırılımı olan gap kategorileri
        brk = g[g["true_break_days"] > 0].copy()
        if len(brk) >= 3:
            true_sig = np.log(brk["true_break_factor"].astype(float))
            # motor skoru yüksek -> sıra küçük; gerçek büyüme -> yüksek olmalı
            rho_t, _ = stats.spearmanr(-brk["eng"], true_sig)
            rt.append(rho_t)
        else:
            rt.append(np.nan)
    return rc, rt


# ── 1. Replika doğrulaması: mevcut ağırlıklarla motor replikası vs gerçek uygulama ──
val_rows = []
for seed in SEEDS:
    agg, app = DS[seed]
    if app is None:
        continue
    sc = engine_score(agg, *CURRENT)
    m = agg[["category_id"]].copy()
    m["replica"] = sc.values
    m = m.merge(app, on="category_id", how="inner")
    rho, _ = stats.spearmanr(m["replica"].rank(ascending=False),
                             m["app_score"].rank(ascending=False))
    # trend eşleşmesi de kontrol
    mt = agg.copy()
    mt["rep_trend"] = ((mt["recent_spend"] - mt["prior_spend"])
                       / mt["prior_spend"].where(mt["prior_spend"] > 0)).fillna(0)
    mt = mt.merge(app, on="category_id")
    mae_trend = float((mt["rep_trend"] - mt["app_trend"].fillna(0)).abs().mean())
    val_rows.append((seed, rho, mae_trend))

# ── 2. Izgara taraması ──
WS_GRID = [0.40, 0.55, 0.70, 0.85, 1.00]
WT_GRID = [1.0, 1.3, 1.6, 1.9, 2.2, 2.5]
WSE_GRID = [1.0, 1.25]

grid = []
for ws in WS_GRID:
    for wt in WT_GRID:
        for wse in WSE_GRID:
            rc, rt = eval_weights(ws, wt, wse)
            grid.append(dict(ws=ws, wt=wt, wse=wse,
                             rc1=rc[0], rc2=rc[1], rc3=rc[2],
                             rc_avg=float(np.mean(rc)),
                             rt1=rt[0], rt2=rt[1], rt3=rt[2],
                             rt_avg=float(np.nanmean(rt))))
gdf = pd.DataFrame(grid)

cur = eval_weights(*CURRENT)
pro = eval_weights(*PROPOSED)
best_comp = gdf.loc[gdf["rc_avg"].idxmax()]
best_truth = gdf.loc[gdf["rt_avg"].idxmax()]
# birleşik: iki ölçütün ortalaması
gdf["combined"] = (gdf["rc_avg"] + gdf["rt_avg"]) / 2
best_comb = gdf.loc[gdf["combined"].idxmax()]


def wtuple(row):
    return (round(float(row.ws), 2), round(float(row.wt), 2), round(float(row.wse), 2))

L = []
def w(s=""):
    L.append(s)

w("# Ağırlık Taraması — 3 Veri Seti")
w()
w("| Veri seti | Tür | Enjekte kırılım (gerçek trend) |")
w("|---|---|---|")
for seed in SEEDS:
    agg, _ = DS[seed]
    brk = agg[agg["true_break_days"] > 0]
    tag = "elle kurgulanmış" if seed == "20260901" else "rastgele"
    txt = ", ".join(f"{r['name'].split(' /')[0].split(' &')[0]} ×{r['true_break_factor']:.2f}"
                    for _, r in brk.iterrows())
    w(f"| DS`{seed}` | {tag} | {txt} |")
w()

w("## 1. Motor replikası doğru mu?")
w()
w("Python replikası (mevcut ağırlıklarla) ile gerçek `GET /api/campaign-recommendations` "
  "çıktısının karşılaştırması:")
w()
w("| Veri seti | Spearman ρ (replika sırası ↔ uygulama sırası) | Ort. trend farkı |")
w("|---|--:|--:|")
for seed, rho, mae in val_rows:
    w(f"| DS`{seed}` | {rho:.3f} | {mae:.3f} |")
w()
w("ρ ≈ 1 → replika, motorun kararlarını sadık biçimde yeniden üretiyor; ağırlık taraması "
  "gerçek motoru temsil eder.")
w()

w("## 2. Mevcut vs önerilen vs en iyi")
w()
w("- **rho_comp**: motor sırası ↔ bağımsız kompozit sıra (yüksek = daha iyi)")
w("- **rho_truth**: motor skoru ↔ enjekte edilen gerçek trend, yalnız kırılımlı kategoriler (yüksek = daha iyi)")
w()
w("| Ağırlıklar (Ws, Wt, Wse) | rho_comp DS1 / DS2 / DS3 (ort.) | rho_truth DS1 / DS2 / DS3 (ort.) |")
w("|---|--:|--:|")

def fmt(rc, rt):
    return (f"{rc[0]:.2f} / {rc[1]:.2f} / {rc[2]:.2f}  (**{np.mean(rc):.2f}**)",
            f"{rt[0]:.2f} / {rt[1]:.2f} / {rt[2]:.2f}  (**{np.nanmean(rt):.2f}**)")

a, b = fmt(*cur)
w(f"| **MEVCUT** {CURRENT} | {a} | {b} |")
a, b = fmt(*pro)
w(f"| **ÖNERİLEN (revize)** {PROPOSED} | {a} | {b} |")
bc, bt, bcm = wtuple(best_comp), wtuple(best_truth), wtuple(best_comb)
w(f"| en iyi rho_comp {bc} | {best_comp.rc1:.2f} / {best_comp.rc2:.2f} / {best_comp.rc3:.2f}  "
  f"(**{best_comp.rc_avg:.2f}**) | {best_comp.rt1:.2f} / {best_comp.rt2:.2f} / {best_comp.rt3:.2f}  "
  f"(**{best_comp.rt_avg:.2f}**) |")
w(f"| en iyi rho_truth {bt} | {best_truth.rc1:.2f} / {best_truth.rc2:.2f} / {best_truth.rc3:.2f}  "
  f"(**{best_truth.rc_avg:.2f}**) | {best_truth.rt1:.2f} / {best_truth.rt2:.2f} / {best_truth.rt3:.2f}  "
  f"(**{best_truth.rt_avg:.2f}**) |")
w(f"| en iyi birleşik {bcm} | {best_comb.rc1:.2f} / {best_comb.rc2:.2f} / {best_comb.rc3:.2f}  "
  f"(**{best_comb.rc_avg:.2f}**) | {best_comb.rt1:.2f} / {best_comb.rt2:.2f} / {best_comb.rt3:.2f}  "
  f"(**{best_comb.rt_avg:.2f}**) |")
w()

w("## 3. `SpendWeight` × `TrendWeight` etkisi")
w()
w("Her hücre = 3 veri setinde ortalama **birleşik skor** ((rho_comp + rho_truth) / 2).")
w()
for wse_v in WSE_GRID:
    w(f"### Wse = {wse_v} (sezon önceli teriminin ağırlığı)")
    w()
    sub = gdf[gdf["wse"] == wse_v].pivot(index="ws", columns="wt", values="combined")
    w("| Ws \\ Wt | " + " | ".join(f"{c:.1f}" for c in sub.columns) + " |")
    w("|---" + "|--:" * len(sub.columns) + "|")
    for ws, row in sub.iterrows():
        w(f"| **{ws:.2f}** | " + " | ".join(f"{v:.2f}" for v in row.values) + " |")
    w()

w("## 4. Veri seti bazında gerçek-trend uyumu (rho_truth)")
w()
w("| Ağırlıklar | DS1 | DS2 | DS3 |")
w("|---|--:|--:|--:|")
for label, ww in [("MEVCUT", CURRENT), ("ÖNERİLEN (revize)", PROPOSED),
                  ("en iyi birleşik", bcm)]:
    _, rt = eval_weights(float(ww[0]), float(ww[1]), float(ww[2]))
    w(f"| {label} {tuple(ww)} | {rt[0]:.2f} | {rt[1]:.2f} | {rt[2]:.2f} |")
w()

w("## 5. Yorum")
w()
w("Ayrıntılı değerlendirme ana raporda (`docs/kampanya-oneri-motoru-rapor.md`, §B.6).")

open(REPORT, "w", encoding="utf-8").write("\n".join(L))
print(f"yazıldı: {REPORT}\n")
print("Replika doğrulaması:")
for seed, rho, mae in val_rows:
    print(f"  DS{seed}: rho={rho:.3f}  trend_mae={mae:.3f}")
print(f"\nMEVCUT   {CURRENT}: rho_comp ort={np.mean(cur[0]):.3f}  rho_truth ort={np.nanmean(cur[1]):.3f}")
print(f"ÖNERİLEN {PROPOSED}: rho_comp ort={np.mean(pro[0]):.3f}  rho_truth ort={np.nanmean(pro[1]):.3f}")
print(f"en iyi rho_comp   {bc}: {best_comp.rc_avg:.3f}")
print(f"en iyi rho_truth  {bt}: {best_truth.rt_avg:.3f}")
print(f"en iyi birleşik   {bcm}: comp={best_comb.rc_avg:.3f} truth={best_comb.rt_avg:.3f}")
print("\nWs\\Wt birleşik skor tablosu:")
print(sub.round(3).to_string())
