"""
Purchase transactions — the largest table and the behavioural core.

Model
-----
For each customer, draw a total purchase count Poisson(segment rate x activity x months),
then for each purchase:
  - day    ~ weighted by weekday + payday-cycle multipliers (seasonality)
  - category ~ the customer's Dirichlet ``category_prefs``
  - merchant ~ popularity within that category
  - amount ~ log-normal(segment ``spend_mu`` + category adjust, ``spend_sigma``)
  - code   ~ mostly PS (cash sale); large tickets sometimes TS (instalment)
This is the *baseline* (untreated) behaviour. Campaign-induced incremental purchases and
sleeping-dog suppression are layered on later, in the rewards step, so the counterfactual
stays clean.

Returns
-------
transactions_df with TRANSACTION columns (Id, Rrn, CardId, CustomerId, MerchantId,
TransactionCodeId, TransactionDate, Amount, OriginalTransactionId=NA, ClawbackProcessedAt=NaT).
"""

from __future__ import annotations

import numpy as np
import pandas as pd

# Additive nudge to the log-space mean per category, so tickets differ by category
# (electronics/travel big, grocery small).
_CATEGORY_MU_ADJUST = {
    "GRO": -0.2, "FUE": 0.0, "DIN": -0.1, "APP": 0.2,
    "ELE": 0.8, "TRV": 1.0, "HEA": 0.1, "ONL": 0.1,
}


def generate_transactions(rng, config, customers_df, latent_df, cards_df, merchants_df):
    # ── Calendar weights ──────────────────────────────────────────────────────
    days = pd.date_range(config.START_DATE, config.END_DATE, freq="D")
    n_days = len(days)
    dow = days.dayofweek.to_numpy()
    wk = np.array([config.WEEKDAY_MULTIPLIER[int(d)] for d in dow])
    dom = days.day.to_numpy()
    since_payday = (dom - config.PAYDAY_DAY_OF_MONTH) % 30
    payday = np.where(
        since_payday < config.PAYDAY_DECAY_DAYS,
        1.0 + (config.PAYDAY_LIFT - 1.0) * (1 - since_payday / config.PAYDAY_DECAY_DAYS),
        1.0,
    )
    day_weight = wk * payday
    day_prob = day_weight / day_weight.sum()
    day_values = days.to_numpy()  # datetime64[ns]
    n_months = n_days / 30.44

    # ── Lookups as position-indexed numpy (no per-row pandas in the loop) ─────
    seg_by_id = {i + 1: s for i, s in enumerate(config.SEGMENTS)}
    cats = config.MERCHANT_CATEGORIES
    cat_codes = [c.code for c in cats]
    cat_mu_adj = np.array([_CATEGORY_MU_ADJUST[c] for c in cat_codes])

    merch_by_cat = {}
    for c in cats:
        sub = merchants_df[merchants_df["CategoryCode"] == c.code]
        ids = sub["Id"].to_numpy()
        prob = sub["Popularity"].to_numpy()
        merch_by_cat[c.code] = (ids, prob / prob.sum())

    # Cards grouped by customer id.
    cards_by_cust: dict[int, list[tuple[int, int]]] = {}
    for cid, card_id, ctype in zip(
        cards_df["CustomerId"].to_numpy(),
        cards_df["Id"].to_numpy(),
        cards_df["CardType"].to_numpy(),
    ):
        cards_by_cust.setdefault(int(cid), []).append((int(card_id), int(ctype)))

    seg_ids = customers_df["SegmentId"].to_numpy()
    cust_ids = customers_df["Id"].to_numpy()
    activity = latent_df["Activity"].to_numpy()
    pref_cols = [f"pref_{c.code}" for c in cats]
    pref_arr = latent_df[pref_cols].to_numpy()

    chunks_card, chunks_cust, chunks_merch = [], [], []
    chunks_code, chunks_date, chunks_amt = [], [], []

    for i in range(len(cust_ids)):
        cid = int(cust_ids[i])
        seg = seg_by_id[int(seg_ids[i])]
        expected = seg.monthly_txn * activity[i] * n_months
        n_tx = rng.poisson(expected)
        if n_tx == 0:
            continue

        day_ix = rng.choice(n_days, size=n_tx, p=day_prob)

        prefs = pref_arr[i]
        prefs = prefs / prefs.sum()
        cat_ix = rng.choice(len(cats), size=n_tx, p=prefs)

        mu = seg.spend_mu + cat_mu_adj[cat_ix]
        amt = np.round(rng.lognormal(mean=mu, sigma=seg.spend_sigma), 2)

        merch = np.empty(n_tx, dtype=np.int64)
        for k in range(len(cats)):
            mask = cat_ix == k
            m = int(mask.sum())
            if m:
                ids, prob = merch_by_cat[cat_codes[k]]
                merch[mask] = rng.choice(ids, size=m, p=prob)

        cl = cards_by_cust[cid]
        card_ids = np.array([c[0] for c in cl])
        cw = np.array([1.0 if c[1] == 1 else 0.5 for c in cl])
        cw = cw / cw.sum()
        cards_assigned = card_ids[rng.choice(len(card_ids), size=n_tx, p=cw)]

        code = np.where((amt > 1000) & (rng.random(n_tx) < 0.4), 2, 1)

        secs = rng.integers(8 * 3600, 22 * 3600, size=n_tx)
        dt = day_values[day_ix] + secs.astype("timedelta64[s]")

        chunks_card.append(cards_assigned)
        chunks_cust.append(np.full(n_tx, cid, dtype=np.int64))
        chunks_merch.append(merch)
        chunks_code.append(code)
        chunks_date.append(dt)
        chunks_amt.append(amt)

    card_arr = np.concatenate(chunks_card)
    n = len(card_arr)
    df = pd.DataFrame({
        "Id": np.arange(1, n + 1, dtype=np.int64),
        "Rrn": np.char.add("R", (np.arange(n) + 1_000_000_000).astype(str)),
        "CardId": card_arr,
        "CustomerId": np.concatenate(chunks_cust),
        "MerchantId": np.concatenate(chunks_merch),
        "TransactionCodeId": np.concatenate(chunks_code),
        "TransactionDate": np.concatenate(chunks_date),
        "Amount": np.concatenate(chunks_amt),
        "OriginalTransactionId": pd.array([pd.NA] * n, dtype="Int64"),
        "ClawbackProcessedAt": pd.NaT,
    })
    return df
