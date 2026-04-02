param(
    [int]$Threshold = 80
)

Write-Host "Restoring tools..."
dotnet tool restore

Write-Host "Restoring and building solution..."
dotnet restore
dotnet build --configuration Release

Write-Host "Running tests (coverage will be collected via Directory.Build.props)..."
dotnet test --configuration Release

Write-Host "Collecting coverage files..."
$coverageFiles = Get-ChildItem -Path . -Filter coverage.cobertura.xml -Recurse -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName

if (-not $coverageFiles -or $coverageFiles.Count -eq 0) {
    Write-Error "No coverage files found (looked for 'coverage.cobertura.xml'). Ensure test projects include coverlet.msbuild and ran with CollectCoverage=true."
    exit 2
}

$reportsArg = $coverageFiles -join ";"
Write-Host "Found coverage files:`n$reportsArg"

Write-Host "Running reportgenerator to merge and compute summary..."
reportgenerator -reports:$reportsArg -reporttypes:TextSummary -targetdir:coverage-report 2>&1 | ForEach-Object { Write-Host $_ }

# Try to read the generated summary file
$summaryPath = Join-Path -Path (Resolve-Path ./coverage-report).Path -ChildPath "Summary.txt"
$overallPct = $null
if (Test-Path $summaryPath) {
    $summary = Get-Content $summaryPath -Raw
    $m = [regex]::Match($summary, "Overall.*?(\d{1,3}(?:\.\d+)?)%")
    if ($m.Success) { $overallPct = [double]$m.Groups[1].Value }
}

if (-not $overallPct) {
    # Fallback: try to parse console output from reportgenerator if Summary.txt missing
    $rgConsole = & reportgenerator -reports:$reportsArg -reporttypes:TextSummary -targetdir:coverage-report 2>&1
    $m = [regex]::Match($rgConsole -join "`n", "Overall.*?(\d{1,3}(?:\.\d+)?)%")
    if ($m.Success) { $overallPct = [double]$m.Groups[1].Value }
}

if (-not $overallPct) {
    Write-Error "Could not determine overall coverage percentage from reportgenerator output."
    exit 3
}

Write-Host "Overall line coverage: $overallPct% (threshold: $Threshold%)"
if ($overallPct -lt $Threshold) {
    Write-Error "Coverage threshold not met. Failing (overall $overallPct% < $Threshold%)."
    exit 1
}

Write-Host "Coverage threshold met: $overallPct% >= $Threshold%"
exit 0
