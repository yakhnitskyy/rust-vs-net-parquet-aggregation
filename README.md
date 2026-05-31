# Parquet Performance

This repository contains three console apps that work with the same local Parquet orders file.

- `dotnet-app`: .NET 10 app that can generate fake orders and aggregate them.
- `rust-aggregator`: Rust app that only aggregates an existing Parquet file.
- `cpp-aggregator`: C++ app that only aggregates an existing Parquet file.

All aggregators read `Quantity`, `UnitPrice`, and `RegionId`, then print:

- file path and file size
- row-group processing progress
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

## Generate Data With .NET

Generate the default 100 million rows into the repository root:

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

## Run The Rust Aggregator

The Rust app reads `orders.parquet` from the repository root when no path is supplied:

```powershell
.\rust-aggregator\target\release\rust-aggregator.exe
```

You can also pass a file path explicitly:

```powershell
.\rust-aggregator\target\release\rust-aggregator.exe --path .\orders.parquet
```

The Rust app processes row groups in parallel. To tune CPU usage:

```powershell
$env:RAYON_NUM_THREADS = "8"
.\rust-aggregator\target\release\rust-aggregator.exe
```

## Run The C++ Aggregator

The C++ app reads `orders.parquet` from the repository root when no path is supplied:

```powershell
.\cpp-aggregator\build\cpp-aggregator.exe
```

You can also pass a file path explicitly:

```powershell
.\cpp-aggregator\build\cpp-aggregator.exe --path .\orders.parquet
```

## Test / Smoke Test

There are no dedicated unit test projects yet. Use these commands to verify all apps end to end with a small Parquet file:

```powershell
dotnet build .\dotnet-app\ParquetPerformance.csproj -c Release
cd .\rust-aggregator
cargo build --release
cargo test
cd ..
.\build-cpp-aggregator.ps1
dotnet run --project .\dotnet-app\ParquetPerformance.csproj -c Release -- generate --rows 10000 --row-group-size 2500
dotnet run --project .\dotnet-app\ParquetPerformance.csproj -c Release -- aggregate
.\rust-aggregator\target\release\rust-aggregator.exe
.\cpp-aggregator\build\cpp-aggregator.exe
Remove-Item .\orders.parquet
```
