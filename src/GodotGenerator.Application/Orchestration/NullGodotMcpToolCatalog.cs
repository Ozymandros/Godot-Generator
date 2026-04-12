#nullable enable
using GodotGenerator.Application.Abstractions;

namespace GodotGenerator.Application.Orchestration;

/// <summary>
/// No-op catalog when AI infrastructure is not loaded (e.g. Blazor WASM stub).
/// </summary>
public sealed class NullGodotMcpToolCatalog : IGodotMcpToolCatalog
{
    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListRegisteredToolNamesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}
