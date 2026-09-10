"""
Purchase transactions — the largest table and the behavioural core.

Model
-----
Each customer's purchases are drawn jointly over (day, merchant category), with intensity

    day-of-week × payday
      × the category's season for that month (the app's SEASONAL_PATTERN prior, times the
        segment's own seasonal effects)
      × the category's trend
      × the customer's category preference

The purchase count is Poisson(segment rate × activity × months), scaled by how much of
that intensity the year actually holds — so a peak month adds purchases in its category
rather than borrowing them from the others. Then, per purchase:
  - merchant ~ popularity within the category
  - amount   ~ log-normal(segment ``spend_mu`` + category ``mu_adjust``, segment ``spend_sigma``)
  - code     = SA, the only spending code
This is the *baseline* (untreated) behaviour. Campaign effects are layered on in the
rewards step, so the counterfactual stays clean.

Returns
-------
transactions_df with TRANSACTION columns (Id, Rrn, CardId, CustomerId, MerchantId,
TransactionCodeId, TransactionDate, Amount, OriginalTransactionId=NA, ClawbackProcessedAt=NaT).
"""

from __future__ import annotations

import numpy as np
import pandas as pd


def _intensity_by_segment(config, days):
    """(n_days, n_categories) purchase intensity per segment id; 1.0 everywhere is neutral."""
    codes = [c.code for c in config.MERCHANT_CATEGORIES]

    weekday = np.array([config.WEEKDAY_MULTIPLIER[int(d)] for d in days.dayofweek])
    since_payday = (days.day.to_numpy() - config.PAYDAY_DAY_OF_MONTH) % 30
    payday = np.where(
        since_payday < config.PAYDAY_DECAY_DAYS,
        1.0 + (config.PAYDAY_LIFT - 1.0) * (1 - since_payday / config.PAYDAY_DECAY_DAYS),
        1.0,
    )
    day_weight = weekday * payday
    day_weight = day_weight / day_weight.mean()

    season = np.ones((12, len(codes)))
    for j, code in enumerate(codes):
        for month, weight in config.CATEGORY_SEASONALITY.get(code, {}).items():
            season[month - 1, j] = weight

    elapsed_months = np.arange(len(days)) / 30.44
    trend = np.column_stack([
        (1.0 + config.CATEGORY_TREND.get(code, 0.0)) ** elapsed_months for code in codes
    ])

    month_ix = days.month.to_numpy() - 1
    intensity = {}
    for seg in config.SEGMENTS:
        seg_season = season.copy()
        for code, months in seg.seasonal.items():
            j = codes.index(code)
            for month, weight in months.items():
                seg_season[month - 1, j] *= weight
        intensity[seg.id] = day_weight[:, None] * seg_season[month_ix] * trend
    return intensity


def generate_transactions(rng, config, customers_df, latent_df, cards_df, merchants_df):
    days = pd.date_range(config.START_DATE, config.END_DATE, freq="D")
    n_days = len(days)
    day_values = days.to_numpy()
    n_months = n_days / 30.44
    intensity = _intensity_by_segment(config, days)

    cats = config.MERCHANT_CATEGORIES
    n_cat = len(cats)
    cat_mu_adj = np.array([c.mu_adjust for c in cats])
    seg_by_id = {s.id: s for s in config.SEGMENTS}

    # Position-indexed lookups, so the per-customer loop touches only numpy.
    merch_by_cat = {}
    for j, c in enumerate(cats):
        sub = merchants_df[merchants_df["MerchantCategoryId"] == c.id]
        prob = sub["Popularity"].to_numpy()
        merch_by_cat[j] = (sub["Id"].to_numpy(), prob / prob.sum())

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
    pref_arr = latent_df[[f"pref_{c.code}" for c in cats]].to_numpy()

    chunks_card, chunks_cust, chunks_merch = [], [], []
    chunks_date, chunks_amt = [], []

    for i in range(len(cust_ids)):
        cid = int(cust_ids[i])
        seg = seg_by_id[int(seg_ids[i])]
        prefs = pref_arr[i] / pref_arr[i].sum()
        cell = intensity[seg.id] * prefs  # (n_days, n_cat)

        # A neutral year sums to n_days, so this keeps monthly_txn the rate of an ordinary
        # month; seasonality and trend then move the total up or down from there.
        expected = seg.monthly_txn * activity[i] * n_months * cell.sum() / n_days
        n_tx = rng.poisson(expected)
        if n_tx == 0:
            continue

        flat = cell.ravel()
        pick = rng.choice(flat.size, size=n_tx, p=flat / flat.sum())
        day_ix, cat_ix = np.divmod(pick, n_cat)

        mu = seg.spend_mu + cat_mu_adj[cat_ix]
        amt = np.maximum(np.round(rng.lognormal(mean=mu, sigma=seg.spend_sigma), 2), 1.0)

        merch = np.empty(n_tx, dtype=np.int64)
        for k in np.unique(cat_ix):
            mask = cat_ix == k
            ids, prob = merch_by_cat[int(k)]
            merch[mask] = rng.choice(ids, size=int(mask.sum()), p=prob)

        cl = cards_by_cust[cid]
        card_ids = np.array([c[0] for c in cl])
        cw = np.array([1.0 if c[1] == 1 else 0.5 for c in cl])
        cw = cw / cw.sum()
        cards_assigned = card_ids[rng.choice(len(card_ids), size=n_tx, p=cw)]

        secs = rng.integers(8 * 3600, 22 * 3600, size=n_tx)
        dt = day_values[day_ix] + secs.astype("timedelta64[s]")

        chunks_card.append(cards_assigned)
        chunks_cust.append(np.full(n_tx, cid, dtype=np.int64))
        chunks_merch.append(merch)
        chunks_date.append(dt)
        chunks_amt.append(amt)

    card_arr = np.concatenate(chunks_card)
    n = len(card_arr)
    return pd.DataFrame({
        "Id": np.arange(1, n + 1, dtype=np.int64),
        "Rrn": np.char.add("R", (np.arange(n) + 1_000_000_000).astype(str)),
        "CardId": card_arr,
        "CustomerId": np.concatenate(chunks_cust),
        "MerchantId": np.concatenate(chunks_merch),
        "TransactionCodeId": config.SALE_CODE_ID,
        "TransactionDate": np.concatenate(chunks_date),
        "Amount": np.concatenate(chunks_amt),
        "OriginalTransactionId": pd.array([pd.NA] * n, dtype="Int64"),
        "ClawbackProcessedAt": pd.NaT,
    })
