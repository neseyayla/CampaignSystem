"""
Campaign definitions and their targeting criteria (setup data).

Model
-----
Generate ``N_CAMPAIGNS`` campaigns spread across the window, mixing CampaignType (Mass vs
EnrollmentRequired), EarningType (CardBased vs CustomerBased), and scope. Scope is kept as
scalar helper columns (a single optional segment and a single optional merchant category,
plus optional gender / card type and Min/Max amount) — a synthetic simplification of the
four junction tables that still produces genuine variation in reach, which is the signal a
recommendation model needs. A subset enables refund clawback so that path is exercised.

Status is derived against ``END_DATE`` as "today": Pending (not started), Ongoing (live) or
Ended (finished).

Returns
-------
campaigns_df — one row per campaign. Beyond the CAMPAIGN schema columns it carries
``SegmentIdScope`` and ``CategoryCodeScope`` helpers used by the rewards step.
"""

from __future__ import annotations

import numpy as np
import pandas as pd


def generate_campaigns(rng, config):
    today = np.datetime64(config.END_DATE)
    start = np.datetime64(config.START_DATE)
    span_days = (today - start).astype(int)

    seg_ids = [i + 1 for i in range(len(config.SEGMENTS))]
    cat_codes = [c.code for c in config.MERCHANT_CATEGORIES]

    rows = []
    for cid in range(1, config.N_CAMPAIGNS + 1):
        # Duration 30–60 days, start anywhere that keeps most of it inside the window.
        duration = int(rng.integers(30, 61))
        start_offset = int(rng.integers(0, max(span_days - 10, 1)))
        s_date = start + np.timedelta64(start_offset, "D")
        e_date = s_date + np.timedelta64(duration, "D")

        if s_date > today:
            status = "Pending"
        elif e_date < today:
            status = "Ended"
        else:
            status = "Ongoing"

        camp_type = "Mass" if rng.random() < 0.5 else "EnrollmentRequired"
        earn_type = "CardBased" if rng.random() < 0.4 else "CustomerBased"

        seg_scope = int(rng.choice(seg_ids)) if rng.random() < 0.4 else None
        cat_scope = str(rng.choice(cat_codes)) if rng.random() < 0.6 else None
        gender_scope = int(rng.choice([1, 2])) if rng.random() < 0.2 else None
        cardtype_scope = int(rng.choice([1, 2])) if rng.random() < 0.15 else None

        min_amount = float(rng.choice([100, 150, 200, 300, 500]))
        max_amount = None if rng.random() < 0.8 else float(min_amount + rng.choice([500, 1000, 2000]))
        reward_point = float(rng.choice([10, 20, 25, 50, 100]))
        max_reward = None if rng.random() < 0.5 else float(rng.choice([100, 250, 500]))

        clawback = bool(rng.random() < 0.5)
        clawback_days = int(rng.choice([30, 60, 90])) if clawback else None

        rows.append({
            "Id": cid,
            "Name": f"Kampanya {cid}",
            "CampaignType": camp_type,
            "EarningType": earn_type,
            "StartDate": s_date,
            "EndDate": e_date,
            "MinimumAmount": min_amount,
            "MaximumAmount": max_amount,
            "RewardPoint": reward_point,
            "MaxRewardAmount": max_reward,
            "RefundClawbackEnabled": clawback,
            "RefundClawbackDays": clawback_days,
            "Gender": gender_scope,
            "CardType": cardtype_scope,
            "Status": status,
            "IsActive": True,
            # Helper scope columns (not part of the CAMPAIGN schema).
            "SegmentIdScope": seg_scope,
            "CategoryCodeScope": cat_scope,
        })

    return pd.DataFrame(rows)
