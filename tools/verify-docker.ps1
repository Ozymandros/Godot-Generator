<#
.SYNOPSIS
    Local/manual: runs tools/verify.ps1 (full suite, including E2E) inside the Docker image used by .devcontainer.

.EXAMPLE
    ./tools/verify-docker.ps1
    ./tools/verify-docker.ps1 -NoBuild
#>
param(
    [switch] $NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$composeFile = Join-Path $repoRoot "docker-compose.verify.yml"
if (-not (Test-Path $composeFile)) {
    throw "Missing $composeFile"
}

$dockerCliArgs = @("compose", "-f", $composeFile, "run", "--rm")
if (-not $NoBuild) {
    $dockerCliArgs += "--build"
}
$dockerCliArgs += "verify"

Write-Host "docker $($dockerCliArgs -join ' ')" -ForegroundColor Cyan
& docker @dockerCliArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
