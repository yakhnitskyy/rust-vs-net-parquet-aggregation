# Node.js Parquet Aggregator

Aggregation-only Node.js console app for reading `data\orders.parquet` and printing the same metrics as the .NET, Rust, and C++ aggregators.

## Requirements

- Node.js 24+
- npm 10+

## Install

From the repository root:

```powershell
cd .\node-aggregator
npm install
```

## Run

By default, the app reads `data\orders.parquet` from the repository root:

```powershell
npm run aggregate
```

You can also pass the Parquet file path explicitly:

```powershell
node .\src\main.mjs --path ..\data\orders.parquet
```

The app uses DuckDB's vectorized Parquet scan and automatically sets thread count to your machine's available parallelism.

## Output

The app prints:

- file path and file size
- DuckDB thread count and metadata row-group count
- order count and revenue by region
- total rows processed
- elapsed time
- rows per second throughput
