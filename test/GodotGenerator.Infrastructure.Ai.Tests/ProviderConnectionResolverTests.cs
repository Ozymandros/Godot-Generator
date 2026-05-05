#nullable enable

using System.Collections.Generic;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Infrastructure.Ai.Options;
using GodotGenerator.Infrastructure.Ai.Services;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GodotGenerator.Infrastructure.Ai.Tests;

public sealed class ProviderConnectionResolverTests
{
    [Fact]
    public async Task ResolveAsync_uses_provider_specific_endpoint_override_first()
    {
        var prefs = new InMemoryPreferenceRepository();
        await prefs.SetAsync("providers/deepseek", """{"endpoint":"https://custom.deepseek.local/v1"}""");
        await prefs.SetAsync("providers.registry.v1", """{"version":1,"providers":[{"id":"deepseek","openAiCompatibility":true,"endpoint":"https://api.deepseek.com/v1"}]}""");

        var secretResolver = new Mock<IProviderSecretResolver>();
        secretResolver.Setup(x => x.ResolveApiKeyAsync("deepseek", It.IsAny<CancellationToken>()))
            .ReturnsAsync("deepseek-key");

        var sut = Build(prefs, secretResolver.Object);
        var resolved = await sut.ResolveAsync("deepseek", "deepseek-chat");

        Assert.Equal("deepseek", resolved.Provider);
        Assert.Equal("deepseek-chat", resolved.ModelId);
        Assert.Equal("deepseek-key", resolved.ApiKey);
        Assert.Equal("https://custom.deepseek.local/v1", resolved.Endpoint?.ToString());
    }

    [Fact]
    public async Task ResolveAsync_uses_registry_endpoint_when_provider_override_missing()
    {
        var prefs = new InMemoryPreferenceRepository();
        await prefs.SetAsync("providers.registry.v1", """{"version":1,"providers":[{"id":"deepseek","openAiCompatibility":true,"endpoint":"https://api.deepseek.com/v1"}]}""");

        var secretResolver = new Mock<IProviderSecretResolver>();
        secretResolver.Setup(x => x.ResolveApiKeyAsync("deepseek", It.IsAny<CancellationToken>()))
            .ReturnsAsync("deepseek-key");

        var sut = Build(prefs, secretResolver.Object);
        var resolved = await sut.ResolveAsync("deepseek", null);

        Assert.Equal("https://api.deepseek.com/v1", resolved.Endpoint?.ToString());
    }

    [Fact]
    public async Task ResolveAsync_uses_default_endpoint_when_provider_not_in_registry()
    {
        var prefs = new InMemoryPreferenceRepository();
        var secretResolver = new Mock<IProviderSecretResolver>();
        secretResolver.Setup(x => x.ResolveApiKeyAsync("deepseek", It.IsAny<CancellationToken>()))
            .ReturnsAsync("deepseek-key");

        var sut = Build(prefs, secretResolver.Object);
        var resolved = await sut.ResolveAsync("deepseek", null);

        Assert.Equal("https://api.deepseek.com/v1", resolved.Endpoint?.ToString());
    }

    [Fact]
    public async Task ResolveAsync_throws_for_non_openai_compatible_provider()
    {
        var prefs = new InMemoryPreferenceRepository();
        await prefs.SetAsync("providers.registry.v1", """{"version":1,"providers":[{"id":"custom","openAiCompatibility":false,"endpoint":"https://custom.local/v1"}]}""");
        var secretResolver = new Mock<IProviderSecretResolver>();
        secretResolver.Setup(x => x.ResolveApiKeyAsync("custom", It.IsAny<CancellationToken>()))
            .ReturnsAsync("custom-key");

        var sut = Build(prefs, secretResolver.Object);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ResolveAsync("custom", null));
        Assert.Contains("not marked OpenAI-compatible", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_uses_openai_key_fallback_from_options()
    {
        var prefs = new InMemoryPreferenceRepository();
        var secretResolver = new Mock<IProviderSecretResolver>();
        secretResolver.Setup(x => x.ResolveApiKeyAsync("openai", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = Build(
            prefs,
            secretResolver.Object,
            Microsoft.Extensions.Options.Options.Create(new LlmOptions { ChatModelId = "gpt-4o-mini", ApiKey = "fallback-key" }));
        var resolved = await sut.ResolveAsync("openai", null);

        Assert.Equal("fallback-key", resolved.ApiKey);
        Assert.Equal("gpt-4o-mini", resolved.ModelId);
    }

    [Fact]
    public async Task ResolveAsync_throws_when_non_openai_key_missing()
    {
        var prefs = new InMemoryPreferenceRepository();
        var secretResolver = new Mock<IProviderSecretResolver>();
        secretResolver.Setup(x => x.ResolveApiKeyAsync("deepseek", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = Build(prefs, secretResolver.Object);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ResolveAsync("deepseek", null));
        Assert.Contains("API key for provider 'deepseek' is not configured", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_throws_on_invalid_endpoint_uri()
    {
        var prefs = new InMemoryPreferenceRepository();
        await prefs.SetAsync("providers/deepseek", """{"endpoint":"not-a-uri"}""");

        var secretResolver = new Mock<IProviderSecretResolver>();
        secretResolver.Setup(x => x.ResolveApiKeyAsync("deepseek", It.IsAny<CancellationToken>()))
            .ReturnsAsync("deepseek-key");

        var sut = Build(prefs, secretResolver.Object);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ResolveAsync("deepseek", null));
        Assert.Contains("not a valid absolute URI", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ProviderConnectionResolver Build(
        IPreferenceRepository preferences,
        IProviderSecretResolver secretResolver,
        IOptions<LlmOptions>? llmOptions = null) =>
        new(
            preferences,
            secretResolver,
            llmOptions ?? Microsoft.Extensions.Options.Options.Create(new LlmOptions { ChatModelId = "gpt-4o-mini" }));

    private sealed class InMemoryPreferenceRepository : IPreferenceRepository
    {
        private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
        {
            if (value is null)
            {
                _values.Remove(key);
            }
            else
            {
                _values[key] = value;
            }

            return Task.CompletedTask;
        }
    }
}
