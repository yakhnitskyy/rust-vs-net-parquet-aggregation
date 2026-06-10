# Parquet Performance

This repository is a comparative performance benchmark for evaluating how different programming languages and data-processing technologies read and aggregate data from Apache Parquet files. The primary goal is to identify fast, practical approaches for processing large local Parquet datasets with a consistent workload, schema, and output format across implementations.

For a concise presentation of the key findings, see [Parquet Processing Benchmark Findings](presentation/parquet-performance-findings.pdf).

The benchmark uses a generated orders dataset and runs the same aggregation in each implementation: read the required columns from `data\orders.parquet`, calculate revenue as `Quantity * UnitPrice`, group by `RegionId`, and report elapsed time and throughput. Keeping the workload consistent makes it easier to compare language/runtime overhead, Parquet reader performance, vectorized execution engines, and embedded analytical databases.

## Implementations

Current implementations in this repository:

| Folder | Language / Runtime | Main technology | Purpose |
| --- | --- | --- | --- |
| `dotnet-app` | C# / .NET 10 | Parquet.Net | Generates the shared fake orders dataset and provides a baseline .NET Parquet reader aggregation. |
| `dotnet-duckdb` | C# / .NET 10 | DuckDB.NET / DuckDB | Runs the same aggregation through DuckDB's Parquet SQL engine from .NET, either directly from Parquet or from a preloaded in-memory DuckDB table. |
| `rust-aggregator` | Rust | Apache Parquet crate + Rayon | Reads Parquet columns directly and aggregates row groups in parallel. |
| `cpp-aggregator` | C++ | Apache Arrow with Parquet support | Native C++ Parquet aggregation using Arrow's columnar libraries. |
| `node-aggregator` | Node.js 24+ | DuckDB Node API | Runs the aggregation through DuckDB from Node.js. |
| `clickhouse-aggregator` | C# / .NET 10 + Docker | ClickHouse | Runs ClickHouse in Docker with the repository `data` folder mapped into the container, loads the Parquet data into a temporary table, and runs the aggregation there. |
| `python-aggregator` | Python | Polars + PyArrow | Uses Python's columnar data ecosystem to aggregate Parquet data. |
| `web-aggregator` | Browser JavaScript | DuckDB-WASM | Runs the aggregation in-browser against an uploaded Parquet file. |

Potential future comparison targets include additional combinations such as Rust with Polars, Java with Apache Arrow/DataFusion-style engines, Go with native Parquet readers, and other embedded OLAP engines. These should use the same input file and aggregation contract to remain comparable.

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

Install Python dependencies:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r .\python-aggregator\requirements.txt
```

Run the browser app locally:

```powershell
python -m http.server 8080 --directory .\web-aggregator
```

Then open `http://localhost:8080`.
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

The default source mode is `file`, which runs the aggregation query directly against the Parquet file with DuckDB `read_parquet`:

```powershell
dotnet run --project .\dotnet-duckdb\dotnet-duckdb.csproj -c Release -- --source file --path .\data\orders.parquet
```

Use `--source memory` to load the Parquet file into a DuckDB in-memory temp table before timing. The reported elapsed time excludes the preload and measures only the aggregation query over the in-memory table:

```powershell
dotnet run --project .\dotnet-duckdb\dotnet-duckdb.csproj -c Release -- --source memory --path .\data\orders.parquet
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
dotnet run --project .\dotnet-duckdb\dotnet-duckdb.csproj -c Release -- --source memory
.\rust-aggregator\target\release\rust-aggregator.exe
.\cpp-aggregator\build\cpp-aggregator.exe
cd .\node-aggregator
npm run aggregate
cd ..
dotnet run --project .\clickhouse-aggregator\ClickHouseAggregator.csproj -c Release -- --path .\data\orders.parquet
Remove-Item .\data\orders.parquet
```
