# Synthetic data generator

Causal synthetic data for **CampaignSystem** — realistic customer, card, transaction,
merchant, campaign, participation and reward data that mimics real behaviour, built to
support later AI/ML work: campaign recommendation, profitability prediction and uplift
modelling.

This is **not** dummy data. Instead of sampling columns independently, the generator
defines a data generating process (DGP): every customer has hidden latent traits, and all
observable rows are functions of those traits. The relationships the ML models are meant
to learn are deliberately built in — and the ground truth is saved so those models can be
validated.

## Why a separate tool (not part of the .NET solution)

It is standalone Python tooling: its own `uv` environment and dependencies, no place in
the `.sln`, no effect on the API build. It lives in the same repo so it stays in step with
the schema it targets.

## Requirements

- Python ≥ 3.10
- [uv](https://docs.astral.sh/uv/)

## Run

```bash
cd tools/synthetic-data
uv sync          # create .venv and install numpy / pandas / scipy / faker / matplotlib
uv run python main.py
uv run python views.py  # join CSVs into readable, labelled wide tables
uv run python eda.py    # join CSVs and plot the modelled relationships
```

CSVs (one per entity, plus `latent.csv`, `treatment.csv`, `ground_truth.csv`) are
written to `output/`, which is git-ignored. EDA figures and `REPORT.md` go to
`output/eda/`. Loading CSVs into SQL Server (bulk insert / EF) is a separate step.

`eda.py` exists because the relationships live in **joins**, not in any single
table: segment → spend, weekday/payday, category prefs, enrollment confounding,
heterogeneous tau, refund clawback, and profit. Isolated CSVs will not show them.

## Reading the data (`views.py`)

The CSVs above mirror the DB tables, so a row is mostly foreign keys and enum codes —
faithful to the schema, unreadable by eye. `views.py` denormalises them into wide tables
under `output/views/`, where every key is resolved to its label:

| View | Grain | What it answers |
|---|---|---|
| `movements_wide` | one card movement (purchase **and** refund) | who spent, on which card type/product, at which merchant and category, how much came back as a refund, and **which campaign(s) the purchase qualifies for** |
| `campaigns_wide` | one campaign | the targeting rules in words, plus reach, qualifying volume, earned/clawed-back points, incremental spend and profit |
| `rewards_wide` | one reward row | who earned or gave back what, on which campaign and card |
| `participation_wide` | one enrollment | who joined, with the hidden response type beside it, so self-selection is visible |
| `customers_wide` | one customer | profile, cards, spend/refund behaviour, campaign outcome and the latent traits |

Campaign attribution is the part no single table carries: a purchase belongs to a
campaign only where the treatment list, the campaign window and every targeting criterion
(segment, gender, card type, category, Min/Max amount) intersect, so `views.py` recomputes
it with the same rule the reward step applies. A purchase can qualify for several
campaigns at once, hence the joined `Campaigns` string and `CampaignCount`.

Each view is written twice: the full `<name>.csv`, and a 200-row `<name>_sample.csv` for
opening in Excel (`movements_wide.csv` has more rows than Excel can hold — filter it in
pandas, or use the sample). Both are UTF-8 with BOM so Turkish names survive Excel.

```bash
uv run python views.py               # full views + samples, prints the head of each
uv run python views.py --sample-only # samples only (fast)
uv run python views.py --rows 20     # print more rows per view
```

Labels live in one block at the top of `views.py` (`GENDER_LABELS`, `SEGMENT_LABELS`, …);
change them there to relabel every view at once.

## What gets modelled

| Relationship | Where |
|---|---|
| segment → spend level & frequency (log-normal ticket, Poisson count) | `customers`, `transactions` |
| latent category preference → merchant choice (Dirichlet basket) | `customers`, `transactions` |
| time structure (weekday, payday cycle, seasonality) | `transactions` |
| campaign eligibility → enrollment (**self-selection / confounding**) | `participation` |
| heterogeneous treatment effect (persuadable / sure-thing / lost-cause / sleeping-dog) | `rewards` |
| refund → clawback (partial refunds above/below minimum) | `refunds`, `rewards` |
| profitability (interchange margin on incremental spend − reward cost) | `rewards` |

`ground_truth.csv` holds the hidden latent traits and the true per-(customer, campaign)
treatment effect (tau) — the counterfactual you cannot observe in real data but need to
score an uplift model.

## Layout

```
config.py            run controls + DGP parameters + reference data (mirrors the DB seed)
main.py              runs the chain in dependency order, writes CSVs
views.py             joins the CSVs into labelled wide tables -> output/views/
eda.py               joins the CSVs and writes relationship plots + output/eda/REPORT.md
generators/
  customers.py       segments + latent traits
  cards.py           cards per customer
  merchants.py       merchant categories + merchants (reference)
  transactions.py    baseline purchase behaviour
  campaigns.py       campaign definitions + criteria
  participation.py   enrollment with self-selection
  rewards.py         treatment effect + rewards + profit (the causal payload)
  refunds.py         İade transactions
```

## Note on reference data

The lookup constants in `config.py` (segments, transaction codes, merchant categories,
enum codes) must match the values the .NET application seeds. If the DB seed changes,
update `config.py` so generated rows reference valid lookup keys.
