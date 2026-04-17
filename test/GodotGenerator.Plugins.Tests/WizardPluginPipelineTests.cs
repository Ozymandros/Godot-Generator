#nullable enable

using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Mcp.Api.Services;
using GodotGenerator.Plugins.Plugins;
using GodotGenerator.Wizard.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace GodotGenerator.Plugins.Tests;

/// <summary>
/// Integration-style unit tests for the wizard utility plugin pipeline.
/// Covers <see cref="ApiWizardGenerationGateway"/> (the IPC adapter) and
/// <see cref="WizardMcpPlugin"/> (the SK-callable wrapper).
/// </summary>
public sealed class WizardPluginPipelineTests
{
    // ── ApiWizardGenerationGateway.GetConfigurationAsync ───────────────────

    [Fact]
    public async Task GetConfiguration_returns_formatted_summary_when_api_succeeds()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(x => x.GetAllConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(
                new Dictionary<string, object?>
                {
                    ["defaultLlmProvider"] = "openai",
                    ["defaultChatModelId"] = "gpt-4o",
                }));

        var sut = new ApiWizardGenerationGateway(api.Object);
        var result = await sut.GetConfigurationAsync();

        Assert.Contains("openai", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gpt-4o", result, StringComparison.OrdinalIgnoreCase);
        api.Verify(x => x.GetAllConfigAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetConfiguration_returns_error_text_when_api_fails()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(x => x.GetAllConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Fail("connection refused"));

        var sut = new ApiWizardGenerationGateway(api.Object);
        var result = await sut.GetConfigurationAsync();

        Assert.Contains("[Error", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("connection refused", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetConfiguration_returns_fallback_message_when_data_has_no_highlights()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(x => x.GetAllConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(
                new Dictionary<string, object?>()));

        var sut = new ApiWizardGenerationGateway(api.Object);
        var result = await sut.GetConfigurationAsync();

        Assert.Contains("no highlights", result, StringComparison.OrdinalIgnoreCase);
    }

    // ── ApiWizardGenerationGateway.SetPreferenceAsync ──────────────────────

    [Fact]
    public async Task SetPreference_returns_success_message_when_api_succeeds()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(x => x.SetPreferenceAsync(
                It.Is<SetPreferenceRequest>(r => r.Key == "preferred_llm_provider" && r.Value == "anthropic"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, string?>>.Ok(new Dictionary<string, string?>()));

        var sut = new ApiWizardGenerationGateway(api.Object);
        var result = await sut.SetPreferenceAsync("preferred_llm_provider", "anthropic");

        Assert.Contains("preferred_llm_provider", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("anthropic", result, StringComparison.OrdinalIgnoreCase);
        api.Verify(x => x.SetPreferenceAsync(
            It.Is<SetPreferenceRequest>(r => r.Key == "preferred_llm_provider"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetPreference_returns_error_text_when_api_fails()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(x => x.SetPreferenceAsync(It.IsAny<SetPreferenceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, string?>>.Fail("validation error"));

        var sut = new ApiWizardGenerationGateway(api.Object);
        var result = await sut.SetPreferenceAsync("some_key", "some_value");

        Assert.Contains("[Error", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("validation error", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetPreference_rejects_empty_key_without_calling_api(string emptyKey)
    {
        var api = new Mock<IGodotGeneratorApiService>();
        var sut = new ApiWizardGenerationGateway(api.Object);

        var result = await sut.SetPreferenceAsync(emptyKey, "any-value");

        Assert.Contains("[Error", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("must not be empty", result, StringComparison.OrdinalIgnoreCase);
        api.Verify(x => x.SetPreferenceAsync(
            It.IsAny<SetPreferenceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── WizardMcpPlugin kernel-function delegation ──────────────────────────

    [Fact]
    public async Task WizardMcpPlugin_get_configuration_delegates_to_gateway()
    {
        var gateway = new Mock<IWizardGenerationGateway>();
        gateway.Setup(x => x.GetConfigurationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("provider: openai, model: gpt-4o");

        var services = new ServiceCollection();
        services.AddScoped(_ => gateway.Object);
        var provider = services.BuildServiceProvider();

        var sut = new WizardMcpPlugin(provider.GetRequiredService<IServiceScopeFactory>());
        var result = await sut.GetConfigurationAsync();

        Assert.Equal("provider: openai, model: gpt-4o", result);
        gateway.Verify(x => x.GetConfigurationAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WizardMcpPlugin_set_preference_delegates_to_gateway()
    {
        var gateway = new Mock<IWizardGenerationGateway>();
        gateway.Setup(x => x.SetPreferenceAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Preference 'preferred_llm_model' set to 'gpt-4o'.");

        var services = new ServiceCollection();
        services.AddScoped(_ => gateway.Object);
        var provider = services.BuildServiceProvider();

        var sut = new WizardMcpPlugin(provider.GetRequiredService<IServiceScopeFactory>());
        var result = await sut.SetPreferenceAsync("preferred_llm_model", "gpt-4o");

        Assert.Contains("gpt-4o", result, StringComparison.Ordinal);
        gateway.Verify(x => x.SetPreferenceAsync(
            "preferred_llm_model", "gpt-4o", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WizardMcpPlugin_wraps_gateway_exception_as_error_string()
    {
        var gateway = new Mock<IWizardGenerationGateway>();
        gateway.Setup(x => x.GetConfigurationAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("service unavailable"));

        var services = new ServiceCollection();
        services.AddScoped(_ => gateway.Object);
        var provider = services.BuildServiceProvider();

        var sut = new WizardMcpPlugin(provider.GetRequiredService<IServiceScopeFactory>());
        var result = await sut.GetConfigurationAsync();

        Assert.StartsWith("[Tool call error:", result, StringComparison.Ordinal);
        Assert.Contains("service unavailable", result, StringComparison.Ordinal);
    }
}
