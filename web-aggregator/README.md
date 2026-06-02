# Browser DuckDB-WASM Aggregator

Simple HTML app that lets you upload an `orders.parquet`-compatible file and run aggregation in the browser with DuckDB-WASM.

## Requirements

- Modern browser with WebAssembly and Web Workers (Chrome, Edge, Firefox)

## Run

Serve the `web-aggregator` folder over HTTP (do not open with `file://`):

```powershell
python -m http.server 8080 --directory .\web-aggregator
```

Then open:

```text
http://localhost:8080
```

## Output

The page shows:

- file size
- row-group metadata
- order count and revenue by region
- total rows processed
- elapsed time
- rows per second throughput

Expected columns in the parquet file: `Quantity`, `UnitPrice`, and `RegionId`.
