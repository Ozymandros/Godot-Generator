# GodotGenerator.Blazor

Blazor Web App (ASP.NET Core **Server** host + **WebAssembly** interactive UI) with **Fluent UI Blazor**, a JSON **BFF** under `/api`, and an optional **Electron** shell in the repo root [`electron/`](../electron/).

## Run (web)

From the repository root:

```bash
dotnet run --project GodotGenerator.Blazor/GodotGenerator.Blazor/GodotGenerator.Blazor.csproj
```

Default dev URL (see `Properties/launchSettings.json`): `http://localhost:5044`.

Configure LLM and MCP under `GodotGenerator.Blazor/GodotGenerator.Blazor/appsettings.json` (same shape as the Avalonia host).

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
- **Components (bUnit):** `ProjectHeaderClient` (Browse / Electron messaging) with Fluent UI services registered in the test context.

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
