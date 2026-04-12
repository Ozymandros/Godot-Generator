#nullable enable
using GodotGenerator.Infrastructure.Ai.Services;
using Microsoft.Extensions.Options;
using LlmOptions = GodotGenerator.Infrastructure.Ai.Options.LlmOptions;
using Xunit;

namespace GodotGenerator.Infrastructure.Ai.Tests;

/// <summary>
/// Unit tests for <see cref="LlmDiscoveryInfoProvider"/>.
/// </summary>
public sealed class LlmDiscoveryInfoProviderTests
{
    /// <summary>
    /// Verifies configured chat model id is exposed for discovery payloads.
    /// </summary>
    [Fact]
    public void GetDefaultChatModel_returns_openai_and_configured_model()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new LlmOptions { ChatModelId = "gpt-test", ApiKey = "x" });
        var sut = new LlmDiscoveryInfoProvider(options);

        var (provider, modelId) = sut.GetDefaultChatModel();

        Assert.Equal("openai", provider);
        Assert.Equal("gpt-test", modelId);
    }
}
