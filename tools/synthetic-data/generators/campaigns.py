"""
Campaign definitions and their targeting (setup data).

Model
-----
Generate ``N_CAMPAIGNS`` campaigns spread across the window, mixing CampaignType (Mass vs
EnrollmentRequired), EarningType (CardBased vs CustomerBased) and scope. Scope is kept as
scalar helper columns — at most one segment and one merchant category, plus optional
gender / card type and a Min/Max amount band — which main.py writes out the way the
application stores scope: CAMPAIGN_SEGMENT and CAMPAIGN_MERCHANT junction rows. A subset
enables refund clawback so that path is exercised.

Status is derived against ``END_DATE`` as "today": Pending (not started), Ongoing (live)
or Ended (finished).

Returns
-------
campaigns_df — one row per campaign with the CAMPAIGN columns (CampaignType/EarningType
as enum names, Gender/CardType as 1/2 — main.py writes the database codes) plus the
``SegmentIdScope`` and ``CategoryCodeScope`` helpers.
"""

from __future__ import annotations

import numpy as np
import pandas as pd


def _campaign_name(campaign_id, segment, category):
    """"Akaryakıt · Çiftçi Kampanyası #3" — readable on the campaign screens and to the advisor."""
    parts = [x.name for x in (category, segment) if x is not None]
    label = " · ".join(parts) if parts else "Genel Harcama"
    return f"{label} Kampanyası #{campaign_id}"


def generate_campaigns(rng, config):
    today = np.datetime64(config.END_DATE)
    start = np.datetime64(config.START_DATE)
    span_days = (today - start).astype(int)

    segments = {s.id: s for s in config.SEGMENTS}
    categories = {c.code: c for c in config.MERCHANT_CATEGORIES}

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

        seg_scope = int(rng.choice(list(segments))) if rng.random() < 0.4 else None
        cat_scope = str(rng.choice(list(categories))) if rng.random() < 0.6 else None
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
            "Name": _campaign_name(cid, segments.get(seg_scope), categories.get(cat_scope)),
            "Description": None,
            "CampaignType": camp_type,
            # CreateCampaignDto requires it for SI and the service clears it for MASS. Every
            # enrollment here is dated on the start day, so the two bases coincide;
            # CampaignPeriod says that plainly.
            "EnrollmentBasis": "CampaignPeriod" if camp_type == "EnrollmentRequired" else None,
            "EarningType": earn_type,
            "StartDate": s_date,
            "EndDate": e_date,
            "MinimumAmount": min_amount,
            "MaximumAmount": max_amount,
            "RewardPoint": reward_point,
            "MaxRewardAmount": max_reward,
            "RefundClawbackEnabled": clawback,
            "RefundClawbackDays": clawback_days,
            # Unused-points clawback is not modelled: no PS (point spending) rows are generated.
            "UnusedPointsClawbackEnabled": False,
            "UnusedPointsClawbackDays": None,
            "UnusedPointsClawbackProcessedAt": None,
            "Gender": gender_scope,
            "CardType": cardtype_scope,
            "Status": status,
            "IsActive": True,
            # Helper scope columns (not CAMPAIGN columns; written as junction rows).
            "SegmentIdScope": seg_scope,
            "CategoryCodeScope": cat_scope,
        })

    return pd.DataFrame(rows)
