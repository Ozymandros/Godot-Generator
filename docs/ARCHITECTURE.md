# Architecture Overview

This repository provides backend libraries and integration pieces to connect Semantic Kernel with a Godot MCP server plugin. The design is intentionally modular so the AI orchestration layer can be reused across different frontends.

Core components

- `GodotGenerator.Application` - DTOs and application contract abstractions.
- `GodotGenerator.Infrastructure.Ai` - Semantic Kernel adapters, KernelFactory, AI orchestration services, and Godot MCP plugin integrations.
- `GodotGenerator.Infrastructure.Persistence` - Lightweight persistence helpers (preferences, small SQLite stores) used by the UI host.

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
- When adding new providers or MCP features, add unit tests and update the coverage gate if needed.
