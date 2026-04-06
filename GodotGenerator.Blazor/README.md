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

Coverage includes:

- **Unit:** BFF controller (mocked `IGodotGeneratorApiService`), `ModalityRoutes`, `LogBufferService`, `GodotGeneratorHttpClient` (stub HTTP handler).
- **HTTP integration:** `WebApplicationFactory` hosts the real Blazor server with a fake `IGodotGeneratorApiService` and calls `POST /api/generate/*` and `GET /api/config`.
- **Components (bUnit):** `ProjectHeaderClient` (Browse / Electron messaging) with Fluent UI services registered in the test context.

## Electron (desktop shell)

```bash
cd electron
npm install
# Terminal 1: dotnet run --project ../GodotGenerator.Blazor/GodotGenerator.Blazor/GodotGenerator.Blazor.csproj
# Terminal 2:
npm start
```

Override URL: `set GODOT_BLAZOR_URL=http://localhost:5044` (Windows) before `npm start` if needed.

## Documentation

- **XML:** `GodotGenerator.Blazor` emits `GenerateDocumentationFile` (missing members suppressed with `CS1591`). Key services and the BFF controller include XML summaries.
- **Tests:** See `test/GodotGenerator.Blazor.Tests/` for examples of mocking `IGodotGeneratorApiService` and stubbing HTTP for the WASM client.
