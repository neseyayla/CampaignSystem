"""
Treatment effect, rewards, and refund clawback — the causal payload.

Pipeline
--------
1. Apply the treatment effect to the baseline transactions:
     - persuadable treated → extra qualifying purchases in the campaign window/category
       (positive uplift, observable)
     - sleeping_dog treated → a fraction of their in-scope baseline purchases suppressed
       (negative uplift, observable)
     - sure_thing / lost_cause → no change
   The true per-(customer, campaign) incremental spend (tau) is recorded for ground truth.
2. Generate refunds on the treated transactions (delegated to ``refunds``).
3. Compute rewards exactly as the .NET batch would: qualifying purchases per campaign
   criteria → Earn rows (per card for K, per customer for M, capped by MaxRewardAmount);
   then refunds that drop a purchase below MinimumAmount produce negative Clawback rows,
   using the same effective-amount rule the app uses.
4. Build ground truth: response type, true tau, reward cost and profit per treated pair.

Returns
-------
(rewards_df, ground_truth_df, final_txns_df, refund_df)
"""

from __future__ import annotations

import numpy as np
import pandas as pd

from . import refunds as refunds_mod

_INCREMENTAL_AMOUNT_SCALE = 0.8  # exponential scale above the minimum, for extra purchases


def _opt(v):
    """Normalise an optional campaign attribute: None if missing (None or NaN), else v.

    Numeric optional columns (SegmentIdScope, MaximumAmount, ...) become float with NaN
    for missing values when the DataFrame is built, and ``NaN is not None`` is True — so a
    plain ``is not None`` check would misfire. This collapses both cases to None.
    """
    return None if pd.isna(v) else v


def _reward_points(count, reward_point, max_reward):
    pts = count * reward_point
    if max_reward is not None:
        pts = np.minimum(pts, max_reward)
    return pts


def generate_rewards(rng, config, customers_df, latent_df, cards_df, txns_df,
                     campaigns_df, treatment_df, merchants_df):
    # ── Lookups ───────────────────────────────────────────────────────────────
    n_cust = len(customers_df)
    pos_of_cust = {int(c): i for i, c in enumerate(customers_df["Id"].to_numpy())}
    responsiveness = latent_df["Responsiveness"].to_numpy()
    response_type = latent_df["ResponseType"].to_numpy()

    cats = config.MERCHANT_CATEGORIES
    cat_codes = [c.code for c in cats]
    pref_cols = [f"pref_{c.code}" for c in cats]
    top_cat_idx = latent_df[pref_cols].to_numpy().argmax(axis=1)

    merch_by_cat = {}
    for c in cats:
        sub = merchants_df[merchants_df["CategoryCode"] == c.code]
        merch_by_cat[c.code] = (sub["Id"].to_numpy(), (sub["Popularity"] / sub["Popularity"].sum()).to_numpy())
    merch_cat = merchants_df.set_index("Id")["CategoryCode"]

    seg_by_cust = customers_df.set_index("Id")["SegmentId"]
    # First (primary) card per customer, for attributing incremental purchases.
    primary_card = cards_df.sort_values("Id").groupby("CustomerId")["Id"].first()

    treated = treatment_df[treatment_df["Treated"]]
    treated_by_camp = {cid: g["CustomerId"].to_numpy()
                       for cid, g in treated.groupby("CampaignId")}

    tau_spend: dict[tuple[int, int], float] = {}
    keep = np.ones(len(txns_df), dtype=bool)  # baseline rows to keep (sleeping-dog drops)
    inc_chunks = []

    txn_date = txns_df["TransactionDate"].to_numpy()
    txn_cust = txns_df["CustomerId"].to_numpy()
    txn_code = txns_df["TransactionCodeId"].to_numpy()
    txn_merch = txns_df["MerchantId"].to_numpy()
    txn_amt = txns_df["Amount"].to_numpy()
    is_purchase = np.isin(txn_code, config.PURCHASE_CODE_IDS)
    txn_catcode = txns_df["MerchantId"].map(merch_cat).to_numpy()

    # ── 1. Treatment effect ──────────────────────────────────────────────────
    for camp in campaigns_df.itertuples():
        members = treated_by_camp.get(camp.Id)
        if members is None or members.size == 0:
            continue
        pos = np.array([pos_of_cust[int(c)] for c in members])
        rtype = response_type[pos]
        s_date = np.datetime64(camp.StartDate)
        e_date = np.datetime64(camp.EndDate)
        duration = max(int((e_date - s_date).astype("timedelta64[D]").astype(int)), 1)
        cat_scope = _opt(camp.CategoryCodeScope)

        # -- persuadables: add incremental qualifying purchases --
        pmask = rtype == "persuadable"
        if pmask.any():
            p_pos = pos[pmask]
            p_cust = members[pmask]
            n_extra = rng.poisson(config.PERSUADABLE_EXTRA_LAMBDA * responsiveness[p_pos])
            total = int(n_extra.sum())
            if total > 0:
                cust_rep = np.repeat(p_cust, n_extra)
                offs = rng.integers(0, duration, size=total)
                secs = rng.integers(8 * 3600, 22 * 3600, size=total)
                dates = s_date + offs.astype("timedelta64[D]") + secs.astype("timedelta64[s]")
                amt = np.round(camp.MinimumAmount * (1.0 + rng.exponential(_INCREMENTAL_AMOUNT_SCALE, size=total)), 2)

                if cat_scope is not None:
                    cat_of_row = np.full(total, cat_scope)
                else:
                    top_rep = np.repeat(top_cat_idx[p_pos], n_extra)
                    cat_of_row = np.array([cat_codes[k] for k in top_rep])

                merch = np.empty(total, dtype=np.int64)
                for code in np.unique(cat_of_row):
                    mk = cat_of_row == code
                    ids, prob = merch_by_cat[code]
                    merch[mk] = rng.choice(ids, size=int(mk.sum()), p=prob)

                cards_rep = primary_card.loc[cust_rep].to_numpy()
                inc_chunks.append(pd.DataFrame({
                    "CardId": cards_rep,
                    "CustomerId": cust_rep,
                    "MerchantId": merch,
                    "TransactionCodeId": 1,
                    "TransactionDate": dates,
                    "Amount": amt,
                }))
                # Record incremental spend per customer for this campaign.
                per_cust = pd.Series(amt).groupby(cust_rep).sum()
                for cust, s in per_cust.items():
                    tau_spend[(camp.Id, int(cust))] = tau_spend.get((camp.Id, int(cust)), 0.0) + float(s)

        # -- sleeping dogs: suppress a fraction of in-scope baseline purchases --
        smask = rtype == "sleeping_dog"
        if smask.any():
            s_cust = set(int(c) for c in members[smask])
            cand = (
                is_purchase
                & keep
                & np.isin(txn_cust, list(s_cust))
                & (txn_date >= s_date)
                & (txn_date <= e_date)
            )
            if cat_scope is not None:
                cand &= txn_catcode == cat_scope
            cand_ix = np.flatnonzero(cand)
            if cand_ix.size:
                drop = cand_ix[rng.random(cand_ix.size) < config.SLEEPING_DOG_SUPPRESS]
                keep[drop] = False
                lost = pd.Series(txn_amt[drop]).groupby(txn_cust[drop]).sum()
                for cust, s in lost.items():
                    tau_spend[(camp.Id, int(cust))] = tau_spend.get((camp.Id, int(cust)), 0.0) - float(s)

    # ── 2. Assemble final transactions, then refunds ─────────────────────────
    baseline = txns_df[keep].copy()
    if inc_chunks:
        inc = pd.concat(inc_chunks, ignore_index=True)
        start_id = int(txns_df["Id"].max()) + 1
        inc.insert(0, "Id", np.arange(start_id, start_id + len(inc), dtype=np.int64))
        inc.insert(1, "Rrn", np.char.add("R", (np.arange(len(inc)) + 3_000_000_000).astype(str)))
        inc["OriginalTransactionId"] = pd.array([pd.NA] * len(inc), dtype="Int64")
        inc["ClawbackProcessedAt"] = pd.NaT
        final_txns = pd.concat([baseline, inc[baseline.columns]], ignore_index=True)
    else:
        final_txns = baseline.reset_index(drop=True)

    refund_df = refunds_mod.generate_refunds(rng, config, final_txns, latent_df)

    # ── 3. Rewards over the final purchases ──────────────────────────────────
    purchases = final_txns[final_txns["TransactionCodeId"].isin(config.PURCHASE_CODE_IDS)].copy()
    purchases["CatCode"] = purchases["MerchantId"].map(merch_cat)
    purchases["SegmentId"] = purchases["CustomerId"].map(seg_by_cust)
    purchases = purchases.merge(
        customers_df[["Id", "Gender"]].rename(columns={"Id": "CustomerId"}),
        on="CustomerId", how="left",
    )
    purchases = purchases.merge(
        cards_df[["Id", "CardType"]].rename(columns={"Id": "CardId"}),
        on="CardId", how="left",
    )
    # Effective amount = amount + sum of its refunds.
    if len(refund_df):
        rsum = refund_df.groupby("OriginalTransactionId")["Amount"].sum()
        purchases["Eff"] = purchases["Amount"] + purchases["Id"].map(rsum).fillna(0.0)
    else:
        purchases["Eff"] = purchases["Amount"]

    reward_rows = []
    reward_cost: dict[tuple[int, int], float] = {}  # (campaign, customer) -> earn points

    for camp in campaigns_df.itertuples():
        members = treated_by_camp.get(camp.Id)
        if members is None or members.size == 0:
            continue
        s_date = np.datetime64(camp.StartDate)
        e_date = np.datetime64(camp.EndDate)
        seg_scope = _opt(camp.SegmentIdScope)
        gender_scope = _opt(camp.Gender)
        cardtype_scope = _opt(camp.CardType)
        cat_scope = _opt(camp.CategoryCodeScope)
        max_amount = _opt(camp.MaximumAmount)
        max_reward = _opt(camp.MaxRewardAmount)

        base = (
            purchases["CustomerId"].isin(members).to_numpy()
            & (purchases["TransactionDate"].to_numpy() >= s_date)
            & (purchases["TransactionDate"].to_numpy() <= e_date)
        )
        if seg_scope is not None:
            base &= purchases["SegmentId"].to_numpy() == seg_scope
        if gender_scope is not None:
            base &= purchases["Gender"].to_numpy() == gender_scope
        if cardtype_scope is not None:
            base &= purchases["CardType"].to_numpy() == cardtype_scope
        if cat_scope is not None:
            base &= purchases["CatCode"].to_numpy() == cat_scope
        if not base.any():
            continue

        sub = purchases[base]
        group_keys = ["CustomerId"] if camp.EarningType == "CustomerBased" else ["CustomerId", "CardId"]

        def _qualify(amount_col):
            m = sub[amount_col] >= camp.MinimumAmount
            if max_amount is not None:
                m &= sub[amount_col] <= max_amount
            g = sub[m].groupby(group_keys, dropna=False).size()
            return g

        orig = _qualify("Amount")
        if orig.empty:
            continue
        orig_pts = _reward_points(orig.to_numpy(), camp.RewardPoint, max_reward)

        reward_date = camp.EndDate
        for key, cnt, pts in zip(orig.index, orig.to_numpy(), orig_pts):
            cust, card = (key, None) if camp.EarningType == "CustomerBased" else key
            reward_rows.append({
                "CampaignId": camp.Id, "CustomerId": int(cust),
                "CardId": (pd.NA if card is None else int(card)),
                "RewardType": "Earn", "QualifyingCount": int(cnt),
                "RewardPoint": float(pts), "RewardDate": reward_date,
            })
            reward_cost[(camp.Id, int(cust))] = reward_cost.get((camp.Id, int(cust)), 0.0) + float(pts)

        # Clawback: recompute qualification on effective amounts.
        if camp.RefundClawbackEnabled:
            eff = _qualify("Eff")
            eff = eff.reindex(orig.index, fill_value=0)
            eff_pts = _reward_points(eff.to_numpy(), camp.RewardPoint, max_reward)
            claw_cnt = orig.to_numpy() - eff.to_numpy()
            claw_pts = orig_pts - eff_pts
            for key, cc, cp in zip(orig.index, claw_cnt, claw_pts):
                if cc <= 0:
                    continue
                cust, card = (key, None) if camp.EarningType == "CustomerBased" else key
                reward_rows.append({
                    "CampaignId": camp.Id, "CustomerId": int(cust),
                    "CardId": (pd.NA if card is None else int(card)),
                    "RewardType": "Clawback", "QualifyingCount": -int(cc),
                    "RewardPoint": -float(cp), "RewardDate": reward_date,
                })

    rewards_df = pd.DataFrame(reward_rows)
    if len(rewards_df):
        rewards_df.insert(0, "Id", np.arange(1, len(rewards_df) + 1, dtype=np.int64))

    # ── 4. Ground truth (per treated pair) ───────────────────────────────────
    gt_rows = []
    for camp_id, cust in treated[["CampaignId", "CustomerId"]].itertuples(index=False):
        pos = pos_of_cust[int(cust)]
        tau = tau_spend.get((int(camp_id), int(cust)), 0.0)
        cost = reward_cost.get((int(camp_id), int(cust)), 0.0)
        gt_rows.append({
            "CampaignId": int(camp_id),
            "CustomerId": int(cust),
            "ResponseType": response_type[pos],
            "TauSpend": round(tau, 2),
            "EarnPoints": round(cost, 2),
            "Profit": round(config.INTERCHANGE_MARGIN * tau - cost, 2),
        })
    ground_truth_df = pd.DataFrame(gt_rows)

    return rewards_df, ground_truth_df, final_txns, refund_df
