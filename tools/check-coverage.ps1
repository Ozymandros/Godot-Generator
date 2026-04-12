param(
    [int]$Threshold = 80,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$sln = Join-Path $repoRoot "Godot-Generator-Avalonia.sln"

if (-not (Test-Path $sln)) {
    Write-Error "Solution not found: $sln"
    exit 2
}

Write-Host "Restoring tools..."
dotnet tool restore

Write-Host "Restoring and building solution..."
dotnet restore $sln
$prevNodeReuse = $env:MSBUILDDISABLENODEREUSE
$env:MSBUILDDISABLENODEREUSE = "1"
try {
    # -m:1: serializes MSBuild; reduces Blazor WASM tmp-webcil file-lock races on Windows.
    dotnet build $sln --configuration $Configuration --no-restore -m:1
}
finally {
    if ($null -eq $prevNodeReuse) {
        Remove-Item "Env:MSBUILDDISABLENODEREUSE" -ErrorAction SilentlyContinue
    }
    else {
        $env:MSBUILDDISABLENODEREUSE = $prevNodeReuse
    }
}

Write-Host "Running tests (coverlet via test/Directory.Build.props)..."
dotnet test $sln --configuration $Configuration --no-build

# Enforce line coverage on core domain + persistence (merged). Full-solution merges include Blazor UI
# and sit far below 80% without excluding large .razor / WASM surfaces; Application + Persistence
# tracks business rules and storage and currently meets the threshold when tests pass.
$coreCoverageFiles = @(
    (Join-Path $repoRoot "test/GodotGenerator.Application.Tests/TestResults/coverage.cobertura.xml"),
    (Join-Path $repoRoot "test/GodotGenerator.Infrastructure.Persistence.Tests/TestResults/coverage.cobertura.xml")
)
$missing = $coreCoverageFiles | Where-Object { -not (Test-Path $_) }
if ($missing.Count -gt 0) {
    Write-Error "Missing core coverage files (run tests first): $($missing -join ', ')"
    exit 2
}

$reportsArg = $coreCoverageFiles -join ";"
Write-Host "Core coverage gate (Application + Persistence):`n$reportsArg"

$reportDir = Join-Path $repoRoot "coverage-report"
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
& dotnet tool run reportgenerator -- "-reports:$reportsArg" "-reporttypes:TextSummary" "-targetdir:$reportDir" 2>&1 | ForEach-Object { Write-Host $_ }

$summaryPath = Join-Path $reportDir "Summary.txt"
if (-not (Test-Path $summaryPath)) {
    Write-Error "reportgenerator did not write Summary.txt to $reportDir"
    exit 3
}

$summary = Get-Content $summaryPath -Raw
$m = [regex]::Match($summary, "Line coverage:\s*(\d+(?:\.\d+)?)%")
if (-not $m.Success) {
    Write-Error "Could not parse line coverage from Summary.txt."
    exit 3
}

$overallPct = [double]$m.Groups[1].Value
Write-Host "Core line coverage (Application + Persistence merged): $overallPct% (threshold: $Threshold%)"
if ($overallPct -lt $Threshold) {
    Write-Error "Coverage threshold not met: $overallPct% < $Threshold%."
    exit 1
}

Write-Host "Coverage threshold met: $overallPct% >= $Threshold%"
exit 0
