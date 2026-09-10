"""
Customers, segments, and the latent traits that drive everything downstream.

Model
-----
1. Assign each customer a segment by the population mix in ``config.SEGMENTS``.
2. Draw the hidden per-customer traits (never persisted to the transactional tables):
     - activity        : log-normal(1.0) multiplier on transaction frequency
     - responsiveness  : Beta(a, b) in [0, 1] — strength of campaign uplift
     - refund_propensity: Gamma around 1.0 — multiplier on refund probability
     - category_prefs  : Dirichlet over merchant categories — basket composition
     - response_type   : persuadable / sure_thing / lost_cause / sleeping_dog
   Traits correlate with segment (e.g. higher tiers spend more) but keep independent
   noise, so the segment label alone does not determine behaviour.
3. Emit the observable CUSTOMER columns (CustomerNumber, Gender, SegmentId, ...).

Returns
-------
(customers_df, latent_df)
    ``customers_df`` holds only observable columns (safe to load into the DB).
    ``latent_df``, keyed by the same customer id, holds the hidden traits and feeds
    ground_truth.csv.
"""

from __future__ import annotations

import numpy as np
import pandas as pd


def generate_customers(rng, config):
    n = config.N_CUSTOMERS
    segs = config.SEGMENTS

    seg_weights = np.array([s.weight for s in segs])
    seg_weights = seg_weights / seg_weights.sum()
    seg_idx = rng.choice(len(segs), size=n, p=seg_weights)
    segment_id = seg_idx + 1  # 1-based, aligns with SEGMENTS order and product tiers

    gender = rng.choice([1, 2], size=n, p=[0.5, 0.5])  # 1=Male(E), 2=Female(K)

    lat = config.LATENT
    activity = rng.lognormal(mean=0.0, sigma=lat.activity_sigma, size=n)
    responsiveness = rng.beta(lat.responsiveness_a, lat.responsiveness_b, size=n)
    # Gamma with mean 1.0: shape=k, scale=1/k.
    refund_propensity = rng.gamma(
        lat.refund_propensity_shape, 1.0 / lat.refund_propensity_shape, size=n
    )

    rt_names = list(config.RESPONSE_TYPE_MIX.keys())
    rt_probs = np.array(list(config.RESPONSE_TYPE_MIX.values()))
    rt_probs = rt_probs / rt_probs.sum()
    response_type = rng.choice(rt_names, size=n, p=rt_probs)

    # Per-customer category preference: Dirichlet centred on the population base weights.
    base = np.array([c.base_weight for c in config.MERCHANT_CATEGORIES])
    base = base / base.sum()
    alpha = base * config.CATEGORY_DIRICHLET_ALPHA * len(base)
    cat_prefs = rng.dirichlet(alpha, size=n)  # (n, n_categories)

    customer_id = np.arange(1, n + 1)
    customers_df = pd.DataFrame({
        "Id": customer_id,
        "CustomerNumber": [f"C{100000 + i}" for i in range(n)],
        "Gender": gender,
        "SegmentId": segment_id,
        "IsActive": True,
        "IsAdmin": False,
    })

    latent_df = pd.DataFrame({
        "CustomerId": customer_id,
        "SegmentId": segment_id,
        "Activity": activity,
        "Responsiveness": responsiveness,
        "RefundPropensity": refund_propensity,
        "ResponseType": response_type,
    })
    for j, c in enumerate(config.MERCHANT_CATEGORIES):
        latent_df[f"pref_{c.code}"] = cat_prefs[:, j]

    return customers_df, latent_df
