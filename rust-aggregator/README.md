# Rust Parquet Aggregator

Aggregation-only Rust console app for reading `orders.parquet` and printing the same metrics as the .NET aggregator.

## Build

From the repository root:

```powershell
cd rust-aggregator
cargo build --release
```

## Run

By default, the app reads `data\orders.parquet` from the repository root.

```powershell
.\target\release\rust-aggregator.exe
```

You can also pass the Parquet file path explicitly:

```powershell
.\target\release\rust-aggregator.exe --path ..\data\orders.parquet
```

The app processes Parquet row groups in parallel. To limit or tune CPU usage, set `RAYON_NUM_THREADS` before running it:

```powershell
$env:RAYON_NUM_THREADS = "8"
.\target\release\rust-aggregator.exe
```

## Output

The app prints:

- file path and file size
- row-group processing progress
- order count and revenue by region
- total rows processed
- elapsed time
- rows per second throughput

This Rust app does not generate test data. Generate `data\orders.parquet` in the repository root with the .NET app first, or pass an existing compatible Parquet file with `--path`.
