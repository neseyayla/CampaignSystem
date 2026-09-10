"""
Entry point: runs the whole generation chain and writes CSVs to ``config.OUTPUT_DIR``.

Run with:  uv run python main.py

The chain is strictly ordered because each step consumes the ones before it. Every step
draws from a single seeded numpy Generator so the full run is reproducible. Each entity
lands as its own CSV, plus a ``ground_truth.csv`` carrying the hidden traits and true
treatment effects for validation.
"""

from __future__ import annotations

import numpy as np
import pandas as pd

import config
from generators import (
    campaigns,
    cards,
    customers,
    merchants,
    participation,
    rewards,
    transactions,
)


def main() -> None:
    print(f"[synthetic-data] {config.summary()}")
    rng = np.random.default_rng(config.SEED)

    # 1. Reference / setup data
    categories_df, merchants_df = merchants.generate_merchants(rng, config)
    products_df = pd.DataFrame(
        [{"Id": p.id, "Name": p.name} for p in config.PRODUCTS]
    )
    campaigns_df = campaigns.generate_campaigns(rng, config)

    # 2. Population and its hidden traits
    customers_df, latent_df = customers.generate_customers(rng, config)
    cards_df = cards.generate_cards(rng, config, customers_df)

    # 3. Baseline behaviour (untreated potential outcomes)
    baseline_txns = transactions.generate_transactions(
        rng, config, customers_df, latent_df, cards_df, merchants_df
    )

    # 4. Targeting (with self-selection)
    participation_df, treatment_df = participation.generate_participation(
        rng, config, customers_df, latent_df, campaigns_df
    )

    # 5. Treatment effect + refunds + rewards (the causal payload)
    rewards_df, ground_truth_df, final_txns, refund_df = rewards.generate_rewards(
        rng, config, customers_df, latent_df, cards_df, baseline_txns,
        campaigns_df, treatment_df, merchants_df,
    )

    # 6. Persist
    config.OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    outputs = {
        "merchant_categories": categories_df,
        "merchants": merchants_df,
        "products": products_df,
        "campaigns": campaigns_df,
        "customers": customers_df,
        "latent": latent_df,
        "cards": cards_df,
        "transactions": final_txns,
        "refunds": refund_df,
        "participation": participation_df,
        "treatment": treatment_df,
        "rewards": rewards_df,
        "ground_truth": ground_truth_df,
    }
    for name, df in outputs.items():
        path = config.OUTPUT_DIR / f"{name}.csv"
        df.to_csv(path, index=False)
        print(f"[synthetic-data] wrote {len(df):>9,} rows -> {path.name}")


if __name__ == "__main__":
    main()
