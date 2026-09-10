"""
Central configuration for the synthetic data generator.

Everything the generators need to reproduce a run lives here: the random seed, the
population size, the calendar window, and — most importantly — the *parameters of the
data generating process* (DGP). These parameters are the "ground truth" the downstream
work (segment analysis, campaign recommendation, profitability, uplift) is meant to
recover, so they are deliberate, not incidental.

Reference data — segments, merchant categories, card products, transaction codes and enum
codes — mirrors what the .NET application seeds through EF Core ``HasData``. Those ids are
fixed in the migrations and exist in every database, so generated rows reference them
directly and the generator never writes the lookup tables itself. If the application's
seed changes, change it here too; ``_check_reference_data`` at the bottom catches the
typos that would otherwise produce silently wrong data.
"""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from datetime import date, timedelta
from pathlib import Path

# ── Run controls ─────────────────────────────────────────────────────────────
# Each has an environment override so a quick or pinned run needs no edit here:
#   SYNTH_SEED, SYNTH_CUSTOMERS, SYNTH_END_DATE (YYYY-MM-DD)

# Fix the seed so a run — every customer, transaction and reward — is reproducible.
SEED: int = int(os.environ.get("SYNTH_SEED", 20260830))

# Where CSVs are written. Git-ignored.
OUTPUT_DIR: Path = Path(__file__).parent / "output"

# The window ends yesterday unless pinned. The application measures its lookback from
# DateTime.Now, so data that stops weeks before "today" would read there as a sudden drop
# in every category's recent spend. Pin SYNTH_END_DATE for a byte-for-byte repeatable run.
END_DATE: date = (
    date.fromisoformat(os.environ["SYNTH_END_DATE"])
    if os.environ.get("SYNTH_END_DATE")
    else date.today() - timedelta(days=1)
)
# A full year, so every calendar month — and every seasonal peak — appears once.
START_DATE: date = END_DATE - timedelta(days=364)

# ── Population size ───────────────────────────────────────────────────────────

N_CUSTOMERS: int = int(os.environ.get("SYNTH_CUSTOMERS", 10_000))

# Cards per customer: most have one, some have a supplementary card or two.
# Modelled as 1 + Poisson(EXTRA_CARDS_LAMBDA), capped at MAX_CARDS.
EXTRA_CARDS_LAMBDA: float = 0.6
MAX_CARDS: int = 4

# Share of extra cards that are supplementary (CardType.Supplementary) rather than primary.
SUPPLEMENTARY_SHARE: float = 0.7


# ── Enum encodings (as the database stores them) ─────────────────────────────
# Generators work with small ints and enum names; main.py converts to these when writing,
# so every table file holds exactly what its database column holds.

# The four enums EnumCodeConverters.cs maps to bank codes:
GENDER_DB = {1: "E", 2: "K"}                                      # 1 = male, 2 = female
CARD_TYPE_DB = {1: "A", 2: "E"}                                   # 1 = primary, 2 = supplementary
CAMPAIGN_TYPE_DB = {"Mass": "MASS", "EnrollmentRequired": "SI"}
EARNING_TYPE_DB = {"CardBased": "K", "CustomerBased": "M"}
# Status, EnrollmentBasis, RewardType and ParticipationStatus are stored as the member name
# (HasConversion<string>), so the generators' values for those are already database-ready.


# ── Reference data (must match the application's HasData seed) ───────────────

@dataclass(frozen=True)
class ProductSpec:
    """A PRODUCT row (ProductConfiguration)."""

    id: int
    code: str
    name: str


PRODUCTS: tuple[ProductSpec, ...] = (
    ProductSpec(1, "201", "Visa Classic"),
    ProductSpec(2, "202", "MasterCard Classic"),
    ProductSpec(3, "203", "Visa Gold"),
    ProductSpec(4, "204", "MasterCard Gold"),
    ProductSpec(5, "205", "Platinum Plus"),
    ProductSpec(6, "206", "Platinum Plus Metal"),
)


@dataclass(frozen=True)
class TxnCodeSpec:
    """A TRANSACTION_CODE row (TransactionCodeConfiguration)."""

    id: int
    code: str
    name: str


TRANSACTION_CODES: tuple[TxnCodeSpec, ...] = (
    TxnCodeSpec(1, "SA", "Satış"),
    TxnCodeSpec(2, "NA", "Nakit Avans"),
    TxnCodeSpec(3, "OD", "Borç Ödeme"),
    TxnCodeSpec(4, "IA", "İade"),
    TxnCodeSpec(5, "PS", "Puan Harcama"),
)
# Only SA (spending) and IA (refund) rows are generated. NA, OD and PS are listed for the
# labels: the application has no example yet of how their amounts and merchants are
# recorded, so they are left out rather than guessed.
SALE_CODE_ID: int = 1
PURCHASE_CODE_IDS: tuple[int, ...] = (SALE_CODE_ID,)   # codes that count as spending
REFUND_CODE_ID: int = 4


@dataclass(frozen=True)
class MerchantCategorySpec:
    """A MERCHANT_CATEGORY row and how purchases in it behave.

    ``base_weight`` is the population-average share of purchases (by count) in the
    category — each segment reshapes it, each customer perturbs it. ``mu_adjust`` is added
    to the segment's log-amount mean: the category's own ticket size (white goods big,
    groceries small).
    """

    id: int
    code: str
    name: str
    base_weight: float
    n_merchants: int
    mu_adjust: float


MERCHANT_CATEGORIES: tuple[MerchantCategorySpec, ...] = (
    MerchantCategorySpec(1,  "GDA", "Gıda / Market",           base_weight=0.20, n_merchants=40, mu_adjust=0.0),
    MerchantCategorySpec(2,  "RST", "Restoran / Yeme-İçme",    base_weight=0.10, n_merchants=60, mu_adjust=-0.2),
    MerchantCategorySpec(3,  "AKY", "Akaryakıt",               base_weight=0.10, n_merchants=15, mu_adjust=1.0),
    MerchantCategorySpec(4,  "GYM", "Giyim",                   base_weight=0.07, n_merchants=40, mu_adjust=0.6),
    MerchantCategorySpec(5,  "AYK", "Ayakkabı & Aksesuar",     base_weight=0.03, n_merchants=15, mu_adjust=0.6),
    MerchantCategorySpec(6,  "KOZ", "Kozmetik",                base_weight=0.03, n_merchants=15, mu_adjust=0.1),
    MerchantCategorySpec(7,  "ELK", "Elektronik",              base_weight=0.06, n_merchants=20, mu_adjust=1.8),
    MerchantCategorySpec(8,  "TEL", "Telekomünikasyon / GSM",  base_weight=0.04, n_merchants=8,  mu_adjust=0.4),
    MerchantCategorySpec(9,  "MOB", "Mobilya & Ev Tekstili",   base_weight=0.03, n_merchants=15, mu_adjust=1.9),
    MerchantCategorySpec(10, "BYZ", "Beyaz Eşya",              base_weight=0.03, n_merchants=10, mu_adjust=2.6),
    MerchantCategorySpec(11, "OTO", "Otomotiv & Oto Bakım",    base_weight=0.03, n_merchants=15, mu_adjust=1.4),
    MerchantCategorySpec(12, "ARK", "Araç Kiralama",           base_weight=0.01, n_merchants=6,  mu_adjust=1.6),
    MerchantCategorySpec(13, "TUR", "Turizm / Seyahat / Otel", base_weight=0.04, n_merchants=20, mu_adjust=2.0),
    MerchantCategorySpec(14, "HVY", "Havayolları / Ulaşım",    base_weight=0.03, n_merchants=8,  mu_adjust=1.7),
    MerchantCategorySpec(15, "EGT", "Eğitim",                  base_weight=0.03, n_merchants=12, mu_adjust=1.5),
    MerchantCategorySpec(16, "SGL", "Sağlık / Eczane / Optik", base_weight=0.05, n_merchants=30, mu_adjust=0.2),
    MerchantCategorySpec(17, "SGR", "Sigorta",                 base_weight=0.02, n_merchants=6,  mu_adjust=1.9),
    MerchantCategorySpec(18, "SPR", "Spor",                    base_weight=0.02, n_merchants=12, mu_adjust=0.5),
    MerchantCategorySpec(19, "KUY", "Kuyumculuk / Saat",       base_weight=0.02, n_merchants=10, mu_adjust=2.0),
    MerchantCategorySpec(20, "KRT", "Kırtasiye / Oyuncak",     base_weight=0.02, n_merchants=12, mu_adjust=-0.1),
    MerchantCategorySpec(21, "YPI", "Yapı & İnşaat",           base_weight=0.02, n_merchants=12, mu_adjust=1.3),
    MerchantCategorySpec(22, "EGL", "Eğlence",                 base_weight=0.02, n_merchants=12, mu_adjust=0.0),
)

# Merchants the generator creates start here. The application seeds merchants 1–12 itself,
# so starting well clear of them keeps both sets loadable into one database.
MERCHANT_ID_START: int = 1001

# Dirichlet concentration for per-customer category preference: the sum of the Dirichlet
# parameters. Higher = customers close to their segment's basket; lower = sharper
# individual specialisation. With 22 categories the rarer ones get small parameters, so
# many customers never buy in them at all — as in real card data.
CATEGORY_CONCENTRATION: float = 60.0


@dataclass(frozen=True)
class SegmentSpec:
    """A SEGMENT row (SegmentConfiguration) and the spending world its members live in.

      - spend_mu / spend_sigma: a purchase amount is log-normal, exp(N(mu, sigma)), before
        the category's own ``mu_adjust``
      - monthly_txn: mean purchases per member in an ordinary month (Poisson rate, before
        the member's activity)
      - female_share: probability a member is recorded as female (K)
      - category_affinity: multipliers on the population base weights — the segment's
        basket. A code left out is 1.0. THIS is the ground truth a segment analysis
        should rediscover.
      - product_weights: relative odds of each card product, in PRODUCTS order
      - seasonal: extra month multipliers per category, on top of CATEGORY_SEASONALITY,
        for effects only this segment has (the farmer's harvest)
    """

    id: int
    code: str
    name: str
    weight: float          # share of the population
    spend_mu: float
    spend_sigma: float
    monthly_txn: float
    female_share: float
    category_affinity: dict[str, float] = field(default_factory=dict)
    product_weights: tuple[float, ...] = ()
    seasonal: dict[str, dict[int, float]] = field(default_factory=dict)


SEGMENTS: tuple[SegmentSpec, ...] = (
    SegmentSpec(
        1, "OGR", "Öğrenci", weight=0.14,
        spend_mu=5.2, spend_sigma=0.70, monthly_txn=12, female_share=0.52,
        category_affinity={
            "KRT": 3.0, "EGT": 2.5, "EGL": 2.5, "TEL": 1.8, "RST": 1.6, "SPR": 1.3, "GYM": 1.2,
            "AKY": 0.4, "OTO": 0.3, "MOB": 0.3, "KUY": 0.3, "SGR": 0.2, "YPI": 0.2, "BYZ": 0.15,
        },
        product_weights=(0.45, 0.45, 0.05, 0.05, 0.0, 0.0),
        seasonal={"EGL": {6: 1.30, 7: 1.40, 8: 1.30}},
    ),
    SegmentSpec(
        2, "PER", "Şirket Çalışanı", weight=0.38,
        spend_mu=5.8, spend_sigma=0.80, monthly_txn=15, female_share=0.45,
        category_affinity={
            "HVY": 1.8, "RST": 1.7, "ARK": 1.6, "TUR": 1.4, "ELK": 1.4, "SPR": 1.3, "AKY": 1.2,
            "KRT": 0.7,
        },
        product_weights=(0.15, 0.15, 0.22, 0.22, 0.18, 0.08),
    ),
    SegmentSpec(
        3, "CFT", "Çiftçi", weight=0.12,
        spend_mu=5.7, spend_sigma=0.95, monthly_txn=8, female_share=0.25,
        category_affinity={
            "AKY": 2.8, "OTO": 2.4, "YPI": 2.2, "SGR": 1.3, "GDA": 1.2, "BYZ": 1.2,
            "RST": 0.6, "EGT": 0.6, "KUY": 0.6, "KOZ": 0.4,
            "TUR": 0.3, "EGL": 0.3, "SPR": 0.3, "ARK": 0.3, "HVY": 0.2,
        },
        product_weights=(0.30, 0.30, 0.18, 0.18, 0.04, 0.0),
        # Spring planting and the autumn harvest: machinery runs, fuel and repairs peak.
        seasonal={
            "AKY": {4: 1.35, 5: 1.30, 9: 1.55, 10: 1.45},
            "OTO": {3: 1.25, 4: 1.25, 9: 1.40, 10: 1.35},
            "YPI": {9: 1.25, 10: 1.30},
        },
    ),
    SegmentSpec(
        4, "EVH", "Ev Hanımı", weight=0.16,
        spend_mu=5.6, spend_sigma=0.75, monthly_txn=11, female_share=0.98,
        category_affinity={
            "MOB": 2.0, "KOZ": 2.0, "BYZ": 1.8, "GDA": 1.7, "GYM": 1.4, "AYK": 1.4, "KRT": 1.3,
            "YPI": 0.6, "SGR": 0.6, "AKY": 0.5, "HVY": 0.4, "OTO": 0.4, "ARK": 0.3,
        },
        product_weights=(0.35, 0.35, 0.12, 0.12, 0.05, 0.01),
    ),
    SegmentSpec(
        5, "EMK", "Emekli", weight=0.20,
        spend_mu=5.4, spend_sigma=0.70, monthly_txn=9, female_share=0.50,
        category_affinity={
            "SGL": 2.5, "GDA": 1.5, "SGR": 1.4, "TUR": 1.2, "MOB": 1.1,
            "TEL": 0.8, "HVY": 0.8, "KRT": 0.8, "GYM": 0.8, "EGL": 0.5, "SPR": 0.4, "EGT": 0.3,
        },
        product_weights=(0.30, 0.30, 0.17, 0.17, 0.05, 0.01),
        # Retirees travel off-peak: shoulder months up, the school-holiday peak down.
        seasonal={"TUR": {5: 1.40, 6: 1.20, 7: 0.85, 8: 0.85, 9: 1.35, 10: 1.30}},
    ),
)


# ── Time structure ───────────────────────────────────────────────────────────

# Multiplicative seasonality applied to a customer's daily transaction rate.
WEEKDAY_MULTIPLIER = {0: 0.9, 1: 0.9, 2: 0.95, 3: 1.0, 4: 1.15, 5: 1.35, 6: 1.1}  # Mon..Sun
# Spending lifts in the days just after a nominal payday (the 1st of the month).
PAYDAY_DAY_OF_MONTH: int = 1
PAYDAY_LIFT: float = 1.4          # peak multiplier on payday
PAYDAY_DECAY_DAYS: int = 7        # lift fades linearly back to 1.0 over this many days

# Month multipliers per category — copied from SeasonalPatternConfiguration.BuildSeed, so
# the data's true seasonality is the prior the recommendation engine already assumes. A
# category or month left out is an ordinary 1.00.
CATEGORY_SEASONALITY: dict[str, dict[int, float]] = {
    "AKY": {1: 0.85, 2: 0.85, 6: 1.20, 7: 1.35, 8: 1.30, 9: 1.05, 12: 0.95},
    "GYM": {1: 1.10, 2: 0.90, 3: 1.20, 4: 1.15, 7: 0.85, 9: 1.25, 10: 1.15, 11: 1.20, 12: 1.15},
    "AYK": {3: 1.15, 4: 1.10, 7: 0.85, 9: 1.25, 10: 1.10, 11: 1.15, 12: 1.10},
    "ELK": {1: 0.80, 2: 0.80, 3: 0.90, 8: 1.15, 9: 1.20, 11: 1.55, 12: 1.25},
    "MOB": {1: 0.85, 2: 0.85, 5: 1.25, 6: 1.30, 7: 1.20, 9: 1.10, 11: 1.15},
    "BYZ": {1: 0.80, 2: 0.85, 5: 1.25, 6: 1.30, 7: 1.15, 11: 1.20, 12: 1.15},
    "TUR": {1: 0.85, 2: 1.15, 3: 0.90, 6: 1.35, 7: 1.55, 8: 1.50, 9: 1.15, 11: 0.80, 12: 0.90},
    "HVY": {1: 0.90, 2: 1.15, 6: 1.25, 7: 1.45, 8: 1.40, 11: 0.85, 12: 1.10},
    "EGT": {1: 1.20, 2: 1.25, 4: 0.85, 5: 0.85, 6: 1.10, 7: 1.10, 8: 1.45, 9: 1.60, 10: 1.10, 11: 0.90, 12: 0.85},
    "SPR": {1: 1.45, 2: 1.15, 6: 0.85, 7: 0.80, 8: 0.85, 9: 1.25, 10: 1.10},
    "KUY": {2: 1.10, 4: 1.15, 5: 1.35, 6: 1.30, 7: 1.15, 8: 0.85, 9: 0.90, 11: 1.20, 12: 1.25},
    "KRT": {1: 1.25, 2: 1.20, 4: 1.10, 5: 0.85, 6: 0.80, 7: 0.90, 8: 1.55, 9: 1.60, 10: 0.90, 12: 1.30},
    "YPI": {1: 0.75, 2: 0.80, 4: 1.20, 5: 1.30, 6: 1.35, 7: 1.30, 8: 1.25, 9: 1.15, 12: 0.80},
}

# Structural trend per category: a compounding monthly growth rate across the window.
# These are what the engine's trend signal should pick up — cosmetics and entertainment
# growing, GSM and furniture shrinking. A category left out is flat.
CATEGORY_TREND: dict[str, float] = {"KOZ": 0.05, "EGL": 0.04, "TEL": -0.03, "MOB": -0.03}


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
    generator and in the answer-key files — but every observed value is a function of
    them. The ML task is precisely to recover them from the observable data.
    """

    # Overall activity multiplier on transaction frequency: log-normal around 1.0.
    activity_sigma: float = 0.5
    # Campaign responsiveness: how strongly a treated persuadable lifts spend. Beta-shaped.
    responsiveness_a: float = 2.0
    responsiveness_b: float = 3.0
    # Refund propensity multiplier: Gamma around 1.0.
    refund_propensity_shape: float = 4.0


LATENT: LatentTraitConfig = LatentTraitConfig()


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
    days = (END_DATE - START_DATE).days + 1
    return (
        f"seed={SEED} customers={N_CUSTOMERS:,} campaigns={N_CAMPAIGNS} "
        f"window={START_DATE}..{END_DATE} ({days}d) "
        f"segments={len(SEGMENTS)} categories={len(MERCHANT_CATEGORIES)}"
    )


def _check_reference_data() -> None:
    """Fail at import on the mistakes that would otherwise generate wrong data quietly."""
    codes = {c.code for c in MERCHANT_CATEGORIES}
    if len(codes) != len(MERCHANT_CATEGORIES):
        raise ValueError("MERCHANT_CATEGORIES has a duplicate code")
    if abs(sum(c.base_weight for c in MERCHANT_CATEGORIES) - 1.0) > 1e-9:
        raise ValueError("MERCHANT_CATEGORIES base weights must sum to 1.0")
    if abs(sum(s.weight for s in SEGMENTS) - 1.0) > 1e-9:
        raise ValueError("SEGMENTS weights must sum to 1.0")
    for s in SEGMENTS:
        unknown = (set(s.category_affinity) | set(s.seasonal)) - codes
        if unknown:
            raise ValueError(f"segment {s.code} names unknown categories: {sorted(unknown)}")
        if len(s.product_weights) != len(PRODUCTS):
            raise ValueError(f"segment {s.code} needs one product weight per product")
    unknown = (set(CATEGORY_SEASONALITY) | set(CATEGORY_TREND)) - codes
    if unknown:
        raise ValueError(f"seasonality/trend name unknown categories: {sorted(unknown)}")


_check_reference_data()
