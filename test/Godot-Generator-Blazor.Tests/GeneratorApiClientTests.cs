#nullable enable
using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using Godot_Generator_Blazor.Models;
using Godot_Generator_Blazor.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Godot_Generator_Blazor.Tests;

/// <summary>
/// Unit tests for <see cref="GeneratorApiClient"/>.
/// </summary>
public sealed class GeneratorApiClientTests
{
    /// <summary>
    /// Verifies that panel override takes precedence over global default.
    /// </summary>
    [Fact]
    public void ResolvePreferredLanguage_prefers_override()
    {
        var effective = GeneratorApiClient.ResolvePreferredLanguage("gdscript", "csharp");
        Assert.Equal("gdscript", effective);
    }

    /// <summary>
    /// Verifies fallback to global default when override is missing.
    /// </summary>
    [Fact]
    public void ResolvePreferredLanguage_uses_global_fallback()
    {
        var effective = GeneratorApiClient.ResolvePreferredLanguage(" ", "csharp");
        Assert.Equal("csharp", effective);
    }

    /// <summary>
    /// Verifies generation routes through the expected API modality method.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_code_calls_generate_code()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(x => x.GenerateCodeAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?> { ["message"] = "ok" }));

        var sut = new GeneratorApiClient(api.Object, NullLogger<GeneratorApiClient>.Instance);
        var result = await sut.GenerateAsync(GenerationModality.Code, "make code", "gdscript", "csharp");

        Assert.True(result.Success);
        Assert.Equal("ok", result.Message);
        api.Verify(x => x.GenerateCodeAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies each modality calls the expected API method.
    /// </summary>
    [Theory]
    [InlineData(GenerationModality.Text)]
    [InlineData(GenerationModality.Image)]
    [InlineData(GenerationModality.Audio)]
    [InlineData(GenerationModality.Video)]
    [InlineData(GenerationModality.Sprites)]
    [InlineData(GenerationModality.GodotUi)]
    [InlineData(GenerationModality.GodotPhysics)]
    public async Task GenerateAsync_routes_by_modality(GenerationModality modality)
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(x => x.GenerateTextAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(new() { ["message"] = "ok" }));
        api.Setup(x => x.GenerateImageAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(new() { ["message"] = "ok" }));
        api.Setup(x => x.GenerateAudioAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(new() { ["message"] = "ok" }));
        api.Setup(x => x.GenerateVideoAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(new() { ["message"] = "ok" }));
        api.Setup(x => x.GenerateSpritesAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(new() { ["message"] = "ok" }));
        api.Setup(x => x.GenerateGodotUiAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(new() { ["message"] = "ok" }));
        api.Setup(x => x.GenerateGodotPhysicsAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(new() { ["message"] = "ok" }));

        var sut = new GeneratorApiClient(api.Object, NullLogger<GeneratorApiClient>.Instance);
        var result = await sut.GenerateAsync(modality, "hello", string.Empty, "csharp");

        Assert.True(result.Success);
        Assert.Equal("ok", result.Message);
    }

    /// <summary>
    /// Verifies empty prompt requests are rejected before API invocation.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_empty_prompt_fails_without_api_call()
    {
        var api = new Mock<IGodotGeneratorApiService>(MockBehavior.Strict);
        var sut = new GeneratorApiClient(api.Object, NullLogger<GeneratorApiClient>.Instance);
        var result = await sut.GenerateAsync(GenerationModality.Text, " ", null, null);
        Assert.False(result.Success);
    }

    /// <summary>
    /// Verifies failed API responses are propagated as errors.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_failed_response_returns_error()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(x => x.GenerateTextAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Fail("bad"));

        var sut = new GeneratorApiClient(api.Object, NullLogger<GeneratorApiClient>.Instance);
        var result = await sut.GenerateAsync(GenerationModality.Text, "hello", null, null);

        Assert.False(result.Success);
        Assert.Equal("bad", result.Error);
    }

    /// <summary>
    /// Verifies preference load handles missing data safely.
    /// </summary>
    [Fact]
    public async Task GetGlobalPreferredLanguageAsync_handles_missing_data()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(x => x.GetPreferenceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, string?>>.Fail("x"));
        var sut = new GeneratorApiClient(api.Object, NullLogger<GeneratorApiClient>.Instance);
        var value = await sut.GetGlobalPreferredLanguageAsync();
        Assert.Equal(string.Empty, value);
    }

    /// <summary>
    /// Verifies preference save returns success flag from API.
    /// </summary>
    [Fact]
    public async Task SaveGlobalPreferredLanguageAsync_returns_api_success()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(x => x.SetPreferenceAsync(It.IsAny<SetPreferenceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, string?>>.Ok(new()));

        var sut = new GeneratorApiClient(api.Object, NullLogger<GeneratorApiClient>.Instance);
        Assert.True(await sut.SaveGlobalPreferredLanguageAsync("csharp"));
    }

    /// <summary>
    /// Verifies unsupported modality path returns explicit error.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_unsupported_modality_returns_error()
    {
        var api = new Mock<IGodotGeneratorApiService>(MockBehavior.Strict);
        var sut = new GeneratorApiClient(api.Object, NullLogger<GeneratorApiClient>.Instance);
        var result = await sut.GenerateAsync((GenerationModality)999, "hello", null, null);
        Assert.False(result.Success);
        Assert.Equal("Unsupported modality.", result.Error);
    }

    /// <summary>
    /// Verifies successful preference load extracts stored value.
    /// </summary>
    [Fact]
    public async Task GetGlobalPreferredLanguageAsync_reads_value()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(x => x.GetPreferenceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, string?>>.Ok(new Dictionary<string, string?> { ["value"] = "rust" }));

        var sut = new GeneratorApiClient(api.Object, NullLogger<GeneratorApiClient>.Instance);
        var value = await sut.GetGlobalPreferredLanguageAsync();
        Assert.Equal("rust", value);
    }

    /// <summary>
    /// Verifies preference load handles missing value key.
    /// </summary>
    [Fact]
    public async Task GetGlobalPreferredLanguageAsync_missing_value_returns_empty()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(x => x.GetPreferenceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, string?>>.Ok(new Dictionary<string, string?>()));

        var sut = new GeneratorApiClient(api.Object, NullLogger<GeneratorApiClient>.Instance);
        var value = await sut.GetGlobalPreferredLanguageAsync();
        Assert.Equal(string.Empty, value);
    }

    /// <summary>
    /// Verifies preference save false branch.
    /// </summary>
    [Fact]
    public async Task SaveGlobalPreferredLanguageAsync_returns_false_when_api_fails()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(x => x.SetPreferenceAsync(It.IsAny<SetPreferenceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, string?>>.Fail("no"));

        var sut = new GeneratorApiClient(api.Object, NullLogger<GeneratorApiClient>.Instance);
        Assert.False(await sut.SaveGlobalPreferredLanguageAsync("csharp"));
    }

    /// <summary>
    /// Verifies successful generation handles responses without message key.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_success_without_message_returns_empty_message()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(x => x.GenerateTextAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?>()));

        var sut = new GeneratorApiClient(api.Object, NullLogger<GeneratorApiClient>.Instance);
        var result = await sut.GenerateAsync(GenerationModality.Text, "hello", null, null);

        Assert.True(result.Success);
        Assert.Equal(string.Empty, result.Message);
    }
}
