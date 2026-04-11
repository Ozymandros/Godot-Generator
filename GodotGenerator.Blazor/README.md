# GodotGenerator.Blazor

Blazor Web App (ASP.NET Core **Server** host + **WebAssembly** interactive UI) with **Fluent UI Blazor**, a JSON **BFF** under `/api`, and an optional **Electron** shell in the repo root [`electron/`](../electron/).

## Run (web)

From the repository root:

```bash
dotnet run --project GodotGenerator.Blazor/GodotGenerator.Blazor/GodotGenerator.Blazor.csproj
```

Default dev URL (see `Properties/launchSettings.json`): `http://localhost:5044`.

**Godot MCP in the UI:** the home page includes a **Scene & engine tools** section with cards for each godot-mcp-oriented generation route (lighting, camera, shaders, signals, nodes, plus Godot UI and physics). Use **Browse all Godot MCP tools** (`/generate/godot-mcp`) for a single hub page with short descriptions and links. The management dashboard lists the same shortcuts under **Scene & MCP tools**, and the sidebar includes **Godot MCP hub** next to the other generate entries.

Configure LLM, orchestration, and MCP under `GodotGenerator.Blazor/GodotGenerator.Blazor/appsettings.json`. Notable keys:

- `Orchestration:EnableModalityToolFiltering` — when `true`, Godot MCP Semantic Kernel functions are narrowed per generation modality (see `ModalityMcpToolPolicy` in `GodotGenerator.Infrastructure.Ai`).
- `GodotMcp` — paths/timeouts for the `godot-mcp` process; ensure **`GODOT_PATH`** is set for the upstream [Godot MCP Server](https://github.com/Ozymandros/Godot-MCP-Server).

### BFF generate routes (`POST`, JSON `GenerateRequest`)

| Route | Purpose |
|-------|---------|
| `/api/generate/text` | Text |
| `/api/generate/code` | Code |
| `/api/generate/image` | Image |
| `/api/generate/audio` | Audio |
| `/api/generate/video` | Video |
| `/api/generate/sprites` | Sprites |
| `/api/generate/godot-ui` | Godot UI |
| `/api/generate/godot-physics` | Godot physics |
| `/api/generate/godot-project` | Godot project |
| `/api/generate/scenes` | Scenes |
| `/api/generate/animations` | Animations |
| `/api/generate/godot-lighting` | Lighting (MCP `light.*` tools) |
| `/api/generate/godot-camera` | Camera (`camera.*`) |
| `/api/generate/godot-shaders` | Shaders / resources |
| `/api/generate/godot-signals` | Signals / scripting |
| `/api/generate/godot-nodes` | Scene graph nodes (`scene.*`) |

The WASM **HTTP** client (`GodotGeneratorHttpClient`) maps each `GenerationModality` to the routes above. The **Electron** shell uses IPC commands (`Generate.*`) instead; both hit the same API service.

## Tests

```bash
dotnet test test/GodotGenerator.Blazor.Tests/GodotGenerator.Blazor.Tests.csproj
```

Playwright startup smoke tests are opt-in:

- `RUN_PLAYWRIGHT_STARTUP_TEST=1`: local host startup smoke test.
- `RUN_PLAYWRIGHT_ELECTRON_STARTUP_TEST=1`: desktop smoke test (`electron` starts backend + serves UI).

Containerized Electron smoke test:

```bash
docker compose -f docker-compose.playwright-electron.yml run --rm playwright-electron-smoke
```

The first run builds a local test image (Playwright .NET + Node/npm), so it can take a while. Following runs are significantly faster due to Docker layer cache.

VS Code Dev Container (optional, same base image as Playwright smoke):

```bash
code .
# Command Palette -> "Dev Containers: Reopen in Container"
```

Inside the container, run the same smoke command:

```bash
dotnet test test/GodotGenerator.Blazor.Tests/GodotGenerator.Blazor.Tests.csproj --filter FullyQualifiedName~GodotGenerator.Blazor.Tests.StartupTests.ElectronDesktopStartsBackendAndServesUi
```

Coverage includes:

- **Unit:** BFF controller (mocked `IGodotGeneratorApiService`), `ModalityRoutes`, `LogBufferService`, `GodotGeneratorHttpClient` (stub HTTP handler).
- **HTTP integration:** `WebApplicationFactory` hosts the real Blazor server with a fake `IGodotGeneratorApiService` and calls `POST /api/generate/*` and `GET /api/config`.
- **Components (bUnit):** `ProjectHeaderClient` (Browse / Electron messaging) with Fluent UI services registered in the test context; `GodotMcpUiSurfacingTests` for Home / hub anchors and `GodotProjectValidationSwitch`.

## Electron (desktop shell)

```bash
cd electron
pnpm install
pnpm start
```

`pnpm start` now supervises the local .NET backend process and uses the typed IPC path (`GODOT_DESKTOP_IPC=1`) automatically.

Default UI URL is `http://127.0.0.1:5044`. Override with:

```bash
set GODOT_BLAZOR_URL=http://127.0.0.1:5044
pnpm start
```

## Documentation

- **XML:** `GodotGenerator.Blazor` emits `GenerateDocumentationFile` (missing members suppressed with `CS1591`). Key services and the BFF controller include XML summaries.
- **Tests:** See `test/GodotGenerator.Blazor.Tests/` for examples of mocking `IGodotGeneratorApiService` and stubbing HTTP for the WASM client.
