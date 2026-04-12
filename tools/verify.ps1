<#
.SYNOPSIS
    Local development verification: restore, format check, build (analyzers + warnings as errors), tests, coverage gate, E2E.

    Defaults with no arguments: Step All (every check) and Configuration Release (full pipeline at Release).

    Run on the host (PowerShell) when your machine has .NET 10, Node/npm, and Playwright deps installed.
    For a reproducible full suite in Docker (same image as .devcontainer; bind-mount repo at /workspace), use:

      ./tools/verify-docker.ps1
      # or: docker compose -f docker-compose.verify.yml run --build --rm verify

    That runs this script inside the container; E2E starts Kestrel + Chromium in that same environment (localhost:5010).

    CI does not invoke this script; use `.github/workflows/ci.yml` for automated checks.

    On Windows, Blazor WebAssembly can hit tmp-webcil file locks when MSBuild runs many nodes in parallel
    or when another process (IDE, dotnet watch) holds obj output. This script builds with -m:1 to reduce
    races; if errors persist, close processes using the repo and retry.

.PARAMETER Step
    One or more of: All, Restore, Format, Build, Test, Coverage, E2E. Default: All (every check).

.PARAMETER Configuration
    MSBuild configuration for build, test, coverage, and E2E (DOTNET_CONFIGURATION). Default: Release.

.EXAMPLE
    ./tools/verify.ps1
    ./tools/verify.ps1 -Step Format, Build
    ./tools/verify.ps1 -Step Coverage -CoverageThreshold 80
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0, HelpMessage = "Pipeline steps. Omit or use All for the full suite.")]
    [ValidateSet("All", "Restore", "Format", "Build", "Test", "Coverage", "E2E")]
    [string[]] $Step = @("All"),

    [int] $CoverageThreshold = 80,

    [Parameter(HelpMessage = "MSBuild configuration (default Release).")]
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$sln = Join-Path $repoRoot "Godot-Generator-Avalonia.sln"
if (-not (Test-Path $sln)) {
    throw "Solution not found: $sln"
}

function Test-Step {
    param([string] $Name)
    return ($Step -contains "All") -or ($Step -contains $Name)
}

function Invoke-Step {
    param(
        [string] $Title,
        [scriptblock] $Action
    )
    Write-Host ""
    Write-Host "=== $Title ===" -ForegroundColor Cyan
    & $Action
    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "Step failed: $Title (exit $LASTEXITCODE)"
    }
}

function Assert-NativeExitCode {
    param([string] $Context)
    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "${Context} failed (exit $LASTEXITCODE)"
    }
}

$needsDotnet = (Test-Step "Restore") -or (Test-Step "Format") -or (Test-Step "Build") -or (Test-Step "Test") -or (Test-Step "Coverage")
$didRestore = $false
$didBuild = $false

if ($needsDotnet) {
    Invoke-Step "dotnet tool restore" { dotnet tool restore }
    Invoke-Step "dotnet restore" {
        dotnet restore $sln
        $script:didRestore = $true
    }
}

if (Test-Step "Format") {
    if (-not $didRestore) {
        Invoke-Step "dotnet tool restore" { dotnet tool restore }
        Invoke-Step "dotnet restore" { dotnet restore $sln; $script:didRestore = $true }
    }
    Invoke-Step "dotnet format (verify no changes)" {
        dotnet format $sln --verify-no-changes --no-restore
    }
}

if (Test-Step "Build") {
    if (-not $didRestore) {
        Invoke-Step "dotnet tool restore" { dotnet tool restore }
        Invoke-Step "dotnet restore" { dotnet restore $sln; $script:didRestore = $true }
    }
    Invoke-Step "dotnet build (analyzers, warnings as errors)" {
        # -m:1: single MSBuild node avoids WebAssembly ConvertDllsToWebCil / tmp-webcil races on Windows.
        $buildArgs = @(
            "build", $sln,
            "-c", $Configuration,
            "--no-restore",
            "--nologo",
            "-m:1",
            "-p:RunAnalyzers=true",
            "-p:EnforceCodeStyleInBuild=true",
            "-p:TreatWarningsAsErrors=true",
            "-warnaserror"
        )
        $prevNodeReuse = $env:MSBUILDDISABLENODEREUSE
        $env:MSBUILDDISABLENODEREUSE = "1"
        try {
            dotnet @buildArgs
        }
        finally {
            if ($null -eq $prevNodeReuse) {
                Remove-Item "Env:MSBUILDDISABLENODEREUSE" -ErrorAction SilentlyContinue
            }
            else {
                $env:MSBUILDDISABLENODEREUSE = $prevNodeReuse
            }
        }
    }
    $didBuild = $true
}

if (Test-Step "Test") {
    if (-not $didRestore) {
        Invoke-Step "dotnet tool restore" { dotnet tool restore }
        Invoke-Step "dotnet restore" { dotnet restore $sln; $script:didRestore = $true }
    }
    if ($didBuild) {
        Invoke-Step "dotnet test" {
            dotnet test $sln -c $Configuration --no-build --no-restore --nologo
        }
    }
    else {
        Invoke-Step "dotnet test" {
            dotnet test $sln -c $Configuration --nologo
        }
    }
}

if (Test-Step "Coverage") {
    $covScript = Join-Path $repoRoot "tools/check-coverage.ps1"
    if (-not (Test-Path $covScript)) {
        throw "Missing $covScript"
    }
    Invoke-Step "coverage gate ($CoverageThreshold% Application + Persistence)" {
        $shell = (Get-Process -Id $PID -ErrorAction SilentlyContinue).Path
        if ([string]::IsNullOrWhiteSpace($shell)) {
            $pwshCmd = Get-Command pwsh -ErrorAction SilentlyContinue
            if ($null -ne $pwshCmd) {
                $shell = $pwshCmd.Source
            }
        }
        if ([string]::IsNullOrWhiteSpace($shell)) {
            throw "Could not resolve PowerShell host to run tools/check-coverage.ps1 (re-run from pwsh or set PATH)."
        }
        & $shell -NoProfile -File $covScript -Threshold $CoverageThreshold -Configuration $Configuration
    }
}

if (Test-Step "E2E") {
    $e2eDir = Join-Path $repoRoot "test/GodotGenerator.Blazor.E2E"
    if (-not (Test-Path $e2eDir)) {
        throw "E2E folder not found: $e2eDir"
    }
    Invoke-Step "Playwright E2E" {
        $prevDotnetCfg = $env:DOTNET_CONFIGURATION
        $env:DOTNET_CONFIGURATION = $Configuration
        try {
            Push-Location $e2eDir
            try {
                if (Test-Path "package-lock.json") {
                    npm ci
                }
                else {
                    npm install
                }
                Assert-NativeExitCode "npm install"
                npx playwright install chromium --with-deps
                Assert-NativeExitCode "playwright install chromium"
                $blazorHostProj = Join-Path $repoRoot "GodotGenerator.Blazor/GodotGenerator.Blazor/GodotGenerator.Blazor.csproj"
                # If the Build step already ran, the solution (including this host) is compiled—do not build again.
                # A second project-only build with EnforceCodeStyleInBuild duplicates analyzer work and can surface
                # extra/noisy diagnostics in the IDE and on the command line.
                if (-not $didBuild) {
                    Invoke-Step "dotnet build (Blazor host for E2E)" {
                        $buildArgs = @(
                            "build", $blazorHostProj,
                            "-c", $Configuration,
                            "--nologo",
                            "-m:1"
                        )
                        if ($didRestore) {
                            $buildArgs += "--no-restore"
                        }
                        $prevNodeReuse = $env:MSBUILDDISABLENODEREUSE
                        $env:MSBUILDDISABLENODEREUSE = "1"
                        try {
                            dotnet @buildArgs
                        }
                        finally {
                            if ($null -eq $prevNodeReuse) {
                                Remove-Item "Env:MSBUILDDISABLENODEREUSE" -ErrorAction SilentlyContinue
                            }
                            else {
                                $env:MSBUILDDISABLENODEREUSE = $prevNodeReuse
                            }
                        }
                    }
                }
                $prevWebserverPrebuilt = $env:E2E_WEBSERVER_PREBUILT
                $env:E2E_WEBSERVER_PREBUILT = "1"
                try {
                    $env:CI = "true"
                    npx playwright test
                }
                finally {
                    if ($null -eq $prevWebserverPrebuilt) {
                        Remove-Item "Env:E2E_WEBSERVER_PREBUILT" -ErrorAction SilentlyContinue
                    }
                    else {
                        $env:E2E_WEBSERVER_PREBUILT = $prevWebserverPrebuilt
                    }
                }
            }
            finally {
                Pop-Location
            }
        }
        finally {
            if ($null -eq $prevDotnetCfg) {
                Remove-Item "Env:DOTNET_CONFIGURATION" -ErrorAction SilentlyContinue
            }
            else {
                $env:DOTNET_CONFIGURATION = $prevDotnetCfg
            }
        }
    }
}

Write-Host ""
Write-Host "Verify completed successfully." -ForegroundColor Green
