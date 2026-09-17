"""
Merchants — the MERCHANT rows the transactions point at.

Model
-----
For each category in ``config.MERCHANT_CATEGORIES`` generate ``n_merchants`` merchants.
Within a category merchants are not equally popular: their pull follows a power law (a few
dominant chains, a long tail of small ones), so transactions later concentrate
realistically.

Names read like the shop fronts on a card statement — "Yılmaz Petrol", "Bereket Market",
"Çınar Eczanesi" — built from the category's own patterns with a common surname or a plain
word. They are made up on purpose: real brands appear only in the application's seeded
merchants (Migros, Shell, Teknosa, ...), placed in the right category by hand. Invented
spend is never attributed to a real company, and a fuel station is never named after an
electronics maker.

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

# Common family names, the way small Turkish businesses are usually named. Ordinary names
# only: no holding or political family names that would make a row read as a real company
# or as a statement.
_SURNAMES = (
    "Yılmaz", "Kaya", "Demir", "Şahin", "Çelik", "Aydın", "Öztürk", "Arslan", "Doğan",
    "Kılıç", "Aslan", "Çetin", "Kara", "Kurt", "Özdemir", "Polat", "Şimşek", "Erdem",
    "Aksoy", "Tekin", "Aktaş", "Keskin", "Bulut", "Kaplan", "Ekinci", "Tunç", "Uçar",
    "Başaran", "Karaca", "Güler", "Bozkurt", "Coşkun", "Duman", "Yavuz", "Sarı", "Ateş",
)

# Plain words for names that are not a family name, chosen so no pairing below spells a
# well-known brand (no "Mavi", "Pınar", "Anadolu", "Işık", "Güneş", ...).
_WORDS = (
    "Kuzey", "Güney", "Bereket", "Çınar", "Lale", "Deniz", "Bahar", "Umut", "Yeşil",
    "Altın", "Gökkuşağı", "Kardelen", "Nergis", "Zeytin", "Yakamoz", "Doruk", "Meltem",
    "Çiğdem", "Selvi", "Poyraz", "Ilgaz", "Karadeniz", "Akdeniz", "Ege",
)

# Shop-front patterns per category code: {s} is a surname, {w} a plain word.
_NAME_PATTERNS = {
    "GDA": ("{s} Market", "{w} Gıda", "{s} Şarküteri", "{w} Süpermarket", "{s} Manav"),
    "RST": ("{s} Kebap", "{w} Lokanta", "{s} Pide Salonu", "{w} Balık Evi", "{w} Cafe", "{s} Döner"),
    "AKY": ("{s} Petrol", "{w} Akaryakıt", "{s} Akaryakıt İstasyonu"),
    "GYM": ("{w} Moda", "{s} Konfeksiyon", "{w} Butik", "{s} Giyim"),
    "AYK": ("{w} Ayakkabı", "{s} Kundura", "{w} Çanta & Aksesuar"),
    "KOZ": ("{w} Kozmetik", "{s} Parfümeri", "{w} Güzellik"),
    "ELK": ("{s} Elektronik", "{w} Teknoloji", "{s} Bilgisayar"),
    "TEL": ("{w} İletişim", "{s} GSM", "{s} Cep Telefonu"),
    "MOB": ("{s} Mobilya", "{w} Ev Tekstili", "{w} Home"),
    "BYZ": ("{s} Beyaz Eşya", "{w} Elektrikli Ev Aletleri", "{s} Dayanıklı Tüketim"),
    "OTO": ("{s} Oto Servis", "{w} Oto Yedek Parça", "{s} Oto Lastik"),
    "ARK": ("{w} Rent a Car", "{s} Araç Kiralama"),
    "TUR": ("{w} Turizm", "{s} Otel", "{w} Tatil Köyü", "{w} Seyahat Acentesi"),
    "HVY": ("{w} Havayolları", "{w} Seyahat Otobüs", "{s} Taşımacılık"),
    "EGT": ("{w} Kurs Merkezi", "{w} Dil Okulu", "{s} Eğitim Kurumları", "{w} Etüt Merkezi"),
    "SGL": ("{w} Eczanesi", "{s} Eczanesi", "{w} Optik", "{s} Tıp Merkezi"),
    # Surnames only: agencies carry their owner's name, and "<word> Sigorta" lands too
    # close to insurers that really exist.
    "SGR": ("{s} Sigorta Acentesi", "{s} Sigorta Aracılık"),
    "SPR": ("{w} Spor", "{s} Spor Malzemeleri", "{w} Fitness"),
    "KUY": ("{s} Kuyumculuk", "{w} Kuyumcusu", "{s} Saat"),
    "KRT": ("{w} Kırtasiye", "{s} Kitabevi", "{w} Oyuncak"),
    "YPI": ("{s} Yapı Market", "{w} Hırdavat", "{s} Nalbur"),
    "EGL": ("{w} Sinema", "{w} Bowling", "{s} Eğlence Merkezi", "{w} Oyun Salonu"),
}


def _shop_name(rng, code, taken):
    """A shop-front name for the category that no merchant has been given yet."""
    patterns = _NAME_PATTERNS[code]
    for _ in range(50):
        name = str(rng.choice(patterns)).format(s=rng.choice(_SURNAMES), w=rng.choice(_WORDS))
        if name not in taken:
            return name
    # Only a category far larger than any configured here runs out of combinations.
    return f"{name} {len(taken)}"


def generate_merchants(rng, config):
    rows = []
    taken: set[str] = set()
    merch_id = config.MERCHANT_ID_START

    for cat in config.MERCHANT_CATEGORIES:
        # Zipf-like popularity: a few merchants dominate, most are small.
        pop = 1.0 / np.arange(1, cat.n_merchants + 1) ** 1.1
        pop = pop / pop.sum()
        # Shuffle so the dominant merchant is not always the lowest id.
        rng.shuffle(pop)

        for k in range(cat.n_merchants):
            name = _shop_name(rng, cat.code, taken)
            taken.add(name)
            rows.append({
                "Id": merch_id,
                # Nine digits like the seeded merchant numbers, in a 9xxxxxxxx range neither
                # the seed ("300…") nor the sample script ("ORN…") uses, and sequential so
                # the unique index on MerchantNumber can never trip.
                "MerchantNumber": f"9{merch_id:08d}",
                "MerchantName": name,
                "MerchantCategoryId": cat.id,
                "IsActive": True,
                "CategoryCode": cat.code,
                "Popularity": float(pop[k]),
            })
            merch_id += 1

    return pd.DataFrame(rows)
