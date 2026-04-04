# Godot Generator (Avalonia)

![Coverage](https://github.com/Ozymandros/Godot-Generator-Avalonia/actions/workflows/coverage.yml/badge.svg)
![Build](https://github.com/Ozymandros/Godot-Generator-Avalonia/actions/workflows/coverage.yml/badge.svg)

Godot Generator (Avalonia) is a desktop host and supporting backend libraries that connect Microsoft Semantic Kernel with a Godot MCP plugin. The codebase provides adapters, orchestration services, and a material-style Avalonia UI to enable AI-driven generation of Godot-ready code, scenes, UI, physics, sprites, and project scaffolding.

Goals of this repository
- Provide small, well-tested adapters and services that integrate Semantic Kernel and the `GodotMcp.SemanticKernel.Plugin` MCP server
- Keep Kernel creation and provider wiring isolated and mockable for unit tests
- Enforce a minimum global test coverage to maintain reliability
- Mirror the reference app’s navigation, settings panels, and generation panel affordances where practical

Key information
- Target framework: .NET 10
- Languages: C# (primary)
- Test framework: xUnit + Moq
- Coverage: coverlet.msbuild + reportgenerator (global gate 80%)
- Frontend: Avalonia desktop shell with material icon support and per-panel generation overrides
- Desktop shell: classic top MenuBar plus left navigation for quick access to settings and generation panels

Where to start
- Architecture overview: `docs/ARCHITECTURE.md`
- Development guide: `docs/DEVELOPMENT.md`
- Testing guide: `docs/TESTING.md`
- CI & coverage: `docs/CI.md`

Supported generation areas
- Text and code generation
- Image, audio, video, and sprites generation
- Godot UI and Godot physics prompts
- Scenes and Godot project bootstrapping
- Animations prompts for AnimationPlayer / AnimationTree workflows

Quickstart (local development)
1. Prerequisites
   - .NET 10 SDK installed
   - PowerShell (pwsh) or Windows PowerShell
2. Restore & build
   - `dotnet restore`
   - `dotnet build --configuration Release`
3. Run tests
   - `dotnet test`
4. Run global coverage check (enforced >= 80%)
   - `dotnet tool restore`
   - `pwsh tools/check-coverage.ps1 -Threshold 80`

Coverage enforcement
- `Directory.Build.props` configures collection and a per-project threshold. The repository includes `tools/check-coverage.ps1` which merges coverage reports and fails when the global coverage is below the threshold.

Repository layout (high level)
- `src/` - library projects (infrastructure, adapters, application DTOs)
- `Godot-Generator-Avalonia/` - desktop shell, view models, converters, and XAML views
- `test/` - unit tests and test utilities
- `tools/` - helper scripts (coverage check, tooling)
- `.github/workflows/` - CI pipelines (coverage enforcement)
- `docs/` - documentation and architecture notes

Common commands and tips
- Restore dotnet tools: `dotnet tool restore`
- Run coverage check locally: `pwsh tools/check-coverage.ps1 -Threshold 80`
- Build solution: `dotnet build --configuration Release`
- Run a specific test project: `dotnet test test/GodotGenerator.Infrastructure.Ai.Tests/GodotGenerator.Infrastructure.Ai.Tests.csproj`
- Run Avalonia UI tests: `dotnet test test/GodotGenerator.Ui.Tests/GodotGenerator.Ui.Tests.csproj`

Contributing
- Keep PRs small and explain the reason for changes
- Add unit tests for new functionality and run the coverage check locally before opening a PR
- When adding a new generation modality or panel override, update the UI, API routing, defaults, tests, and docs together

Troubleshooting
- If a project fails to load: open the .csproj and check for invalid XML or stray console output (MSBuild errors sometimes embed text into .csproj). See `src/GodotGenerator.Infrastructure.Ai/GodotGenerator.Infrastructure.Ai.csproj` for a corrected example.
- If coverage reports are missing: ensure every test project references `coverlet.msbuild` and `CollectCoverage` is enabled.

License
- See repository top-level LICENSE (if present)

