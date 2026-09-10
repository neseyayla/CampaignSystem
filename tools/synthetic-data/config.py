"""
Central configuration for the synthetic data generator.

Everything the generators need to reproduce a run lives here: the random seed, the
population size, the calendar window, and — most importantly — the *parameters of the
data generating process* (DGP). These parameters are the "ground truth" the downstream
ML work (campaign recommendation, profitability, uplift) is meant to recover, so they are
deliberate, not incidental.

Reference-data constants (segments, transaction codes, merchant categories, enum codes)
mirror the lookup tables seeded by the .NET application. If the database seed changes,
change it here too so generated rows still reference valid lookup keys.
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import date
from pathlib import Path

# ── Run controls ─────────────────────────────────────────────────────────────

# Fix the seed so an entire run — every customer, transaction and reward — is
# byte-for-byte reproducible. Change it to draw a different population.
SEED: int = 20260830

# Where CSVs (and ground_truth.csv) are written. Git-ignored.
OUTPUT_DIR: Path = Path(__file__).parent / "output"

# Simulation window. Transactions, campaigns and rewards all fall inside this range.
START_DATE: date = date(2026, 1, 1)
END_DATE: date = date(2026, 8, 31)

# ── Population size ───────────────────────────────────────────────────────────

N_CUSTOMERS: int = 10_000

# Cards per customer: most have one, some have a supplementary card or two.
# Modelled as 1 + Poisson(EXTRA_CARDS_LAMBDA), capped at MAX_CARDS.
EXTRA_CARDS_LAMBDA: float = 0.6
MAX_CARDS: int = 4

# Share of extra cards that are supplementary (CardType.Supplementary) rather than primary.
SUPPLEMENTARY_SHARE: float = 0.7


# ── Reference data (must match the DB lookup seed) ───────────────────────────

# Enum codes as persisted by the .NET side, for reference when emitting rows.
GENDER_CODES = {"Male": "E", "Female": "K"}
CARD_TYPE_CODES = {"Primary": "A", "Supplementary": "E"}
CAMPAIGN_TYPE_CODES = {"Mass": "MASS", "EnrollmentRequired": "SI"}
EARNING_TYPE_CODES = {"CardBased": "K", "CustomerBased": "M"}


@dataclass(frozen=True)
class SegmentSpec:
    """A customer segment and the spending world its members live in.

    ``spend_mu``/``spend_sigma`` parameterise the per-transaction amount as a
    log-normal (amount = exp(N(mu, sigma))), so higher-tier segments carry both a
    higher median ticket and a fatter tail. ``monthly_txn`` is the mean number of
    purchases a member makes per month (Poisson rate).
    """

    code: str
    name: str
    weight: float          # share of the population
    spend_mu: float        # log-normal location of a single purchase (TRY)
    spend_sigma: float     # log-normal scale — dispersion of ticket size
    monthly_txn: float     # mean purchases per member per month


# Population mix is roughly a pyramid: many Classic, few Private.
SEGMENTS: tuple[SegmentSpec, ...] = (
    SegmentSpec("CLS", "Classic",  weight=0.55, spend_mu=5.0, spend_sigma=0.7, monthly_txn=8),
    SegmentSpec("GLD", "Gold",     weight=0.28, spend_mu=5.6, spend_sigma=0.8, monthly_txn=14),
    SegmentSpec("PLT", "Platinum", weight=0.13, spend_mu=6.2, spend_sigma=0.9, monthly_txn=22),
    SegmentSpec("PRV", "Private",  weight=0.04, spend_mu=6.9, spend_sigma=1.0, monthly_txn=30),
)


@dataclass(frozen=True)
class TxnCodeSpec:
    """A TRANSACTION_CODE lookup row. ``id`` mirrors the DB seed."""

    id: int
    code: str
    name: str


# İade (refund) is id 4 / "IA" on the .NET side; refunds are emitted as this code.
TRANSACTION_CODES: tuple[TxnCodeSpec, ...] = (
    TxnCodeSpec(1, "PS", "Peşin Satış"),
    TxnCodeSpec(2, "TS", "Taksitli Satış"),
    TxnCodeSpec(3, "NP", "Nakit Avans"),
    TxnCodeSpec(4, "IA", "İade"),
)
PURCHASE_CODE_IDS: tuple[int, ...] = (1, 2)   # codes that count as spending
REFUND_CODE_ID: int = 4


@dataclass(frozen=True)
class MerchantCategorySpec:
    """A merchant category and how much of it a typical basket contains.

    ``base_weight`` is the population-average share of spend in this category; each
    customer perturbs it through a Dirichlet draw so preferences differ person to person.
    """

    code: str
    name: str
    base_weight: float
    n_merchants: int       # how many merchants to generate in this category


MERCHANT_CATEGORIES: tuple[MerchantCategorySpec, ...] = (
    MerchantCategorySpec("GRO", "Market",       base_weight=0.30, n_merchants=40),
    MerchantCategorySpec("FUE", "Akaryakıt",    base_weight=0.12, n_merchants=15),
    MerchantCategorySpec("DIN", "Restoran",     base_weight=0.15, n_merchants=60),
    MerchantCategorySpec("APP", "Giyim",        base_weight=0.12, n_merchants=45),
    MerchantCategorySpec("ELE", "Elektronik",   base_weight=0.08, n_merchants=20),
    MerchantCategorySpec("TRV", "Seyahat",      base_weight=0.07, n_merchants=25),
    MerchantCategorySpec("HEA", "Sağlık",       base_weight=0.08, n_merchants=30),
    MerchantCategorySpec("ONL", "E-ticaret",    base_weight=0.08, n_merchants=20),
)

# Dirichlet concentration for per-customer category preference. Higher = closer to the
# population average (everyone similar); lower = sharper individual specialisation.
CATEGORY_DIRICHLET_ALPHA: float = 8.0


# ── Time structure ───────────────────────────────────────────────────────────

# Multiplicative seasonality applied to a customer's daily transaction rate.
WEEKDAY_MULTIPLIER = {0: 0.9, 1: 0.9, 2: 0.95, 3: 1.0, 4: 1.15, 5: 1.35, 6: 1.1}  # Mon..Sun
# Spending lifts in the days just after a nominal payday (the 1st of the month).
PAYDAY_DAY_OF_MONTH: int = 1
PAYDAY_LIFT: float = 1.4          # peak multiplier on payday
PAYDAY_DECAY_DAYS: int = 7        # lift fades linearly back to 1.0 over this many days


# ── Refunds ──────────────────────────────────────────────────────────────────

# Base probability a purchase is later (partially or fully) refunded, scaled per
# customer by their latent refund propensity.
BASE_REFUND_PROB: float = 0.05
# When a purchase is refunded, the refunded fraction (of the original amount).
REFUND_FRACTION_MIN: float = 0.2
REFUND_FRACTION_MAX: float = 1.0
# How long after the purchase the refund lands.
REFUND_DELAY_DAYS_MIN: int = 1
REFUND_DELAY_DAYS_MAX: int = 30


# ── Campaigns ────────────────────────────────────────────────────────────────

N_CAMPAIGNS: int = 12

# Uplift response-type mix — the heterogeneous treatment effect the uplift model must
# learn to separate. Shares sum to 1.0.
#   persuadable : only spends more when treated (positive uplift) — the target
#   sure_thing  : would have spent anyway (zero incremental value, pure reward cost)
#   lost_cause  : never responds (zero uplift)
#   sleeping_dog: treatment suppresses spend (negative uplift) — do not target
RESPONSE_TYPE_MIX = {
    "persuadable": 0.25,
    "sure_thing": 0.30,
    "lost_cause": 0.35,
    "sleeping_dog": 0.10,
}


# ── Latent customer traits ───────────────────────────────────────────────────

@dataclass(frozen=True)
class LatentTraitConfig:
    """Distributions for the hidden per-customer traits that drive all behaviour.

    These are never written to the transactional tables — they live only in the
    generator and in ground_truth.csv — but every observed value is a function of them.
    The ML task is precisely to recover them from the observable data.
    """

    # Overall activity multiplier on transaction frequency: log-normal around 1.0.
    activity_sigma: float = 0.5
    # Campaign responsiveness: how strongly a treated persuadable lifts spend. Beta-shaped.
    responsiveness_a: float = 2.0
    responsiveness_b: float = 3.0
    # Refund propensity multiplier: Gamma around 1.0.
    refund_propensity_shape: float = 4.0


LATENT: LatentTraitConfig = LatentTraitConfig()


# ── Products (card products, referenced by CARD.ProductId) ───────────────────

@dataclass(frozen=True)
class ProductSpec:
    """A card product. ``tier`` aligns with segment order so premium products
    concentrate in higher segments."""

    id: int
    name: str
    tier: int


PRODUCTS: tuple[ProductSpec, ...] = (
    ProductSpec(1, "Classic Card", 1),
    ProductSpec(2, "Gold Card", 2),
    ProductSpec(3, "Platinum Card", 3),
    ProductSpec(4, "Private Card", 4),
)


# ── Treatment effect & economics ─────────────────────────────────────────────

# Interchange-style margin the bank keeps on card spend; incremental spend times this,
# minus the reward cost, is a campaign's contribution for the profitability model.
INTERCHANGE_MARGIN: float = 0.015

# Persuadables: mean number of extra qualifying purchases a treated persuadable makes
# during the campaign (scaled by their latent responsiveness).
PERSUADABLE_EXTRA_LAMBDA: float = 4.0
# Sleeping dogs: fraction of a treated sleeping-dog's in-scope baseline purchases that
# the campaign suppresses (negative uplift).
SLEEPING_DOG_SUPPRESS: float = 0.4


# ── Enrollment (self-selection) ──────────────────────────────────────────────

# Enrollment probability for an eligible customer in an SI campaign is
# sigmoid(BASE + SLOPE * responsiveness): responsive customers enroll more, which is the
# confounding the uplift model must overcome.
ENROLL_BASE: float = -1.2
ENROLL_SLOPE: float = 2.5


def summary() -> str:
    """One-line description of the configured run, for logging at startup."""
    days = (END_DATE - START_DATE).days
    return (
        f"seed={SEED} customers={N_CUSTOMERS:,} campaigns={N_CAMPAIGNS} "
        f"window={START_DATE}..{END_DATE} ({days}d) segments={len(SEGMENTS)}"
    )
