# Aggregator Run Results

Run date: 2026-06-01

Shared input file used for all 4 runs:

- `data\orders-smoke.parquet`
- generated with: `dotnet run --project .\dotnet-app\ParquetPerformance.csproj -c Release -- generate --rows 10000 --row-group-size 2500 --path .\data\orders-smoke.parquet`

## 1) .NET Aggregator

Command:

```powershell
dotnet run --project .\dotnet-app\ParquetPerformance.csproj -c Release -- aggregate --path .\data\orders-smoke.parquet
```

Output:

Reading .\data\orders-smoke.parquet
File size: 284.23 KB
Row group 1/4: processed 2,500 rows in 00:00:00.0613885
Row group 2/4: processed 5,000 rows in 00:00:00.0650201
Row group 3/4: processed 7,500 rows in 00:00:00.0655187
Row group 4/4: processed 10,000 rows in 00:00:00.0658400

Aggregation by region
Region       Orders            Revenue
----------------------------------------------
North               1,677      $2,636,470.87
South               1,632      $2,284,479.45
East                1,650      $2,114,298.96
West                1,687      $2,031,902.50
Central             1,726      $2,257,791.75
Online              1,628      $2,445,199.77
----------------------------------------------
Total              10,000     $13,770,143.30

Processed 10,000 rows in 00:00:00.0658479
Throughput: 151,865 rows/sec
```

## 2) Rust Aggregator

Command:

```powershell
.\rust-aggregator\target\release\rust-aggregator.exe --path .\data\orders-smoke.parquet
```

Output:

```text
Reading .\data\orders-smoke.parquet
File size: 284.23 KB
Row group 1/4: processed 2,500 rows in 00:00:00.0015442
Row group 2/4: processed 5,000 rows in 00:00:00.0015443
Row group 3/4: processed 7,500 rows in 00:00:00.0015443
Row group 4/4: processed 10,000 rows in 00:00:00.0015444

Aggregation by region
Region       Orders            Revenue
----------------------------------------------
North               1,677      $2,636,470.87
South               1,632      $2,284,479.45
East                1,650      $2,114,298.96
West                1,687      $2,031,902.50
Central             1,726      $2,257,791.75
Online              1,628      $2,445,199.77
----------------------------------------------
Total              10,000     $13,770,143.30

Processed 10,000 rows in 00:00:00.0015596
Throughput: 6,411,900 rows/sec
```

## 3) C++ Aggregator

Command:

```powershell
.\cpp-aggregator\build\cpp-aggregator.exe --path .\data\orders-smoke.parquet
```

Output:

```text
Reading .\data\orders-smoke.parquet
File size: 284.23 KB
Row group 1/4: processed 2,500 rows in 00:00:00.0077500
Row group 2/4: processed 5,000 rows in 00:00:00.0077773
Row group 3/4: processed 7,500 rows in 00:00:00.0077773
Row group 4/4: processed 10,000 rows in 00:00:00.0078237

Aggregation by region
Region       Orders            Revenue
----------------------------------------------
North              1,677     $2,636,470.87
South              1,632     $2,284,479.45
East               1,650     $2,114,298.96
West               1,687     $2,031,902.50
Central            1,726     $2,257,791.75
Online             1,628     $2,445,199.77
----------------------------------------------
Total             10,000    $13,770,143.30

Processed 10,000 rows in 00:00:00.0083305
Throughput: 1,200,408 rows/sec
```

## 4) Node.js Aggregator

Command:

```powershell
node .\node-aggregator\src\main.mjs --path .\data\orders-smoke.parquet
```

Output:

```text
Reading .\data\orders-smoke.parquet
File size: 284.23 KB
DuckDB threads: 16
Row groups: 4 (metadata), expected rows: 10,000

Aggregation by region
Region       Orders            Revenue
----------------------------------------------
North               1,677      $2,636,470.87
South               1,632      $2,284,479.45
East                1,650      $2,114,298.96
West                1,687      $2,031,902.50
Central             1,726      $2,257,791.75
Online              1,628      $2,445,199.77
----------------------------------------------
Total              10,000     $13,770,143.30

Processed 10,000 rows in 00:00:00.0211570
Throughput: 472,657 rows/sec
```

---

# 1,000,000,000 Row Run Results

Input file:

- `data\orders.parquet`

Note:

- Rust, C++, and .NET print per-row-group progress for 1,000 row groups, so only key summary lines are captured below.

## .NET Aggregator (1B)

Command:

```powershell
dotnet run --project .\dotnet-app\ParquetPerformance.csproj -c Release -- aggregate --path .\data\orders.parquet
```

Summary:

```text
Processed 1,000,000,000 rows in 00:00:18.4660191
Throughput: 54,153,524 rows/sec
Total       1,000,000,000 $1,381,244,602,467.31
```

## Rust Aggregator (1B)

Command:

```powershell
.\rust-aggregator\target\release\rust-aggregator.exe --path .\data\orders.parquet
```

Summary:

```text
Processed 1,000,000,000 rows in 00:00:03.9724427
Throughput: 251,734,279 rows/sec
Total       1,000,000,000 $1,381,244,602,467.34
```

## C++ Aggregator (1B)

Command:

```powershell
.\cpp-aggregator\build\cpp-aggregator.exe --path .\data\orders.parquet
```

Summary:

```text
Processed 1,000,000,000 rows in 00:00:04.1248326
Throughput: 242,434,081 rows/sec
Total      1,000,000,000$1,381,244,602,467.34
```

## Node.js Aggregator (1B)

Command:

```powershell
node .\node-aggregator\src\main.mjs --path .\data\orders.parquet
```

Summary:

```text
Processed 1,000,000,000 rows in 00:00:03.1310109
Throughput: 319,385,666 rows/sec
Total       1,000,000,000 $1,381,244,602,465.79
```

## .NET DuckDB Aggregator (1B, file source)

Command:

```powershell
dotnet run --project .\dotnet-duckdb\dotnet-duckdb.csproj -c Release -- --source file --path .\data\orders.parquet
```

Summary:

```text
Processed 1,000,000,000 rows in 00:00:03.3873785
Throughput: 295,213,541 rows/sec
Total       1,000,000,000 $1,381,244,602,465.79
```

## .NET DuckDB Aggregator (1B, memory source)

Command:

```powershell
dotnet run --project .\dotnet-duckdb\dotnet-duckdb.csproj -c Release -- --source memory --path .\data\orders.parquet
```

Summary:

```text
Processed 1,000,000,000 rows in 00:00:01.1955577 (aggregation query after loading table into memory)
Throughput: 836,429,726 rows/sec
```
