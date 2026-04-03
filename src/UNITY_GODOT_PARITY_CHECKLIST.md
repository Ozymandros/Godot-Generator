# Unity Generator vs Godot Generator — parity checklist (mutatis mutandis)

Living tracker: Unity reference is [Ozymandros/Unity-Generator](https://github.com/Ozymandros/Unity-Generator); Godot client is this repo + [Godot MCP SK plugin](https://github.com/Ozymandros/Godot-MCP-SK-Plugin) (local path). Status: **implemented** | **partial** | **missing**.

| Unity / reference area | Godot target | Status | Notes |
|------------------------|--------------|--------|--------|
| Multi-modality generation (text/code/image/audio/video/sprites + engine-specific) | Same modalities; Godot UI / Godot Physics replace Unity UI / Physics | partial | API facade routes all modalities; orchestration gains modality-aware prompts + options |
| `GET /api/management/all` discovery | `GetAllConfigAsync` + preferences | implemented | Returns `providers` (registry), `models` (by provider), `prompts` (modality map), `defaultLlmProvider` / `defaultChatModelId`, plus scalar `preferences` |
| Provider/model CRUD | JSON registries `providers.registry.v1`, `models.registry.v1` | implemented | Avalonia list/detail + models table; `SetPreference` persists versioned JSON |
| API keys management | `GetApiKeys` / `SaveApiKeys` | implemented | JSON store |
| Preferences (locale, models) | `GetPreference` / `SetPreference` | implemented | Canonical key `preferred_language` in `PreferenceKeys`; legacy `preferred_locale` merged into snapshot when canonical is unset |
| Generation options in body | `GenerateRequest.Options` → orchestration | implemented | e.g. `preferred_language`, optional `godot_project_path` |
| Unity project finalize + ZIP job | Godot export/package workflow | missing | Future: MCP-driven packaging |
| MCP tool exploitation | SK + `RegisterGodotTools` | partial | Auto tool calls; optional `validate_godot_project` pre-phase; tool list diagnostics |
| Electron / web frontend | Avalonia desktop (primary) | in progress | Blazor WASM deprecated for production |

## Client shells

| Shell | Role | Status |
|-------|------|--------|
| `Godot-Generator-Avalonia` | Desktop: real persistence + AI + MCP | **primary** |
| `Godot-Generator-Blazor` | WASM prototype; browser stubs | deprecated |

## Detailed feature ledger (execution baseline)

| FeatureId | Area | Unity intent (mutatis mutandis) | Current state | Target state |
|-----------|------|----------------------------------|---------------|--------------|
| F-ROUTE-001 | Navigation | Deterministic navigation to all generation panels + settings | implemented | keep + persist selected route |
| F-CFG-001 | Config shell | Non-blocking settings load with section isolation | partial | per-section error isolation + shell state machine |
| F-GEN-001 | General tab | Backend URL, output path, language, per-modality default provider/model | implemented | Uses registry-backed combo options + editable values |
| F-PRV-001 | Providers tab | Engine registry add/edit/commit/deregister | implemented | `ConfigurationRegistryService` validation + JSON persistence |
| F-MOD-001 | Models tab | Per-provider model rows add/remove | implemented | Filter by provider; persists `models.registry.v1` |
| F-PRM-001 | Prompts tab | System prompts by modality + reset to defaults | implemented | `prompts.system.v1` + legacy merge; merged into generation system prompt in API |
| F-SEC-001 | Secrets tab | Store modal + row replace + remove | implemented | `SaveApiKeys` null removes; masked entry |
| F-PNL-001 | Generation panels | Prompt + per-panel language + generate response | implemented | add effective settings summary + cancel/progress UX |
| F-PNL-002 | Modality sub-controls | Modality-specific advanced controls | missing | implement by modality family |
| F-API-001 | Endpoint parity | FastAPI-equivalent routes in DLL service | implemented | add endpoint-level contract hardening tests |
| F-POL-001 | Policy | Single source for modality/provider/model/language precedence | partial | centralize through `EffectiveSelectionPolicy` |
| F-ERR-001 | Error UX | Actionable, safe errors without secret leakage | partial | typed error taxonomy + consistent mapping |
| F-A11Y-001 | Accessibility | keyboard flow + labeled controls + visible focus | missing | full pass across shell/config/generation views |
| F-TST-001 | Verification | integration + smoke + a11y evidence | partial | feature-evidence matrix with passing gates |

Last updated: exhaustive parity implementation pass (in-process only).
