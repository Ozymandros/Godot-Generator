# CI / Coverage

This repository enforces a global coverage gate (80%) for tests.

How it works

- Each test project must include `coverlet.msbuild` as a package reference.
- `Directory.Build.props` configures coverage collection and output format (cobertura).
- `tools/check-coverage.ps1` runs tests, finds coverage files, merges them using `reportgenerator` and fails if the global threshold is not met.

Local validation

1. `dotnet tool restore`
2. `pwsh tools/check-coverage.ps1 -Threshold 80`

GitHub Actions

- The workflow `.github/workflows/coverage.yml` runs this script on pushes and PRs and will cause the job to fail if coverage drops below 80%.

***
