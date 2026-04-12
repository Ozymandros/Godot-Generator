#nullable enable

namespace GodotGenerator.Application.Abstractions;

/// <summary>
/// Supplies default LLM provider/model labels for discovery-style config responses.
/// </summary>
public interface ILlmDiscoveryInfoProvider
{
    /// <summary>
    /// Returns the default provider name and chat model id from host configuration.
    /// </summary>
    (string Provider, string ModelId) GetDefaultChatModel();
}
