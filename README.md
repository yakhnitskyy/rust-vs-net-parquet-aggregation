# Parquet Performance

This repository contains six console apps that work with the same local Parquet orders file.

- `dotnet-app`: .NET 10 app that can generate fake orders and aggregate them.
- `dotnet-duckdb`: .NET 10 app that aggregates with the DuckDB engine.
- `rust-aggregator`: Rust app that only aggregates an existing Parquet file.
- `cpp-aggregator`: C++ app that only aggregates an existing Parquet file.
- `node-aggregator`: Node.js 24+ app that only aggregates an existing Parquet file.
- `clickhouse-aggregator`: .NET 10 app that starts ClickHouse in Docker, loads a temporary table, and does aggregation within clickhouse

All aggregators read `Quantity`, `UnitPrice`, and `RegionId`, then print:

- file path and file size
- row-group processing progress (or row-group metadata)
- order count and revenue by region
- total rows processed
- elapsed time
- rows per second throughput

## Data Schema

The generated `orders.parquet` file contains:

- `OrderId`
- `CustomerId`
- `ProductId`
- `OrderDateUtc`
- `Quantity`
- `UnitPrice`
- `RegionId`

## Build

Build the .NET app:

```powershell
dotnet build .\dotnet-app\ParquetPerformance.csproj -c Release
```

Build the .NET DuckDB app:

```powershell
dotnet build .\dotnet-duckdb\dotnet-duckdb.csproj -c Release
```

Build the Rust app:

```powershell
cd .\rust-aggregator
cargo build --release
cd ..
```

Build the C++ app:

```powershell
.\build-cpp-aggregator.ps1
```

The script uses `cpp-aggregator\vcpkg.json` to install Arrow + Parquet through vcpkg, then builds with CMake.
If needed, force a generator explicitly, for example: `./build-cpp-aggregator.ps1 -Generator "MinGW Makefiles"`.

Install Node.js dependencies:

```powershell
cd .\node-aggregator
npm install
cd ..
```

Build the ClickHouse .NET app:

```powershell
dotnet build .\clickhouse-aggregator\ClickHouseAggregator.csproj -c Release
```

## Generate Data With .NET

Generate the default 100 million rows into `data\orders.parquet` under the repository root:

```powershell
dotnet run --project .\dotnet-app\ParquetPerformance.csproj -c Release -- generate
```

For a quick smoke-test file:

```powershell
dotnet run --project .\dotnet-app\ParquetPerformance.csproj -c Release -- generate --rows 10000 --row-group-size 2500
```

## Run The .NET Aggregator

```powershell
dotnet run --project .\dotnet-app\ParquetPerformance.csproj -c Release -- aggregate
```

## Run The .NET DuckDB Aggregator

The .NET DuckDB app reads `data\orders.parquet` from the repository root when no path is supplied:

```powershell
dotnet run --project .\dotnet-duckdb\dotnet-duckdb.csproj -c Release
```

You can also pass a file path explicitly:

```powershell
dotnet run --project .\dotnet-duckdb\dotnet-duckdb.csproj -c Release -- --path .\data\orders.parquet
```

## Run The Rust Aggregator

The Rust app reads `data\orders.parquet` from the repository root when no path is supplied:

```powershell
.\rust-aggregator\target\release\rust-aggregator.exe
```

You can also pass a file path explicitly:

```powershell
.\rust-aggregator\target\release\rust-aggregator.exe --path .\data\orders.parquet
```

The Rust app processes row groups in parallel. To tune CPU usage:

```powershell
$env:RAYON_NUM_THREADS = "8"
.\rust-aggregator\target\release\rust-aggregator.exe
```

## Run The C++ Aggregator

The C++ app reads `data\orders.parquet` from the repository root when no path is supplied:

```powershell
.\cpp-aggregator\build\cpp-aggregator.exe
```

You can also pass a file path explicitly:

```powershell
.\cpp-aggregator\build\cpp-aggregator.exe --path .\data\orders.parquet
```

## Run The Node.js Aggregator

The Node.js app reads `data\orders.parquet` from the repository root when no path is supplied:

```powershell
cd .\node-aggregator
npm run aggregate
cd ..
```

You can also pass a file path explicitly:

```powershell
cd .\node-aggregator
node .\src\main.mjs --path ..\data\orders.parquet
cd ..
```

## Run The ClickHouse Aggregator (Docker Desktop)

The ClickHouse app maps the repository `data` directory into ClickHouse `user_files`, creates a temporary table, inserts parquet rows into it, then runs the same region aggregation.

```powershell
dotnet run --project .\clickhouse-aggregator\ClickHouseAggregator.csproj -c Release
```

You can also pass a file path explicitly (must be under `.\data` so it is visible in the mapped container volume):

```powershell
dotnet run --project .\clickhouse-aggregator\ClickHouseAggregator.csproj -c Release -- --path .\data\orders.parquet
```

## Test / Smoke Test

There are no dedicated unit test projects yet. Use these commands to verify all apps end to end with a small Parquet file:

```powershell
dotnet build .\dotnet-app\ParquetPerformance.csproj -c Release
dotnet build .\dotnet-duckdb\dotnet-duckdb.csproj -c Release
dotnet build .\clickhouse-aggregator\ClickHouseAggregator.csproj -c Release
cd .\rust-aggregator
cargo build --release
cargo test
cd ..
.\build-cpp-aggregator.ps1
cd .\node-aggregator
npm install
cd ..
dotnet run --project .\dotnet-app\ParquetPerformance.csproj -c Release -- generate --rows 10000 --row-group-size 2500
dotnet run --project .\dotnet-app\ParquetPerformance.csproj -c Release -- aggregate
dotnet run --project .\dotnet-duckdb\dotnet-duckdb.csproj -c Release
.\rust-aggregator\target\release\rust-aggregator.exe
.\cpp-aggregator\build\cpp-aggregator.exe
cd .\node-aggregator
npm run aggregate
cd ..
dotnet run --project .\clickhouse-aggregator\ClickHouseAggregator.csproj -c Release -- --path .\data\orders.parquet
Remove-Item .\data\orders.parquet
```
