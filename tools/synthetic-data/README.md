# Synthetic data generator

Causal synthetic data for **CampaignSystem**: customers, cards, merchants, transactions,
campaigns, participation and rewards that behave like real card data and fit the
application's database as it stands — the same segments, merchant categories, card
products and transaction codes, the same ids, the same value encodings. It exists to back
segment analysis, campaign recommendation, profitability prediction and uplift modelling.

This is **not** dummy data. Instead of sampling columns independently, the generator
defines a data generating process (DGP): every customer has hidden latent traits, and every
observable row is a function of them. The relationships an analysis or a model is meant to
find are deliberately built in, and the ground truth is saved so the finding can be checked.

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
uv sync                   # create .venv and install numpy / pandas / scipy / faker / matplotlib
uv run python main.py     # generate -> output/*.csv
uv run python views.py    # join the CSVs into readable, labelled wide tables -> output/views/
```

Environment overrides, no edit needed:

| Variable | Default | |
|---|---|---|
| `SYNTH_CUSTOMERS` | `10000` | population size — `2000` for a quick run |
| `SYNTH_END_DATE` | yesterday | last day of the one-year window; pin it for a repeatable run |
| `SYNTH_SEED` | `20260830` | random seed |

The window ends yesterday by default because the application's recommendation engine
measures its lookback from the current date — data that stopped weeks ago would look to
it like a sudden drop in every category.

## Fit with the application

The generator neither changes the database nor loads anything into it; it writes CSVs,
shaped so that loading them later is a plain bulk insert.

| Application rule | What the generator does |
|---|---|
| Lookup ids are fixed by the `HasData` seed | `config.py` mirrors them: 5 segments, 22 merchant categories, 6 card products, 5 transaction codes. Lookup tables are never written; rows point at the seeded ids. `_check_reference_data` fails fast on a typo. |
| Enums are stored as bank codes | Gender `E`/`K`, card type `A`/`E`, campaign type `MASS`/`SI`, earning type `K`/`M`; statuses, enrollment basis and reward types as their names; flags as `0`/`1`. |
| Only SA is spending; IA is a refund | Purchases are `SA` (1); refunds are `IA` (4) with a negative amount and `OriginalTransactionId`. |
| The seed already owns merchants 1–12 | Generated merchants start at id 1001, with numbers in a `9xxxxxxxx` range. |
| A card-based SI campaign enrolls a card | Card-based enrollments carry `CardId`, one row per card; customer-based ones leave it empty. |
| An SI campaign needs `EnrollmentBasis` | Set to `CampaignPeriod`; empty for MASS. |
| Campaign scope lives in junction tables | Written as `campaign_segments`, `campaign_merchants` (a category campaign lists that category's merchants) and `campaign_transaction_codes` (SA for every campaign). |
| Rewards are loaded once a campaign ends | Reward rows only for `Ended` campaigns. |
| Nothing is dated in the future | Refunds and campaign-driven purchases that would land after `END_DATE` are not generated. |

## Output

| File | Database table | |
|---|---|---|
| `customers.csv` | CUSTOMER | `PasswordHash` empty — generated customers cannot sign in |
| `cards.csv` | CARD | |
| `merchants.csv` | MERCHANT | ids from 1001 |
| `campaigns.csv` | CAMPAIGN | |
| `campaign_segments.csv` | CAMPAIGN_SEGMENT | |
| `campaign_merchants.csv` | CAMPAIGN_MERCHANT | |
| `campaign_transaction_codes.csv` | CAMPAIGN_TRANSACTION_CODE | |
| `transactions.csv` | TRANSACTION | purchases and refunds together |
| `participation.csv` | CAMPAIGN_PARTICIPATION | |
| `rewards.csv` | CAMPAIGN_REWARD | |
| `latent.csv` | — answer key | hidden traits per customer |
| `treatment.csv` | — answer key | who was eligible for each campaign, who was treated |
| `ground_truth.csv` | — answer key | true incremental spend (tau), reward cost and profit per treated pair |

Not written, because the application seeds them: SEGMENT, MERCHANT_CATEGORY, PRODUCT,
TRANSACTION_CODE, SEASONAL_PATTERN. `output/` is git-ignored; every run clears the CSVs a
previous run left there.

## The answer key: how each segment behaves

`config.SEGMENTS` gives each segment its own basket, ticket size, frequency, gender mix,
card products and seasonal effects. These are the patterns a segment analysis should come
back with; `main.py` measures them from the written files and prints the check at the end
of every run.

| Segment | Leans towards | Also |
|---|---|---|
| Öğrenci | Kırtasiye, Eğitim, Eğlence, GSM | small tickets, Classic cards, summer entertainment peak |
| Şirket Çalışanı | Havayolu, Araç Kiralama, Restoran, Turizm | largest segment, Gold and Platinum cards |
| Çiftçi | Akaryakıt, Otomotiv, Yapı & İnşaat | fuel and repairs peak at spring planting and the autumn harvest; mostly male |
| Ev Hanımı | Mobilya, Kozmetik, Beyaz Eşya, Gıda | almost entirely female |
| Emekli | Sağlık / Eczane, Gıda, Sigorta | travels off-season (May–June, September–October) |

On top of the segments: category seasonality copied from the application's
SEASONAL_PATTERN seed, and a structural trend in four categories — cosmetics and
entertainment growing, GSM and furniture shrinking (`CATEGORY_TREND`).

## What gets modelled

| Relationship | Where |
|---|---|
| segment → basket, ticket size, frequency, gender, card product | `customers`, `cards`, `transactions` |
| category seasonality, segment-only seasonal effects, category trend | `transactions` |
| weekday and payday cycle | `transactions` |
| merchant popularity (power law within a category) | `merchants`, `transactions` |
| campaign eligibility → enrollment (**self-selection / confounding**) | `participation` |
| heterogeneous treatment effect (persuadable / sure-thing / lost-cause / sleeping-dog) | `rewards` |
| refund → clawback (partial refunds above/below minimum) | `refunds`, `rewards` |
| profitability (interchange margin on incremental spend − reward cost) | `rewards` |

`ground_truth.csv` holds the true per-(customer, campaign) treatment effect — the
counterfactual you cannot observe in real data but need to score an uplift model. For a
campaign still running it is the effect so far, with no reward cost yet.

## Reading the data (`views.py`)

The table files are faithful to the schema — mostly foreign keys and bank codes —
and unreadable by eye. `views.py` denormalises them into wide tables under
`output/views/`, every key and code resolved to its label:

| View | Grain | What it answers |
|---|---|---|
| `movements_wide` | one card movement (purchase **and** refund) | who spent, on which card, at which merchant and category, how much came back, and **which campaign(s) the purchase qualifies for** |
| `campaigns_wide` | one campaign | the targeting rules in words, plus reach, qualifying volume, earned/clawed-back points, incremental spend and profit |
| `rewards_wide` | one reward row | who earned or gave back what, on which campaign and card |
| `participation_wide` | one enrollment | who joined, with the hidden response type beside it, so self-selection is visible |
| `customers_wide` | one customer | profile, cards, spend/refund behaviour, campaign outcome and the latent traits |

Each view is written twice: the full `<name>.csv` and a 200-row `<name>_sample.csv` for
Excel (`movements_wide.csv` has more rows than Excel holds). Both are UTF-8 with BOM so
Turkish names survive Excel.

```bash
uv run python views.py               # full views + samples
uv run python views.py --sample-only # samples only (fast)
uv run python views.py --rows 20     # print more rows per view
```

## Layout

```
config.py            run controls + DGP parameters + reference data (mirrors the DB seed)
main.py              runs the chain in dependency order, writes the CSVs, prints the check
views.py             joins the CSVs into labelled wide tables -> output/views/
generators/
  customers.py       segments, gender + latent traits (segment-centred basket)
  cards.py           cards per customer, product by segment
  merchants.py       merchants per seeded category
  transactions.py    baseline purchase behaviour
  campaigns.py       campaign definitions + scope
  participation.py   enrollment with self-selection
  rewards.py         treatment effect + rewards + profit (the causal payload)
  refunds.py         İade transactions
```

## Not modelled yet

- NA (nakit avans), OD (borç ödeme) and PS (puan harcama) transactions — the application
  has no example yet of how their amounts and merchants are recorded, so they are left out
  rather than guessed
- product criteria (CAMPAIGN_PRODUCT) and the unused-points clawback
- loading the CSVs into SQL Server — the next step
