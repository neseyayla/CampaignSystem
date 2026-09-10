"""
Readable, joined views over the generated CSVs.

The CSVs in ``output/`` are *normalised* — they mirror the DB tables, so almost every
column is a foreign key or an enum code. A row like ``1,R1000000000,1,1,134,1,...`` is
faithful to the schema and unreadable to a human.

This script denormalises them: it loads every CSV into a pandas DataFrame, resolves the
keys (merchant, category, product, segment, transaction code) and the enum codes (gender,
card type) into their labels, and writes wide tables where each row tells the whole story
in words. It also attributes each purchase to the campaign(s) it qualifies for, which no
single table records — campaign membership lives in the intersection of the treatment
list, the campaign window and the campaign's targeting criteria.

Nothing here changes the generated data; views are a read-only projection written to
``output/views/``.

Run with:  uv run python views.py [--rows N] [--sample-only]
"""

from __future__ import annotations

import argparse
import sys

import numpy as np
import pandas as pd

import config

VIEWS_DIR = config.OUTPUT_DIR / "views"

# ── Label maps ────────────────────────────────────────────────────────────────
# Single place to change if you want the views in another language: every code → text
# substitution below flows from these.

GENDER_LABELS = {1: "Male", 2: "Female"}
CARD_TYPE_LABELS = {1: "Primary", 2: "Supplementary"}
SEGMENT_LABELS = {i + 1: s.name for i, s in enumerate(config.SEGMENTS)}
TXN_CODE_LABELS = {t.id: t.name for t in config.TRANSACTION_CODES}
CATEGORY_LABELS = {c.code: c.name for c in config.MERCHANT_CATEGORIES}
PRODUCT_LABELS = {p.id: p.name for p in config.PRODUCTS}

ALL = "All"    # shown where a campaign puts no restriction on a dimension
NONE = "—"     # shown where a row has no value at all


def _load() -> dict[str, pd.DataFrame]:
    """Read every generated CSV, parsing the date columns as dates."""
    dates = {
        "campaigns": ["StartDate", "EndDate"],
        "transactions": ["TransactionDate", "ClawbackProcessedAt"],
        "refunds": ["TransactionDate", "ClawbackProcessedAt"],
        "participation": ["ParticipationDate"],
        "rewards": ["RewardDate"],
    }
    names = [
        "merchant_categories", "merchants", "products", "campaigns", "customers",
        "latent", "cards", "transactions", "refunds", "participation", "treatment",
        "rewards", "ground_truth",
    ]
    out = {}
    for name in names:
        path = config.OUTPUT_DIR / f"{name}.csv"
        if not path.exists():
            sys.exit(f"missing {path} — run `uv run python main.py` first")
        out[name] = pd.read_csv(path, parse_dates=dates.get(name))
    return out


def _opt_label(value, labels: dict, default: str = ALL) -> str:
    """A campaign scope value → its label, or ``default`` when the scope is unset."""
    if pd.isna(value):
        return default
    return labels.get(int(value), str(value))


# ── Views ────────────────────────────────────────────────────────────────────

def build_movements(d: dict[str, pd.DataFrame]) -> pd.DataFrame:
    """Every card movement — purchases and refunds — as one row of plain words.

    Purchases and refunds live in separate CSVs but are one table in the database, so
    they are unioned here. Each row carries who spent (customer number, segment, gender),
    on which card (type, product), where (merchant, category), what came back as a refund,
    and which campaign(s) the purchase qualifies for.
    """
    merch = d["merchants"].set_index("Id")
    cards = d["cards"].set_index("Id")
    cust = d["customers"].set_index("Id")

    mv = pd.concat([d["transactions"], d["refunds"]], ignore_index=True)
    mv = mv.sort_values("TransactionDate", kind="stable").reset_index(drop=True)

    # Refunds carry the sign, so a purchase's net is amount + its refunds.
    refund_sum = d["refunds"].groupby("OriginalTransactionId")["Amount"].sum()
    refunded = mv["Id"].map(refund_sum).fillna(0.0)

    out = pd.DataFrame({
        "TransactionId": mv["Id"],
        "Rrn": mv["Rrn"],
        "TransactionDate": mv["TransactionDate"],
        "Weekday": mv["TransactionDate"].dt.day_name(),
        "Month": mv["TransactionDate"].dt.to_period("M").astype(str),
        "TransactionType": mv["TransactionCodeId"].map(TXN_CODE_LABELS),
        "CustomerNumber": mv["CustomerId"].map(cust["CustomerNumber"]),
        "Segment": mv["CustomerId"].map(cust["SegmentId"]).map(SEGMENT_LABELS),
        "Gender": mv["CustomerId"].map(cust["Gender"]).map(GENDER_LABELS),
        "CardType": mv["CardId"].map(cards["CardType"]).map(CARD_TYPE_LABELS),
        "CardProduct": mv["CardId"].map(cards["ProductId"]).map(PRODUCT_LABELS),
        "Merchant": mv["MerchantId"].map(merch["MerchantName"]),
        "MerchantCategory": mv["MerchantId"].map(merch["CategoryCode"]).map(CATEGORY_LABELS),
        "Amount": mv["Amount"].round(2),
        # `+ 0.0` so an unrefunded purchase reads 0.0 rather than -0.0.
        "RefundedAmount": (-refunded).round(2) + 0.0,
        "NetAmount": (mv["Amount"] + refunded).round(2),
        "Campaigns": _attribute_campaigns(d, mv),
        # Keys kept at the end so a row can still be traced back to the raw tables.
        "CustomerId": mv["CustomerId"],
        "CardId": mv["CardId"],
        "MerchantId": mv["MerchantId"],
        "OriginalTransactionId": mv["OriginalTransactionId"],
    })
    out["CampaignCount"] = np.where(
        out["Campaigns"] == "", 0, out["Campaigns"].str.count(", ") + 1
    )
    out.loc[out["Campaigns"] == "", "Campaigns"] = NONE
    for col in ("TransactionType", "Segment", "Gender", "CardType", "CardProduct",
                "MerchantCategory", "Weekday"):
        out[col] = out[col].astype("category")
    return out


def _attribute_campaigns(d: dict[str, pd.DataFrame], mv: pd.DataFrame) -> pd.Series:
    """Names of the campaigns each purchase qualifies for, joined into one string.

    Mirrors the qualification rule the reward step applies: the customer must be in the
    campaign's treated set, the purchase must fall inside the window, satisfy every scope
    the campaign sets (segment, gender, card type, merchant category) and clear the
    Minimum/Maximum amount band. A purchase can qualify for more than one campaign, hence
    a joined list rather than a single id. A refund inherits the attribution of the
    purchase it reverses.
    """
    merch_cat = d["merchants"].set_index("Id")["CategoryCode"]
    cust = d["customers"].set_index("Id")
    cards = d["cards"].set_index("Id")

    date = mv["TransactionDate"].to_numpy()
    cust_id = mv["CustomerId"].to_numpy()
    amount = mv["Amount"].to_numpy()
    segment = mv["CustomerId"].map(cust["SegmentId"]).to_numpy()
    gender = mv["CustomerId"].map(cust["Gender"]).to_numpy()
    card_type = mv["CardId"].map(cards["CardType"]).to_numpy()
    cat_code = mv["MerchantId"].map(merch_cat).to_numpy()
    is_purchase = np.isin(mv["TransactionCodeId"].to_numpy(), config.PURCHASE_CODE_IDS)

    treated = d["treatment"][d["treatment"]["Treated"]]
    treated_by_camp = {cid: g["CustomerId"].to_numpy()
                       for cid, g in treated.groupby("CampaignId")}

    acc = np.full(len(mv), "", dtype=object)
    for camp in d["campaigns"].itertuples():
        members = treated_by_camp.get(camp.Id)
        if members is None or members.size == 0:
            continue
        mask = (
            is_purchase
            & np.isin(cust_id, members)
            & (date >= np.datetime64(camp.StartDate))
            & (date <= np.datetime64(camp.EndDate))
            & (amount >= camp.MinimumAmount)
        )
        if not pd.isna(camp.MaximumAmount):
            mask &= amount <= camp.MaximumAmount
        if not pd.isna(camp.SegmentIdScope):
            mask &= segment == camp.SegmentIdScope
        if not pd.isna(camp.Gender):
            mask &= gender == camp.Gender
        if not pd.isna(camp.CardType):
            mask &= card_type == camp.CardType
        if not pd.isna(camp.CategoryCodeScope):
            mask &= cat_code == camp.CategoryCodeScope

        idx = np.flatnonzero(mask)
        if idx.size:
            cur = acc[idx]
            acc[idx] = np.where(cur == "", camp.Name, cur + ", " + camp.Name)

    attributed = pd.Series(acc, index=mv.index)
    # A refund belongs to whatever its original purchase belonged to.
    by_txn = pd.Series(acc, index=mv["Id"].to_numpy())
    inherited = mv["OriginalTransactionId"].map(by_txn)
    return attributed.where(attributed != "", inherited.fillna("")).astype(str)


def build_campaigns(d: dict[str, pd.DataFrame], movements: pd.DataFrame) -> pd.DataFrame:
    """One row per campaign: its rules in words, plus what it actually produced."""
    earn = d["rewards"][d["rewards"]["RewardType"] == "Earn"].groupby("CampaignId")
    claw = d["rewards"][d["rewards"]["RewardType"] == "Clawback"].groupby("CampaignId")
    earn_pts, earn_n = earn["RewardPoint"].sum(), earn.size()
    claw_pts, claw_n = claw["RewardPoint"].sum(), claw.size()
    treated = d["treatment"][d["treatment"]["Treated"]].groupby("CampaignId").size()
    joined = d["participation"].groupby("CampaignId").size()
    gt = d["ground_truth"].groupby("CampaignId").agg(
        Tau=("TauSpend", "sum"), Profit=("Profit", "sum")
    )

    # Qualifying volume, recovered from the attribution built for the movements view.
    qual = (
        movements.loc[(movements["Campaigns"] != NONE) & (movements["Amount"] > 0),
                      ["Campaigns", "Amount"]]
        .assign(Campaigns=lambda x: x["Campaigns"].str.split(", "))
        .explode("Campaigns")
        .groupby("Campaigns")["Amount"].agg(["size", "sum"])
    )

    rows = []
    for c in d["campaigns"].itertuples():
        rows.append({
            "CampaignId": c.Id,
            "Name": c.Name,
            "Status": c.Status,
            "CampaignType": c.CampaignType,
            "EarningType": c.EarningType,
            "StartDate": c.StartDate.date(),
            "EndDate": c.EndDate.date(),
            "DurationDays": (c.EndDate - c.StartDate).days,
            "TargetSegment": _opt_label(c.SegmentIdScope, SEGMENT_LABELS),
            "TargetGender": _opt_label(c.Gender, GENDER_LABELS),
            "TargetCardType": _opt_label(c.CardType, CARD_TYPE_LABELS),
            "TargetCategory": CATEGORY_LABELS.get(c.CategoryCodeScope, ALL),
            "MinimumAmount": c.MinimumAmount,
            "MaximumAmount": NONE if pd.isna(c.MaximumAmount) else c.MaximumAmount,
            "RewardPoint": c.RewardPoint,
            "MaxRewardAmount": NONE if pd.isna(c.MaxRewardAmount) else c.MaxRewardAmount,
            "Clawback": f"Yes ({int(c.RefundClawbackDays)}d)" if c.RefundClawbackEnabled else "No",
            "Participants": int(joined.get(c.Id, 0)),
            "TreatedCustomers": int(treated.get(c.Id, 0)),
            "QualifyingTransactions": int(qual["size"].get(c.Name, 0)),
            "QualifyingAmount": round(float(qual["sum"].get(c.Name, 0.0)), 2),
            "EarnRows": int(earn_n.get(c.Id, 0)),
            "EarnPoints": round(float(earn_pts.get(c.Id, 0.0)), 2),
            "ClawbackRows": int(claw_n.get(c.Id, 0)),
            "ClawbackPoints": round(float(claw_pts.get(c.Id, 0.0)), 2),
            "IncrementalSpend": round(float(gt["Tau"].get(c.Id, 0.0)), 2),
            "Profit": round(float(gt["Profit"].get(c.Id, 0.0)), 2),
        })
    out = pd.DataFrame(rows)
    out["NetPoints"] = (out["EarnPoints"] + out["ClawbackPoints"]).round(2)
    return out


def build_rewards(d: dict[str, pd.DataFrame]) -> pd.DataFrame:
    """One row per reward: who earned (or gave back) what, on which campaign and card."""
    camps = d["campaigns"].set_index("Id")
    cust = d["customers"].set_index("Id")
    cards = d["cards"].set_index("Id")
    r = d["rewards"]

    return pd.DataFrame({
        "RewardId": r["Id"],
        "Campaign": r["CampaignId"].map(camps["Name"]),
        "CampaignType": r["CampaignId"].map(camps["CampaignType"]),
        "EarningType": r["CampaignId"].map(camps["EarningType"]),
        "CustomerNumber": r["CustomerId"].map(cust["CustomerNumber"]),
        "Segment": r["CustomerId"].map(cust["SegmentId"]).map(SEGMENT_LABELS),
        "Gender": r["CustomerId"].map(cust["Gender"]).map(GENDER_LABELS),
        # CustomerBased campaigns reward the customer, not a card, so CardId is empty.
        "CardType": r["CardId"].map(cards["CardType"]).map(CARD_TYPE_LABELS).fillna(NONE),
        "CardProduct": r["CardId"].map(cards["ProductId"]).map(PRODUCT_LABELS).fillna(NONE),
        "RewardType": r["RewardType"],
        "QualifyingCount": r["QualifyingCount"],
        "RewardPoint": r["RewardPoint"],
        "RewardDate": r["RewardDate"].dt.date,
    })


def build_participation(d: dict[str, pd.DataFrame]) -> pd.DataFrame:
    """One row per enrollment, with the hidden response type beside it.

    ``Treated`` and ``ResponseType`` are ground truth — what a targeting model is trying
    to predict — and sit here so the self-selection is visible at a glance.
    """
    camps = d["campaigns"].set_index("Id")
    cust = d["customers"].set_index("Id")
    latent = d["latent"].set_index("CustomerId")
    p = d["participation"]
    treated = d["treatment"].set_index(["CampaignId", "CustomerId"])["Treated"]

    keys = pd.MultiIndex.from_arrays([p["CampaignId"], p["CustomerId"]])
    return pd.DataFrame({
        "ParticipationId": p["Id"],
        "Campaign": p["CampaignId"].map(camps["Name"]),
        "CampaignType": p["CampaignId"].map(camps["CampaignType"]),
        "CustomerNumber": p["CustomerId"].map(cust["CustomerNumber"]),
        "Segment": p["CustomerId"].map(cust["SegmentId"]).map(SEGMENT_LABELS),
        "Gender": p["CustomerId"].map(cust["Gender"]).map(GENDER_LABELS),
        "ParticipationDate": p["ParticipationDate"].dt.date,
        "Status": p["Status"],
        "Treated": treated.reindex(keys).to_numpy(),
        "ResponseType": p["CustomerId"].map(latent["ResponseType"]),
        "Responsiveness": p["CustomerId"].map(latent["Responsiveness"]).round(3),
    })


def build_customers(d: dict[str, pd.DataFrame], movements: pd.DataFrame) -> pd.DataFrame:
    """One row per customer: profile, cards, spending behaviour and campaign outcome."""
    cust = d["customers"]
    latent = d["latent"].set_index("CustomerId")
    rewards = d["rewards"]

    purchases = movements[movements["Amount"] > 0]
    refunds = movements[movements["Amount"] < 0]
    spend = purchases.groupby("CustomerId")["Amount"].agg(["size", "sum", "mean"])
    net = purchases.groupby("CustomerId")["NetAmount"].sum()
    ref = refunds.groupby("CustomerId")["Amount"].agg(["size", "sum"])
    top_cat = (
        purchases.groupby(["CustomerId", "MerchantCategory"], observed=True)["Amount"]
        .sum().reset_index().sort_values("Amount", ascending=False)
        .drop_duplicates("CustomerId").set_index("CustomerId")["MerchantCategory"]
    )
    card_agg = d["cards"].groupby("CustomerId").agg(
        CardCount=("Id", "size"),
        Products=("ProductId", lambda s: ", ".join(sorted({PRODUCT_LABELS[p] for p in s}))),
    )
    earn = rewards[rewards["RewardType"] == "Earn"].groupby("CustomerId")["RewardPoint"].sum()
    claw = rewards[rewards["RewardType"] == "Clawback"].groupby("CustomerId")["RewardPoint"].sum()
    joined = d["participation"].groupby("CustomerId").size()
    treated = d["treatment"][d["treatment"]["Treated"]].groupby("CustomerId").size()

    cid = cust["Id"]
    out = pd.DataFrame({
        "CustomerId": cid,
        "CustomerNumber": cust["CustomerNumber"],
        "Gender": cust["Gender"].map(GENDER_LABELS),
        "Segment": cust["SegmentId"].map(SEGMENT_LABELS),
        "CardCount": cid.map(card_agg["CardCount"]).fillna(0).astype(int),
        "Products": cid.map(card_agg["Products"]).fillna(NONE),
        "TransactionCount": cid.map(spend["size"]).fillna(0).astype(int),
        "TotalSpend": cid.map(spend["sum"]).fillna(0.0).round(2),
        "AvgTicket": cid.map(spend["mean"]).fillna(0.0).round(2),
        "TopCategory": cid.map(top_cat).astype(object).fillna(NONE),
        "RefundCount": cid.map(ref["size"]).fillna(0).astype(int),
        "RefundAmount": cid.map(ref["sum"]).fillna(0.0).round(2),
        "NetSpend": cid.map(net).fillna(0.0).round(2),
        "CampaignsJoined": cid.map(joined).fillna(0).astype(int),
        "CampaignsTreated": cid.map(treated).fillna(0).astype(int),
        "EarnPoints": cid.map(earn).fillna(0.0).round(2),
        "ClawbackPoints": cid.map(claw).fillna(0.0).round(2),
        # Ground truth — hidden from the app, kept here to validate models.
        "ResponseType": cid.map(latent["ResponseType"]),
        "Activity": cid.map(latent["Activity"]).round(3),
        "Responsiveness": cid.map(latent["Responsiveness"]).round(3),
        "RefundPropensity": cid.map(latent["RefundPropensity"]).round(3),
    })
    out["NetPoints"] = (out["EarnPoints"] + out["ClawbackPoints"]).round(2)
    return out


# ── Output ───────────────────────────────────────────────────────────────────

def _preview(name: str, df: pd.DataFrame, rows: int) -> None:
    with pd.option_context("display.max_columns", None, "display.width", 250,
                           "display.max_colwidth", 26):
        print(f"\n── {name}  ({len(df):,} rows x {len(df.columns)} cols) "
              + "─" * max(0, 50 - len(name)))
        print(df.head(rows).to_string(index=False))


def main() -> None:
    ap = argparse.ArgumentParser(description="Build readable joined views of the output CSVs.")
    ap.add_argument("--rows", type=int, default=8,
                    help="rows printed per view (default 8)")
    ap.add_argument("--sample-only", action="store_true",
                    help="skip the full CSVs; write only the 200-row samples")
    args = ap.parse_args()

    d = _load()
    print(f"[views] loaded {len(d)} tables from {config.OUTPUT_DIR}")

    movements = build_movements(d)
    views = {
        "movements_wide": movements,
        "campaigns_wide": build_campaigns(d, movements),
        "rewards_wide": build_rewards(d),
        "participation_wide": build_participation(d),
        "customers_wide": build_customers(d, movements),
    }

    VIEWS_DIR.mkdir(parents=True, exist_ok=True)
    for name, df in views.items():
        _preview(name, df, args.rows)
        # utf-8-sig so Excel opens the Turkish merchant/category names correctly.
        sample = VIEWS_DIR / f"{name}_sample.csv"
        df.head(200).to_csv(sample, index=False, encoding="utf-8-sig")
        if args.sample_only:
            print(f"[views] wrote {min(len(df), 200):>9,} rows -> views/{sample.name}")
            continue
        path = VIEWS_DIR / f"{name}.csv"
        df.to_csv(path, index=False, encoding="utf-8-sig")
        print(f"[views] wrote {len(df):>9,} rows -> views/{path.name} (+ {sample.name})")


if __name__ == "__main__":
    main()
