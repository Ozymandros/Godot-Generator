# Core Orchestration Parity Checklist

This checklist maps Unity backend core orchestration patterns to the Godot backend library implementation.

## Kernel and Plugin Composition

- [x] Kernel factory is single-responsibility and cached: `GodotKernelFactory`.
- [x] Godot plugin registration is centralized through DI: `AddGodotMcp(configuration)`.
- [x] Plugin lifecycle is explicit: plugin initialize before tool registration.
- [x] Initialization failures are surfaced with phase-specific diagnostics.
- [x] Failed initialization does not keep a half-ready kernel instance.

## Tool Invocation and Turn Execution

- [x] Tool calls are enabled via Semantic Kernel auto invocation.
- [x] Turn execution is isolated in `AiOrchestrationService`.
- [x] Empty/multipart completion output is normalized for callers.
- [x] User-facing errors are sanitized while detailed failures are logged.
- [x] Tool invocation behavior can be configured without changing call sites.

## Configuration and Safety

- [x] LLM settings are strongly typed (`LlmOptions`).
- [x] LLM settings enforce required fields and fail fast at startup.
- [x] Data store settings are validated and fail fast at startup.
- [x] Model selection behavior is explicit when a preferred model is provided.

## Preference Persistence

- [x] Repository is thread-safe for concurrent calls.
- [x] Writes are atomic and avoid partial-file corruption.
- [x] Null preference value removes the key.
- [x] Corrupted JSON falls back safely to an empty document.

## Test Coverage Focus

- [x] Kernel/orchestration failure behavior has unit coverage.
- [x] Prompt validation and orchestration delegation have unit coverage.
- [x] Persistence roundtrip and edge-case behavior have unit coverage.
- [x] Options validation has direct unit coverage.
