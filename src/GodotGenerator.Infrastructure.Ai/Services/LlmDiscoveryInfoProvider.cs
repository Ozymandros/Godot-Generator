#nullable enable
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Infrastructure.Ai.Options;
using Microsoft.Extensions.Options;

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Maps configured <see cref="LlmOptions"/> into discovery payloads.
/// </summary>
public sealed class LlmDiscoveryInfoProvider(IOptions<LlmOptions> llmOptions) : ILlmDiscoveryInfoProvider
{
    /// <inheritdoc />
    public (string Provider, string ModelId) GetDefaultChatModel()
    {
        var o = llmOptions.Value;
        return ("openai", o.ChatModelId);
    }
}
