# FastAPI to DLL Route Parity Matrix

This matrix maps Unity FastAPI router behaviors to transport-agnostic DLL service methods.

## Generation Router (`/generate/*`)

- `POST /generate/text` -> `IGodotGeneratorApiService.GenerateTextAsync(...)`
- `POST /generate/code` -> `IGodotGeneratorApiService.GenerateCodeAsync(...)`
- `POST /generate/image` -> `IGodotGeneratorApiService.GenerateImageAsync(...)`
- `POST /generate/audio` -> `IGodotGeneratorApiService.GenerateAudioAsync(...)`
- `POST /generate/video` -> `IGodotGeneratorApiService.GenerateVideoAsync(...)`
- `POST /generate/sprites` -> `IGodotGeneratorApiService.GenerateSpritesAsync(...)`
- `POST /generate/unity-ui` -> `IGodotGeneratorApiService.GenerateGodotUiAsync(...)`
- `POST /generate/unity-physics` -> `IGodotGeneratorApiService.GenerateGodotPhysicsAsync(...)`

Semantics preserved in DLL:
- prompt validation (`Prompt` must be non-empty),
- preferred provider fallback from preferences,
- preferred model fallback from preferences,
- normalized success/error envelope payload,
- modality-specific operation names.

Route hardening checklist (required for parity completion):
- request override > preference > host default precedence is deterministic,
- response payload includes resolved provider and model metadata,
- actionable missing-key and unsupported-provider/model errors are safe for UI display.

## Preferences Router (`/prefs`)

- `GET /prefs/{key}` -> `IGodotGeneratorApiService.GetPreferenceAsync(...)`
- `POST /prefs` -> `IGodotGeneratorApiService.SetPreferenceAsync(...)`

Semantics preserved in DLL:
- key/value operations via application use cases,
- normalized success/error envelope payload.

## Config Router (`/config/keys`)

- `GET /config/keys` -> `IGodotGeneratorApiService.GetApiKeysAsync(...)`
- `POST /config/keys` -> `IGodotGeneratorApiService.SaveApiKeysAsync(...)`

Semantics preserved in DLL:
- batch key save behavior,
- key dictionary return model.

## Management Router (`/api/management/all`)

- `GET /api/management/all` -> `IGodotGeneratorApiService.GetAllConfigAsync(...)`

Semantics preserved in DLL:
- unified discovery-style response from application state:
  - preferences,
  - API key status,
  - lightweight capability metadata.

Contract completeness checklist:
- includes prompts section semantics (read-only or CRUD depending on phase),
- includes provider/model summaries aligned to runtime policy,
- never exposes secret values (names/index only).

