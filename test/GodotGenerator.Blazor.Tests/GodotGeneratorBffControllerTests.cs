using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Blazor.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GodotGenerator.Blazor.Tests;

/// <summary>
/// Unit tests for <see cref="GodotGeneratorBffController"/> mapping to <see cref="IGodotGeneratorApiService"/>.
/// </summary>
public sealed class GodotGeneratorBffControllerTests
{
    [Fact]
    public async Task GenerateText_returns_OkObjectResult_with_payload_when_api_succeeds()
    {
        var payload = new Dictionary<string, object?> { ["message"] = "done" };
        var mock = new Mock<IGodotGeneratorApiService>(MockBehavior.Strict);
        mock.Setup(a => a.GenerateTextAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(payload));

        var sut = new GodotGeneratorBffController(mock.Object);
        var actionResult = await sut.GenerateText(new GenerateRequest("hello"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var body = Assert.IsType<ApiResponse<Dictionary<string, object?>>>(ok.Value);
        Assert.True(body.Success);
        Assert.Equal("done", body.Data?["message"]?.ToString());
        mock.Verify(a => a.GenerateTextAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateText_returns_BadRequest_when_api_fails()
    {
        var mock = new Mock<IGodotGeneratorApiService>(MockBehavior.Strict);
        mock.Setup(a => a.GenerateTextAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Fail("bad"));

        var sut = new GodotGeneratorBffController(mock.Object);
        var actionResult = await sut.GenerateText(new GenerateRequest("x"), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(actionResult);
        var body = Assert.IsType<ApiResponse<Dictionary<string, object?>>>(bad.Value);
        Assert.False(body.Success);
        Assert.Equal("bad", body.Error);
    }

    [Fact]
    public async Task GenerateAnimations_posts_to_same_contract_as_Avalonia_modalities()
    {
        var mock = new Mock<IGodotGeneratorApiService>(MockBehavior.Strict);
        mock.Setup(a => a.GenerateAnimationsAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?>()));

        var sut = new GodotGeneratorBffController(mock.Object);
        var result = await sut.GenerateAnimations(new GenerateRequest("anim"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        mock.Verify(a => a.GenerateAnimationsAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateGodotLighting_returns_Ok_and_calls_api()
    {
        var mock = new Mock<IGodotGeneratorApiService>(MockBehavior.Strict);
        mock.Setup(a => a.GenerateGodotLightingAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?> { ["message"] = "lit" }));

        var sut = new GodotGeneratorBffController(mock.Object);
        var result = await sut.GenerateGodotLighting(new GenerateRequest("sun"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<Dictionary<string, object?>>>(ok.Value);
        Assert.True(body.Success);
        Assert.Equal("lit", body.Data?["message"]?.ToString());
        mock.Verify(a => a.GenerateGodotLightingAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPreference_forwards_key()
    {
        var mock = new Mock<IGodotGeneratorApiService>(MockBehavior.Strict);
        mock.Setup(a => a.GetPreferenceAsync("k", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, string?>>.Ok(new Dictionary<string, string?> { ["value"] = "v" }));

        var sut = new GodotGeneratorBffController(mock.Object);
        var result = await sut.GetPreference("k", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }
}
