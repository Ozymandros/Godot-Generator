#nullable enable
using GodotGenerator.Application;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Infrastructure.Ai.Services;
using Moq;
using Xunit;

namespace GodotGenerator.Infrastructure.Ai.Tests;

public sealed class ProviderCapabilityRouterTests
{
    [Fact]
    public void Supports_returns_false_when_provider_missing_from_registry()
    {
        var prefs = new Mock<IPreferenceRepository>();
        prefs.Setup(p => p.GetAsync(PreferenceKeys.ProvidersRegistryV1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"version\":1,\"providers\":[]}");

        var sut = new ProviderCapabilityRouter(prefs.Object);
        var ok = sut.Supports("missing", "text", out var reason);

        Assert.False(ok);
        Assert.Contains("not listed", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Supports_returns_false_when_openai_compatibility_disabled()
    {
        var json = """
            {"version":1,"providers":[{"id":"custom","openAiCompatibility":false,"modalities":["llm"]}]}
            """;
        var prefs = new Mock<IPreferenceRepository>();
        prefs.Setup(p => p.GetAsync(PreferenceKeys.ProvidersRegistryV1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var sut = new ProviderCapabilityRouter(prefs.Object);
        var ok = sut.Supports("custom", "text", out var reason);

        Assert.False(ok);
        Assert.Contains("openAiCompatibility", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Supports_returns_true_when_openai_compatible()
    {
        var json = """
            {"version":1,"providers":[{"id":"openai","openAiCompatibility":true,"modalities":["llm"]}]}
            """;
        var prefs = new Mock<IPreferenceRepository>();
        prefs.Setup(p => p.GetAsync(PreferenceKeys.ProvidersRegistryV1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var sut = new ProviderCapabilityRouter(prefs.Object);
        var ok = sut.Supports("openai", "text", out var reason);

        Assert.True(ok);
        Assert.Null(reason);
    }

    [Fact]
    public void Supports_uses_default_openai_when_provider_null()
    {
        var json = """
            {"version":1,"providers":[{"id":"openai","openAiCompatibility":true,"modalities":["llm"]}]}
            """;
        var prefs = new Mock<IPreferenceRepository>();
        prefs.Setup(p => p.GetAsync(PreferenceKeys.ProvidersRegistryV1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var sut = new ProviderCapabilityRouter(prefs.Object);
        var ok = sut.Supports(null, "text", out _);

        Assert.True(ok);
    }
}
