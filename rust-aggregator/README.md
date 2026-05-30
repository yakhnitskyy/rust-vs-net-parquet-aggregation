# Rust Parquet Aggregator

Aggregation-only Rust console app for reading `orders.parquet` and printing the same metrics as the .NET aggregator.

## Build

From the repository root:

```powershell
cd rust-aggregator
cargo build --release
```

## Run

By default, the app expects `orders.parquet` to be in the same folder as `rust-aggregator.exe`.

```powershell
copy ..\orders.parquet .\target\release\orders.parquet
.\target\release\rust-aggregator.exe
```

You can also pass the Parquet file path explicitly:

```powershell
.\target\release\rust-aggregator.exe --path ..\orders.parquet
```

The app processes Parquet row groups in parallel. To limit or tune CPU usage, set `RAYON_NUM_THREADS` before running it:

```powershell
$env:RAYON_NUM_THREADS = "8"
.\target\release\rust-aggregator.exe --path ..\orders.parquet
```

## Output

The app prints:

- file path and file size
- row-group processing progress
- order count and revenue by region
- total rows processed
- elapsed time
- rows per second throughput

This Rust app does not generate test data. Generate `orders.parquet` with the .NET app first, or place an existing compatible Parquet file next to the Rust executable.
