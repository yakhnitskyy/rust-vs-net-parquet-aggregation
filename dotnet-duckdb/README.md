# .NET DuckDB Aggregator

This console app runs the shared Parquet aggregation through DuckDB from .NET.

It reads `data\orders.parquet` from the repository root when `--path` is omitted. The aggregation groups rows by `RegionId`, counts orders, and sums revenue as `Quantity * UnitPrice`.

## Build

```powershell
dotnet build .\dotnet-duckdb\dotnet-duckdb.csproj -c Release
```

## Run From Parquet File

`file` is the default source mode. DuckDB reads the Parquet file during the timed aggregation query.

```powershell
dotnet run --project .\dotnet-duckdb\dotnet-duckdb.csproj -c Release
```

```powershell
dotnet run --project .\dotnet-duckdb\dotnet-duckdb.csproj -c Release -- --source file --path .\data\orders.parquet
```

## Run From Memory

Use `--source memory` to load the Parquet file into a DuckDB in-memory temp table before timing. The app prints the preload duration separately and excludes it from the reported aggregation time.

```powershell
dotnet run --project .\dotnet-duckdb\dotnet-duckdb.csproj -c Release -- --source memory --path .\data\orders.parquet
```

## Options

- `--path <file>`: Parquet file path. Defaults to `data\orders.parquet` under the repository root.
- `--source file`: aggregate directly from the Parquet file with DuckDB `read_parquet`.
- `--source memory`: preload the Parquet data into a DuckDB in-memory table, then time only the aggregation query.
