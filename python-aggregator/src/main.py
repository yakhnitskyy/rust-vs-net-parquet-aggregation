#!/usr/bin/env python3

from __future__ import annotations

import argparse
import sys
import time
from pathlib import Path

import polars as pl
import pyarrow.parquet as pq

DEFAULT_FILE_NAME = "orders.parquet"
DEFAULT_DATA_DIRECTORY = "data"
REGION_NAMES = ["North", "South", "East", "West", "Central", "Online"]


def main() -> int:
    options = parse_options()
    full_path = options.path.resolve()

    if not full_path.exists():
        print(f"Parquet file not found: {full_path}", file=sys.stderr)
        return 1

    file_size = full_path.stat().st_size
    print(f"Reading {full_path}")
    print(f"File size: {format_bytes(file_size)}")

    started = time.perf_counter()

    parquet_file = pq.ParquetFile(str(full_path))
    row_group_count = parquet_file.num_row_groups
    expected_rows = parquet_file.metadata.num_rows
    if row_group_count > 0:
        print(f"Row groups: {format_count(row_group_count)} (metadata), expected rows: {format_count(expected_rows)}")

    frame = (
        pl.scan_parquet(str(full_path))
        .select(
            [
                (pl.col("RegionId").cast(pl.Int64) % len(REGION_NAMES)).alias("region_index"),
                pl.col("Quantity").cast(pl.Int64).alias("quantity"),
                pl.col("UnitPrice").cast(pl.Float64).alias("unit_price"),
            ]
        )
        .group_by("region_index")
        .agg(
            [
                pl.len().alias("orders"),
                (pl.col("quantity") * pl.col("unit_price")).sum().alias("revenue"),
            ]
        )
        .sort("region_index")
        .collect()
    )

    orders_by_region = [0] * len(REGION_NAMES)
    revenue_by_region = [0.0] * len(REGION_NAMES)

    for row in frame.iter_rows(named=True):
        index = int(row["region_index"])
        if 0 <= index < len(REGION_NAMES):
            orders_by_region[index] = int(row["orders"])
            revenue_by_region[index] = float(row["revenue"] or 0.0)

    rows_read = sum(orders_by_region)
    total_revenue = sum(revenue_by_region)

    elapsed_seconds = max(time.perf_counter() - started, 0.001)
    elapsed_text = format_elapsed(elapsed_seconds)

    print()
    print("Aggregation by region")
    print("Region       Orders            Revenue")
    print("----------------------------------------------")

    for index, name in enumerate(REGION_NAMES):
        region = name.ljust(10)
        orders = format_count(orders_by_region[index]).rjust(14)
        revenue = format_currency(revenue_by_region[index]).rjust(18)
        print(f"{region} {orders} {revenue}")

    print("----------------------------------------------")
    print(f"{'Total'.ljust(10)} {format_count(rows_read).rjust(14)} {format_currency(total_revenue).rjust(18)}")
    print()
    print(f"Processed {format_count(rows_read)} rows in {elapsed_text}")
    print(f"Throughput: {format_count(round(rows_read / elapsed_seconds))} rows/sec")

    return 0


def parse_options() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        prog="python-aggregator",
        description="Aggregate orders.parquet by region using Polars.",
    )
    parser.add_argument(
        "--path",
        type=Path,
        default=default_orders_path(),
        help="Path to the Parquet file. Defaults to data/orders.parquet in the repository root.",
    )
    return parser.parse_args()


def default_orders_path() -> Path:
    return find_repository_root() / DEFAULT_DATA_DIRECTORY / DEFAULT_FILE_NAME


def find_repository_root() -> Path:
    candidates = [Path.cwd(), Path(__file__).resolve().parent]

    for start in candidates:
        current = start
        while True:
            if (current / "dotnet-app").exists() and (current / "rust-aggregator").exists() and (current / "cpp-aggregator").exists():
                return current

            if current.parent == current:
                break

            current = current.parent

    return Path.cwd()


def format_count(value: int) -> str:
    return f"{value:,}"


def format_currency(value: float) -> str:
    return f"${value:,.2f}"


def format_bytes(byte_count: int) -> str:
    units = ["B", "KB", "MB", "GB", "TB"]
    value = float(byte_count)
    unit_index = 0

    while value >= 1024 and unit_index < len(units) - 1:
        value /= 1024
        unit_index += 1

    return f"{value:,.2f} {units[unit_index]}"


def format_elapsed(seconds: float) -> str:
    total_100ns = round(seconds * 10_000_000)
    total_seconds = total_100ns // 10_000_000

    hours = total_seconds // 3600
    minutes = (total_seconds % 3600) // 60
    secs = total_seconds % 60
    fraction = total_100ns % 10_000_000

    return f"{hours:02}:{minutes:02}:{secs:02}.{fraction:07}"


if __name__ == "__main__":
    raise SystemExit(main())
