# Architecture Overview

This repository provides backend libraries and integration pieces to connect Semantic Kernel with a [Godot MCP Server](https://github.com/Ozymandros/Godot-MCP-Server) via the [GodotMcp Semantic Kernel plugin](https://github.com/Ozymandros/GodotMcpPlugin). The design is modular so the AI orchestration layer can be reused across hosts.

Primary host (supported)

- **Blazor WebAssembly** client + **ASP.NET Core** server with a JSON **BFF** under `/api/*`.
- Optional **Electron** shell uses typed **desktop IPC** to the same in-process `IGodotGeneratorApiService` (no separate HTTP hop in the default desktop path).

Legacy Avalonia shell

- An older Avalonia UI may still appear in the tree; it is **deprecated** in favor of Blazor + Electron.

In-process interaction model

- Generation flows: **UI (Blazor/Electron)** → **API facade** (`GodotGenerator.Api`) → **Application use cases** → **Infrastructure** (`GodotGenerator.Infrastructure.Ai`, persistence).
- Startup remains permissive (no mandatory API keys), while generation is strict (missing provider key fails with an actionable message).

Supported generation modalities include text, code, image, audio, video, sprites, Godot UI, Godot physics, scenes, Godot project bootstrapping, animations, and Godot MCP–oriented flows: **godot-lighting**, **godot-camera**, **godot-shaders**, **godot-signals**, **godot-nodes**.

Core components

- `GodotGenerator.Application` - DTOs, orchestration policies (`EffectiveSelectionPolicy`, `ModalityTurnComposer`), and application abstractions.
- `GodotGenerator.Infrastructure.Ai` - Semantic Kernel adapters, `GodotKernelFactory`, `AiOrchestrationService`, and Godot MCP plugin registration.
- `GodotGenerator.Infrastructure.Persistence` - Lightweight persistence helpers (preferences, SQLite) used by the host.
- `GodotGenerator.Blazor` - BFF controller, WASM client, Electron IPC handlers.

Kernel integration

- `IKernelFactory` builds cached `Kernel` instances with OpenAI chat completion and tools from `RegisterGodotTools`.
- **Per-modality tool filtering:** when `Orchestration:EnableModalityToolFiltering` is `true` (default), `ModalityMcpToolPolicy` + `ModalityGodotToolFilter` rebuild registered `KernelPlugin` instances so only functions relevant to the current modality (e.g. lighting → `light` patterns) remain. The **tool catalog** path (`GetAllConfig` / `IGodotMcpToolCatalog`) requests a kernel with **no** modality key so the full advertised MCP surface is listed.
- Policy tokens are substring-based to tolerate `godot-mcp` version drift; disable filtering if a server build uses unexpected tool names.

Godot MCP

- The host bridges `GodotMcp.SemanticKernel.Plugin` and the local `godot-mcp` process (stdio MCP). Set **`GODOT_PATH`** for Godot binary resolution as required by the upstream server.

Testing

- Tests are isolated and use mocks for kernel and MCP-adjacent services. Coverage is enforced via `coverlet.msbuild` and centralized settings in `Directory.Build.props`.

CI

- The repo includes a GitHub Actions workflow to enforce global coverage and run tests on PRs and main branch pushes.

Notes for maintainers

- Keep `GodotKernelFactory` small and testable; register MCP tools in one place.
- When adding providers, modalities, or panel overrides, add unit tests and update docs.
- Keep modality/provider/model precedence logic centralized in `EffectiveSelectionPolicy` to avoid drift across UI, API, and infrastructure.
