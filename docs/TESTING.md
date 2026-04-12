# Testing Guide

This guide explains how tests are structured and how to run them locally.

Test frameworks

- Unit tests: xUnit + Moq
- Coverage: coverlet.msbuild
- UI tests live in `test/GodotGenerator.Ui.Tests` and cover the Avalonia view models and client adapters.

Run tests

- `dotnet test` to run tests for the entire solution
- `dotnet test <path-to-test-project>` to run a single project

Coverage

- Each test project must reference `coverlet.msbuild` and either set `CollectCoverage=true` or rely on `Directory.Build.props`.
- Coverage reports are written to the test project's `TestResults\coverage.cobertura.xml` file.
- Use `pwsh tools/check-coverage.ps1 -Threshold 80` to merge reports and verify global coverage.

Writing tests

- Keep tests focused and deterministic. When a test touches the Kernel or MCP client, use `IKernelFactory` mocks or provide a test Kernel implementation.
- Avoid touching the filesystem or network in unit tests. Use mocks and test doubles. If integration tests require I/O, isolate them in a separate test project and mark them with a trait.
- Add test coverage for new generation modalities, especially routing, prompt defaults, and view-model title/label mapping.
- Add tests for override propagation when the UI introduces new fields such as temperature, API key, or system prompt.

CI

- CI runs the `tools/check-coverage.ps1` script to validate coverage. The script uses `reportgenerator` to merge cobertura reports and extract a global percentage.

***
