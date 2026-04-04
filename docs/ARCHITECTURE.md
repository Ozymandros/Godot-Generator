# Architecture Overview

This repository provides backend libraries and integration pieces to connect Semantic Kernel with a Godot MCP server plugin. The design is intentionally modular so the AI orchestration layer can be reused across different frontends. The current solution also includes an Avalonia desktop shell that consumes the same application contracts in-process.

In-process interaction model (non-negotiable for parity pass)

- Single process runtime:
  - Avalonia Presentation -> API facade -> Application use cases/policies -> Infrastructure services.
- Explicitly excluded:
  - IPC
  - server host
  - network transport between these layers.
- Startup remains permissive (no mandatory API keys), while generation is strict (missing provider key fails with actionable message).

Supported generation modalities include text, code, image, audio, video, sprites, Godot UI, Godot physics, scenes, Godot project bootstrapping, and animations.

Core components

- `GodotGenerator.Application` - DTOs and application contract abstractions.
- `GodotGenerator.Infrastructure.Ai` - Semantic Kernel adapters, KernelFactory, AI orchestration services, and Godot MCP plugin integrations.
- `GodotGenerator.Infrastructure.Persistence` - Lightweight persistence helpers (preferences, small SQLite stores) used by the UI host.
- `Godot-Generator-Avalonia` - desktop MVVM shell, converters, and settings/generation panels.

Kernel integration

- `IKernelFactory` and implementations create or configure `Kernel` instances and register connectors/adapters (OpenAI, Unity/Godot MCP plugin). This isolates provider configuration and allows tests to mock kernel creation.

Godot MCP

- The repo contains adapter code bridging `GodotMcp.SemanticKernel.Plugin` and Semantic Kernel tools. The MCP plugin exposes in-editor functions and the backend calls local MCP servers via the plugin’s client API.

Testing

- Tests are isolated and use Mock objects for kernel and MCP clients. Coverage is enforced via `coverlet.msbuild` and centralized settings in `Directory.Build.props`.

CI

- The repo includes a GitHub Actions workflow to enforce global coverage and run tests on PRs and main branch pushes.

Notes for maintainers

- Keep KernelFactory small and testable. Register plugin/tool adapters in extension methods that are easy to stub for unit tests.
- When adding new providers, modalities, or panel overrides, add unit tests and update the coverage gate if needed.
- Keep modality/provider/model precedence logic centralized in one policy helper to avoid drift across UI/API/infra.
