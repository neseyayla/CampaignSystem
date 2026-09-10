"""
Refund (İade) transactions.

Model
-----
Each purchase is refunded with probability ``BASE_REFUND_PROB`` scaled by the customer's
latent refund propensity. A refund is a new TRANSACTION row with:
  - TransactionCodeId = ``REFUND_CODE_ID`` (İade / "IA", id 4)
  - a negative Amount equal to a fraction of the original (partial or full)
  - OriginalTransactionId pointing at the purchase it reverses
  - TransactionDate a few days after the original
  - ClawbackProcessedAt left null (the .NET batch sets it)
Partial refunds are deliberately common so both outcomes appear downstream: some drop a
purchase below a campaign's MinimumAmount (points clawed back), some do not.

Returns
-------
refund_rows_df — TRANSACTION rows, to be unioned with the purchase table.
"""

from __future__ import annotations

import numpy as np
import pandas as pd


def generate_refunds(rng, config, transactions_df, latent_df):
    # Only real purchases can be refunded.
    purchases = transactions_df[
        transactions_df["TransactionCodeId"].isin(config.PURCHASE_CODE_IDS)
    ]

    propensity = latent_df.set_index("CustomerId")["RefundPropensity"]
    per_txn_prop = purchases["CustomerId"].map(propensity).to_numpy()
    prob = np.clip(config.BASE_REFUND_PROB * per_txn_prop, 0.0, 0.95)

    refunded = rng.random(len(purchases)) < prob
    src = purchases[refunded]
    n = len(src)
    if n == 0:
        return transactions_df.iloc[0:0].copy()

    frac = rng.uniform(config.REFUND_FRACTION_MIN, config.REFUND_FRACTION_MAX, size=n)
    amount = -np.round(src["Amount"].to_numpy() * frac, 2)

    delay = rng.integers(
        config.REFUND_DELAY_DAYS_MIN, config.REFUND_DELAY_DAYS_MAX + 1, size=n
    )
    date = src["TransactionDate"].to_numpy() + delay.astype("timedelta64[D]")

    start_id = int(transactions_df["Id"].max()) + 1
    ids = np.arange(start_id, start_id + n, dtype=np.int64)

    return pd.DataFrame({
        "Id": ids,
        "Rrn": np.char.add("R", (np.arange(n) + 2_000_000_000).astype(str)),
        "CardId": src["CardId"].to_numpy(),
        "CustomerId": src["CustomerId"].to_numpy(),
        "MerchantId": src["MerchantId"].to_numpy(),
        "TransactionCodeId": config.REFUND_CODE_ID,
        "TransactionDate": date,
        "Amount": amount,
        "OriginalTransactionId": pd.array(src["Id"].to_numpy(), dtype="Int64"),
        "ClawbackProcessedAt": pd.NaT,
    })
