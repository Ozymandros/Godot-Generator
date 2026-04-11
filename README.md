# Godot Generator

![Coverage](https://github.com/Ozymandros/Godot-Generator-Avalonia/actions/workflows/coverage.yml/badge.svg)
![Build](https://github.com/Ozymandros/Godot-Generator-Avalonia/actions/workflows/coverage.yml/badge.svg)

Godot Generator connects **Microsoft Semantic Kernel** to **[Godot MCP Server](https://github.com/Ozymandros/Godot-MCP-Server)** via the **[GodotMcp.SemanticKernel.Plugin](https://github.com/Ozymandros/GodotMcpPlugin)** (`GodotMcp.SemanticKernel.Plugin` on NuGet). The supported desktop experience is the **Blazor + Electron** shell under [`GodotGenerator.Blazor/`](GodotGenerator.Blazor/); the legacy Avalonia project in this repo is deprecated.

Goals of this repository
- Provide small, well-tested adapters and services that integrate Semantic Kernel and the Godot MCP global tool (`godot-mcp` on PATH or `GODOT_MCP_PATH`)
- Keep kernel creation and provider wiring isolated and mockable for unit tests
- Enforce a minimum global test coverage to maintain reliability
- Align generation modalities with Godot MCP tool families (lighting, camera, scene graph, resources, UI, physics, etc.)

**MCP prerequisites (local automation)**  
Install the [.NET global tool](https://github.com/Ozymandros/Godot-MCP-Server) `godot-mcp` and set **`GODOT_PATH`** (or ensure `godot` is on PATH) so the server can resolve the Godot 4.x binary.

Key information
- Target framework: .NET 10
- Languages: C# (primary)
- Test framework: xUnit + Moq
- Coverage: coverlet.msbuild + reportgenerator (global gate 80%)
- **Primary UI:** Blazor WebAssembly + ASP.NET Core host; **Electron** optional (`electron/`)

Where to start
- Blazor app: [`GodotGenerator.Blazor/README.md`](GodotGenerator.Blazor/README.md)
- Architecture overview: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
- Development guide: [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md)
- Testing guide: [`docs/TESTING.md`](docs/TESTING.md)
- CI & coverage: [`docs/CI.md`](docs/CI.md)

Supported generation areas
- Text, code, image, audio, video, sprites
- Godot UI, Godot physics, scenes, Godot project, animations
- **Godot MCP–oriented:** lighting, camera, shaders, signals, nodes (narrowed MCP tool surface per modality when `Orchestration:EnableModalityToolFiltering` is true)

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
- `GodotGenerator.Blazor/` - Blazor server + WASM client, BFF, Electron-oriented IPC
- `test/` - unit tests and test utilities
- `tools/` - helper scripts (coverage check, tooling)
- `.github/workflows/` - CI pipelines (coverage enforcement)
- `docs/` - documentation and architecture notes

Common commands and tips
- Restore dotnet tools: `dotnet tool restore`
- Run coverage check locally: `pwsh tools/check-coverage.ps1 -Threshold 80`
- Build solution: `dotnet build --configuration Release`
- Run a specific test project: `dotnet test test/GodotGenerator.Infrastructure.Ai.Tests/GodotGenerator.Infrastructure.Ai.Tests.csproj`

Contributing
- Keep PRs small and explain the reason for changes
- Add unit tests for new functionality and run the coverage check locally before opening a PR
- When adding a new generation modality or panel override, update the UI, API routing, defaults, tests, and docs together

Troubleshooting
- If a project fails to load: open the .csproj and check for invalid XML or stray console output (MSBuild errors sometimes embed text into .csproj). See `src/GodotGenerator.Infrastructure.Ai/GodotGenerator.Infrastructure.Ai.csproj` for a corrected example.
- If coverage reports are missing: ensure every test project references `coverlet.msbuild` and `CollectCoverage` is enabled.

License
- See repository top-level LICENSE (if present)
