using System.Text.Json;
using Microsoft.JSInterop;

namespace GodotGenerator.Blazor.Client.Services;

file static class JsInteropGuards
{
    internal static bool IsUnavailable(Exception ex) =>
        ex is InvalidOperationException or JSDisconnectedException;
}

/// <summary>
/// Persists active project name/path (parity with Unity-Generator <c>unity_generator_active_project</c>).
/// </summary>
public sealed class ProjectStateService(IJSRuntime js)
{
    private const string StorageKey = "unity_generator_active_project";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string ActiveProjectName { get; private set; } = string.Empty;

    public string ActiveProjectPath { get; private set; } = string.Empty;

    /// <summary>Raised after <see cref="SetActiveProjectAsync"/> persists new values.</summary>
    public event Action? Changed;

    /// <summary>Loads persisted state from browser <c>localStorage</c> (no-op if missing or invalid JSON).</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        string? json;
        try
        {
            json = await js.InvokeAsync<string?>("godotGeneratorStorage.getItem", cancellationToken, StorageKey).ConfigureAwait(false);
        }
        catch (Exception ex) when (JsInteropGuards.IsUnavailable(ex))
        {
            // Prerendering or JS interop not yet available
            return;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            var state = JsonSerializer.Deserialize<ProjectStateDto>(json, JsonOptions);
            if (state is null)
            {
                return;
            }

            ActiveProjectName = state.ActiveProjectName ?? string.Empty;
            ActiveProjectPath = state.ActiveProjectPath ?? string.Empty;
        }
        catch
        {
            /* ignore corrupt */
        }
    }

    /// <summary>Persists the active project name and path under <c>unity_generator_active_project</c>.</summary>
    public async Task SetActiveProjectAsync(string name, string path, CancellationToken cancellationToken = default)
    {
        ActiveProjectName = name ?? string.Empty;
        ActiveProjectPath = path ?? string.Empty;
        var dto = new ProjectStateDto(ActiveProjectName, ActiveProjectPath);
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        try
        {
            await js.InvokeVoidAsync("godotGeneratorStorage.setItem", cancellationToken, StorageKey, json).ConfigureAwait(false);
        }
        catch (Exception ex) when (JsInteropGuards.IsUnavailable(ex))
        {
            // Persist skipped until interactive; in-memory state is still updated.
        }

        Changed?.Invoke();
    }

    private sealed record ProjectStateDto(string? ActiveProjectName, string? ActiveProjectPath);
}
