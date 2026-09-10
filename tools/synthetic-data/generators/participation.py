"""
Campaign participation — with deliberate self-selection.

Model
-----
For EnrollmentRequired (SI) campaigns, enrollment is NOT random: an eligible customer
enrolls with probability sigmoid(ENROLL_BASE + ENROLL_SLOPE x responsiveness), so
responsive customers both enroll more and spend more anyway. That confounding is what
makes the dataset a fair test for uplift / causal methods. Mass (MASS) campaigns treat
every eligible customer (no enrollment row).

Eligibility is the campaign's scope: segment and gender (the scalar scopes on the campaign
row). "Treated" means enrolled (SI) or eligible (Mass); eligible-but-not-enrolled SI
customers form the control group the uplift model scores against.

Returns
-------
(participation_df, treatment_df)
    ``participation_df`` matches CAMPAIGN_PARTICIPATION (observable).
    ``treatment_df`` has (CampaignId, CustomerId, Treated) for every eligible customer.
"""

from __future__ import annotations

import numpy as np
import pandas as pd


def _sigmoid(x):
    return 1.0 / (1.0 + np.exp(-x))


def generate_participation(rng, config, customers_df, latent_df, campaigns_df):
    cust_id = customers_df["Id"].to_numpy()
    gender = customers_df["Gender"].to_numpy()
    segment = customers_df["SegmentId"].to_numpy()
    responsiveness = latent_df["Responsiveness"].to_numpy()

    part_rows = []          # participation records (SI enrolled only)
    treat_frames = []       # eligibility/treatment records for every campaign
    part_id = 1

    for camp in campaigns_df.itertuples():
        eligible = np.ones(len(cust_id), dtype=bool)
        if pd.notna(camp.SegmentIdScope):
            eligible &= segment == camp.SegmentIdScope
        if pd.notna(camp.Gender):
            eligible &= gender == camp.Gender

        elig_ix = np.flatnonzero(eligible)
        if elig_ix.size == 0:
            continue

        if camp.CampaignType == "Mass":
            treated = np.ones(elig_ix.size, dtype=bool)
        else:
            p = _sigmoid(config.ENROLL_BASE + config.ENROLL_SLOPE * responsiveness[elig_ix])
            treated = rng.random(elig_ix.size) < p
            # Enrolled customers get a participation row.
            for j in np.flatnonzero(treated):
                part_rows.append({
                    "Id": part_id,
                    "CampaignId": camp.Id,
                    "CustomerId": int(cust_id[elig_ix[j]]),
                    "CardId": pd.NA,
                    "ParticipationDate": camp.StartDate,
                    "Status": "Active",
                })
                part_id += 1

        treat_frames.append(pd.DataFrame({
            "CampaignId": camp.Id,
            "CustomerId": cust_id[elig_ix],
            "Treated": treated,
        }))

    participation_df = pd.DataFrame(part_rows) if part_rows else pd.DataFrame(
        columns=["Id", "CampaignId", "CustomerId", "CardId", "ParticipationDate", "Status"]
    )
    treatment_df = pd.concat(treat_frames, ignore_index=True)
    return participation_df, treatment_df
