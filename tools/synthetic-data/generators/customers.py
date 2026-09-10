"""
Customers, segments, and the latent traits that drive everything downstream.

Model
-----
1. Assign each customer a segment by the population mix in ``config.SEGMENTS``, and a
   gender by that segment's ``female_share``.
2. Draw the hidden per-customer traits (never persisted to the transactional tables):
     - activity         : log-normal(1.0) multiplier on transaction frequency
     - responsiveness   : Beta(a, b) in [0, 1] — strength of campaign uplift
     - refund_propensity: Gamma around 1.0 — multiplier on refund probability
     - category_prefs   : Dirichlet centred on the *segment's* basket — basket composition
     - response_type    : persuadable / sure_thing / lost_cause / sleeping_dog
   The segment decides where a customer's basket is centred; the Dirichlet adds individual
   spread around it, so the segment label shifts behaviour without fixing it.
3. Emit the observable CUSTOMER columns (CustomerNumber, Gender, SegmentId, ...).

Returns
-------
(customers_df, latent_df)
    ``customers_df`` holds only observable columns (Gender as 1/2 here; main.py writes the
    database's E/K). ``latent_df``, keyed by the same customer id, holds the hidden traits
    and is written as an answer-key file.
"""

from __future__ import annotations

import numpy as np
import pandas as pd


def generate_customers(rng, config):
    n = config.N_CUSTOMERS
    segs = config.SEGMENTS

    seg_weights = np.array([s.weight for s in segs])
    seg_idx = rng.choice(len(segs), size=n, p=seg_weights / seg_weights.sum())
    segment_id = np.array([s.id for s in segs])[seg_idx]

    # Gender leans on the segment: Ev Hanımı is almost entirely female, Çiftçi mostly male.
    female_share = np.array([s.female_share for s in segs])[seg_idx]
    gender = np.where(rng.random(n) < female_share, 2, 1)  # 1 = male (E), 2 = female (K)

    lat = config.LATENT
    activity = rng.lognormal(mean=0.0, sigma=lat.activity_sigma, size=n)
    responsiveness = rng.beta(lat.responsiveness_a, lat.responsiveness_b, size=n)
    # Gamma with mean 1.0: shape=k, scale=1/k.
    refund_propensity = rng.gamma(
        lat.refund_propensity_shape, 1.0 / lat.refund_propensity_shape, size=n
    )

    rt_names = list(config.RESPONSE_TYPE_MIX.keys())
    rt_probs = np.array(list(config.RESPONSE_TYPE_MIX.values()))
    response_type = rng.choice(rt_names, size=n, p=rt_probs / rt_probs.sum())

    # Category preference: a segment's basket is the population base weights times its
    # affinities, and each customer's own basket is a Dirichlet draw around it. This is
    # where "a farmer spends on fuel" enters the data.
    cats = config.MERCHANT_CATEGORIES
    base = np.array([c.base_weight for c in cats])
    cat_prefs = np.empty((n, len(cats)))
    for k, seg in enumerate(segs):
        members = np.flatnonzero(seg_idx == k)
        if members.size == 0:
            continue
        centre = base * np.array([seg.category_affinity.get(c.code, 1.0) for c in cats])
        alpha = centre / centre.sum() * config.CATEGORY_CONCENTRATION
        cat_prefs[members] = rng.dirichlet(alpha, size=members.size)

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
    for j, c in enumerate(cats):
        latent_df[f"pref_{c.code}"] = cat_prefs[:, j]

    return customers_df, latent_df
