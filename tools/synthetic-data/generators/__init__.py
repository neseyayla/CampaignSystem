"""
Generators for the synthetic dataset, in dependency order.

Each module produces one entity as a pandas DataFrame from the random generator and the
config, consuming the outputs of the ones before it. The causal chain is:

    customers ──► cards ──► transactions ──► participation ──► rewards
        │            │           ▲               ▲               ▲
        └ latent ────┴───────────┴───────────────┴───────────────┘
    (merchants and campaigns are reference/setup, built once up front)

The latent customer traits produced alongside `customers` are the hidden variables that
drive every later step; they are carried through the chain and written to ground_truth.csv.
"""
