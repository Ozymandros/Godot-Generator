#nullable enable
using GodotGenerator.Application.Abstractions;

namespace GodotGenerator.Application.Orchestration;

/// <summary>
/// Fallback discovery info when AI infrastructure is not registered (e.g. WASM stub host).
/// </summary>
public sealed class DefaultLlmDiscoveryInfoProvider : ILlmDiscoveryInfoProvider
{
    /// <inheritdoc />
    public (string Provider, string ModelId) GetDefaultChatModel() => ("openai", "gpt-4o-mini");
}
