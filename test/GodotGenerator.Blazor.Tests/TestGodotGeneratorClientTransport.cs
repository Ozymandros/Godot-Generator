#nullable enable

using System.Text.Json;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Blazor.Client.Models;
using GodotGenerator.Blazor.Client.Services.Transport;

namespace GodotGenerator.Blazor.Tests;

/// <summary>
/// In-memory transport for bUnit: captures preference writes and returns config snapshots without JS IPC.
/// </summary>
internal sealed class TestGodotGeneratorClientTransport : IGodotGeneratorClientTransport
{
    private readonly Dictionary<string, string?> _preferences = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SetPreferenceRequest> _setCalls = [];

    public IReadOnlyList<SetPreferenceRequest> SetPreferenceCalls => _setCalls;

    public Task<ApiResponse<Dictionary<string, object?>>> GenerateAsync(
        GenerationModality modality,
        GenerateRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?>
        {
            ["message"] = "fake",
        }));

    public Task<ApiResponse<Dictionary<string, object?>>> GetConfigAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?>
        {
            ["preferences"] = new Dictionary<string, object?>(),
            ["providers"] = JsonSerializer.Deserialize<JsonElement>("[]"),
            ["models"] = JsonSerializer.Deserialize<JsonElement>("{}"),
        }));

    public Task<ApiResponse<Dictionary<string, string?>>> GetPreferenceAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        _preferences.TryGetValue(key, out var value);
        return Task.FromResult(ApiResponse<Dictionary<string, string?>>.Ok(new Dictionary<string, string?> { ["value"] = value }));
    }

    public Task<ApiResponse<Dictionary<string, string?>>> SetPreferenceAsync(
        SetPreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        _setCalls.Add(request);
        _preferences[request.Key] = request.Value;
        return Task.FromResult(ApiResponse<Dictionary<string, string?>>.Ok(new Dictionary<string, string?> { ["value"] = request.Value }));
    }

    public Task<ApiResponse<Dictionary<string, object?>>> SaveApiKeysAsync(
        ApiKeysRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?>()));
}
