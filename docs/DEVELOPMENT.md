# Development Guide

This document explains how to work with the codebase, how to run locally, and recommended patterns.

Local setup

1. Clone repository and open in Visual Studio 2026
2. Restore packages and dotnet tools:
   - `dotnet restore`
   - `dotnet tool restore`
3. Build the solution: `dotnet build --configuration Release`

Project structure

- `GodotGenerator.Application` - DTOs and domain-agnostic interfaces used by the infrastructure layer.
- `GodotGenerator.Infrastructure.Ai` - kernel adapters, `KernelFactory`, orchestration services, and DI extension methods.
- `GodotGenerator.Infrastructure.Persistence` - persistence helpers for preferences and small storage.
- `Godot-Generator-Avalonia` - primary desktop shell (MVVM) and in-process service client.

UI notes
- The main window uses left navigation plus a settings shell and generation panels.
- Generation panels support shared overrides for language, temperature, API key, and system prompt.
- Tabs and panels should preserve the material icon style already used in the shell.

DI patterns

- Use `IServiceCollection` extension methods to register large groups of related services (Kernel wiring, provider adapters). Keep DI wiring in a single place per project: `DependencyInjection` folder.
- Do not resolve `IServiceProvider` manually except in host creation code. Prefer constructor injection.

KernelFactory

- Implement `IKernelFactory` to encapsulate kernel creation and tool registration. This allows tests to mock kernel creation and prevents global static state.
- Keep the factory small: accept configuration options and return a pre-configured `Kernel` or throw meaningful exceptions when configuration is invalid.

Parity implementation notes

- Provider/model/language resolution must follow one deterministic precedence policy.
- Settings shell must load without secrets and must not display secret plaintext.
- Generation-time key validation is required; startup-time key validation is prohibited.
- Treat this parity pass as feature-evidence driven: each feature requires tests + docs evidence.
- If a new modality is added, update the enum, routing, prompt defaults, sidebar navigation, and tests together.

Adding new providers / tools

1. Write adapters that translate between the provider API and Semantic Kernel tool interfaces.
2. Add registration methods to the DI extension so the provider is registered conditionally in production and can be swapped in tests.
3. Update `GenerationPanelViewModel`, `GeneratorApiClient`, and the Avalonia views when adding new generation overrides or UI affordances.

Code quality

- Follow .editorconfig and keep new files small and focused.
- Write unit tests for business logic and DI registrations where possible.
