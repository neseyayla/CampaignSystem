"""
Merchants — the MERCHANT rows the transactions point at.

Model
-----
For each category in ``config.MERCHANT_CATEGORIES`` generate ``n_merchants`` merchants
with Faker-generated names. Within a category merchants are not equally popular: their
pull follows a power law (a few dominant chains, a long tail of small ones), so
transactions later concentrate realistically.

The categories themselves are not generated — the application seeds them, and each
merchant points at the seeded id. Merchant ids start at ``MERCHANT_ID_START`` because the
seed already owns merchants 1–12.

Returns
-------
merchants_df with the MERCHANT columns plus two helper columns used when assigning
transactions — ``CategoryCode`` and ``Popularity`` — which main.py drops when writing.
"""

from __future__ import annotations

import numpy as np
import pandas as pd
from faker import Faker


def generate_merchants(rng, config):
    fake = Faker("tr_TR")
    Faker.seed(int(rng.integers(0, 2**31 - 1)))

    rows = []
    merch_id = config.MERCHANT_ID_START

    for cat in config.MERCHANT_CATEGORIES:
        # Zipf-like popularity: a few merchants dominate, most are small.
        pop = 1.0 / np.arange(1, cat.n_merchants + 1) ** 1.1
        pop = pop / pop.sum()
        # Shuffle so the dominant merchant is not always the lowest id.
        rng.shuffle(pop)

        for k in range(cat.n_merchants):
            rows.append({
                "Id": merch_id,
                # Nine digits like the seeded merchant numbers, in a 9xxxxxxxx range neither
                # the seed ("300…") nor the sample script ("ORN…") uses, and sequential so
                # the unique index on MerchantNumber can never trip.
                "MerchantNumber": f"9{merch_id:08d}",
                "MerchantName": fake.company(),
                "MerchantCategoryId": cat.id,
                "IsActive": True,
                "CategoryCode": cat.code,
                "Popularity": float(pop[k]),
            })
            merch_id += 1

    return pd.DataFrame(rows)
