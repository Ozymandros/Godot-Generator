#nullable enable
using System.Threading.Tasks;
using Godot_Generator_Avalonia.Services;
using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Application;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GodotGenerator.Ui.Tests;

public class GeneratorApiClientTests
{
    [Fact]
    public async Task GetAllConfigSnapshotAsync_parses_host_payload()
    {
        var mock = new Mock<IGodotGeneratorApiService>();
        mock.Setup(a => a.GetAllConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?>
            {
                ["preferences"] = new Dictionary<string, string?> { [PreferenceKeys.PreferredLanguage] = "rust" },
                ["keys"] = new List<string> { "svc" },
                ["providers"] = new List<object>(),
                ["models"] = new Dictionary<string, object?>(),
                ["prompts"] = new Dictionary<string, object?>(),
                ["defaultLlmProvider"] = "p1",
                ["defaultChatModelId"] = "m1",
                ["godotToolNames"] = new List<string> { "tool_x" },
            }));

        var client = new GeneratorApiClient(mock.Object, NullLogger<GeneratorApiClient>.Instance);
        var snap = await client.GetAllConfigSnapshotAsync();

        Assert.NotNull(snap);
        Assert.Equal("rust", snap!.Preferences[PreferenceKeys.PreferredLanguage]);
        Assert.Equal("p1", snap.DefaultLlmProvider);
        Assert.Equal("m1", snap.DefaultChatModelId);
        Assert.Single(snap.KeyNames);
        Assert.Single(snap.GodotToolNames);
    }

    [Fact]
    public async Task SetPreferenceAsync_delegates_to_api()
    {
        var mock = new Mock<IGodotGeneratorApiService>();
        mock.Setup(a => a.SetPreferenceAsync(It.IsAny<SetPreferenceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, string?>>.Ok(new Dictionary<string, string?>(StringComparer.Ordinal)));

        var client = new GeneratorApiClient(mock.Object, NullLogger<GeneratorApiClient>.Instance);
        var ok = await client.SetPreferenceAsync(PreferenceKeys.PreferredLlmProvider, "openai");

        Assert.True(ok);
        mock.Verify(
            a => a.SetPreferenceAsync(
                It.Is<SetPreferenceRequest>(r => r.Key == PreferenceKeys.PreferredLlmProvider && r.Value == "openai"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_uses_modality_provider_and_model_preferences()
    {
        var mock = new Mock<IGodotGeneratorApiService>();
        mock.Setup(a => a.GetPreferenceAsync(PreferenceKeys.PreferredImageProvider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, string?>>.Ok(new Dictionary<string, string?>
            {
                ["key"] = PreferenceKeys.PreferredImageProvider,
                ["value"] = "stability",
            }));
        mock.Setup(a => a.GetPreferenceAsync(PreferenceKeys.PreferredImageModel, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, string?>>.Ok(new Dictionary<string, string?>
            {
                ["key"] = PreferenceKeys.PreferredImageModel,
                ["value"] = "sdxl",
            }));
        mock.Setup(a => a.GenerateImageAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?>
            {
                ["message"] = "ok",
            }));

        var client = new GeneratorApiClient(mock.Object, NullLogger<GeneratorApiClient>.Instance);
        var (ok, message, error) = await client.GenerateAsync(
            Godot_Generator_Avalonia.Models.GenerationModality.Image,
            "draw",
            preferredLanguageOverride: "",
            globalPreferredLanguage: "csharp",
            temperature: 1.2,
            apiKeyOverride: "key123",
            systemPromptOverride: "override123");

        Assert.True(ok);
        Assert.Equal("ok", message);
        Assert.Null(error);
        mock.Verify(
            a => a.GenerateImageAsync(
                It.Is<GenerateRequest>(r =>
                    r.Provider == "stability" &&
                    r.PreferredModelId == "sdxl" &&
                    r.ApiKey == "key123" &&
                    r.SystemPrompt == "override123" &&
                    r.Options != null &&
                    r.Options.ContainsKey("temperature") &&
                    (double)r.Options["temperature"]! == 1.2 &&
                    r.Options.ContainsKey("preferred_language") &&
                    (r.Options["preferred_language"] as string) == "csharp"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
