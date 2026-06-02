# Python Polars Parquet Aggregator

Aggregation-only Python console app for reading `data\orders.parquet` and printing the same metrics as the other aggregators.

## Requirements

- Python 3.11+

## Install

From the repository root:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r .\python-aggregator\requirements.txt
```

## Run

By default, the app reads `data\orders.parquet` from the repository root:

```powershell
python .\python-aggregator\src\main.py
```

You can also pass the Parquet file path explicitly:

```powershell
python .\python-aggregator\src\main.py --path .\data\orders.parquet
```

## Output

The app prints:

- file path and file size
- row-group metadata
- order count and revenue by region
- total rows processed
- elapsed time
- rows per second throughput
