# Unity Generator vs Godot Generator — parity checklist (mutatis mutandis)

Living tracker: Unity reference is [Ozymandros/Unity-Generator](https://github.com/Ozymandros/Unity-Generator); Godot client is this repo + [Godot MCP SK plugin](https://github.com/Ozymandros/Godot-MCP-SK-Plugin) (local path). Status: **implemented** | **partial** | **missing**.

| Unity / reference area | Godot target | Status | Notes |
|------------------------|--------------|--------|--------|
| Multi-modality generation (text/code/image/audio/video/sprites + engine-specific) | Same modalities; Godot UI / Godot Physics replace Unity UI / Physics | partial | API facade routes all modalities; orchestration gains modality-aware prompts + options |
| `GET /api/management/all` discovery | `GetAllConfigAsync` + preferences | partial | Snapshot includes LLM provider/model from config; rich registry TBD |
| Provider/model CRUD | Preferences + config | partial | Single OpenAI-compatible stack today; extend as multi-provider |
| API keys management | `GetApiKeys` / `SaveApiKeys` | implemented | JSON store |
| Preferences (locale, models) | `GetPreference` / `SetPreference` | implemented | Includes preferred language |
| Generation options in body | `GenerateRequest.Options` → orchestration | implemented | e.g. `preferred_language`, optional `godot_project_path` |
| Unity project finalize + ZIP job | Godot export/package workflow | missing | Future: MCP-driven packaging |
| MCP tool exploitation | SK + `RegisterGodotTools` | partial | Auto tool calls; optional `validate_godot_project` pre-phase; tool list diagnostics |
| Electron / web frontend | Avalonia desktop (primary) | in progress | Blazor WASM deprecated for production |

## Client shells

| Shell | Role | Status |
|-------|------|--------|
| `Godot-Generator-Avalonia` | Desktop: real persistence + AI + MCP | **primary** |
| `Godot-Generator-Blazor` | WASM prototype; browser stubs | deprecated |

Last updated: implementation pass (Avalonia UI + backend parity).
