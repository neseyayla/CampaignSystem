"""
Entry point: runs the whole generation chain and writes CSVs to ``config.OUTPUT_DIR``.

Run with:        uv run python main.py
Quick run:       SYNTH_CUSTOMERS=2000 uv run python main.py
Pinned window:   SYNTH_END_DATE=2026-09-09 uv run python main.py

The chain is strictly ordered because each step consumes the ones before it. Every step
draws from a single seeded numpy Generator, so a run with the same seed and window is
reproducible.

Two kinds of file come out:

* Table files — one per database table, with exactly the columns and value encodings the
  table stores (E/K genders, MASS/SI campaign types, 0/1 flags, enum names for statuses),
  so loading them is a plain bulk insert. The lookup tables the application seeds itself
  (SEGMENT, MERCHANT_CATEGORY, PRODUCT, TRANSACTION_CODE, SEASONAL_PATTERN) are not
  written: their rows already exist in every migrated database, and the table files point
  at their fixed ids.
* Answer-key files — ``latent``, ``treatment``, ``ground_truth``: the hidden traits and the
  true treatment effects. Never loaded into the database; they are what an analysis or a
  model is checked against.
"""

from __future__ import annotations

import sys

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
    # Turkish names in the console: Windows otherwise prints them in the ANSI code page.
    sys.stdout.reconfigure(encoding="utf-8")
    print(f"[synthetic-data] {config.summary()}")
    rng = np.random.default_rng(config.SEED)

    # 1. Setup data
    merchants_df = merchants.generate_merchants(rng, config)
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
        rng, config, customers_df, latent_df, campaigns_df, cards_df
    )

    # 5. Treatment effect + refunds + rewards (the causal payload)
    rewards_df, ground_truth_df, final_txns, refund_df = rewards.generate_rewards(
        rng, config, customers_df, latent_df, cards_df, baseline_txns,
        campaigns_df, treatment_df, merchants_df,
    )

    # 6. Persist
    tables = _table_files(
        customers_df, cards_df, merchants_df, campaigns_df,
        final_txns, refund_df, participation_df, rewards_df,
    )
    answer_key = {"latent": latent_df, "treatment": treatment_df, "ground_truth": ground_truth_df}

    config.OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    # A file an earlier version wrote (merchant_categories.csv, refunds.csv, ...) would
    # otherwise sit next to the new ones and could be loaded by mistake.
    for stale in config.OUTPUT_DIR.glob("*.csv"):
        stale.unlink()
    for name, df in {**tables, **answer_key}.items():
        path = config.OUTPUT_DIR / f"{name}.csv"
        df.to_csv(path, index=False)
        print(f"[synthetic-data] wrote {len(df):>9,} rows -> {path.name}")

    _print_answer_key_check(tables)


def _table_files(customers_df, cards_df, merchants_df, campaigns_df,
                 txns_df, refund_df, participation_df, rewards_df) -> dict[str, pd.DataFrame]:
    """Every table file in its database shape: schema columns only, database encodings."""

    def int_code(values: pd.Series, mapping: dict) -> pd.Series:
        # Optional scopes arrive as floats with NaN; a missing value stays missing.
        return values.map(lambda v: pd.NA if pd.isna(v) else mapping[int(v)])

    customers_out = pd.DataFrame({
        "Id": customers_df["Id"],
        "CustomerNumber": customers_df["CustomerNumber"],
        "Gender": customers_df["Gender"].map(config.GENDER_DB),
        "SegmentId": customers_df["SegmentId"],
        # Generated customers cannot sign in: the app matches nothing against a null hash.
        "PasswordHash": pd.NA,
        "IsActive": customers_df["IsActive"],
        "IsAdmin": customers_df["IsAdmin"],
    })

    cards_out = cards_df.assign(CardType=cards_df["CardType"].map(config.CARD_TYPE_DB))[
        ["Id", "CustomerId", "ProductId", "CardType", "IsActive"]
    ]

    merchants_out = merchants_df[["Id", "MerchantNumber", "MerchantName", "MerchantCategoryId", "IsActive"]]

    campaigns_out = campaigns_df.assign(
        CampaignType=campaigns_df["CampaignType"].map(config.CAMPAIGN_TYPE_DB),
        EarningType=campaigns_df["EarningType"].map(config.EARNING_TYPE_DB),
        Gender=int_code(campaigns_df["Gender"], config.GENDER_DB),
        CardType=int_code(campaigns_df["CardType"], config.CARD_TYPE_DB),
        RefundClawbackDays=campaigns_df["RefundClawbackDays"].astype("Int64"),
        UnusedPointsClawbackDays=campaigns_df["UnusedPointsClawbackDays"].astype("Int64"),
    )[[
        "Id", "Name", "Description", "CampaignType", "EnrollmentBasis", "StartDate", "EndDate",
        "MinimumAmount", "MaximumAmount", "RewardPoint", "MaxRewardAmount", "EarningType",
        "Gender", "CardType", "Status", "IsActive", "RefundClawbackEnabled", "RefundClawbackDays",
        "UnusedPointsClawbackEnabled", "UnusedPointsClawbackDays", "UnusedPointsClawbackProcessedAt",
    ]]

    # Scope, the way the application stores it: an empty junction means "no restriction".
    scoped = campaigns_df.dropna(subset=["SegmentIdScope"])
    campaign_segments = pd.DataFrame({
        "CampaignId": scoped["Id"].astype(int),
        "SegmentId": scoped["SegmentIdScope"].astype(int),
    })
    # The application has no category criterion: a category campaign lists that category's
    # merchants, exactly as the recommendation engine's draft does.
    category_id = {c.code: c.id for c in config.MERCHANT_CATEGORIES}
    per_campaign = [
        pd.DataFrame({
            "CampaignId": camp.Id,
            "MerchantId": merchants_df.loc[
                merchants_df["MerchantCategoryId"] == category_id[camp.CategoryCodeScope], "Id"
            ].to_numpy(),
        })
        for camp in campaigns_df.dropna(subset=["CategoryCodeScope"]).itertuples()
    ]
    campaign_merchants = (
        pd.concat(per_campaign, ignore_index=True) if per_campaign
        else pd.DataFrame(columns=["CampaignId", "MerchantId"])
    )
    # Every campaign counts sales only, explicitly. Left empty, the junction would mean "any
    # transaction type" to the application, while the rewards step here only counts SA.
    campaign_transaction_codes = pd.DataFrame({
        "CampaignId": campaigns_df["Id"],
        "TransactionCodeId": config.SALE_CODE_ID,
    })

    # One TRANSACTION table in the database, so purchases and refunds share one file.
    transactions_out = (
        pd.concat([txns_df, refund_df], ignore_index=True)
        .sort_values("Id", kind="stable")[[
            "Id", "Rrn", "CardId", "CustomerId", "MerchantId", "TransactionCodeId",
            "TransactionDate", "Amount", "OriginalTransactionId", "ClawbackProcessedAt",
        ]]
    )

    participation_out = participation_df[
        ["Id", "CampaignId", "CustomerId", "CardId", "ParticipationDate", "Status"]
    ]
    rewards_out = rewards_df.reindex(columns=[
        "Id", "CampaignId", "CustomerId", "CardId", "RewardType",
        "QualifyingCount", "RewardPoint", "RewardDate",
    ])

    tables = {
        "customers": customers_out,
        "cards": cards_out,
        "merchants": merchants_out,
        "campaigns": campaigns_out,
        "campaign_segments": campaign_segments,
        "campaign_merchants": campaign_merchants,
        "campaign_transaction_codes": campaign_transaction_codes,
        "transactions": transactions_out,
        "participation": participation_out,
        "rewards": rewards_out,
    }
    # bit columns: a SQL Server bulk load reads 1/0, not True/False.
    for df in tables.values():
        for col in df.columns:
            if df[col].dtype == bool:
                df[col] = df[col].astype(int)
    return tables


def _print_answer_key_check(tables: dict[str, pd.DataFrame]) -> None:
    """Each segment's most over-represented categories, measured from the written files.

    This is the pattern config.SEGMENTS deliberately put in — the answer a segment analysis
    should come back with. Index = the segment's share of spend in a category divided by the
    bank-wide share; 2.0 means twice the average customer's share.
    """
    txns = tables["transactions"]
    sales = txns[txns["TransactionCodeId"] == config.SALE_CODE_ID]
    segment = tables["customers"].set_index("Id")["SegmentId"]
    category = tables["merchants"].set_index("Id")["MerchantCategoryId"]
    spend = pd.DataFrame({
        "Segment": sales["CustomerId"].map(segment),
        "Category": sales["MerchantId"].map(category),
        "Amount": sales["Amount"],
    }).groupby(["Segment", "Category"])["Amount"].sum()

    share = spend / spend.groupby(level="Segment").transform("sum")
    bank = spend.groupby(level="Category").sum()
    index = share / (bank / bank.sum()).reindex(share.index, level="Category")

    names = {c.id: c.name for c in config.MERCHANT_CATEGORIES}
    present = set(index.index.get_level_values("Segment"))
    print("\n[synthetic-data] answer-key check — top categories per segment (index vs bank)")
    for seg in config.SEGMENTS:
        if seg.id not in present:
            continue
        top = index.loc[seg.id].sort_values(ascending=False).head(3)
        cells = ", ".join(f"{names[c]} x{v:.1f}" for c, v in top.items())
        print(f"  {seg.name:<16} {cells}")


if __name__ == "__main__":
    main()
