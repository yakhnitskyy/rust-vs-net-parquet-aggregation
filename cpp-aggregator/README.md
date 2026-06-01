# C++ Parquet Aggregator

Aggregation-only C++ console app for reading `orders.parquet` and printing the same metrics as the .NET and Rust aggregators.

## Requirements

- GCC 15.2.0 or newer
- CMake 3.24+
- vcpkg (the repository includes `vcpkg.json` for reproducible dependencies)

## Build

One command from the repository root:

```powershell
.\build-cpp-aggregator.ps1
```

Note: if your repository path contains spaces, the script automatically creates a temporary junction under `C:\Users\yakhn\AppData\Local\Temp\opencode` so MinGW/vcpkg builds can proceed.
By default, the script also clones vcpkg to `C:\Users\yakhn\AppData\Local\Temp\opencode\vcpkg` (no spaces, MinGW-safe).

Optional examples:

```powershell
# Debug build
.\build-cpp-aggregator.ps1 -Configuration Debug

# Use existing vcpkg installation
.\build-cpp-aggregator.ps1 -VcpkgRoot C:\dev\vcpkg

# Force a specific CMake generator
.\build-cpp-aggregator.ps1 -Generator "MinGW Makefiles"

# Build and run immediately
.\build-cpp-aggregator.ps1 -Run
```

Manual build (if you do not want to use the script):

```powershell
vcpkg install --x-manifest-root .\cpp-aggregator --triplet x64-mingw-dynamic
cmake -S .\cpp-aggregator -B .\cpp-aggregator\build -DCMAKE_BUILD_TYPE=Release -DCMAKE_CXX_COMPILER=g++ -DCMAKE_TOOLCHAIN_FILE="$env:VCPKG_ROOT\scripts\buildsystems\vcpkg.cmake" -DVCPKG_TARGET_TRIPLET=x64-mingw-dynamic
cmake --build .\cpp-aggregator\build
```

If auto-detection picks a generator that does not work on your machine, use `-Generator` to force one.

## Run

By default, the app reads `data\orders.parquet` from the repository root:

```powershell
.\cpp-aggregator\build\cpp-aggregator.exe
```

You can also pass the Parquet file path explicitly:

```powershell
.\cpp-aggregator\build\cpp-aggregator.exe --path .\data\orders.parquet
```

## Output

The app prints:

- file path and file size
- row-group processing progress
- order count and revenue by region
- total rows processed
- elapsed time
- rows per second throughput
