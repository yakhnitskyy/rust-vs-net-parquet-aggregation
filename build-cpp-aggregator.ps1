param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [string]$Triplet = "x64-mingw-dynamic",

    [string]$Generator,

    [string]$VcpkgRoot,

    [switch]$Run
)

$ErrorActionPreference = "Stop"

function Resolve-WorkspaceRoot {
    param([string]$RootPath)

    $resolved = (Resolve-Path -LiteralPath $RootPath).Path
    if ($resolved -notmatch "\s") {
        return $resolved
    }

    $tempRoot = "C:\Users\yakhn\AppData\Local\Temp\opencode"
    if (-not (Test-Path -LiteralPath $tempRoot)) {
        throw "Temporary directory not found: $tempRoot"
    }

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($resolved.ToLowerInvariant())
    $hash = [System.Security.Cryptography.SHA256]::HashData($bytes)
    $hashText = ([System.BitConverter]::ToString($hash) -replace '-', '').Substring(0, 12).ToLowerInvariant()
    $linkPath = Join-Path $tempRoot "pp-$hashText"

    if (Test-Path -LiteralPath $linkPath) {
        $item = Get-Item -LiteralPath $linkPath -Force
        $targetText = ($item.Target | Out-String).Trim()
        if ($item.LinkType -eq "Junction" -and $targetText -eq $resolved) {
            return $linkPath
        }

        Remove-Item -LiteralPath $linkPath -Recurse -Force
    }

    New-Item -ItemType Junction -Path $linkPath -Target $resolved | Out-Null
    return $linkPath
}

$repoRoot = Resolve-WorkspaceRoot -RootPath $PSScriptRoot
$projectDir = Join-Path $repoRoot "cpp-aggregator"
$buildDir = Join-Path $projectDir "build"

if (-not (Test-Path -LiteralPath $projectDir)) {
    throw "cpp-aggregator folder was not found at: $projectDir"
}

function Resolve-VcpkgRoot {
    param(
        [string]$Preferred,
        [string]$ActiveTriplet
    )

    function Validate-PathForTriplet {
        param(
            [string]$CandidatePath,
            [string]$Triplet
        )

        if ($Triplet -match "mingw" -and $CandidatePath -match "\s") {
            throw "For MinGW triplets, vcpkg root must not contain spaces: $CandidatePath"
        }

        return $CandidatePath
    }

    function Normalize-FullPath {
        param([string]$PathValue)
        return [System.IO.Path]::GetFullPath($PathValue)
    }

    if ($Preferred -and (Test-Path -LiteralPath $Preferred)) {
        $full = Normalize-FullPath -PathValue $Preferred
        return Validate-PathForTriplet -CandidatePath $full -Triplet $ActiveTriplet
    }

    if ($env:VCPKG_ROOT -and (Test-Path -LiteralPath $env:VCPKG_ROOT)) {
        $full = Normalize-FullPath -PathValue $env:VCPKG_ROOT
        return Validate-PathForTriplet -CandidatePath $full -Triplet $ActiveTriplet
    }

    $local = "C:\Users\yakhn\AppData\Local\Temp\opencode\vcpkg"
    if (-not (Test-Path -LiteralPath $local)) {
        git clone https://github.com/microsoft/vcpkg "$local"
    }

    $full = Normalize-FullPath -PathValue $local
    return Validate-PathForTriplet -CandidatePath $full -Triplet $ActiveTriplet
}

function Select-CMakeGenerator {
    param([string]$Preferred)

    if ($Preferred) {
        return $Preferred
    }

    if ((Get-Command ninja -ErrorAction SilentlyContinue) -and ((& ninja --version) -ne $null)) {
        return "Ninja"
    }

    if (Get-Command mingw32-make -ErrorAction SilentlyContinue) {
        return "MinGW Makefiles"
    }

    if (Get-Command make -ErrorAction SilentlyContinue) {
        return "Unix Makefiles"
    }

    throw "No supported generator found. Install Ninja/mingw32-make/make or pass -Generator explicitly."
}

function Prepare-MingwEnvironment {
    param([string]$ActiveTriplet)

    if ($ActiveTriplet -notmatch "mingw") {
        return
    }

    $prepend = @()
    foreach ($candidate in @("C:\msys64\ucrt64\bin", "C:\msys64\mingw64\bin")) {
        if (Test-Path -LiteralPath $candidate) {
            $prepend += $candidate
        }
    }

    $usrBin = "C:\msys64\usr\bin"
    if (Test-Path -LiteralPath $usrBin) {
        $prepend += $usrBin
    }

    if ($prepend.Count -eq 0) {
        return
    }

    $current = @($env:Path -split ';' | Where-Object { $_ -and ($_ -notin $prepend) })
    $env:Path = (@($prepend) + $current) -join ';'
}

function Get-CachedGenerator {
    param([string]$BuildDirectory)

    $cacheFile = Join-Path $BuildDirectory "CMakeCache.txt"
    if (-not (Test-Path -LiteralPath $cacheFile)) {
        return $null
    }

    $line = Select-String -LiteralPath $cacheFile -Pattern '^CMAKE_GENERATOR:INTERNAL=' | Select-Object -First 1
    if (-not $line) {
        return $null
    }

    return ($line.Line -replace '^CMAKE_GENERATOR:INTERNAL=', '').Trim()
}

$resolvedVcpkgRoot = Resolve-VcpkgRoot -Preferred $VcpkgRoot -ActiveTriplet $Triplet
$bootstrapScript = Join-Path $resolvedVcpkgRoot "bootstrap-vcpkg.bat"
$vcpkgExe = Join-Path $resolvedVcpkgRoot "vcpkg.exe"

Prepare-MingwEnvironment -ActiveTriplet $Triplet

if (-not (Test-Path -LiteralPath $vcpkgExe)) {
    & $bootstrapScript
}

& $vcpkgExe install --x-manifest-root="$projectDir" --triplet "$Triplet"

$generator = Select-CMakeGenerator -Preferred $Generator
$cachedGenerator = Get-CachedGenerator -BuildDirectory $buildDir

if ($cachedGenerator -and $cachedGenerator -ne $generator) {
    Remove-Item -LiteralPath $buildDir -Recurse -Force
}

$toolchainFile = Join-Path $resolvedVcpkgRoot "scripts\buildsystems\vcpkg.cmake"
$compiler = if ($Triplet -match "mingw") { "x86_64-w64-mingw32-g++" } else { "g++" }

cmake -S "$projectDir" -B "$buildDir" -G "$generator" `
    -DCMAKE_BUILD_TYPE="$Configuration" `
    -DCMAKE_CXX_COMPILER="$compiler" `
    -DCMAKE_TOOLCHAIN_FILE="$toolchainFile" `
    -DVCPKG_TARGET_TRIPLET="$Triplet"

cmake --build "$buildDir" --config "$Configuration"

if ($Run) {
    $exe = Join-Path $buildDir "cpp-aggregator.exe"
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "Built executable was not found at: $exe"
    }

    & $exe
}
