"""
Cards per customer.

Model
-----
Each customer holds 1 + Poisson(``EXTRA_CARDS_LAMBDA``) cards, capped at ``MAX_CARDS``.
The first card is always Primary; extras are Supplementary with probability
``SUPPLEMENTARY_SHARE``. Each card's product is drawn from the segment's
``product_weights`` — a student holds a Classic, a company employee is as likely to hold
a Gold or a Platinum. This matters for CardBased (K) campaigns, where each card accrues
its own reward.

Returns
-------
cards_df with columns (Id, CustomerId, ProductId, CardType, IsActive). CardType is 1/2
here; main.py writes the database's A/E.
"""

from __future__ import annotations

import numpy as np
import pandas as pd


def generate_cards(rng, config, customers_df):
    prod_ids = np.array([p.id for p in config.PRODUCTS])
    weights_by_segment = {}
    for seg in config.SEGMENTS:
        w = np.array(seg.product_weights, dtype=float)
        weights_by_segment[seg.id] = w / w.sum()

    seg_ids = customers_df["SegmentId"].to_numpy()
    cust_ids = customers_df["Id"].to_numpy()
    n = len(cust_ids)

    n_extra = np.minimum(
        rng.poisson(config.EXTRA_CARDS_LAMBDA, size=n), config.MAX_CARDS - 1
    )

    out_cust, out_prod, out_type = [], [], []
    for i in range(n):
        weights = weights_by_segment[int(seg_ids[i])]
        n_cards = 1 + int(n_extra[i])
        for k in range(n_cards):
            if k == 0:
                ctype = 1  # Primary
            else:
                ctype = 2 if rng.random() < config.SUPPLEMENTARY_SHARE else 1
            out_cust.append(cust_ids[i])
            out_prod.append(int(rng.choice(prod_ids, p=weights)))
            out_type.append(ctype)

    m = len(out_cust)
    return pd.DataFrame({
        "Id": np.arange(1, m + 1),
        "CustomerId": np.array(out_cust),
        "ProductId": np.array(out_prod),
        "CardType": np.array(out_type),
        "IsActive": True,
    })
