# AGENTS

## What This Repo Is
- Four console apps operate on the same Parquet dataset: `dotnet-app`, `rust-aggregator`, `cpp-aggregator`, `node-aggregator`.
- Shared default data path is `{repo-root}\data\orders.parquet` when `--path` is omitted.

## High-Value Entry Points
- `.NET`: `dotnet-app/Program.cs` (`generate` and `aggregate` commands).
- `Rust`: `rust-aggregator/src/main.rs` -> `src/app.rs` (aggregate-only).
- `C++`: `cpp-aggregator/src/main.cpp` (aggregate-only).
- `Node.js`: `node-aggregator/src/main.mjs` (aggregate-only, DuckDB-based).

## Exact Commands (Verified)
- Build .NET: `dotnet build .\dotnet-app\ParquetPerformance.csproj -c Release`
- Run .NET generator (small smoke file): `dotnet run --project .\dotnet-app\ParquetPerformance.csproj -c Release -- generate --rows 10000 --row-group-size 2500`
- Run .NET aggregator: `dotnet run --project .\dotnet-app\ParquetPerformance.csproj -c Release -- aggregate`
- Build Rust: run in `rust-aggregator` -> `cargo build --release`
- Test Rust: run in `rust-aggregator` -> `cargo test`
- Build C++: run from repo root -> `.\build-cpp-aggregator.ps1`
- Install Node deps: run in `node-aggregator` -> `npm install`
- Run Node aggregator: run in `node-aggregator` -> `npm run aggregate`

## Build/Test Order That Avoids Mistakes
- If you need end-to-end verification, generate data first with .NET, then run aggregators (`.NET`, `Rust`, `C++`, `Node`).
- Rust/C++/Node apps do not generate test data.

## Repo-Specific Gotchas
- The C++ build script is the source of truth (`build-cpp-aggregator.ps1`), not manual CMake snippets.
- Path-with-spaces handling for C++ is already built into the script: it creates a temp junction under `C:\Users\yakhn\AppData\Local\Temp\opencode`.
- For MinGW triplets, `vcpkg` path must not contain spaces (enforced by script).
- C++ generator changes invalidate the existing build dir; the script deletes `cpp-aggregator\build` automatically when generator differs from cache.
- `*.parquet` is gitignored at repo root; generated data files will not show up in commits.

## Toolchain Constraints
- .NET target is `net10.0` (`dotnet-app/ParquetPerformance.csproj`).
- Rust edition is `2024` (`rust-aggregator/Cargo.toml`).
- Node app requires Node `>=24.0.0` (`node-aggregator/package.json`).

## Current Automation Reality
- No CI workflows are present in this repo.
- No repo-level lint/format/typecheck tasks are defined; use per-app build/smoke commands above for validation.
