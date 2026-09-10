"""
Merchant categories and merchants (reference data, built once).

Model
-----
For each category in ``config.MERCHANT_CATEGORIES`` generate ``n_merchants`` merchants
with Faker-generated names and BKM-style merchant numbers. Within a category, merchants
are not equally popular: their pull follows a power-law (a few dominant chains, a long
tail of small ones), so transaction assignment later concentrates realistically.

Returns
-------
(categories_df, merchants_df)
    ``merchants_df`` carries two helper columns beyond the schema — ``CategoryCode`` and
    ``Popularity`` — used when assigning transactions.
"""

from __future__ import annotations

import numpy as np
import pandas as pd
from faker import Faker


def generate_merchants(rng, config):
    fake = Faker("tr_TR")
    Faker.seed(int(rng.integers(0, 2**31 - 1)))

    cat_rows = []
    merch_rows = []
    merch_id = 1

    for cat_id, cat in enumerate(config.MERCHANT_CATEGORIES, start=1):
        cat_rows.append({"Id": cat_id, "Code": cat.code, "Name": cat.name})

        # Zipf-like popularity: a few merchants dominate, most are small.
        pop = 1.0 / np.arange(1, cat.n_merchants + 1) ** 1.1
        pop = pop / pop.sum()
        # Shuffle so the dominant merchant is not always the lowest id.
        rng.shuffle(pop)

        for k in range(cat.n_merchants):
            merch_rows.append({
                "Id": merch_id,
                "MerchantNumber": f"{int(rng.integers(10**8, 10**9))}",
                "MerchantName": fake.company(),
                "MerchantCategoryId": cat_id,
                "IsActive": True,
                "CategoryCode": cat.code,
                "Popularity": float(pop[k]),
            })
            merch_id += 1

    return pd.DataFrame(cat_rows), pd.DataFrame(merch_rows)
