"""
Loads the generated CSVs into a development database.

Run with:   uv run python load.py                       # into CampaignSystem_Dev
            uv run python load.py --reset               # empty it first, then load again
            uv run python load.py --database OtherDev   # another development database

Target: the Docker SQL Server on localhost,1433, signed in as sa with the password from the
repository's .env (MSSQL_SA_PASSWORD) — the same server the application uses. If the
database does not exist yet it is created with the application's own migrations
(``dotnet ef database update``), so its schema and lookup rows are exactly the app's.

The loader only adds rows. It never creates, alters or drops a table, and it never writes
the lookup tables the migrations seed (segments, categories, products, transaction codes,
seasonal patterns, the twelve seeded merchants). The one setting it changes is the
database's recovery model, to SIMPLE — see ``_use_simple_recovery``.

Two guards keep it away from real data:
  * the presentation database, CampaignSystem, is refused outright, whatever the flags;
  * a database that already holds customers is refused unless --reset is given. --reset
    empties that database's own data — customers, cards, campaigns and everything hanging
    off them, and merchants from MERCHANT_ID_START up — and keeps the seeded rows.
"""

from __future__ import annotations

import argparse
import os
import subprocess
import sys
import time
from decimal import Decimal
from pathlib import Path

import pandas as pd
import pyodbc

import config

REPO_ROOT = Path(__file__).resolve().parents[2]
ENV_FILE = REPO_ROOT / ".env"
PROTECTED_DATABASES = {"campaignsystem"}

# Load order: every table after the tables its foreign keys point at. Identity tables keep
# the generated ids (IDENTITY_INSERT), so the links between the files survive the load.
TABLES = (
    # (file, table, has identity)
    ("merchants", "MERCHANT", True),
    ("customers", "CUSTOMER", True),
    ("cards", "CARD", True),
    ("campaigns", "CAMPAIGN", True),
    ("campaign_segments", "CAMPAIGN_SEGMENT", False),
    ("campaign_merchants", "CAMPAIGN_MERCHANT", False),
    ("campaign_transaction_codes", "CAMPAIGN_TRANSACTION_CODE", False),
    ("transactions", "TRANSACTION", True),
    ("participation", "CAMPAIGN_PARTICIPATION", True),
    ("rewards", "CAMPAIGN_REWARD", True),
)

# What --reset deletes, children before parents. Includes the campaign tables the generator
# never writes (conditions, products, clawback exemptions) because a campaign created by
# hand in the development app may have rows there.
RESET_ORDER = (
    "CAMPAIGN_REWARD", "CAMPAIGN_PARTICIPATION", "TRANSACTION", "CAMPAIGN_TRANSACTION_CODE",
    "CAMPAIGN_MERCHANT", "CAMPAIGN_SEGMENT", "CAMPAIGN_PRODUCT",
    "CAMPAIGN_CLAWBACK_EXEMPT_PRODUCT", "CAMPAIGN_CONDITION", "CAMPAIGN", "CARD", "CUSTOMER",
)

# "123456", hashed the way the application stores passwords — the same hash
# docs/test-data.sql gives every development customer.
DEV_PASSWORD_HASH = "AQAAAAIAAYagAAAAEJFQXNYaIC1YsFJirJtMW9NYhciP2xIaiqkgVXxvIMOl7UgMCyyHioTSfbubY17Zlw=="
ADMIN_NUMBER = "90000001"

# Rows per insert batch, and per commit: under SIMPLE recovery the log only ever has to hold
# one batch, not a whole 1.8-million-row table.
CHUNK_ROWS = 50_000
LOG_SIZE_MB = 256


def _password() -> str:
    if os.environ.get("MSSQL_SA_PASSWORD"):
        return os.environ["MSSQL_SA_PASSWORD"]
    if ENV_FILE.exists():
        for line in ENV_FILE.read_text(encoding="utf-8").splitlines():
            key, _, value = line.partition("=")
            if key.strip() == "MSSQL_SA_PASSWORD":
                return value.strip()
    sys.exit(f"MSSQL_SA_PASSWORD is neither in the environment nor in {ENV_FILE}")


def _connect(database: str, autocommit: bool = False) -> pyodbc.Connection:
    driver = next((d for d in ("ODBC Driver 18 for SQL Server", "ODBC Driver 17 for SQL Server")
                   if d in pyodbc.drivers()), None)
    if driver is None:
        sys.exit("No 'ODBC Driver 17/18 for SQL Server' is installed.")
    password = "{" + _password().replace("}", "}}") + "}"
    return pyodbc.connect(
        f"DRIVER={{{driver}}};SERVER=localhost,1433;DATABASE={database};"
        f"UID=sa;PWD={password};TrustServerCertificate=yes",
        autocommit=autocommit,
    )


def _ensure_database(database: str) -> None:
    """Create the database with the application's migrations if it is not there yet."""
    conn = _connect("master")
    try:
        exists = conn.cursor().execute("SELECT DB_ID(?)", database).fetchone()[0] is not None
    finally:
        conn.close()
    if exists:
        return

    print(f"[load] {database} does not exist — creating it with the application's migrations")
    password = _password().replace('"', '""')
    connection = (f'Server=localhost,1433;Database={database};User Id=sa;Password="{password}";'
                  "TrustServerCertificate=True;Encrypt=False")
    result = subprocess.run(
        ["dotnet", "ef", "database", "update",
         "--project", str(REPO_ROOT / "CampaignSystem"), "--connection", connection],
        capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    if result.returncode != 0:
        sys.exit("dotnet ef database update failed:\n" + "\n".join(result.stdout.splitlines()[-15:]))


def _use_simple_recovery(database: str) -> None:
    """A development database needs no point-in-time restore. Under the default FULL model a
    load logs every row and the log grows to several times the data; SQL Server then takes
    long to recover the database at startup, and an application connecting in that window
    takes the database for missing. SIMPLE keeps the log to one batch."""
    conn = _connect("master", autocommit=True)
    try:
        conn.cursor().execute(f"ALTER DATABASE [{database}] SET RECOVERY SIMPLE")
    finally:
        conn.close()


def _shrink_log(database: str) -> None:
    conn = _connect(database, autocommit=True)
    try:
        cur = conn.cursor()
        cur.execute("CHECKPOINT")
        log_file = cur.execute("SELECT file_id FROM sys.database_files WHERE type = 1").fetchone()[0]
        cur.execute(f"DBCC SHRINKFILE ({log_file}, {LOG_SIZE_MB}) WITH NO_INFOMSGS")
    finally:
        conn.close()


def _reset(conn: pyodbc.Connection) -> None:
    cur = conn.cursor()
    for table in RESET_ORDER:
        cur.execute(f"DELETE FROM dbo.[{table}]")
    cur.execute("DELETE FROM dbo.MERCHANT WHERE Id >= ?", config.MERCHANT_ID_START)
    conn.commit()
    print("[load] emptied the previous data (seeded rows kept)")


def _column_types(cur: pyodbc.Cursor, table: str) -> dict[str, str]:
    cur.execute(
        "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = ?",
        table,
    )
    return {name: data_type for name, data_type in cur.fetchall()}


def _rows(chunk: pd.DataFrame, sql_types: list[str]) -> list[tuple]:
    """The chunk as plain Python values of the type each target column expects.

    pyodbc takes neither numpy scalars nor NaN, and a float is not a decimal(18,2): each
    column is converted by what the table says it is, missing values becoming None.
    """
    columns = []
    for (_, series), sql_type in zip(chunk.items(), sql_types):
        if sql_type in ("int", "bigint", "smallint", "tinyint", "bit"):
            values = [None if pd.isna(v) else int(v) for v in series]
        elif sql_type in ("decimal", "numeric"):
            values = [None if pd.isna(v) else Decimal(str(v)) for v in series]
        elif sql_type in ("datetime2", "datetime", "date"):
            values = [None if pd.isna(v) else v.to_pydatetime() for v in pd.to_datetime(series)]
        else:
            values = [None if pd.isna(v) else str(v) for v in series]
        columns.append(values)
    return list(zip(*columns))


def _load_table(conn: pyodbc.Connection, file: str, table: str, identity: bool) -> int:
    cur = conn.cursor()
    cur.fast_executemany = True
    target = f"dbo.[{table}]"
    types = _column_types(cur, table)

    # IDENTITY_INSERT belongs to the session, so it holds across the per-batch commits.
    if identity:
        cur.execute(f"SET IDENTITY_INSERT {target} ON")

    total = 0
    for chunk in pd.read_csv(config.OUTPUT_DIR / f"{file}.csv", chunksize=CHUNK_ROWS):
        unknown = [c for c in chunk.columns if c not in types]
        if unknown:
            # The generator and the schema have drifted apart: stop rather than guess.
            raise SystemExit(f"{file}.csv has columns {table} does not: {unknown}")
        names = ", ".join(f"[{c}]" for c in chunk.columns)
        marks = ", ".join("?" for _ in chunk.columns)
        cur.executemany(f"INSERT INTO {target} ({names}) VALUES ({marks})",
                        _rows(chunk, [types[c] for c in chunk.columns]))
        conn.commit()
        total += len(chunk)

    if identity:
        cur.execute(f"SET IDENTITY_INSERT {target} OFF")
    conn.commit()
    return total


def _add_logins(conn: pyodbc.Connection) -> list[tuple[str, str]]:
    """An admin for the staff app, and one sign-in per segment for the customer app."""
    cur = conn.cursor()
    cur.execute(
        "INSERT INTO dbo.CUSTOMER (CustomerNumber, Gender, SegmentId, PasswordHash, IsActive, IsAdmin) "
        "VALUES (?, 'E', NULL, ?, 1, 1)",
        ADMIN_NUMBER, DEV_PASSWORD_HASH,
    )
    cur.execute(
        "UPDATE dbo.CUSTOMER SET PasswordHash = ? WHERE Id IN "
        "(SELECT MIN(Id) FROM dbo.CUSTOMER WHERE SegmentId IS NOT NULL AND IsAdmin = 0 GROUP BY SegmentId)",
        DEV_PASSWORD_HASH,
    )
    cur.execute(
        "SELECT s.SegmentName, c.CustomerNumber FROM dbo.CUSTOMER c JOIN dbo.SEGMENT s ON s.Id = c.SegmentId "
        "WHERE c.PasswordHash IS NOT NULL AND c.IsAdmin = 0 ORDER BY s.Id"
    )
    samples = [tuple(r) for r in cur.fetchall()]
    conn.commit()
    return samples


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description="Load the generated CSVs into a development database.")
    ap.add_argument("--database", default="CampaignSystem_Dev",
                    help="target database (default CampaignSystem_Dev); CampaignSystem is refused")
    ap.add_argument("--reset", action="store_true",
                    help="empty the database's own data first (seeded rows are kept)")
    args = ap.parse_args()

    if args.database.lower() in PROTECTED_DATABASES:
        sys.exit(f"Refusing to load into {args.database}: that is the presentation database.")
    for file, _, _ in TABLES:
        if not (config.OUTPUT_DIR / f"{file}.csv").exists():
            sys.exit(f"missing output/{file}.csv — run `uv run python main.py` first")

    _ensure_database(args.database)
    _use_simple_recovery(args.database)

    conn = _connect(args.database)
    try:
        cur = conn.cursor()
        segments = cur.execute("SELECT COUNT(*) FROM dbo.SEGMENT").fetchone()[0]
        if segments != len(config.SEGMENTS):
            sys.exit(f"{args.database} has {segments} segments, expected {len(config.SEGMENTS)}: "
                     "its schema or seed does not match the application.")
        customers = cur.execute("SELECT COUNT(*) FROM dbo.CUSTOMER").fetchone()[0]
        if customers and not args.reset:
            sys.exit(f"{args.database} already holds {customers:,} customers. "
                     "Run with --reset to empty it and load again.")
        if args.reset:
            _reset(conn)

        started = time.perf_counter()
        for file, table, identity in TABLES:
            try:
                count = _load_table(conn, file, table, identity)
            except pyodbc.Error as error:
                conn.rollback()
                sys.exit(f"[load] stopped at {table}: {error}\n"
                         "Fix the cause, then run again with --reset.")
            print(f"[load] {count:>9,} rows -> {table}")
        samples = _add_logins(conn)
    finally:
        conn.close()

    _shrink_log(args.database)

    print(f"\n[load] done in {time.perf_counter() - started:.0f}s -> {args.database}")
    print(f"[load] staff app admin : {ADMIN_NUMBER} / 123456")
    for segment, number in samples:
        print(f"[load] customer app    : {number} / 123456  ({segment})")
    print("[load] if the development API is already running, restart it so its caches reload.")


if __name__ == "__main__":
    main()
